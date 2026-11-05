using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SmartLife.AR;
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
        private EmergencyDetector _emergency;
        private EmergencyCoordinator _emergencies;
        private bool _soundAlertsOn;

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

            if (voiceRouter != null && voiceRouter.isActiveAndEnabled) voiceRouter.Init(new AndroidSpeechRecognizer());
            else Debug.LogWarning("[SceneBootstrapper] VoiceCommandRouter is missing or disabled, so voice commands are off.", this);

            _emergency = GetComponent<EmergencyDetector>();
            if (_emergency != null)
            {
                _emergencies = gameObject.AddComponent<EmergencyCoordinator>();
                _emergencies.Init(_emergency, session != null ? telemetry : null, _taskDefinition.PrivateSpace, ReleaseMicrophone);
            }

            var hesitation = GetComponent<HesitationMonitor>();
            if (hesitation != null)
                gameObject.AddComponent<AacBoard>().Init(_machine, hesitation, telemetry, GetComponent<MascotStateManager>());

            if (session == null)
            {
                BeginTask(_taskDefinition, false);
                return;
            }
            _auth.FetchRoutine(_taskDefinition.RouteId, routine => BeginTask(_taskDefinition.Arranged(routine), true));
        }

        private void BeginTask(TaskDefinition task, bool signedIn)
        {
            _machine.Begin(task);
            if (_menu != null) _menu.EnableMenuAccess(BuildMenu(signedIn));
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
            if (voiceRouter != null)
                actions.Add(new GuardianMenu.MenuAction(() => voiceRouter.Enabled ? "Voice commands: on" : "Voice commands: off", ToggleVoice));
            if (_emergency != null)
                actions.Add(new GuardianMenu.MenuAction(() => _soundAlertsOn ? "Sound alerts: on" : "Sound alerts: off", ToggleSoundAlerts));
            if (_emergencies != null)
            {
                actions.Add(new GuardianMenu.MenuAction("End camera or microphone call", _emergencies.EndStream));
                actions.Add(new GuardianMenu.MenuAction("Emergency is over", _emergencies.ResolveFromTablet));
            }
            if (signedIn) actions.Add(new GuardianMenu.MenuAction("Sign out this tablet", SignOut));
            return actions;
        }

        // Android's recognizer and Unity's Microphone can't share the mic, so these two switches exclude each other.
        private void ToggleVoice()
        {
            bool turnOn = !voiceRouter.Enabled;
            if (turnOn) SetSoundAlerts(false);
            voiceRouter.SetEnabled(turnOn);
        }

        private void ToggleSoundAlerts()
        {
            bool turnOn = !_soundAlertsOn;
            if (turnOn)
            {
                if (voiceRouter != null) voiceRouter.SetEnabled(false);
                // The prompt appears now. The guardian switches this on again after allowing it.
                if (!AndroidSpeechRecognizer.EnsureMicrophonePermission()) return;
            }
            SetSoundAlerts(turnOn);
        }

        private void ReleaseMicrophone()
        {
            if (voiceRouter != null) voiceRouter.SetEnabled(false);
            SetSoundAlerts(false);
        }

        private void SetSoundAlerts(bool on)
        {
            _soundAlertsOn = on;
            if (_emergency == null) return;
            if (on) _emergency.StartAudioMonitoring();
            else _emergency.StopAudioMonitoring();
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
