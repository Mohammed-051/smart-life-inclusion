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
    public class Pr1UiTests
    {
        private GameObject _host;
        private TaskStateMachine _machine;
        private TaskDefinition _task;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("pr1");
            _machine = _host.AddComponent<TaskStateMachine>();
            _task = AssetDatabase.LoadAssetAtPath<TaskDefinition>("Assets/_Project/Data/ADL_Handwashing.asset");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                Object.DestroyImmediate(canvas.gameObject);
            PauseLock.Clear();
        }

        private InstructionCard MakeCard()
        {
            var card = _host.AddComponent<InstructionCard>();
            card.Init(_machine, _host.AddComponent<TextMeshProUGUI>());
            return card;
        }

        [Test]
        public void Instruction_SurvivesAWrongMarkerHint()
        {
            InstructionCard card = MakeCard();
            _machine.Begin(_task);
            string prompt = card.Cue;
            int fades = card.FadesStarted;
            _machine.HandleMarker("MK_NOT_A_REAL_MARKER");
            Assert.AreEqual(_task[0].prompt, prompt);
            Assert.AreEqual(prompt, card.Cue);
            Assert.AreEqual(fades, card.FadesStarted);
        }

        [Test]
        public void OneFade_PerContentChange()
        {
            InstructionCard card = MakeCard();
            _machine.Begin(_task);
            Assert.AreEqual(1, card.FadesStarted);
            // Awaiting -> Active on the same step repeats the prompt: no second fade.
            _machine.HandleMarker(_task[0].markerId);
            Assert.AreEqual(1, card.FadesStarted);
            // A real content change fades exactly once more.
            _machine.Apply(OverrideCommand.Skip);
            Assert.AreEqual(2, card.FadesStarted);
        }

        [Test]
        public void HelpCard_SaysStayHereFindAnAdult()
        {
            var overlay = _host.AddComponent<EmergencyOverlay>();
            overlay.ShowHelpCard();
            Assert.IsTrue(Object.FindObjectsByType<TMP_Text>().Any(t => t.text == "Stay here. Find an adult."));
        }

        // Canvas rect in reference px (1280x800 base, match 0.5), at a given effective canvas size.
        private static Rect RectIn(RectTransform r, float w, float h)
        {
            var size = r.sizeDelta;
            return new Rect(r.anchorMin.x * w + r.anchoredPosition.x - r.pivot.x * size.x,
                            r.anchorMin.y * h + r.anchoredPosition.y - r.pivot.y * size.y, size.x, size.y);
        }

        [Test]
        public void HelpAndSos_NeverOverlap_EachOtherOrTheInstructionCard()
        {
            _host.AddComponent<EmergencyOverlay>().Init(() => { });
            var boot = _host.AddComponent<SceneBootstrapper>();
            typeof(SceneBootstrapper).GetMethod("AddHelpButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(boot, null);
            RectTransform Find(string name) => (RectTransform)Object.FindObjectsByType<Button>().Single(b => b.name == name).transform;
            RectTransform help = Find("Help"), sos = Find("SOS");
            Assert.GreaterOrEqual(help.sizeDelta.y, sos.sizeDelta.y); // touch target at least SOS size
            Assert.AreNotEqual(help.GetComponent<Image>().color, sos.GetComponent<Image>().color);
            // 1067x800 = 4:3 narrowest tablet, 1280x800, 2160x1000 ~ the Samsung (2340x1080) after match 0.5.
            foreach (var (w, h) in new[] { (1067f, 800f), (1280f, 800f), (2160f, 1000f) })
            {
                Rect a = RectIn(help, w, h), b = RectIn(sos, w, h);
                Rect card = Rect.MinMaxRect(0.15f * w, 0.05f * h, 0.85f * w, 0.25f * h); // CardPanel anchors in ADLSceneBuilder
                Assert.IsFalse(a.Overlaps(b), $"Help overlaps SOS at {w}x{h}");
                Assert.IsFalse(a.Overlaps(card), $"Help overlaps the card at {w}x{h}");
            }
        }

        [Test]
        public void Back_ClosesBoard_WithoutChangingTask()
        {
            var board = _host.AddComponent<AacBoard>();
            board.Init(_machine, null, null, null);
            _machine.Begin(_task);
            int step = _machine.StepIndex;
            board.Open(0);
            Object.FindObjectsByType<Button>().Single(b => b.GetComponentInChildren<TMP_Text>().text == "Back").onClick.Invoke();
            Assert.IsFalse(board.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(MachineState.Awaiting, _machine.State);
            Assert.AreEqual(step, _machine.StepIndex);
        }
    }
}
