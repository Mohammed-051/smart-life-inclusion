using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartLife.UI
{
    /// <summary>
    /// Builds uGUI from code, so overlays need no scene edits. Colors mirror docs/DESIGN_SYSTEM.md.
    /// Buttons change state instantly, with no fades or motion.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Bg = new Color32(0xF7, 0xF5, 0xF0, 0xFF);
        public static readonly Color Ink = new Color32(0x1F, 0x2A, 0x37, 0xFF);
        public static readonly Color Go = new Color32(0x7F, 0xC8, 0xA9, 0xFF);
        public static readonly Color Turn = new Color32(0x8F, 0xB8, 0xDE, 0xFF);
        public static readonly Color Wait = new Color32(0xF3, 0xD2, 0x7A, 0xFF);
        public static readonly Color Help = new Color32(0xE8, 0xA0, 0xA0, 0xFF);
        public static readonly Color Surface = Color.white;

        private const float TouchTarget = 96f;

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>A centered vertical stack of width-limited controls.</summary>
        public static RectTransform Column(Transform parent, float width = 640f)
        {
            var go = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0f);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        public static TMP_Text Label(Transform parent, string text, float size = 32f, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Ink;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string text, Color color, UnityAction onClick, float height = TouchTarget)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<LayoutElement>().minHeight = height;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.fadeDuration = 0f;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) button.onClick.AddListener(onClick);

            var label = Label(go.transform, text, 32f, FontStyles.Bold);
            Stretch((RectTransform)label.transform);
            return button;
        }

        public static TMP_InputField Input(Transform parent, string placeholder, TMP_InputField.ContentType contentType)
        {
            GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().minHeight = TouchTarget * 0.8f;

            var field = go.GetComponent<TMP_InputField>();
            field.contentType = contentType;
            field.pointSize = 30f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            if (field.placeholder is TMP_Text hint) hint.text = placeholder;
            field.textComponent.color = Ink;
            return field;
        }

        /// <summary>Destroys every child. Handles edit mode, where the EditMode tests run.</summary>
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }
    }
}
