using System;

namespace SmartLife.Voice
{
    /// <summary>Platform speech recognizer abstraction (Android plugin in builds, keyboard stub in Editor).</summary>
    public interface IVoiceRecognizer
    {
        /// <summary>Raised with a lowercase transcript and confidence 0..1.</summary>
        event Action<string, float> OnPhrase;
        void StartListening();
        void StopListening();
    }
}
