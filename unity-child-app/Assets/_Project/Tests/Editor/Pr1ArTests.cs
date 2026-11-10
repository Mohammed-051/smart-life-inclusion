using System.Reflection;
using NUnit.Framework;
using SmartLife.AR;
using SmartLife.Core;
using UnityEngine;

namespace SmartLife.Tests
{
    /// <summary>Anchoring defer and shared-marker re-scan rules that need no running Vuforia.</summary>
    public class Pr1ArTests
    {
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void RescanGate_BlocksSameDetection_UntilLostThenFound()
        {
            var gate = new RescanGate();
            Assert.IsTrue(gate.CanFire);
            gate.MarkFired();
            Assert.IsFalse(gate.CanFire, "Still-visible marker must not complete the next step.");
            gate.MarkLost();
            Assert.IsTrue(gate.CanFire);
        }

        [Test]
        public void MarkerObserver_IgnoredScan_DoesNotConsumeRescanGate()
        {
            var go = new GameObject("observer");
            var machine = go.AddComponent<TaskStateMachine>(); // Idle: HandleMarker ignores the scan.
            var observer = go.AddComponent<MarkerObserver>();
            observer.Bind(machine, "MK_SINK");

            typeof(MarkerObserver).GetMethod("Register", Priv).Invoke(observer, null);

            var gate = (RescanGate)typeof(MarkerObserver).GetField("_rescan", Priv).GetValue(observer);
            Assert.IsTrue(gate.CanFire, "A scan the machine ignored must not block the later pickup.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void StepStage_BeforeTargetsExist_HidesContentAndKeepsParent()
        {
            var registryGo = new GameObject("registry");
            registryGo.AddComponent<ImageTargetRegistry>().Init(null); // Edit mode: no Vuforia, so no targets.

            var stageGo = new GameObject("stage");
            var content = new GameObject("content").transform;
            var stage = stageGo.AddComponent<StepStage>();
            typeof(StepStage).GetField("markerId", Priv).SetValue(stage, "MK_SINK");
            typeof(StepStage).GetField("content", Priv).SetValue(stage, content);

            typeof(StepStage).GetMethod("Start", Priv).Invoke(stage, null);

            Assert.IsFalse(content.gameObject.activeSelf, "Content must not float at the scene origin.");
            Assert.IsNull(content.parent);
            Object.DestroyImmediate(stageGo); // Unsubscribes from TargetsReady without throwing.
            Object.DestroyImmediate(content.gameObject);
            Object.DestroyImmediate(registryGo);
        }
    }
}
