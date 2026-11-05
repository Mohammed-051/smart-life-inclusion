using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;
using SmartLife.Core;

namespace SmartLife.Telemetry
{
    /// <summary>
    /// Writes checkpoint logs + live status to Firebase RTDB and listens for dashboard overrides.
    /// Writes are fire-and-forget with offline persistence so the child is never blocked by network.
    /// </summary>
    public class TelemetryService : MonoBehaviour
    {
        [SerializeField] private TaskStateMachine stateMachine;

        private DatabaseReference _root;
        private string _studentId;
        private string _sessionId;
        private string _uid;
        private int _reached, _autonomous;
        private Query _pendingOverrides;
        private bool _initStarted;

        /// <summary>
        /// Checks Firebase, then connects in the background so the child is never blocked. Any failure,
        /// such as a missing config file or no signed-in device, leaves telemetry off with a warning.
        /// </summary>
        public void Init(string studentId)
        {
            if (_initStarted) return;
            _initStarted = true;

            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(check =>
            {
                if (this == null) return;
                if (!check.IsCompletedSuccessfully || check.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning($"[Telemetry] Off: Firebase is not available ({(check.IsCompletedSuccessfully ? check.Result.ToString() : "check failed")}).", this);
                    return;
                }
                Connect(studentId);
            });
        }

        private void Connect(string studentId)
        {
            try
            {
                _uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
                // Database rules reject every unsigned write, so don't queue writes that can only fail.
                if (_uid == null)
                {
                    Debug.LogWarning("[Telemetry] Off: this device is not signed in to Firebase.", this);
                    return;
                }

                FirebaseDatabase.DefaultInstance.SetPersistenceEnabled(true);
                _root = FirebaseDatabase.DefaultInstance.RootReference;
                _studentId = studentId;

                stateMachine.OnStepEvent += LogStep;
                stateMachine.OnStateChanged += PushLiveStatus;
                _pendingOverrides = _root.Child(DbPaths.Overrides(_studentId)).OrderByChild("consumed").EqualTo(false);
                _pendingOverrides.ChildAdded += OnOverride;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Telemetry] Off: could not connect to Firebase. {e.Message}", this);
            }
        }

        /// <summary>Open a new session row for the current task.</summary>
        public void StartSession()
        {
            var ses = _root.Child(DbPaths.Sessions(_studentId)).Push();
            _sessionId = ses.Key;
            _reached = _autonomous = 0;
            ses.SetValueAsync(new Dictionary<string, object> {
                { "routeId", stateMachine.Task.RouteId }, { "startedAt", ServerValue.Timestamp },
                { "status", "active" }, { "checkpointsReached", 0 }, { "autonomousCount", 0 }
            }).ContinueWithOnMainThread(LogFault);
        }

        /// <summary>Record a step event; same call is used for voice/dashboard override outcomes.</summary>
        public void IssueOverride(OverrideCommand cmd, string source)
        {
            _root.Child(DbPaths.Overrides(_studentId)).Push().SetValueAsync(new Dictionary<string, object> {
                { "command", cmd.ToString().ToLowerInvariant() }, { "source", source },
                { "issuedBy", _uid }, { "timestamp", ServerValue.Timestamp }, { "consumed", true }
            }).ContinueWithOnMainThread(LogFault);
        }

        private void LogStep(int stepIndex, TaskStatus status, bool autonomous)
        {
            if (_sessionId == null) StartSession();
            var step = stateMachine.Task[Mathf.Min(stepIndex, stateMachine.Task.Count - 1)];

            _root.Child(DbPaths.CheckpointLogs(_studentId)).Push().SetValueAsync(new Dictionary<string, object> {
                { "studentId", _studentId }, { "sessionId", _sessionId }, { "routeId", stateMachine.Task.RouteId },
                { "checkpointId", step.checkpointId }, { "markerId", step.markerId },
                { "taskStatus", status.ToString().ToLowerInvariant() }, { "autonomous", autonomous },
                { "deviceId", _uid }, { "timestamp", ServerValue.Timestamp }
            }).ContinueWithOnMainThread(LogFault);

            if (status is TaskStatus.Completed or TaskStatus.Skipped)
            {
                _reached++;
                if (autonomous && status == TaskStatus.Completed) _autonomous++;
                var ses = _root.Child(DbPaths.Sessions(_studentId)).Child(_sessionId);
                ses.UpdateChildrenAsync(new Dictionary<string, object> {
                    { "checkpointsReached", _reached }, { "autonomousCount", _autonomous }
                }).ContinueWithOnMainThread(LogFault);
            }
        }

        private void PushLiveStatus(MachineState state, int stepIndex)
        {
            if (_sessionId == null) return;
            var step = stateMachine.Task[Mathf.Min(stepIndex, stateMachine.Task.Count - 1)];
            _root.Child(DbPaths.LiveStatus(_studentId)).SetValueAsync(new Dictionary<string, object> {
                { "sessionId", _sessionId }, { "routeId", stateMachine.Task.RouteId }, { "checkpointId", step.checkpointId },
                { "progressPct", Mathf.RoundToInt(stateMachine.ProgressPct) }, { "state", state.ToString().ToLowerInvariant() },
                { "lastSeen", ServerValue.Timestamp }
            }).ContinueWithOnMainThread(LogFault);

            if (state == MachineState.Complete)
            {
                _root.Child(DbPaths.Sessions(_studentId)).Child(_sessionId).UpdateChildrenAsync(new Dictionary<string, object> {
                    { "status", "completed" }, { "completedAt", ServerValue.Timestamp }
                }).ContinueWithOnMainThread(LogFault);
                _sessionId = null;
            }
        }

        private void OnOverride(object sender, ChildChangedEventArgs e)
        {
            if (e.DatabaseError != null || !e.Snapshot.Exists) return;
            if (System.Enum.TryParse(e.Snapshot.Child("command").Value as string, true, out OverrideCommand cmd))
                stateMachine.Apply(cmd);
            e.Snapshot.Reference.Child("consumed").SetValueAsync(true);
        }

        private static void LogFault(System.Threading.Tasks.Task t)
        {
            if (t.IsFaulted) Debug.LogWarning($"[Telemetry] write failed: {t.Exception?.GetBaseException().Message}");
        }

        private void OnDestroy()
        {
            if (_pendingOverrides != null) _pendingOverrides.ChildAdded -= OnOverride;
            if (stateMachine == null) return;
            stateMachine.OnStepEvent -= LogStep;
            stateMachine.OnStateChanged -= PushLiveStatus;
        }
    }
}
