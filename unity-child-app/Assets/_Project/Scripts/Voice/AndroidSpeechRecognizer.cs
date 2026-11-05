using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Android;

namespace SmartLife.Voice
{
    /// <summary>
    /// Guardian voice commands through Android's SpeechRecognizer, with no plugin. It prefers on-device
    /// recognition, and only the resolved text is passed on: no audio or transcript is stored
    /// (docs/SECURITY.md). Everywhere except an Android player it stays silent, which keeps it safe in the
    /// Editor. Android requires SpeechRecognizer calls on the UI thread, and its callbacks arrive there
    /// too, so results are posted back to Unity's main thread.
    /// </summary>
    public sealed class AndroidSpeechRecognizer : IVoiceRecognizer, IDisposable
    {
        private const int RestartAfterResultMs = 250;
        private const int RestartAfterErrorMs = 1000;
        private const int ErrorClient = 5;
        private const int ErrorInsufficientPermissions = 9;
        private const float AssumedConfidence = 0.7f;

        public event Action<string, float> OnPhrase;

        private readonly SynchronizationContext _unityThread = SynchronizationContext.Current;
        private AndroidJavaObject _activity;
        private AndroidJavaObject _recognizer;
        private AndroidJavaObject _intent;
        private AndroidJavaObject _handler;
        private volatile bool _wanted;

        private static bool IsAndroidPlayer => Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// True when the microphone can be used now. Otherwise shows the permission prompt once, and
        /// onGranted runs if the guardian allows it.
        /// </summary>
        public static bool EnsureMicrophonePermission(Action onGranted = null)
        {
            if (!IsAndroidPlayer || Permission.HasUserAuthorizedPermission(Permission.Microphone)) return true;
            var callbacks = new PermissionCallbacks();
            if (onGranted != null) callbacks.PermissionGranted += _ => onGranted();
            Permission.RequestUserPermission(Permission.Microphone, callbacks);
            return false;
        }

        public void StartListening()
        {
            _wanted = true;
            if (!IsAndroidPlayer) return;
            if (!EnsureMicrophonePermission(StartIfWanted)) return;
            RunOnUiThread(BeginListening);
        }

        public void StopListening()
        {
            _wanted = false;
            if (!IsAndroidPlayer || _recognizer == null) return;
            RunOnUiThread(() => _recognizer?.Call("cancel"));
        }

        public void Dispose()
        {
            _wanted = false;
            if (!IsAndroidPlayer || _recognizer == null) return;
            RunOnUiThread(DestroyRecognizer);
        }

        private void StartIfWanted()
        {
            if (_wanted) RunOnUiThread(BeginListening);
        }

        // ---------- UI thread only ----------

        private void BeginListening()
        {
            if (!_wanted) return;
            if (_recognizer == null && !CreateRecognizer()) return;
            _recognizer.Call("startListening", _intent);
        }

        private bool CreateRecognizer()
        {
            using var recognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer");
            if (!recognizerClass.CallStatic<bool>("isRecognitionAvailable", _activity))
            {
                _wanted = false;
                Debug.LogWarning("[Voice] Off: this tablet has no speech recognition service.");
                return false;
            }

            _recognizer = recognizerClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", _activity);
            _recognizer.Call("setRecognitionListener", new Listener(this));

            _intent = new AndroidJavaObject("android.content.Intent", "android.speech.action.RECOGNIZE_SPEECH");
            _intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE_MODEL", "free_form").Dispose();
            _intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE", "en-US").Dispose();
            _intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.PREFER_OFFLINE", true).Dispose();
            _intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.MAX_RESULTS", 1).Dispose();
            return true;
        }

        private void DestroyRecognizer()
        {
            if (_recognizer == null) return;
            _recognizer.Call("destroy");
            _recognizer.Dispose();
            _recognizer = null;
            _intent?.Dispose();
            _intent = null;
        }

        private void HandleResults(AndroidJavaObject bundle)
        {
            using (AndroidJavaObject texts = bundle.Call<AndroidJavaObject>("getStringArrayList", "results_recognition"))
            {
                if (texts != null && texts.Call<int>("size") > 0)
                {
                    string phrase = texts.Call<string>("get", 0)?.ToLowerInvariant();
                    float confidence = ReadConfidence(bundle);
                    if (!string.IsNullOrEmpty(phrase)) _unityThread.Post(_ => OnPhrase?.Invoke(phrase, confidence), null);
                }
            }
            RestartLater(RestartAfterResultMs);
        }

        // Many recognition services leave the score unset, so a missing score counts as just above the router's minimum.
        private static float ReadConfidence(AndroidJavaObject bundle)
        {
            float[] scores = bundle.Call<float[]>("getFloatArray", "confidence_scores");
            return scores != null && scores.Length > 0 && scores[0] > 0f ? scores[0] : AssumedConfidence;
        }

        private void HandleError(int code)
        {
            if (code == ErrorInsufficientPermissions)
            {
                _wanted = false;
                Debug.LogWarning("[Voice] Off: microphone permission was refused.");
                return;
            }
            if (code == ErrorClient) DestroyRecognizer();
            // Silence and "no match" arrive here as errors, and are normal while waiting for a command.
            RestartLater(RestartAfterErrorMs);
        }

        // Android's recognizer ends after each phrase, so keep it listening while the guardian toggle is on.
        private void RestartLater(int delayMs)
        {
            if (!_wanted) return;
            if (_handler == null)
            {
                using var looperClass = new AndroidJavaClass("android.os.Looper");
                using AndroidJavaObject looper = looperClass.CallStatic<AndroidJavaObject>("getMainLooper");
                _handler = new AndroidJavaObject("android.os.Handler", looper);
            }
            _handler.Call<bool>("postDelayed", new AndroidJavaRunnable(BeginListening), (long)delayMs);
        }

        private void RunOnUiThread(Action action)
        {
            if (_activity == null)
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                _activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            }
            _activity.Call("runOnUiThread", new AndroidJavaRunnable(() => action()));
        }

        /// <summary>android.speech.RecognitionListener. Unity finds each method by name, so all of them must exist.</summary>
        private sealed class Listener : AndroidJavaProxy
        {
            private readonly AndroidSpeechRecognizer _owner;

            public Listener(AndroidSpeechRecognizer owner) : base("android.speech.RecognitionListener") => _owner = owner;

            public void onResults(AndroidJavaObject results) => _owner.HandleResults(results);
            public void onError(int error) => _owner.HandleError(error);
            public void onReadyForSpeech(AndroidJavaObject parameters) { }
            public void onBeginningOfSpeech() { }
            public void onRmsChanged(float rmsdB) { }
            public void onBufferReceived(AndroidJavaObject buffer) { }
            public void onEndOfSpeech() { }
            public void onPartialResults(AndroidJavaObject partialResults) { }
            public void onEvent(int eventType, AndroidJavaObject parameters) { }
        }
    }
}
