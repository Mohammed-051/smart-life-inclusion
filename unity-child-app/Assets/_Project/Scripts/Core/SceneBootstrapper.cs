using UnityEngine;
using SmartLife.Telemetry;
using SmartLife.Voice;

namespace SmartLife.Core
{
    /// <summary>
    /// Scene entry point. Initializes services, then starts the configured task so the state
    /// machine leaves Idle and begins accepting markers. A missing service never stops the task.
    /// </summary>
    [RequireComponent(typeof(TaskStateMachine))]
    public class SceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private TaskDefinition _taskDefinition;
        [SerializeField] private TelemetryService telemetry;
        [SerializeField] private VoiceCommandRouter voiceRouter;
        // shortcut: fixed student, read it from /devices/{uid}.studentId once tablet sign-in exists.
        [SerializeField] private string studentId = "stu_001";

        private void Start()
        {
            InitServices();

            // Begin() would make InstructionCard read a prompt from a missing task, so stop here instead.
            if (_taskDefinition == null)
            {
                Debug.LogError("[SceneBootstrapper] Assign a TaskDefinition before entering Play mode.", this);
                return;
            }

            GetComponent<TaskStateMachine>().Begin(_taskDefinition);
        }

        private void InitServices()
        {
            if (telemetry != null && telemetry.isActiveAndEnabled) telemetry.Init(studentId);
            else Debug.LogWarning("[SceneBootstrapper] TelemetryService is missing or disabled, so telemetry is off.", this);

            if (voiceRouter != null && voiceRouter.isActiveAndEnabled) voiceRouter.Init(new NoOpVoiceRecognizer());
            else Debug.LogWarning("[SceneBootstrapper] VoiceCommandRouter is missing or disabled, so voice commands are off.", this);
        }
    }
}
