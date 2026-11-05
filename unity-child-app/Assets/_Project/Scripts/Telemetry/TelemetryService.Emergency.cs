using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;
using SmartLife.Core;

namespace SmartLife.Telemetry
{
    /// <summary>A supervisor's request to start a call, read from /interventions.</summary>
    public readonly struct InterventionRequest
    {
        public readonly string Id;
        public readonly string EmergencyId;
        public readonly string OfferSdp;
        public readonly long ExpiresAtMs;

        public InterventionRequest(string id, string emergencyId, string offerSdp, long expiresAtMs)
        {
            Id = id;
            EmergencyId = emergencyId;
            OfferSdp = offerSdp;
            ExpiresAtMs = expiresAtMs;
        }
    }

    /// <summary>Emergency records and WebRTC signaling. RTDB carries setup data only, never media.</summary>
    public partial class TelemetryService
    {
        private const int ConsentTimeoutMs = 5000;

        /// <summary>A supervisor asked to start a call. The tablet still decides for itself.</summary>
        public event Action<InterventionRequest> OnInterventionRequested;

        /// <summary>Server clock, so a tablet with a wrong clock cannot accept an expired request.</summary>
        public long ServerNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + _serverOffsetMs;

        private long _serverOffsetMs;
        private DatabaseReference _offsetRef;
        private Query _requestedInterventions;
        private DatabaseReference _watchedEmergencyStatus;
        private EventHandler<ValueChangedEventArgs> _emergencyStatusHandler;
        private DatabaseReference _watchedInterventionStatus;
        private EventHandler<ValueChangedEventArgs> _interventionStatusHandler;
        private DatabaseReference _watchedCandidates;
        private EventHandler<ChildChangedEventArgs> _candidateHandler;

        private void ConnectEmergencySignaling()
        {
            _offsetRef = FirebaseDatabase.DefaultInstance.GetReference(DbPaths.ServerTimeOffset);
            _offsetRef.ValueChanged += OnServerOffset;
            _requestedInterventions = _root.Child(DbPaths.Interventions(_studentId)).OrderByChild("status").EqualTo("requested");
            _requestedInterventions.ChildAdded += OnInterventionAdded;
        }

        private void DisconnectEmergencySignaling()
        {
            if (_offsetRef != null) _offsetRef.ValueChanged -= OnServerOffset;
            if (_requestedInterventions != null) _requestedInterventions.ChildAdded -= OnInterventionAdded;
            StopWatchingEmergency();
            StopWatchingIntervention();
        }

        private void OnServerOffset(object sender, ValueChangedEventArgs e)
        {
            if (e.DatabaseError == null && e.Snapshot.Value != null) _serverOffsetMs = Convert.ToInt64(e.Snapshot.Value);
        }

        private void OnInterventionAdded(object sender, ChildChangedEventArgs e)
        {
            if (e.DatabaseError != null || !e.Snapshot.Exists) return;
            string emergencyId = e.Snapshot.Child("emergencyId").Value as string;
            string offer = e.Snapshot.Child("offer").Value as string;
            object expires = e.Snapshot.Child("expiresAt").Value;
            if (string.IsNullOrEmpty(emergencyId) || string.IsNullOrEmpty(offer) || expires == null) return;
            OnInterventionRequested?.Invoke(new InterventionRequest(e.Snapshot.Key, emergencyId, offer, Convert.ToInt64(expires)));
        }

        /// <summary>Creates the /emergencies record, and returns its ID. Null if telemetry is off.</summary>
        public string OpenEmergency(EmergencySource source)
        {
            if (_root == null) return null;
            DatabaseReference record = _root.Child(DbPaths.Emergencies(_studentId)).Push();
            var value = new Dictionary<string, object> {
                { "source", source.ToString().ToLowerInvariant() },
                { "status", "open" },
                { "timestamp", ServerValue.Timestamp }
            };
            if (_sessionId != null) value["sessionId"] = _sessionId;
            record.SetValueAsync(value).ContinueWithOnMainThread(LogFault);
            return record.Key;
        }

