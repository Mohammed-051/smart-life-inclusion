using System.Reflection;
using NUnit.Framework;
using SmartLife.Core;
using UnityEditor;
using UnityEngine;

namespace SmartLife.Tests
{
    /// <summary>Hold time lock, using the same marker and hold layout as ADL_Handwashing.</summary>
    public class TaskStateMachineHoldTests
    {
        private const string Sink = "MK_SINK", Soap = "MK_SOAP", Rub = "MK_RUB", Towel = "MK_TOWEL";

        private TaskDefinition _task;
        private GameObject _host;
        private TaskStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _task = ScriptableObject.CreateInstance<TaskDefinition>();
            var so = new SerializedObject(_task);
            SerializedProperty steps = so.FindProperty("steps");
            var data = new (string marker, float hold)[] { (Sink, 0f), (Soap, 0f), (Rub, 20f), (Towel, 5f) };
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
            _machine.Begin(_task);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_task);
        }

        private void Tick(float seconds) =>
            typeof(TaskStateMachine).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_machine, new object[] { seconds });

        private void ScanToRub()
        {
            _machine.HandleMarker(Sink);
            _machine.HandleMarker(Soap);
            _machine.HandleMarker(Rub);
        }

        [Test]
        public void NextMarker_WithoutHold_CompletesCurrentStep()
        {
            _machine.HandleMarker(Sink);
            _machine.HandleMarker(Soap);
            Assert.AreEqual(1, _machine.StepIndex);
            Assert.AreEqual(MachineState.Active, _machine.State);
        }

        [Test]
        public void NextMarker_DuringHold_IsIgnored()
        {
            ScanToRub();
            Tick(19f);
            _machine.HandleMarker(Towel);

            Assert.AreEqual(2, _machine.StepIndex);
            Assert.IsTrue(_machine.IsHolding);
            Assert.AreEqual(1f, _machine.HoldSecondsRemaining, 0.001f);
        }

        [Test]
        public void WrongMarker_DuringHold_ShowsNoHint()
        {
            ScanToRub();
            bool hinted = false;
            _machine.OnUnexpectedMarker += _ => hinted = true;
            _machine.HandleMarker(Sink);
            Assert.IsFalse(hinted);
        }

        [Test]
        public void HoldElapsed_MovesToNextStep_ThenTaskCompletes()
        {
            ScanToRub();
            Tick(20f);
            Assert.AreEqual(3, _machine.StepIndex);
            Assert.AreEqual(MachineState.Awaiting, _machine.State);

            _machine.HandleMarker(Towel);
            Assert.IsTrue(_machine.IsHolding);
            Tick(5f);
            Assert.AreEqual(MachineState.Complete, _machine.State);
        }

        [Test]
        public void Pause_FreezesHold_ResumeKeepsRemainingTime()
        {
            ScanToRub();
            Tick(5f);
            _machine.Apply(OverrideCommand.Pause);
            Tick(100f);
            _machine.Apply(OverrideCommand.Resume);

            Assert.AreEqual(2, _machine.StepIndex);
            Assert.AreEqual(15f, _machine.HoldSecondsRemaining, 0.001f);
        }

        [Test]
        public void GuardianSkip_CanEndHoldEarly()
        {
            ScanToRub();
            _machine.Apply(OverrideCommand.Skip);
            Assert.AreEqual(3, _machine.StepIndex);
            Assert.AreEqual(MachineState.Awaiting, _machine.State);
        }
    }
}
