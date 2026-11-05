using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmartLife.UI
{
    /// <summary>
    /// SOS button, calm help card and the steady "Camera on" indicator. The indicator sits above every
    /// other canvas, including the guardian menu, so nothing can hide a running camera. It never
    /// blinks or animates (docs/SECURITY.md, docs/DESIGN_SYSTEM.md).
    /// </summary>
    public class EmergencyOverlay : MonoBehaviour
    {
        public const string CameraOnText = "Camera on";
        public const string MicrophoneOnText = "Microphone on";

        private const int CardOrder = 40;
        private const int SosOrder = 60;
        private const int IndicatorOrder = 110;
        private const float SosSize = 120f;

        private Canvas _cardCanvas;
        private Canvas _sosCanvas;
        private Canvas _indicatorCanvas;
        private TMP_Text _indicatorLabel;

        public bool IsHelpCardShown => _cardCanvas != null && _cardCanvas.gameObject.activeSelf;
        public bool IsIndicatorShown => _indicatorCanvas != null && _indicatorCanvas.gameObject.activeSelf;

        public void Init(Action onSos)
        {
            _sosCanvas = UiKit.CreateCanvas("SosCanvas", SosOrder);
            Button sos = UiKit.Button(_sosCanvas.transform, "SOS", UiKit.Help, () => onSos());
            var rect = (RectTransform)sos.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(SosSize, SosSize);
        }

        public void ShowHelpCard()
        {
            if (_cardCanvas == null)
            {
                _cardCanvas = UiKit.CreateCanvas("HelpCardCanvas", CardOrder);
                RectTransform panel = UiKit.Panel(_cardCanvas.transform, "HelpCard", UiKit.Bg);
                RectTransform column = UiKit.Column(panel, 800f);
                UiKit.Label(column, "Help is coming", 56f, FontStyles.Bold);
                UiKit.Label(column, "Stay here. Find an adult.", 40f);
            }
            _cardCanvas.gameObject.SetActive(true);
        }

        public void HideHelpCard()
        {
            if (_cardCanvas != null) _cardCanvas.gameObject.SetActive(false);
        }

        /// <summary>Shown before capture starts, and hidden only after capture has ended.</summary>
        public void ShowIndicator(bool video)
        {
            if (_indicatorCanvas == null)
            {
                _indicatorCanvas = UiKit.CreateCanvas("StreamIndicatorCanvas", IndicatorOrder);
                _indicatorCanvas.GetComponent<GraphicRaycaster>().enabled = false;
                var go = new GameObject("StreamIndicator", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_indicatorCanvas.transform, false);
                Image background = go.GetComponent<Image>();
                background.color = UiKit.Wait;
                background.raycastTarget = false;
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -16f);
                rect.sizeDelta = new Vector2(360f, 72f);
                _indicatorLabel = UiKit.Label(go.transform, string.Empty, 36f, FontStyles.Bold);
                UiKit.Stretch((RectTransform)_indicatorLabel.transform);
            }
            _indicatorLabel.text = video ? CameraOnText : MicrophoneOnText;
            _indicatorCanvas.gameObject.SetActive(true);
        }

        public void HideIndicator()
        {
            if (_indicatorCanvas != null) _indicatorCanvas.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyCanvas(_cardCanvas);
            DestroyCanvas(_sosCanvas);
            DestroyCanvas(_indicatorCanvas);
        }

        private static void DestroyCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            if (Application.isPlaying) Destroy(canvas.gameObject);
            else DestroyImmediate(canvas.gameObject);
        }
    }
}
