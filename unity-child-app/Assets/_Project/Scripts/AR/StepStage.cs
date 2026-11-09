using SmartLife.Core;
using UnityEngine;
using Vuforia;

namespace SmartLife.AR
{
    /// <summary>
    /// Root of a step scene's AR content. On load it moves its content onto the step's printed marker
    /// (a runtime Image Target in the boot scene) and shows it only while that marker is tracked.
    /// Hides the mascot when the learner's profile turns it off. Never changes task state.
    /// </summary>
    public class StepStage : MonoBehaviour
    {
        [Tooltip("Marker this step's content sits on, e.g. MK_SINK")]
        [SerializeField] private string markerId;
        [Tooltip("Everything shown on the marker; moved under the Image Target at runtime")]
        [SerializeField] private Transform content;
        [Tooltip("Placeholder mascot; hidden when the profile turns the mascot off")]
        [SerializeField] private GameObject mascot;

        private ObserverBehaviour _target;

        public string MarkerId => markerId;

        private void Start()
        {
            if (content == null) return;

            StepSceneLoader loader = StepSceneLoader.Current;
            if (mascot != null && loader != null && !loader.Profile.Mascot) mascot.SetActive(false);

            // Without a running Vuforia (Editor preview) the content stays where the scene put it.
            ImageTargetRegistry registry = ImageTargetRegistry.Current;
            if (registry == null || !registry.TryGetTarget(markerId, out ImageTargetBehaviour target)) return;

            _target = target;
            content.SetParent(target.transform, false);
            target.OnTargetStatusChanged += HandleStatus;
            HandleStatus(target, target.TargetStatus);
        }

        private void HandleStatus(ObserverBehaviour behaviour, TargetStatus status)
        {
            if (content == null) return;
            bool visible = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;
            content.gameObject.SetActive(visible);
        }

        // The content now lives under a target in the boot scene, so unloading this scene would leave it behind.
        private void OnDestroy()
        {
            if (_target != null) _target.OnTargetStatusChanged -= HandleStatus;
            if (content != null && _target != null) Destroy(content.gameObject);
        }
    }
}
