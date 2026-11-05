using System.Reflection;
using NUnit.Framework;
using SmartLife.Core;
using SmartLife.Voice;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SmartLife.Tests
{
    /// <summary>A missing service must never stop the child's task from starting.</summary>
    public class SceneBootstrapperTests
    {
        private GameObject _host;
        private TaskDefinition _task;

        [SetUp]
        public void SetUp()
        {
            _task = ScriptableObject.CreateInstance<TaskDefinition>();
            _host = new GameObject("controller");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_task);
        }

        private static void Start(SceneBootstrapper bootstrapper) =>
            typeof(SceneBootstrapper).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bootstrapper, null);

        private SceneBootstrapper AddBootstrapper()
        {
            var bootstrapper = _host.AddComponent<SceneBootstrapper>();
            var so = new SerializedObject(bootstrapper);
            so.FindProperty("_taskDefinition").objectReferenceValue = _task;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bootstrapper;
        }

        [Test]
        public void MissingServices_WarnAndTaskStillStarts()
        {
            var bootstrapper = AddBootstrapper();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("TelemetryService is missing"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VoiceCommandRouter is missing"));

            Start(bootstrapper);

            Assert.AreEqual(MachineState.Awaiting, _host.GetComponent<TaskStateMachine>().State);
        }

        [Test]
        public void VoiceRouter_AcceptsNoOpRecognizer()
        {
            var router = _host.AddComponent<VoiceCommandRouter>();
            router.Init(new NoOpVoiceRecognizer());
            Assert.DoesNotThrow(() => router.SetEnabled(true));
        }
    }
}
