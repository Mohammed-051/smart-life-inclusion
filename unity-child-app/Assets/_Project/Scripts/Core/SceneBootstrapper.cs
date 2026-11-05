using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SmartLife.Telemetry;
using SmartLife.UI;
using SmartLife.Voice;

namespace SmartLife.Core
{
    /// <summary>
    /// Scene entry point. Passes the guardian gate first, then initializes services and starts the
    /// configured task, so the state machine leaves Idle and begins accepting markers. A missing
    /// service never stops the task. The gate needs no scene wiring: it is added here at runtime.
    /// </summary>
    [RequireComponent(typeof(TaskStateMachine))]
    public class SceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private TaskDefinition _taskDefinition;
        [SerializeField] private TelemetryService telemetry;
        [SerializeField] private VoiceCommandRouter voiceRouter;

        private TaskStateMachine _machine;
        private IDeviceAuth _auth;
        private GuardianMenu _menu;

        private void Start()
        {
            _machine = GetComponent<TaskStateMachine>();

            // Begin() would make InstructionCard read a prompt from a missing task, so stop here instead.
            if (_taskDefinition == null)
            {
                Debug.LogError("[SceneBootstrapper] Assign a TaskDefinition before entering Play mode.", this);
                return;
            }

            if (telemetry == null || !telemetry.isActiveAndEnabled)
            {
                Debug.LogWarning("[SceneBootstrapper] TelemetryService is missing or disabled, so telemetry is off.", this);
                StartTask(null);
                return;
            }

            _auth = gameObject.AddComponent<DeviceAuthService>();
            _menu = gameObject.AddComponent<GuardianMenu>();
            _menu.Open(_auth, new GuardianPinStore(new PlayerPrefsStore()), StartTask);
        }

        /// <summary>A null session means no sign-in: the task runs with telemetry off.</summary>
        private void StartTask(DeviceSession session)
        {
            if (session != null) telemetry.Init(session.StudentId, session.Uid);
            else if (telemetry != null) Debug.LogWarning("[SceneBootstrapper] Not signed in, so telemetry is off.", this);

            if (voiceRouter != null && voiceRouter.isActiveAndEnabled) voiceRouter.Init(new NoOpVoiceRecognizer());
            else Debug.LogWarning("[SceneBootstrapper] VoiceCommandRouter is missing or disabled, so voice commands are off.", this);

            _machine.Begin(_taskDefinition);
            if (_menu != null) _menu.EnableMenuAccess(BuildMenu(session != null));
        }

        private List<GuardianMenu.MenuAction> BuildMenu(bool signedIn)
        {
            var actions = new List<GuardianMenu.MenuAction>
            {
                new GuardianMenu.MenuAction("Skip this step", () => Override(OverrideCommand.Skip)),
                new GuardianMenu.MenuAction("Reset task", () => Override(OverrideCommand.Reset)),
                new GuardianMenu.MenuAction("Pause", () => Override(OverrideCommand.Pause)),
                new GuardianMenu.MenuAction("Resume", () => Override(OverrideCommand.Resume)),
            };
            if (signedIn) actions.Add(new GuardianMenu.MenuAction("Sign out this tablet", SignOut));
            return actions;
        }

        private void Override(OverrideCommand command)
        {
            _machine.Apply(command);
            if (telemetry != null) telemetry.IssueOverride(command, "menu");
        }

        private void SignOut()
        {
            _auth.SignOut();
            PauseLock.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
