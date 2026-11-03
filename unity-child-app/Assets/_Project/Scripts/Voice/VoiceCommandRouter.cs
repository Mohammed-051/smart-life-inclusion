using UnityEngine;
using SmartLife.Core;
using SmartLife.Telemetry;

namespace SmartLife.Voice
{
    /// <summary>
    /// Maps guardian phrases to OverrideCommand. Only active while the guardian enables it,
    /// so the child's speech never triggers overrides. Audio/transcripts are never stored.
    /// </summary>
    public class VoiceCommandRouter : MonoBehaviour
    {
        private const float MinConfidence = 0.6f;

        [SerializeField] private TaskStateMachine stateMachine;
        [SerializeField] private TelemetryService telemetry;

        private IVoiceRecognizer _recognizer;

        public bool Enabled { get; private set; }

        public void Init(IVoiceRecognizer recognizer)
        {
            _recognizer = recognizer;
            _recognizer.OnPhrase += HandlePhrase;
        }

        public void SetEnabled(bool on)
        {
            Enabled = on;
            if (on) _recognizer?.StartListening(); else _recognizer?.StopListening();
        }

        /// <summary>Pure mapping, unit-tested in EditMode.</summary>
        public static bool TryParse(string phrase, out OverrideCommand cmd)
        {
            phrase = phrase?.ToLowerInvariant() ?? string.Empty;
            // Pause first: safety command wins if phrases overlap.
            if (phrase.Contains("pause") || phrase.Contains("stop") || phrase.Contains("emergency")) { cmd = OverrideCommand.Pause; return true; }
            if (phrase.Contains("resume") || phrase.Contains("continue")) { cmd = OverrideCommand.Resume; return true; }
            if (phrase.Contains("skip") || phrase.Contains("next step")) { cmd = OverrideCommand.Skip; return true; }
            if (phrase.Contains("reset") || phrase.Contains("start over")) { cmd = OverrideCommand.Reset; return true; }
            cmd = default;
            return false;
        }

        private void HandlePhrase(string phrase, float confidence)
        {
            if (!Enabled || confidence < MinConfidence || !TryParse(phrase, out var cmd)) return;
            stateMachine.Apply(cmd);
            telemetry.IssueOverride(cmd, "voice");
        }

        private void OnDestroy()
        {
            if (_recognizer != null) _recognizer.OnPhrase -= HandlePhrase;
        }
    }
}