        public void ResolveEmergency(string emergencyId)
        {
            if (_root == null) return;
            _root.Child(DbPaths.Emergencies(_studentId)).Child(emergencyId).Child("status").SetValueAsync("resolved").ContinueWithOnMainThread(LogFault);
        }

        public void WatchEmergency(string emergencyId, Action<string> onStatus)
        {
            if (_root == null) return;
            StopWatchingEmergency();
            _watchedEmergencyStatus = _root.Child(DbPaths.Emergencies(_studentId)).Child(emergencyId).Child("status");
            _emergencyStatusHandler = (_, e) =>
            {
                if (e.DatabaseError == null) onStatus(e.Snapshot.Value as string);
            };
            _watchedEmergencyStatus.ValueChanged += _emergencyStatusHandler;
        }

        public void StopWatchingEmergency()
        {
            if (_watchedEmergencyStatus != null) _watchedEmergencyStatus.ValueChanged -= _emergencyStatusHandler;
            _watchedEmergencyStatus = null;
            _emergencyStatusHandler = null;
        }

        /// <summary>Whether an admin recorded the guardian's consent. Anything unclear counts as no.</summary>
        public void ReadStreamingConsent(Action<bool> done)
        {
            if (_root == null)
            {
                done(false);
                return;
            }
            Task<DataSnapshot> read = _root.Child(DbPaths.StreamingConsent(_studentId)).GetValueAsync();
            Task.WhenAny(read, Task.Delay(ConsentTimeoutMs)).ContinueWithOnMainThread(first =>
            {
                if (this == null) return;
                bool ok = first.Result == read && read.IsCompletedSuccessfully && read.Result.Value is bool consent && consent;
                done(ok);
            });
        }

        public void WriteAnswer(string interventionId, string sdp) =>
            InterventionChild(interventionId, "answer").SetValueAsync(sdp).ContinueWithOnMainThread(LogFault);

        public void AddTabletCandidate(string interventionId, string candidateJson) =>
            InterventionChild(interventionId, "candidates").Child("tablet").Push().SetValueAsync(candidateJson).ContinueWithOnMainThread(LogFault);

        public void SetInterventionStatus(string interventionId, string status)
        {
            if (_root == null) return;
            var update = new Dictionary<string, object> { { "status", status } };
            if (status is "ended" or "expired") update["endedAt"] = ServerValue.Timestamp;
            _root.Child(DbPaths.Interventions(_studentId)).Child(interventionId).UpdateChildrenAsync(update).ContinueWithOnMainThread(LogFault);
        }

        public void WatchIntervention(string interventionId, Action<string> onRemoteCandidate, Action<string> onStatus)
        {
            StopWatchingIntervention();
            _watchedCandidates = InterventionChild(interventionId, "candidates").Child("supervisor");
            _candidateHandler = (_, e) =>
            {
                if (e.DatabaseError == null && e.Snapshot.Value is string json) onRemoteCandidate(json);
            };
            _watchedCandidates.ChildAdded += _candidateHandler;

            _watchedInterventionStatus = InterventionChild(interventionId, "status");
            _interventionStatusHandler = (_, e) =>
            {
                if (e.DatabaseError == null) onStatus(e.Snapshot.Value as string);
            };
            _watchedInterventionStatus.ValueChanged += _interventionStatusHandler;
        }

        public void StopWatchingIntervention()
        {
            if (_watchedCandidates != null) _watchedCandidates.ChildAdded -= _candidateHandler;
            if (_watchedInterventionStatus != null) _watchedInterventionStatus.ValueChanged -= _interventionStatusHandler;
            _watchedCandidates = null;
            _candidateHandler = null;
            _watchedInterventionStatus = null;
            _interventionStatusHandler = null;
        }

        private DatabaseReference InterventionChild(string interventionId, string child) =>
            _root.Child(DbPaths.Interventions(_studentId)).Child(interventionId).Child(child);
    }
}
