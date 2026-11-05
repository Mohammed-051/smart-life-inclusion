using System;
using UnityEngine;
using SmartLife.Telemetry;
using SmartLife.UI;

namespace SmartLife.Core
{
    /// <summary>
    /// Connects EmergencyDetector, the /emergencies and /interventions records, the call and the tablet
    /// UI. An emergency unlocks a stream but never starts one. Only a supervisor's request can do that,
    /// and only after StreamPolicy agrees. Logs hold IDs only. See docs/SECURITY.md.
    /// </summary>
    public class EmergencyCoordinator : MonoBehaviour
    {
        private EmergencyDetector _detector;
        private TelemetryService _telemetry;
        private EmergencyOverlay _overlay;
        private EmergencyStream _stream;
        private bool _routeIsPrivate;
        private Action _beforeCapture;

        private string _emergencyId;
        private EmergencySource? _source;
        private string _interventionId;
        private bool _localOpen;

        /// <summary>Telemetry is optional: without it the calm card still works, but no stream can ever start.</summary>
        /// <param name="beforeCapture">Frees the microphone from voice commands and sound alerts before a call uses it.</param>
        public void Init(EmergencyDetector detector, TelemetryService telemetry, bool routeIsPrivate, Action beforeCapture)
        {
            _beforeCapture = beforeCapture;
            _detector = detector;
            _telemetry = telemetry;
            _routeIsPrivate = routeIsPrivate;
            _overlay = gameObject.AddComponent<EmergencyOverlay>();
            _overlay.Init(detector.TriggerSos);
            _stream = gameObject.AddComponent<EmergencyStream>();
            _stream.OnEnded += HandleStreamEnded;
            _stream.OnConnected += HandleStreamConnected;

            _detector.OnEmergency += HandleEmergency;
            if (_telemetry != null) _telemetry.OnInterventionRequested += HandleRequest;
        }

        public bool HasOpenEmergency => _localOpen;
        public bool IsStreaming => _stream != null && _stream.IsActive;

        /// <summary>An adult at the tablet ends the call. The emergency stays open.</summary>
        public void EndStream() => _stream.End();

        /// <summary>An adult at the tablet says it is over. Ends any call too. The task stays paused until a Resume.</summary>
        public void ResolveFromTablet()
        {
            if (_telemetry != null && _emergencyId != null) _telemetry.ResolveEmergency(_emergencyId);
            CloseEmergency();
        }

        private void HandleEmergency(EmergencySource source)
        {
            _localOpen = true;
            _overlay.ShowHelpCard();

            // A press of SOS during an audio or fall alert upgrades it, since only SOS unlocks a stream.
            bool alreadyRecorded = _source != null && (source != EmergencySource.Sos || _source == EmergencySource.Sos);
            if (alreadyRecorded) return;

            string previous = _emergencyId;
            _source = source;
            if (_telemetry == null) return;
            _emergencyId = _telemetry.OpenEmergency(source);
            if (previous != null) _telemetry.ResolveEmergency(previous);
            if (_emergencyId != null) _telemetry.WatchEmergency(_emergencyId, HandleEmergencyStatus);
        }

        private void HandleEmergencyStatus(string status)
        {
            if (status == "resolved") CloseEmergency();
        }

        private void CloseEmergency()
        {
            _localOpen = false;
            _emergencyId = null;
            _source = null;
            if (_telemetry != null) _telemetry.StopWatchingEmergency();
            _stream.End();
            _overlay.HideHelpCard();
        }

        private void HandleRequest(InterventionRequest request)
        {
            // Checked once now, to avoid a consent read for a request that can never pass.
            if (StreamPolicy.Evaluate(_emergencyId, _source, request.EmergencyId, true, _telemetry.ServerNowMs, request.ExpiresAtMs, _stream.IsActive) != StreamDecision.Allowed)
            {
                Refuse(request, "preconditions");
                return;
            }

            _telemetry.ReadStreamingConsent(consent =>
            {
                StreamDecision decision = StreamPolicy.Evaluate(_emergencyId, _source, request.EmergencyId, consent, _telemetry.ServerNowMs, request.ExpiresAtMs, _stream.IsActive);
                if (decision != StreamDecision.Allowed)
                {
                    Refuse(request, decision.ToString());
                    return;
                }
                StartCall(request);
            });
        }

        private void Refuse(InterventionRequest request, string reason)
        {
            Debug.LogWarning($"[Emergency] Stream request {request.Id} refused: {reason}.", this);
            _telemetry.SetInterventionStatus(request.Id, "expired");
        }

        private void StartCall(InterventionRequest request)
        {
            _interventionId = request.Id;

            _beforeCapture?.Invoke();

            bool video = StreamPolicy.AllowVideo(_routeIsPrivate);
            _overlay.ShowIndicator(video);

            // Answer first: the stream only accepts candidates once the call is active.
            _stream.Answer(
                request.OfferSdp,
                video,
                sdp => _telemetry.WriteAnswer(request.Id, sdp),
                candidate => _telemetry.AddTabletCandidate(request.Id, candidate));
            _telemetry.WatchIntervention(request.Id, _stream.AddRemoteCandidate, HandleInterventionStatus);
        }

        private void HandleInterventionStatus(string status)
        {
            if (status is "ended" or "expired") _stream.End();
        }

        private void HandleStreamConnected()
        {
            if (_interventionId != null) _telemetry.SetInterventionStatus(_interventionId, "connected");
        }

        private void HandleStreamEnded()
        {
            _overlay.HideIndicator();
            if (_interventionId == null || _telemetry == null) return;
            _telemetry.SetInterventionStatus(_interventionId, "ended");
            _telemetry.StopWatchingIntervention();
            _interventionId = null;
        }

        private void OnDestroy()
        {
            if (_detector != null) _detector.OnEmergency -= HandleEmergency;
            if (_telemetry != null) _telemetry.OnInterventionRequested -= HandleRequest;
            if (_stream != null)
            {
                _stream.OnEnded -= HandleStreamEnded;
                _stream.OnConnected -= HandleStreamConnected;
            }
        }
    }
}
