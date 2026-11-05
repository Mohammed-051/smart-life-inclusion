using System.Linq;
using NUnit.Framework;
using SmartLife.Core;
using SmartLife.Telemetry;
using UnityEditor;
using UnityEngine;

namespace SmartLife.Tests
{
    public class TaskDefinitionRoutineTests
    {
        private TaskDefinition _task;

        [SetUp]
        public void SetUp() => _task = AssetDatabase.LoadAssetAtPath<TaskDefinition>("Assets/_Project/Data/ADL_Handwashing.asset");

        private static string[] Ids(TaskDefinition task) =>
            Enumerable.Range(0, task.Count).Select(i => task[i].checkpointId).ToArray();

        [Test]
        public void Arranged_ReordersAndDropsDisabledSteps()
        {
            TaskDefinition result = _task.Arranged(new[]
            {
                new RoutineEntry("cp_rub", 1, true),
                new RoutineEntry("cp_soap", 2, false),
                new RoutineEntry("cp_wet", 3, true),
                new RoutineEntry("cp_rinse", 4, true),
            });
            CollectionAssert.AreEqual(new[] { "cp_rub", "cp_wet", "cp_rinse" }, Ids(result));
            Assert.AreEqual(_task.RouteId, result.RouteId);
            Assert.AreEqual(4, _task.Count, "The authored asset must not change.");
            Object.DestroyImmediate(result);
        }

        [Test]
        public void Arranged_FallsBackToAuthoredTask_WhenNothingUsableArrives()
        {
            Assert.AreSame(_task, _task.Arranged(null));
            Assert.AreSame(_task, _task.Arranged(new[] { new RoutineEntry("cp_wet", 1, false) }));
            Assert.AreSame(_task, _task.Arranged(new[] { new RoutineEntry("cp_unknown", 1, true) }));
        }
    }

    public class NodeMetricsTrackerTests
    {
        [Test]
        public void Hold_IsNotHesitation()
        {
            var tracker = new NodeMetricsTracker();
            tracker.Tick(2f, waitingOnChild: true, holding: false);
            tracker.Tick(3f, waitingOnChild: false, holding: true);

            Assert.AreEqual(2f, tracker.StepHesitationSeconds, 0.001f);
            Assert.AreEqual(3f, tracker.StepHoldSeconds, 0.001f);
            Assert.AreEqual(5f, tracker.SessionActiveSeconds, 0.001f);
        }

        [Test]
        public void NextStep_ClearsStepOnly()
        {
            var tracker = new NodeMetricsTracker();
            tracker.Tick(2f, true, false);
            tracker.RecordWrongScan();
            tracker.RecordAacSelection("break");

            tracker.NextStep();

            Assert.AreEqual(0f, tracker.StepHesitationSeconds);
            Assert.AreEqual(0, tracker.StepWrongScans);
            Assert.IsFalse(tracker.StepAacOpened);
            Assert.IsNull(tracker.StepAacSelection);
            Assert.AreEqual(2f, tracker.SessionHesitationSeconds, 0.001f);
            Assert.AreEqual(1, tracker.SessionWrongScans);
        }

        [Test]
        public void NewSession_ClearsEverything()
        {
            var tracker = new NodeMetricsTracker();
            tracker.Tick(2f, true, false);
            tracker.RecordWrongScan();

            tracker.NewSession();

            Assert.AreEqual(0f, tracker.SessionActiveSeconds);
            Assert.AreEqual(0f, tracker.SessionHesitationSeconds);
            Assert.AreEqual(0, tracker.SessionWrongScans);
        }
    }
}
