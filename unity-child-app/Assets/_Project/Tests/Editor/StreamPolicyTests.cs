using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using SmartLife.Core;
using SmartLife.UI;

namespace SmartLife.Tests
{
    public class EmergencyCoordinatorTests
    {
        private GameObject _host;
        private EmergencyDetector _detector;
        private EmergencyCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("emergency");
            _detector = _host.AddComponent<EmergencyDetector>();
            _coordinator = _host.AddComponent<EmergencyCoordinator>();
            _coordinator.Init(_detector, null, true, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            // OnDestroy does not run in edit mode, so remove the overlay's canvases here.
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                Object.DestroyImmediate(canvas.gameObject);
        }

        [Test]
        public void Sos_ShowsHelpCard_ButNeverStartsAStreamByItself()
        {
            _detector.TriggerSos();
            Assert.IsTrue(_coordinator.HasOpenEmergency);
            Assert.IsFalse(_coordinator.IsStreaming, "Detection unlocks a stream. Only a supervisor can start one.");
            Assert.IsTrue(_host.GetComponent<EmergencyOverlay>().IsHelpCardShown);
            Assert.IsFalse(_host.GetComponent<EmergencyOverlay>().IsIndicatorShown);
        }

        [Test]
        public void ResolveFromTablet_ClosesTheCard()
        {
            _detector.TriggerSos();
            _coordinator.ResolveFromTablet();
            Assert.IsFalse(_coordinator.HasOpenEmergency);
            Assert.IsFalse(_host.GetComponent<EmergencyOverlay>().IsHelpCardShown);
        }

        [TestCase(true, EmergencyOverlay.CameraOnText)]
        [TestCase(false, EmergencyOverlay.MicrophoneOnText)]
        public void Indicator_SaysWhatIsLive_AboveEveryOtherCanvas(bool video, string text)
        {
            var overlay = _host.GetComponent<EmergencyOverlay>();
            overlay.ShowIndicator(video);

            Assert.IsTrue(overlay.IsIndicatorShown);
            Canvas[] canvases = Object.FindObjectsByType<Canvas>();
            Canvas indicator = canvases.First(c => c.name == "StreamIndicatorCanvas");
            Assert.AreEqual(text, indicator.GetComponentInChildren<TMP_Text>().text);
            Assert.Greater(indicator.sortingOrder, 100, "It must sit above the guardian menu.");

            overlay.HideIndicator();
            Assert.IsFalse(overlay.IsIndicatorShown);
        }
    }

    public class StreamPolicyTests
    {
        private const long Now = 1000;
        private const long Later = 2000;

        private static StreamDecision Check(
            string openId = "em1",
            EmergencySource? source = EmergencySource.Sos,
            string requestedId = "em1",
            bool consent = true,
            long expires = Later,
            bool streaming = false) =>
            StreamPolicy.Evaluate(openId, source, requestedId, consent, Now, expires, streaming);

        [Test]
        public void Allows_OpenSos_MatchingRequest_WithConsent() => Assert.AreEqual(StreamDecision.Allowed, Check());

        [Test]
        public void Refuses_WithNoEmergency() =>
            Assert.AreEqual(StreamDecision.NoOpenEmergency, Check(openId: null, source: null));

        [TestCase(EmergencySource.Audio)]
        [TestCase(EmergencySource.Imu)]
        public void Refuses_AnythingButSos(EmergencySource source) =>
            Assert.AreEqual(StreamDecision.NotSos, Check(source: source));

        [Test]
        public void Refuses_ARequestForAnotherEmergency() =>
            Assert.AreEqual(StreamDecision.WrongEmergency, Check(requestedId: "old"));

        [Test]
        public void Refuses_WithoutConsent() => Assert.AreEqual(StreamDecision.NoConsent, Check(consent: false));

        [Test]
        public void Refuses_AnExpiredRequest() => Assert.AreEqual(StreamDecision.Expired, Check(expires: Now));

        [Test]
        public void Refuses_ASecondCall() => Assert.AreEqual(StreamDecision.AlreadyStreaming, Check(streaming: true));

        [Test]
        public void PrivateRoute_MeansAudioOnly()
        {
            Assert.IsFalse(StreamPolicy.AllowVideo(routeIsPrivate: true));
            Assert.IsTrue(StreamPolicy.AllowVideo(routeIsPrivate: false));
        }

        [Test]
        public void Tasks_ArePrivateByDefault()
        {
            var task = ScriptableObject.CreateInstance<TaskDefinition>();
            Assert.IsTrue(task.PrivateSpace);
            Object.DestroyImmediate(task);
        }
    }
}
