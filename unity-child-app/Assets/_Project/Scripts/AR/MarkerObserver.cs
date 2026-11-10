using UnityEngine;
using Vuforia;
using SmartLife.Core;

namespace SmartLife.AR
{
    /// <summary>
    /// Attach to each Vuforia ImageTarget (replaces DefaultObserverEventHandler on that target).
    /// Forwards TRACKED detections to the state machine with a debounce, so jitter or
    /// re-acquisition never double-fires a checkpoint.
    /// </summary>
    public class MarkerObserver : DefaultObserverEventHandler
    {
        private const float DebounceSeconds = 2f;

        [SerializeField] private TaskStateMachine stateMachine;
        [Tooltip("Defaults to the ImageTarget name if empty")]
        [SerializeField] private string markerIdOverride;

        private float _lastFiredAt = -999f;
        private RescanGate _rescan;

        /// <summary>Wires an observer added at runtime. Call right after AddComponent, before its Start runs.</summary>
        public void Bind(TaskStateMachine machine, string markerId)
        {
            stateMachine = machine;
            markerIdOverride = markerId;
        }
        private string MarkerId => string.IsNullOrEmpty(markerIdOverride) ? mObserverBehaviour.TargetName : markerIdOverride;

        // Deterministic: only full TRACKED pose triggers a transition, never EXTENDED_TRACKED.
        private bool IsTracked => mObserverBehaviour != null && mObserverBehaviour.TargetStatus.Status == Status.TRACKED;

        protected override void OnTrackingFound()
        {
            base.OnTrackingFound(); // enables child renderers (avatar/arrow anchor)
            // A scan during a hold is ignored, so it must not start the debounce and delay the
            // pickup in Update when the hold ends.
            if (IsTracked && !stateMachine.IsHolding && _rescan.CanFire) Register();
        }

        protected override void OnTrackingLost()
        {
            base.OnTrackingLost();
            _rescan.MarkLost(); // a marker shared by two steps fires again only after it left the view
        }

        /// <summary>
        /// Vuforia only reports a marker when it is newly found. If this marker stays in view until
        /// its step comes up (such as when a hold timer ends), register it here so the child never
        /// has to look away and back. Only the expected marker fires, so no wrong-marker hint appears.
        /// </summary>
        private void Update()
        {
            if (IsTracked && _rescan.CanFire && IsExpectedNext()) Register();
        }

        private bool IsExpectedNext()
        {
            if (stateMachine.State != MachineState.Awaiting || stateMachine.Task == null) return false;
            int step = stateMachine.StepIndex;
            return step < stateMachine.Task.Count && stateMachine.Task[step].markerId == MarkerId;
        }

        private void Register()
        {
            if (Time.unscaledTime - _lastFiredAt < DebounceSeconds) return;

            // Consume the debounce and the rescan gate only if the machine acted on the scan. An ignored
            // scan (Idle, Paused, holding, wrong marker) must leave Update's pickup free to fire later.
            var stateBefore = stateMachine.State;
            int stepBefore = stateMachine.StepIndex;
            stateMachine.HandleMarker(MarkerId);
            if (stateMachine.State == stateBefore && stateMachine.StepIndex == stepBefore) return;

            _lastFiredAt = Time.unscaledTime;
            _rescan.MarkFired();
        }
    }
}
