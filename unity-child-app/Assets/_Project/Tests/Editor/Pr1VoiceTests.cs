using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SmartLife.Core;
using SmartLife.Telemetry;
using SmartLife.UI;
using SmartLife.Voice;
using Object = UnityEngine.Object;

namespace SmartLife.Tests
{
    public class Pr1VoiceRouterTests
    {
        private sealed class FakeRecognizer : IVoiceRecognizer
        {
            public event Action<string, float> OnPhrase { add { } remove { } }
            public bool Listening;
            public void StartListening() => Listening = true;
            public void StopListening() => Listening = false;
        }

        private GameObject _host;
        private VoiceCommandRouter _router;
        private FakeRecognizer _rec;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("router");
            _router = _host.AddComponent<VoiceCommandRouter>();
            _rec = new FakeRecognizer();
            _router.Init(_rec);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [TestCase("skip step")]
        [TestCase("reset task")]
        [TestCase("emergency pause")]
        [TestCase("resume")]
        public void Locked_IgnoresEveryPhrase_EvenIfEnabledWasRequested(string phrase)
        {
            _router.SetEnabled(true);
            Assert.IsFalse(_router.Enabled);
            Assert.IsFalse(_rec.Listening, "Mic must stay off while locked.");
            Assert.IsFalse(_router.Accepts(phrase, 0.9f, out _));
        }

        [TestCase("stop")]
        [TestCase("stop it")]
        [TestCase("no stop")]
        public void Stop_MapsToNothing(string phrase)
        {
            Assert.IsFalse(VoiceCommandRouter.TryParse(phrase, out _));
            _router.SetGuardianUnlocked(true);
            _router.SetEnabled(true);
            Assert.IsFalse(_router.Accepts(phrase, 0.9f, out _));
        }

        [TestCase("skip step", OverrideCommand.Skip)]
        [TestCase("reset task", OverrideCommand.Reset)]
        [TestCase("emergency pause", OverrideCommand.Pause)]
        [TestCase("resume", OverrideCommand.Resume)]
        public void Unlocked_AndListening_AcceptsDocumentedPhrases(string phrase, OverrideCommand expected)
        {
            _router.SetGuardianUnlocked(true);
            _router.SetEnabled(true);
            Assert.IsTrue(_rec.Listening);
            Assert.IsTrue(_router.Accepts(phrase, 0.9f, out var cmd));
            Assert.AreEqual(expected, cmd);
            Assert.IsFalse(_router.Accepts(phrase, 0.1f, out _), "Low confidence is still dropped.");
        }

        [Test]
        public void Relock_TurnsRecognitionOff()
        {
            _router.SetGuardianUnlocked(true);
            _router.SetEnabled(true);
            _router.SetGuardianUnlocked(false);
            Assert.IsFalse(_router.Enabled);
            Assert.IsFalse(_rec.Listening);
            Assert.IsFalse(_router.Accepts("skip step", 0.9f, out _));
        }
    }

