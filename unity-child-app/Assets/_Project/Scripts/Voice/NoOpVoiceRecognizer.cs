using System;

namespace SmartLife.Voice
{
    /// <summary>
    /// shortcut: hears nothing, so voice commands do nothing. Replace with the Android
    /// SpeechRecognizer plugin (docs/ARCHITECTURE.md) when it exists.
    /// </summary>
    public class NoOpVoiceRecognizer : IVoiceRecognizer
    {
        public event Action<string, float> OnPhrase { add { } remove { } }
        public void StartListening() { }
        public void StopListening() { }
    }
}
