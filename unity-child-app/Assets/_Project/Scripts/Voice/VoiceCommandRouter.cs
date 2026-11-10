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
        /// <summary>Set by the guardian menu. While false every phrase is ignored and the mic is off.</summary>
        public bool GuardianUnlocked { get; private set; }

        public void SetGuardianUnlocked(bool unlocked)
        {
            GuardianUnlocked = unlocked;
            if (!unlocked) SetEnabled(false);
        }

        public void Init(IVoiceRecognizer recognizer)
        {
            _recognizer = recognizer;
            _recognizer.OnPhrase += HandlePhrase;
        }

        public void SetEnabled(bool on)
        {
            on &= GuardianUnlocked;
            Enabled = on;
            if (on) _recognizer?.StartListening(); else _recognizer?.StopListening();
        }

        /// <summary>Pure mapping, unit-tested in EditMode.</summary>
        public static bool TryParse(string phrase, out OverrideCommand cmd)
        {
            phrase = phrase?.ToLowerInvariant() ?? string.Empty;
            // No bare "stop": children say it naturally. Pause first: safety command wins if phrases overlap.
            if (phrase.Contains("pause") || phrase.Contains("emergency")) { cmd = OverrideCommand.Pause; return true; }
            if (phrase.Contains("resume") || phrase.Contains("continue")) { cmd = OverrideCommand.Resume; return true; }
            if (phrase.Contains("skip") || phrase.Contains("next step")) { cmd = OverrideCommand.Skip; return true; }
            if (phrase.Contains("reset") || phrase.Contains("start over")) { cmd = OverrideCommand.Reset; return true; }
            cmd = default;
            return false;
        }

        /// <summary>The gate: a phrase counts only while the guardian is unlocked and listening.</summary>
        public bool Accepts(string phrase, float confidence, out OverrideCommand cmd)
        {
            cmd = default;
            return GuardianUnlocked && Enabled && confidence >= MinConfidence && TryParse(phrase, out cmd);
        }

        private void HandlePhrase(string phrase, float confidence)
        {
            if (!Accepts(phrase, confidence, out var cmd)) return;
            stateMachine.Apply(cmd);
            telemetry.IssueOverride(cmd, "voice");
        }

        private void OnDestroy()
        {
            if (_recognizer != null) _recognizer.OnPhrase -= HandlePhrase;
        }
    }
}
