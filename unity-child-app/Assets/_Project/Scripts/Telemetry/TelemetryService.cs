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
    /// Writes checkpoint logs, step metrics and live status to Firebase RTDB and listens for dashboard
    /// overrides. Writes are fire-and-forget with offline persistence so the child is never blocked by network.
    /// </summary>
    public partial class TelemetryService : MonoBehaviour
    {
        private const float LivePushSeconds = 5f;
        private const int MsPerSecond = 1000;

        [SerializeField] private TaskStateMachine stateMachine;

        private readonly NodeMetricsTracker _metrics = new NodeMetricsTracker();
        private DatabaseReference _root;
        private string _studentId;
        private string _sessionId;
        private string _uid;
        private int _reached, _autonomous;
        private Query _pendingOverrides;
        private bool _initStarted;
        private float _nextLivePush;

        /// <summary>
        /// Checks Firebase, then connects in the background so the child is never blocked. Any failure,
        /// such as a missing config file, leaves telemetry off with a warning. The uid comes from the
        /// guardian sign-in, and database rules require it to match the /devices mapping for this student.
        /// </summary>
        public void Init(string studentId, string uid)
        {
            if (_initStarted) return;
            if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[Telemetry] Off: no signed-in device.", this);
                return;
            }
            _initStarted = true;
            _uid = uid;

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
                // Database rules reject a write from any other account, so don't queue writes that can only fail.
                if (FirebaseAuth.DefaultInstance.CurrentUser?.UserId != _uid)
                {
                    Debug.LogWarning("[Telemetry] Off: this device is not signed in to Firebase.", this);
                    return;
                }

                DatabaseSetup.Ensure();
                _root = FirebaseDatabase.DefaultInstance.RootReference;
                _studentId = studentId;

                stateMachine.OnStepEvent += LogStep;
                stateMachine.OnStateChanged += PushLiveStatus;
                stateMachine.OnUnexpectedMarker += LogWrongScan;
                _pendingOverrides = _root.Child(DbPaths.Overrides(_studentId)).OrderByChild("consumed").EqualTo(false);
                _pendingOverrides.ChildAdded += OnOverride;
                ConnectEmergencySignaling();

                // The task may already have begun while Firebase was starting.
                if (stateMachine.State == MachineState.Awaiting) PushLiveStatus(stateMachine.State, stateMachine.StepIndex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Telemetry] Off: could not connect to Firebase. {e.GetType().Name}", this);
            }
        }

        /// <summary>Open a new session row for the current task.</summary>
        public void StartSession()
        {
            if (_root == null) return;
            var ses = _root.Child(DbPaths.Sessions(_studentId)).Push();
            _sessionId = ses.Key;
            _reached = _autonomous = 0;
            _metrics.NewSession();
            ses.SetValueAsync(new Dictionary<string, object> {
                { "routeId", stateMachine.Task.RouteId }, { "startedAt", ServerValue.Timestamp },
                { "status", "active" }, { "checkpointsReached", 0 }, { "autonomousCount", 0 }
            }).ContinueWithOnMainThread(LogFault);
        }

        /// <summary>Record a step event; same call is used for voice/dashboard override outcomes.</summary>
        public void IssueOverride(OverrideCommand cmd, string source)
        {
            // A guardian can act while telemetry is off or still connecting. The local override already applied.
            if (_root == null) return;
            _root.Child(DbPaths.Overrides(_studentId)).Push().SetValueAsync(new Dictionary<string, object> {
                { "command", cmd.ToString().ToLowerInvariant() }, { "source", source },
                { "issuedBy", _uid }, { "timestamp", ServerValue.Timestamp }, { "consumed", true }
            }).ContinueWithOnMainThread(LogFault);
        }

        /// <summary>The AAC board opened on the current step.</summary>
        public void RecordAacOpened() => _metrics.RecordAacOpened();

        /// <summary>The child chose a symbol. Saved as a short stable ID on the step's metrics.</summary>
        public void RecordAacSelection(string selection)
        {
            _metrics.RecordAacSelection(selection);
            // Right away, so a child's "Need help" reaches the dashboard while the step is still open.
            if (_root != null && _sessionId != null) PushLiveStatus(stateMachine.State, stateMachine.StepIndex);
        }

        private void Update()
        {
            if (_root == null || _sessionId == null) return;
            MachineState state = stateMachine.State;
            if (state is MachineState.Paused or MachineState.Complete or MachineState.Idle) return;

            bool holding = stateMachine.IsHolding;
            _metrics.Tick(Time.deltaTime, !holding, holding);

            // Lets the dashboard's hesitation number move while a step is still open.
            if (Time.time >= _nextLivePush) PushLiveStatus(state, stateMachine.StepIndex);
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
            }
            if (status is TaskStatus.Completed or TaskStatus.Skipped or TaskStatus.Reset)
            {
                WriteNodeMetric(stepIndex, step.checkpointId, status, autonomous);
                _metrics.NextStep();
                WriteSessionTotals();
            }
        }

        private void WriteNodeMetric(int stepIndex, string checkpointId, TaskStatus status, bool autonomous)
        {
            var metric = new Dictionary<string, object> {
                { "checkpointId", checkpointId }, { "stepIndex", stepIndex },
                { "hesitationMs", ToMs(_metrics.StepHesitationSeconds) }, { "holdMs", ToMs(_metrics.StepHoldSeconds) },
                { "wrongScans", _metrics.StepWrongScans }, { "aacOpened", _metrics.StepAacOpened },
                { "outcome", status.ToString().ToLowerInvariant() }, { "autonomous", autonomous },
                { "timestamp", ServerValue.Timestamp }
            };
            if (_metrics.StepAacSelection != null) metric["aacSelection"] = _metrics.StepAacSelection;
            _root.Child(DbPaths.NodeMetrics(_studentId, _sessionId)).Push().SetValueAsync(metric).ContinueWithOnMainThread(LogFault);
        }

        private void WriteSessionTotals()
        {
            _root.Child(DbPaths.Sessions(_studentId)).Child(_sessionId).UpdateChildrenAsync(new Dictionary<string, object> {
                { "checkpointsReached", _reached }, { "autonomousCount", _autonomous },
                { "activeDurationMs", ToMs(_metrics.SessionActiveSeconds) },
                { "hesitationTotalMs", ToMs(_metrics.SessionHesitationSeconds) },
                { "wrongScanCount", _metrics.SessionWrongScans }
            }).ContinueWithOnMainThread(LogFault);
        }

        private void LogWrongScan(int stepIndex)
        {
            if (_sessionId == null) return;
            _metrics.RecordWrongScan();
            WriteSessionTotals();
            PushLiveStatus(stateMachine.State, stepIndex);
        }

        private void PushLiveStatus(MachineState state, int stepIndex)
        {
            if (_sessionId == null)
            {
                if (state != MachineState.Awaiting) return;
                StartSession();
            }

            _nextLivePush = Time.time + LivePushSeconds;
            var step = stateMachine.Task[Mathf.Min(stepIndex, stateMachine.Task.Count - 1)];
            var live = new Dictionary<string, object> {
                { "sessionId", _sessionId }, { "routeId", stateMachine.Task.RouteId }, { "checkpointId", step.checkpointId },
                { "progressPct", Mathf.RoundToInt(stateMachine.ProgressPct) }, { "state", state.ToString().ToLowerInvariant() },
                { "stepHesitationMs", ToMs(_metrics.StepHesitationSeconds) }, { "wrongScanCount", _metrics.SessionWrongScans },
                { "lastSeen", ServerValue.Timestamp }
            };
            if (_metrics.StepAacSelection != null) live["aacRequest"] = _metrics.StepAacSelection;
            _root.Child(DbPaths.LiveStatus(_studentId)).SetValueAsync(live).ContinueWithOnMainThread(LogFault);

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

        private static long ToMs(float seconds) => (long)(seconds * MsPerSecond);

        private static void LogFault(System.Threading.Tasks.Task t)
        {
            if (t.IsFaulted) Debug.LogWarning($"[Telemetry] write failed: {t.Exception?.GetBaseException().Message}");
        }

        private void OnDestroy()
        {
            DisconnectEmergencySignaling();
            if (_pendingOverrides != null) _pendingOverrides.ChildAdded -= OnOverride;
            if (stateMachine == null) return;
            stateMachine.OnStepEvent -= LogStep;
            stateMachine.OnStateChanged -= PushLiveStatus;
            stateMachine.OnUnexpectedMarker -= LogWrongScan;
        }
    }
}
