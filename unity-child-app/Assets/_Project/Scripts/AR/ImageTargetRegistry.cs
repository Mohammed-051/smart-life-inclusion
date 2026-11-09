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
        private TaskStateMachine _machine;
        private bool _created;
        private bool _subscribed;

        public int TargetCount { get; private set; }

        public void Init(TaskStateMachine machine)
        {
            _machine = machine;
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
        }

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
                TargetCount++;
            }

            Debug.Log($"[ImageTargetRegistry] {TargetCount} of {catalog.MarkerIds.Count} marker targets ready.", this);
        }
    }
}
