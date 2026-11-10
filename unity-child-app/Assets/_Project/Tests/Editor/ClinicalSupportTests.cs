using System.Reflection;
using NUnit.Framework;
using SmartLife.AR;
using SmartLife.Core;
using UnityEditor;
using UnityEngine;

namespace SmartLife.Tests
{
    /// <summary>Mascot mode mapping, hesitation timer and emergency triggers.</summary>
    public class ClinicalSupportTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private TaskDefinition _task;
        private GameObject _host;
        private TaskStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _task = ScriptableObject.CreateInstance<TaskDefinition>();
            var so = new SerializedObject(_task);
            SerializedProperty steps = so.FindProperty("steps");
            var data = new (string marker, float hold)[] { ("MK_A", 0f), ("MK_B", 20f) };
            steps.arraySize = data.Length;
            for (int i = 0; i < data.Length; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("markerId").stringValue = data[i].marker;
                step.FindPropertyRelative("holdSeconds").floatValue = data[i].hold;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            _host = new GameObject("machine");
            _machine = _host.AddComponent<TaskStateMachine>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_task);
        }

        private T AddWired<T>() where T : Component
        {
            var component = _host.AddComponent<T>();
            var so = new SerializedObject(component);
            so.FindProperty("stateMachine").objectReferenceValue = _machine;
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private | BindingFlags.Public).Invoke(target, args);

        [TestCase(MachineState.Awaiting, TaskDefinition.Cue.Avatar, MascotMode.Instruction)]
        [TestCase(MachineState.Active, TaskDefinition.Cue.Avatar, MascotMode.Ambient)]
        [TestCase(MachineState.Paused, TaskDefinition.Cue.Avatar, MascotMode.Ambient)]
        [TestCase(MachineState.Idle, TaskDefinition.Cue.Avatar, MascotMode.Hidden)]
        [TestCase(MachineState.Complete, TaskDefinition.Cue.Avatar, MascotMode.Hidden)]
        [TestCase(MachineState.Awaiting, TaskDefinition.Cue.ArrowLeft, MascotMode.Hidden)]
        public void Mascot_ResolvesMode(MachineState state, TaskDefinition.Cue cue, MascotMode expected) =>
            Assert.AreEqual(expected, MascotStateManager.Resolve(state, cue));

        private HesitationMonitor StartHesitation(out int[] fired)
        {
            var monitor = AddWired<HesitationMonitor>();
            Call(monitor, "OnEnable");
            _machine.Begin(_task);
            var count = new int[1];
            monitor.OnStall += _ => count[0]++;
            fired = count;
            return monitor;
        }

        [Test]
        public void Hesitation_FiresOnceAfter15Seconds()
        {
            var monitor = StartHesitation(out var fired);
            Call(monitor, "Tick", 14.9f);
            Assert.AreEqual(0, fired[0]);
            Call(monitor, "Tick", 0.2f);
            Call(monitor, "Tick", 60f);
            Assert.AreEqual(1, fired[0]);
        }

        [Test]
        public void Hesitation_RearmStartsFreshWait()
        {
            var monitor = StartHesitation(out var fired);
            Call(monitor, "Tick", 15f);
            monitor.Rearm();
            Call(monitor, "Tick", 14f);
            Assert.AreEqual(1, fired[0]);
            Call(monitor, "Tick", 1f);
            Assert.AreEqual(2, fired[0]);
        }

        [Test]
        public void Hesitation_PauseFreezesAndResumeStartsFreshWait()
        {
            var monitor = StartHesitation(out _);
            Call(monitor, "Tick", 10f);
            _machine.Apply(OverrideCommand.Pause);
            Call(monitor, "Tick", 100f);
            Assert.AreEqual(10f, monitor.ElapsedSeconds, 0.001f);
            _machine.Apply(OverrideCommand.Resume);
            Assert.AreEqual(0f, monitor.ElapsedSeconds, 0.001f);
        }

        [Test]
        public void Hesitation_ResetsWhenChildActs_AndIgnoresHold()
        {
            var monitor = StartHesitation(out var fired);
            Call(monitor, "Tick", 10f);
            _machine.HandleMarker("MK_A");
            Assert.AreEqual(0f, monitor.ElapsedSeconds, 0.001f);

            _machine.HandleMarker("MK_B");
            Assert.IsTrue(_machine.IsHolding);
            Call(monitor, "Tick", 18f);
            Assert.AreEqual(0, fired[0]);
        }

        private EmergencyDetector StartDetector(out EmergencySource?[] last)
        {
            var detector = AddWired<EmergencyDetector>();
            _machine.Begin(_task);
            var box = new EmergencySource?[1];
            detector.OnEmergency += s => box[0] = s;
            last = box;
            return detector;
        }

        [Test]
        public void Emergency_Sos_PausesTask()
        {
            var detector = StartDetector(out var last);
            detector.TriggerSos();
            Assert.AreEqual(EmergencySource.Sos, last[0]);
            Assert.AreEqual(MachineState.Paused, _machine.State);
        }

        [Test]
        public void Emergency_ImuFallThenImpact_Fires()
        {
            var detector = StartDetector(out var last);
            Call(detector, "FeedImu", 0.1f, 0.15f);
            Call(detector, "FeedImu", 3f, 0.02f);
            Assert.AreEqual(EmergencySource.Imu, last[0]);
        }

        [Test]
        public void Emergency_ImuImpactWithoutFall_DoesNotFire()
        {
            var detector = StartDetector(out var last);
            Call(detector, "FeedImu", 3f, 0.02f);
            Assert.IsNull(last[0]);
        }

        [Test]
        public void Emergency_AudioNeedsSustainedLevel()
        {
            var detector = StartDetector(out var last);
            Call(detector, "FeedAudio", 0.9f, 0.3f);
            Call(detector, "FeedAudio", 0.1f, 0.3f);
            Call(detector, "FeedAudio", 0.9f, 0.3f);
            Assert.IsNull(last[0]);
            Call(detector, "FeedAudio", 0.9f, 0.3f);
            Assert.AreEqual(EmergencySource.Audio, last[0]);
        }

        [Test]
        public void Emergency_CooldownBlocksSensors_ButNotSos()
        {
            var detector = StartDetector(out var last);
            Call(detector, "FeedAudio", 0.9f, 0.6f);
            Assert.AreEqual(EmergencySource.Audio, last[0]);

            last[0] = null;
            Call(detector, "FeedAudio", 0.9f, 0.6f);
            Assert.IsNull(last[0]);

            detector.TriggerSos();
            Assert.AreEqual(EmergencySource.Sos, last[0]);
        }
    }
}
