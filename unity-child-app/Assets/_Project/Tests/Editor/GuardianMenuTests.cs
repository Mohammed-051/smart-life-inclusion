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
using Object = UnityEngine.Object;

namespace SmartLife.Tests
{
    public class GuardianPinStoreTests
    {
        private sealed class MemoryStore : IKeyValueStore
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public string GetString(string key) => Values.TryGetValue(key, out var v) ? v : string.Empty;
            public void SetString(string key, string value) => Values[key] = value;
            public void Delete(string key) => Values.Remove(key);
        }

        private MemoryStore _store;
        private long _now;
        private GuardianPinStore _pins;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryStore();
            _now = 1000;
            _pins = new GuardianPinStore(_store, () => _now, iterations: 10);
        }

        [Test]
        public void SetPin_StoresSaltedHash_NeverThePin()
        {
            _pins.SetPin("4827");
            Assert.IsTrue(_pins.HasPin);
            Assert.IsFalse(_store.Values.Values.Any(v => v.Contains("4827")));

            string firstHash = _store.Values.Values.Single();
            _pins.SetPin("4827");
            Assert.AreNotEqual(firstHash, _store.Values.Values.Single(), "A fresh salt should change the hash.");
        }

        [Test]
        public void Verify_AcceptsRightPin_RejectsWrongPin()
        {
            _pins.SetPin("4827");
            Assert.AreEqual(PinResult.Wrong, _pins.Verify("0000"));
            Assert.AreEqual(PinResult.Ok, _pins.Verify("4827"));
        }

        [TestCase("123")]
        [TestCase("12345")]
        [TestCase("12a4")]
        [TestCase(null)]
        public void SetPin_RejectsBadFormat(string pin) => Assert.Throws<ArgumentException>(() => _pins.SetPin(pin));

        [Test]
        public void FifthWrongTry_LocksOut_EvenForTheRightPin()
        {
            _pins.SetPin("4827");
            for (int i = 0; i < 4; i++) Assert.AreEqual(PinResult.Wrong, _pins.Verify("0000"));
            Assert.AreEqual(PinResult.LockedOut, _pins.Verify("0000"));
            Assert.AreEqual(PinResult.LockedOut, _pins.Verify("4827"));
            Assert.AreEqual(30, _pins.LockoutSecondsRemaining);

            _now += 31;
            Assert.AreEqual(PinResult.Ok, _pins.Verify("4827"));
        }

        [Test]
        public void Lockout_Grows_AndSurvivesANewInstance()
        {
            _pins.SetPin("4827");
            for (int i = 0; i < 5; i++) _pins.Verify("0000");
            _now += 31;
            Assert.AreEqual(PinResult.LockedOut, _pins.Verify("0000"));
            Assert.AreEqual(60, new GuardianPinStore(_store, () => _now, 10).LockoutSecondsRemaining);
        }
    }

    public class GuardianMenuTests
    {
        private sealed class MemoryStore : IKeyValueStore
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string GetString(string key) => _values.TryGetValue(key, out var v) ? v : string.Empty;
            public void SetString(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        private sealed class FakeAuth : IDeviceAuth
        {
            public DeviceSession Saved;
            public DeviceSession SignInResult;
            public string SignInError = "nope";
            public string LastEmail;
            public bool FirebaseAvailable { get; set; } = true;
            public void Restore(Action<DeviceSession> done) => done(Saved);
            public void SignIn(string email, string password, Action<DeviceSession, string> done)
            {
                LastEmail = email;
                done(SignInResult, SignInResult == null ? SignInError : null);
            }
            public void SignOut() { }
        }

        private GameObject _host;
        private GuardianMenu _menu;
        private FakeAuth _auth;
        private GuardianPinStore _pins;
        private readonly List<DeviceSession> _results = new List<DeviceSession>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("menu");
            _menu = _host.AddComponent<GuardianMenu>();
            _auth = new FakeAuth();
            _pins = new GuardianPinStore(new MemoryStore(), iterations: 10);
            _results.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            foreach (var go in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.DestroyImmediate(go.gameObject);
            PauseLock.Clear();
        }

        private void Open() => _menu.Open(_auth, _pins, _results.Add);

        private static Button Find(string label) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.GetComponentInChildren<TMP_Text>().text == label);

        private static void Press(string label)
        {
            Button button = Find(label);
            Assert.IsNotNull(button, $"No button labelled {label}.");
            button.onClick.Invoke();
        }

        private static void Type(string pin)
        {
            foreach (char c in pin) Press(c.ToString());
        }

        private static bool ShowsText(string fragment) =>
            Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t => t.text.Contains(fragment));

        [Test]
        public void SavedSession_SkipsAllGuardianScreens()
        {
            var saved = new DeviceSession("uid1", "stu_001");
            _auth.Saved = saved;
            Open();
            Assert.AreSame(saved, _results.Single());
            Assert.IsFalse(_menu.IsOpen);
            Assert.IsNull(Find("1"));
        }

        [Test]
        public void FirstBoot_CreatesPin_ThenAsksForLogin_ThenFinishes()
        {
            _auth.SignInResult = new DeviceSession("uid1", "stu_001");
            Open();
            Assert.IsTrue(ShowsText("Create a guardian PIN"));

            Type("4827");
            Assert.IsTrue(ShowsText("Enter the same PIN again"));
            Type("4827");
            Assert.IsTrue(_pins.HasPin);
            Assert.AreEqual(PinResult.Ok, _pins.Verify("4827"));
            Assert.IsTrue(ShowsText("Sign in this tablet"));

            var fields = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
            fields.Single(f => f.contentType == TMP_InputField.ContentType.EmailAddress).text = " tablet@example.test ";
            fields.Single(f => f.contentType == TMP_InputField.ContentType.Password).text = "test-password";
            Press("Sign in");

            Assert.AreEqual("tablet@example.test", _auth.LastEmail);
            Assert.AreEqual("uid1", _results.Single().Uid);
            Assert.IsFalse(_menu.IsOpen);
        }

        [Test]
        public void MismatchedPinConfirm_StartsOver_AndSavesNothing()
        {
            Open();
            Type("4827");
            Type("1111");
            Assert.IsFalse(_pins.HasPin);
            Assert.IsTrue(ShowsText("did not match"));
        }

        [Test]
        public void WrongPin_NeverReachesLogin()
        {
            _pins.SetPin("4827");
            Open();
            Type("0000");
            Assert.IsFalse(ShowsText("Sign in this tablet"));
            Assert.IsTrue(ShowsText("did not match"));
            Assert.IsEmpty(_results);
        }

        [Test]
        public void FailedSignIn_OffersOfflineContinue_WithNullSession()
        {
            _pins.SetPin("4827");
            Open();
            Type("4827");
            Assert.IsNull(Find("Continue without saving progress"), "Offline should only appear after a failure.");

            var fields = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
            foreach (var f in fields) f.text = "x";
            Press("Sign in");
            Assert.IsTrue(ShowsText("nope"));

            Press("Continue without saving progress");
            Assert.IsNull(_results.Single());
        }

        [Test]
        public void FirebaseUnavailable_DisablesSignIn_AndOffersOffline()
        {
            _auth.FirebaseAvailable = false;
            _pins.SetPin("4827");
            Open();
            Type("4827");
            Assert.IsFalse(Find("Sign in").interactable);
            Assert.IsNotNull(Find("Continue without saving progress"));
        }

        [Test]
        public void InTaskMenu_NeedsPin_FreezesClock_AndRunsAction()
        {
            _auth.Saved = new DeviceSession("uid1", "stu_001");
            _pins.SetPin("4827");
            Open();
            int ran = 0;
            _menu.EnableMenuAccess(new[] { new GuardianMenu.MenuAction("Reset task", () => ran++) });

            _menu.OpenMenu();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsNull(Find("Reset task"), "The menu must stay hidden until the PIN is entered.");

            Type("4827");
            Press("Reset task");
            Assert.AreEqual(1, ran);
            Assert.IsFalse(_menu.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