    public class Pr1GuardianLockTests
    {
        private sealed class MemoryStore : IKeyValueStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string GetString(string key) => _values.TryGetValue(key, out var v) ? v : string.Empty;
            public void SetString(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        private sealed class SavedAuth : IDeviceAuth
        {
            public bool FirebaseAvailable => true;
            public void Restore(Action<DeviceSession> done) => done(new DeviceSession("uid1", "stu_001"));
            public void SignIn(string email, string password, Action<DeviceSession, string> done) { }
            public void SignOut() { }
            public void FetchRoutine(string routeId, Action<IReadOnlyList<RoutineEntry>> done) => done(null);
            public void FetchProfile(string studentId, Action<StudentProfile> done) => done(StudentProfile.Default);
        }

        private GameObject _host;
        private GuardianMenu _menu;
        private float _clock;
        private long _pinNow;
        private readonly List<bool> _changes = new List<bool>();

        [SetUp]
        public void SetUp()
        {
            _clock = 0f;
            _pinNow = 1000;
            _changes.Clear();
            _host = new GameObject("menu");
            _menu = _host.AddComponent<GuardianMenu>();
            _menu.Clock = () => _clock;
            _menu.UnlockChanged += _changes.Add;
            var pins = new GuardianPinStore(new MemoryStore(), () => _pinNow, iterations: 10);
            pins.SetPin("4827");
            _menu.Open(new SavedAuth(), pins, _ => { });
            _menu.EnableMenuAccess(new[] { new GuardianMenu.MenuAction("Reset task", () => { }) });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            foreach (var go in Object.FindObjectsByType<Canvas>()) Object.DestroyImmediate(go.gameObject);
            PauseLock.Clear();
        }

        private static Button Find(string label) =>
            Object.FindObjectsByType<Button>().FirstOrDefault(b => b.GetComponentInChildren<TMP_Text>().text == label);

        private static void Type(string pin)
        {
            foreach (char c in pin) Find(c.ToString()).onClick.Invoke();
        }

        private void Unlock()
        {
            _menu.OpenMenu();
            Type("4827");
        }

        [Test]
        public void CorrectPin_Unlocks_WrongPin_DoesNot()
        {
            _menu.OpenMenu();
            Type("0000");
            Assert.IsFalse(_menu.Unlocked);
            Type("4827");
            Assert.IsTrue(_menu.Unlocked);
            CollectionAssert.AreEqual(new[] { true }, _changes);
        }

        [Test]
        public void RelocksAfter60sIdle_AndClosesMenu()
        {
            Unlock();
            _clock = 59f;
            _menu.Tick();
            Assert.IsTrue(_menu.Unlocked);
            _clock = 60f;
            _menu.Tick();
            Assert.IsFalse(_menu.Unlocked);
            Assert.IsFalse(_menu.IsOpen);
            Assert.AreEqual(1f, Time.timeScale, "Relock must release the pause hold.");
            CollectionAssert.AreEqual(new[] { true, false }, _changes);
        }

        [Test]
        public void GuardianInput_ResetsIdleTimer()
        {
            Unlock();
            _clock = 50f;
            Find("Reset task").onClick.Invoke();
            Assert.IsTrue(_menu.Unlocked);
            _clock = 100f;
            _menu.Tick();
            Assert.IsTrue(_menu.Unlocked, "50 s since last input.");
        }

        [Test]
        public void NoteGuardianActivity_ResetsIdleTimer()
        {
            Unlock();
            _clock = 50f;
            _menu.NoteGuardianActivity();
            _clock = 100f;
            _menu.Tick();
            Assert.IsTrue(_menu.Unlocked, "50 s since the voice command.");
            _clock = 111f;
            _menu.Tick();
            Assert.IsFalse(_menu.Unlocked);
        }

        [Test]
        public void Background_Relocks()
        {
            Unlock();
            // SendMessage does not reach components in EditMode; invoke the Unity message directly.
            typeof(GuardianMenu).GetMethod("OnApplicationPause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_menu, new object[] { true });
            Assert.IsFalse(_menu.Unlocked);
            Assert.IsFalse(_menu.IsOpen);
        }

        [Test]
        public void FocusLoss_Relocks()
        {
            Unlock();
            typeof(GuardianMenu).GetMethod("OnApplicationFocus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_menu, new object[] { false });
            Assert.IsFalse(_menu.Unlocked);
        }

        [Test]
        public void Lockout_IsNotBypassed_AndShowsTheWait()
        {
            _menu.OpenMenu();
            for (int i = 0; i < 5; i++) Type("0000");
            Assert.IsTrue(Object.FindObjectsByType<TMP_Text>().Any(t => t.text.Contains("Wait 30 seconds")));

            Type("4827"); // right PIN during lockout
            Assert.IsFalse(_menu.Unlocked);
            Assert.IsNull(Find("Reset task"));

            _pinNow += 31;
            Type("4827");
            Assert.IsTrue(_menu.Unlocked);
        }
    }
}
