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
        private string MarkerId => string.IsNullOrEmpty(markerIdOverride) ? mObserverBehaviour.TargetName : markerIdOverride;

        protected override void OnTrackingFound()
        {
            base.OnTrackingFound(); // enables child renderers (avatar/arrow anchor)

            // Deterministic: only full TRACKED pose triggers a transition, never EXTENDED_TRACKED.
            if (mObserverBehaviour.TargetStatus.Status != Status.TRACKED) return;
            if (Time.unscaledTime - _lastFiredAt < DebounceSeconds) return;

            _lastFiredAt = Time.unscaledTime;
            stateMachine.HandleMarker(MarkerId);
        }
    }
}
