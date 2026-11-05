using System;
using UnityEngine;

namespace SmartLife.Core
{
    public enum EmergencySource { Audio, Imu, Sos }

    /// <summary>
    /// Watches microphone level, accelerometer and the SOS button, then pauses the task and raises
    /// OnEmergency. Audio stays in memory and is never stored or sent. Threshold defaults are
    /// starting values that need calibrating on the target tablet.
    /// See docs/clinical_architecture.md section 3.3.
    /// </summary>
    public class EmergencyDetector : MonoBehaviour
    {
        private const int SampleRate = 16000;
        private const int WindowSamples = 1024;

        [SerializeField] private TaskStateMachine stateMachine;
        [SerializeField] private float cooldownSeconds = 10f;

        [Header("Audio")]
        [SerializeField] private float audioRmsThreshold = 0.6f;
        [SerializeField] private float audioSustainSeconds = 0.5f;

        [Header("IMU (in g)")]
        [SerializeField] private float freeFallG = 0.3f;
        [SerializeField] private float freeFallSeconds = 0.1f;
        [SerializeField] private float impactG = 2.5f;
        [SerializeField] private float impactWindowSeconds = 0.5f;

        /// <summary>Consumed by the calm help card and Telemetry.</summary>
        public event Action<EmergencySource> OnEmergency;

        private readonly float[] _samples = new float[WindowSamples];
        private AudioClip _micClip;
        private float _cooldown;
        private float _audioAbove;
        private float _fallTime;
        private float _sinceFall = float.PositiveInfinity;

        /// <summary>
        /// Start audio monitoring. Call only after guardian consent and microphone permission, and
        /// stop it while guardian voice control is on, since the recognizer needs the microphone.
        /// </summary>
        public void StartAudioMonitoring()
        {
            if (_micClip != null) return;
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[EmergencyDetector] No microphone found, so audio monitoring is off.", this);
                return;
            }
            _micClip = Microphone.Start(null, true, 1, SampleRate);
        }

        public void StopAudioMonitoring()
        {
            if (_micClip == null) return;
            Microphone.End(null);
            _micClip = null;
            _audioAbove = 0f;
        }

        /// <summary>Wire the UI SOS button to this.</summary>
        public void TriggerSos() => Raise(EmergencySource.Sos);

        private void Update()
        {
            // Unscaled, so detection keeps working while the AAC board or guardian menu freezes the clock.
            float dt = Time.unscaledDeltaTime;
            if (_cooldown > 0f) _cooldown -= dt;
            FeedImu(Input.acceleration.magnitude, dt);
            if (_micClip != null && TryReadRms(out float rms)) FeedAudio(rms, dt);
        }

        private void OnDisable() => StopAudioMonitoring();

        /// <summary>Free fall followed by an impact, so a hard shake alone does not fire.</summary>
        private void FeedImu(float magnitudeG, float deltaTime)
        {
            if (magnitudeG < freeFallG)
            {
                _fallTime += deltaTime;
                if (_fallTime >= freeFallSeconds) _sinceFall = 0f;
                return;
            }
            _fallTime = 0f;
            _sinceFall += deltaTime;
            if (magnitudeG > impactG && _sinceFall <= impactWindowSeconds)
            {
                _sinceFall = float.PositiveInfinity;
                Raise(EmergencySource.Imu);
            }
        }

        // shortcut: RMS level cannot tell distress from a loud room, swap in the classifier once one is chosen.
        private void FeedAudio(float rms, float deltaTime)
        {
            _audioAbove = rms > audioRmsThreshold ? _audioAbove + deltaTime : 0f;
            if (_audioAbove < audioSustainSeconds) return;
            _audioAbove = 0f;
            Raise(EmergencySource.Audio);
        }

        private bool TryReadRms(out float rms)
        {
            rms = 0f;
            int start = Microphone.GetPosition(null) - WindowSamples;
            if (start < 0) return false;
            _micClip.GetData(_samples, start);
            float sum = 0f;
            for (int i = 0; i < WindowSamples; i++) sum += _samples[i] * _samples[i];
            rms = Mathf.Sqrt(sum / WindowSamples);
            return true;
        }

        private void Raise(EmergencySource source)
        {
            // SOS always gets through: a child's press must never be swallowed by the cooldown.
            if (_cooldown > 0f && source != EmergencySource.Sos) return;
            _cooldown = cooldownSeconds;
            if (stateMachine != null) stateMachine.Apply(OverrideCommand.Pause);
            OnEmergency?.Invoke(source);
        }
    }
}
