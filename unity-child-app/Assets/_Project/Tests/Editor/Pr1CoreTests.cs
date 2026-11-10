using System.Reflection;
using NUnit.Framework;
using SmartLife.Core;
using UnityEditor;
using UnityEngine;

namespace SmartLife.Tests
{
    /// <summary>Stall monitor freezes while paused; every pause entry has a Resume exit.</summary>
    public class Pr1CoreTests
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

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
            target.GetType().GetMethod(method, Any).Invoke(target, args);

        private HesitationMonitor StartMonitor(out int[] stalls)
        {
            var monitor = AddWired<HesitationMonitor>();
            Call(monitor, "OnEnable");
            _machine.Begin(_task);
            var count = new int[1];
            monitor.OnStall += _ => count[0]++;
            stalls = count;
            return monitor;
        }

        [Test]
        public void Monitor_TimeDoesNotAdvanceWhilePaused()
        {
            var monitor = StartMonitor(out var stalls);
            Call(monitor, "Tick", 5f);
            _machine.Apply(OverrideCommand.Pause);
            Call(monitor, "Tick", 500f);
            Assert.AreEqual(5f, monitor.ElapsedSeconds, 0.001f);
            Assert.AreEqual(0, stalls[0]);
        }

        [Test]
        public void Monitor_NoStallBeforeThresholdAfterResume()
        {
            var monitor = StartMonitor(out var stalls);
            Call(monitor, "Tick", 14f);
            _machine.Apply(OverrideCommand.Pause);
            Call(monitor, "Tick", 500f);
            _machine.Apply(OverrideCommand.Resume);
            Call(monitor, "Tick", monitor.HesitationSeconds - 0.1f);
            Assert.AreEqual(0, stalls[0]);
            Call(monitor, "Tick", 0.2f);
            Assert.AreEqual(1, stalls[0]);
        }

        [TestCase(MachineState.Idle)]
        [TestCase(MachineState.Awaiting)]
        [TestCase(MachineState.Active)]
        [TestCase(MachineState.Complete)]
        public void Pause_FromAnyState_ResumeReturnsToIt(MachineState entry)
        {
            switch (entry)
            {
                case MachineState.Awaiting: _machine.Begin(_task); break;
                case MachineState.Active: _machine.Begin(_task); _machine.HandleMarker("MK_A"); _machine.HandleMarker("MK_B"); break;
                case MachineState.Complete: _machine.Begin(_task); _machine.Apply(OverrideCommand.Skip); _machine.Apply(OverrideCommand.Skip); break;
            }
            Assert.AreEqual(entry, _machine.State);
            int step = _machine.StepIndex;

            _machine.Apply(OverrideCommand.Pause);
            Assert.AreEqual(MachineState.Paused, _machine.State);
            _machine.Apply(OverrideCommand.Pause); // a second pause must not overwrite the return state
            _machine.Apply(OverrideCommand.Resume);

            Assert.AreEqual(entry, _machine.State);
            Assert.AreEqual(step, _machine.StepIndex);
        }

        [Test]
        public void Pause_EmergencySos_ResumeExits()
        {
            var detector = AddWired<EmergencyDetector>();
            _machine.Begin(_task);
            detector.TriggerSos();
            Assert.AreEqual(MachineState.Paused, _machine.State);
            _machine.Apply(OverrideCommand.Resume);
            Assert.AreEqual(MachineState.Awaiting, _machine.State);
        }

        [Test]
        public void Pause_ResetExits()
        {
            _machine.Begin(_task);
            _machine.Apply(OverrideCommand.Pause);
            _machine.Apply(OverrideCommand.Reset);
            Assert.AreEqual(MachineState.Awaiting, _machine.State);
        }
    }
}
