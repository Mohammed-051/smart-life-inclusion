using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SmartLife.Tests
{
    /// <summary>
    /// Runs the hesitation and emergency flows on the real frame loop with the real ADL_Handwashing
    /// task, without a camera or microphone. Uses reflection because an assembly definition cannot
    /// reference Assembly-CSharp, where the app scripts compile. Phase4AutoWirer checks scene wiring.
    /// </summary>
    public class Phase4ValidationTests
    {
        private const string TaskPath = "Assets/_Project/Data/ADL_Handwashing.asset";
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private GameObject _host;
        private Component _machine;
        private Component _hesitation;
        private Component _emergency;

        /// <summary>Holds what an event raised, for any event argument type.</summary>
        private sealed class Recorder
        {
            public readonly List<object> Calls = new List<object>();
            public void Record<T>(T value) => Calls.Add(value);
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Phase4Rig");
            _host.SetActive(false); // Links must be in place before OnEnable subscribes.
            _machine = Add("SmartLife.Core.TaskStateMachine");
            _hesitation = Add("SmartLife.Core.HesitationMonitor");
            _emergency = Add("SmartLife.Core.EmergencyDetector");
            SetField(_hesitation, "stateMachine", _machine);
            SetField(_emergency, "stateMachine", _machine);
            _host.SetActive(true);

            var task = AssetDatabase.LoadMainAssetAtPath(TaskPath);
            Assert.IsNotNull(task, $"{TaskPath} is missing.");
            Call(_machine, "Begin", task);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Object.Destroy(_host);
        }

        [UnityTest]
        public IEnumerator Hesitation_MkSinkStep_FiresAfter15SimulatedSeconds()
        {
            Recorder fired = Listen(_hesitation, "OnHesitation");

            Call(_machine, "HandleMarker", "MK_SINK");
            Assert.AreEqual("Active", State(), "MK_SINK should start the first step.");
            Assert.AreEqual(0, Get(_machine, "StepIndex"));
            Assert.IsFalse((bool)Get(_machine, "IsHolding"), "cp_wet has no hold, so the child should be timed.");

            float start = Time.time;
            Time.timeScale = 15f; // 15 simulated seconds in about 1 real second.
            float deadline = Time.realtimeSinceStartup + 10f;
            while (fired.Calls.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            float simulated = Time.time - start;

            Assert.AreEqual(1, fired.Calls.Count, "OnHesitation should fire once.");
            Assert.AreEqual(0, fired.Calls[0], "It should report the MK_SINK step.");
            Assert.GreaterOrEqual((float)Get(_hesitation, "ElapsedSeconds"), 15f);
            Assert.That(simulated, Is.InRange(14.5f, 18f), "It should fire at about 15 simulated seconds.");

            // Stays quiet until the board is dismissed.
            float keepRunning = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < keepRunning) yield return null;
            Assert.AreEqual(1, fired.Calls.Count, "OnHesitation should not repeat on its own.");
        }

        [Test]
        [Ignore("NOT BUILT: no AAC board exists to subscribe to OnHesitation, so no board can open yet.")]
        public void Hesitation_OpensAacPictureBoard() => Assert.Fail("Replace when the AAC board UI exists.");

        [UnityTest]
        public IEnumerator Emergency_AudioThresholdBreak_RaisesEmergencyAndPausesTask()
        {
            Recorder raised = Listen(_emergency, "OnEmergency");
            yield return null; // Let Update run once, as it would on a device.

            // Below the threshold, nothing happens.
            Call(_emergency, "FeedAudio", 0.1f, 1f);
            Assert.AreEqual(0, raised.Calls.Count);
            Assert.AreEqual("Awaiting", State());

            // Above the 0.6 RMS threshold for longer than the 0.5 s sustain, as the microphone path would report.
            Call(_emergency, "FeedAudio", 0.9f, 0.6f);
            yield return null;

            Assert.AreEqual(1, raised.Calls.Count, "OnEmergency should fire once.");
            Assert.AreEqual("Audio", raised.Calls[0].ToString());
            Assert.AreEqual("Paused", State());
        }

        [UnityTest]
        public IEnumerator Emergency_SosButton_RaisesEmergencyAndPausesTask()
        {
            Recorder raised = Listen(_emergency, "OnEmergency");

            Call(_emergency, "TriggerSos");
            yield return null;

            Assert.AreEqual("Sos", raised.Calls[0].ToString());
            Assert.AreEqual("Paused", State());
        }

        [Test]
        [Ignore("NOT BUILT: no WebRTC or /emergencies code exists, so an emergency cannot escalate to a stream yet.")]
        public void Emergency_EscalatesToWebRtc() => Assert.Fail("Replace when the WebRTC intervention exists.");

        private string State() => Get(_machine, "State").ToString();

        private Component Add(string typeName)
        {
            Type type = Type.GetType($"{typeName}, Assembly-CSharp", throwOnError: true);
            return _host.AddComponent(type);
        }

        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, Any).SetValue(target, value);

        private static object Get(object target, string property) =>
            target.GetType().GetProperty(property, Any).GetValue(target);

        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Any).Invoke(target, args);

        private static Recorder Listen(object target, string eventName)
        {
            var recorder = new Recorder();
            EventInfo info = target.GetType().GetEvent(eventName);
            Type argument = info.EventHandlerType.GetGenericArguments()[0];
            MethodInfo record = typeof(Recorder).GetMethod(nameof(Recorder.Record)).MakeGenericMethod(argument);
            info.AddEventHandler(target, Delegate.CreateDelegate(info.EventHandlerType, recorder, record));
            return recorder;
        }
    }
}
