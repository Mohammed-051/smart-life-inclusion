using System;

namespace SmartLife.Voice
{
    /// <summary>Platform speech recognizer abstraction (AndroidSpeechRecognizer in builds, silent in the Editor).</summary>
    public interface IVoiceRecognizer
    {
        /// <summary>Raised with a lowercase transcript and confidence 0..1.</summary>
        event Action<string, float> OnPhrase;
        void StartListening();
        void StopListening();
    }
}
