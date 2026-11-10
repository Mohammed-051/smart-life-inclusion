using System;
using System.Collections.Generic;
using SmartLife.Core;
using UnityEngine;
using Vuforia;

namespace SmartLife.AR
{
    /// <summary>
    /// Creates one Vuforia Image Target per marker in the <see cref="MarkerCatalog"/> once Vuforia has
    /// started, and puts a <see cref="MarkerObserver"/> on each so detections reach the state machine.
    /// Only creates targets; it never changes task state itself.
    /// </summary>
    public class ImageTargetRegistry : MonoBehaviour
    {
        /// <summary>The registry in the boot scene, for step scenes that anchor content to a marker.</summary>
        public static ImageTargetRegistry Current { get; private set; }

        private readonly Dictionary<string, ImageTargetBehaviour> _targets = new Dictionary<string, ImageTargetBehaviour>();
        private TaskStateMachine _machine;
        private bool _created;
        private bool _subscribed;

        public int TargetCount { get; private set; }

        /// <summary>Raised once, after the targets exist. Late subscribers should check <see cref="TryGetTarget"/> first.</summary>
        public event Action TargetsReady;

        public void Init(TaskStateMachine machine)
        {
            _machine = machine;
            Current = this;
            if (!Application.isPlaying) return; // Vuforia only runs in Play mode and on device.
            if (VuforiaApplication.Instance.IsRunning) CreateTargets();
            else
            {
                VuforiaApplication.Instance.OnVuforiaStarted += CreateTargets;
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_subscribed) VuforiaApplication.Instance.OnVuforiaStarted -= CreateTargets;
            if (Current == this) Current = null;
        }

        /// <summary>The runtime Image Target for a marker, once Vuforia has created it.</summary>
        public bool TryGetTarget(string markerId, out ImageTargetBehaviour target) =>
            _targets.TryGetValue(markerId, out target);

        private void CreateTargets()
        {
            // OnVuforiaStarted fires again after the app resumes; the targets already exist by then.
            if (_created) return;
            _created = true;

            MarkerCatalog catalog = MarkerCatalog.LoadFromResources();
            if (catalog == null)
            {
                Debug.LogError("[ImageTargetRegistry] Resources/Markers/catalog.json is missing, so no marker can be tracked.", this);
                return;
            }

            foreach (string markerId in catalog.MarkerIds)
            {
                Texture2D image = MarkerCatalog.LoadImage(markerId);
                if (image == null)
                {
                    Debug.LogError($"[ImageTargetRegistry] Marker image '{markerId}' is missing or unreadable.", this);
                    continue;
                }

                ImageTargetBehaviour target =
                    VuforiaBehaviour.Instance.ObserverFactory.CreateImageTarget(image, catalog.PrintedWidthMeters, markerId);
                if (target == null)
                {
                    Debug.LogError($"[ImageTargetRegistry] Vuforia could not create a target from '{markerId}'.", this);
                    continue;
                }

                target.gameObject.AddComponent<MarkerObserver>().Bind(_machine, markerId);
                _targets[markerId] = target;
                TargetCount++;
            }

            Debug.Log($"[ImageTargetRegistry] {TargetCount} of {catalog.MarkerIds.Count} marker targets ready.", this);
            TargetsReady?.Invoke();
        }
    }

    /// <summary>
    /// Re-scan rule for steps that share a marker: once a marker has fired, it may not fire again
    /// from the same still-visible detection. Tracking must be lost, then found, in between.
    /// </summary>
    public struct RescanGate
    {
        private bool _fired;

        /// <summary>False while the marker that already fired has stayed in view.</summary>
        public bool CanFire => !_fired;
        public void MarkFired() => _fired = true;
        public void MarkLost() => _fired = false;
    }
}
