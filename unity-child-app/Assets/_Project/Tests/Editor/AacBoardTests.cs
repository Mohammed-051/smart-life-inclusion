using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SmartLife.AR;
using SmartLife.Core;
using SmartLife.UI;

namespace SmartLife.Tests
{
    public class AacBoardTests
    {
        private GameObject _host;
        private TaskStateMachine _machine;
        private AacBoard _board;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("board");
            _machine = _host.AddComponent<TaskStateMachine>();
            _board = _host.AddComponent<AacBoard>();
            _board.Init(_machine, null, null, null);
            _machine.Begin(AssetDatabase.LoadAssetAtPath<TaskDefinition>("Assets/_Project/Data/ADL_Handwashing.asset"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            // OnDestroy does not run in edit mode, so remove the board's canvas here.
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                Object.DestroyImmediate(canvas.gameObject);
            PauseLock.Clear();
        }

        private static string[] ButtonLabels() =>
            Object.FindObjectsByType<Button>().Select(b => b.GetComponentInChildren<TMP_Text>().text).ToArray();

        private static void Press(string label) =>
            Object.FindObjectsByType<Button>().Single(b => b.GetComponentInChildren<TMP_Text>().text == label).onClick.Invoke();

        [Test]
        public void Open_FreezesClock_AndShowsTheFourSymbols()
        {
            _board.Open(0);
            Assert.AreEqual(0f, Time.timeScale);
            CollectionAssert.AreEquivalent(new[] { "Too loud", "Need help", "Show me", "Break" }, ButtonLabels());
        }

        [Test]
        public void TooLoud_ClosesBoard_AndLeavesTaskRunning()
        {
            _board.Open(0);
            Press("Too loud");
            Assert.IsFalse(_board.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(MachineState.Awaiting, _machine.State);
        }

        [Test]
        public void Break_PausesTask_AndRestartsClock()
        {
            _board.Open(0);
            Press("Break");
            Assert.AreEqual(MachineState.Paused, _machine.State);
            Assert.IsFalse(_board.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void NeedHelp_MakesTheStepAssisted()
        {
            bool? autonomous = null;
            _machine.OnStepEvent += (_, status, auto) => { if (status == TaskStatus.Started) autonomous = auto; };
            _board.Open(0);
            Press("Need help");
            _machine.HandleMarker("MK_SINK");
            Assert.AreEqual(false, autonomous);
        }

        [Test]
        public void OverrideWhileOpen_ClosesBoard()
        {
            _board.Open(0);
            _machine.Apply(OverrideCommand.Pause);
            Assert.IsFalse(_board.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void GuardianMenuHold_KeepsClockFrozen_AfterBoardCloses()
        {
            PauseLock.Acquire();
            _board.Open(0);
            Press("Too loud");
            Assert.AreEqual(0f, Time.timeScale);
            PauseLock.Release();
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
