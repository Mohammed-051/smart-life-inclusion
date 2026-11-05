using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SmartLife.Core;
using SmartLife.Telemetry;

namespace SmartLife.UI
{
    /// <summary>
    /// Guardian gate and menu, built from code. On boot a saved session goes straight to the task with
    /// no UI. Otherwise: PIN (created on first run), then Firebase email and password. During the task
    /// a 3 s press on the top-left corner reopens it behind the PIN. The password never leaves this
    /// class except to Firebase Auth, and it is never logged or saved.
    /// </summary>
    public class GuardianMenu : MonoBehaviour
    {
        private const int CanvasOrder = 100;
        private const float HotspotSize = 140f;

        /// <summary>One row in the in-task menu. Label returns the current text, so toggles can read on or off.</summary>
        public sealed class MenuAction
        {
            public readonly Func<string> Label;
            public readonly Action Run;
            public readonly bool KeepOpen;
            public MenuAction(string label, Action run) { Label = () => label; Run = run; }
            public MenuAction(Func<string> label, Action run) { Label = label; Run = run; KeepOpen = true; }
        }

        private IDeviceAuth _auth;
        private GuardianPinStore _pins;
        private Action<DeviceSession> _unlocked;
        private IReadOnlyList<MenuAction> _actions;

        private Canvas _canvas;
        private RectTransform _screen;
        private RectTransform _hotspot;
        private TMP_Text _message;
        private TMP_Text _dots;
        private string _entry = string.Empty;
        private string _firstEntry;
        private Action _afterPin;
        private bool _holdsPause;
        private bool _busy;

        public bool IsOpen => _screen != null && _screen.gameObject.activeSelf;

        /// <summary>Boot entry. Calls back with a session, or with null when the guardian continues offline.</summary>
        public void Open(IDeviceAuth auth, GuardianPinStore pins, Action<DeviceSession> unlocked)
        {
            _auth = auth;
            _pins = pins;
            _unlocked = unlocked;
            EnsureCanvas();
            // Covers the app, so the learner never sees the task before the gate decides.
            ShowStatus("Starting...");
            _auth.Restore(session =>
            {
                if (session != null) Finish(session);
                else ShowPinThen(ShowLogin);
            });
        }

        /// <summary>Turns on the hidden corner press that reopens the guardian menu during the task.</summary>
        public void EnableMenuAccess(IReadOnlyList<MenuAction> actions)
        {
            _actions = actions;
            EnsureCanvas();
            if (_hotspot != null) return;
            var go = new GameObject("GuardianHotspot", typeof(RectTransform), typeof(Image), typeof(LongPressHotspot));
            go.transform.SetParent(_canvas.transform, false);
            go.transform.SetAsFirstSibling();
            go.GetComponent<Image>().color = Color.clear;
            _hotspot = (RectTransform)go.transform;
            _hotspot.anchorMin = _hotspot.anchorMax = _hotspot.pivot = new Vector2(0f, 1f);
            _hotspot.sizeDelta = new Vector2(HotspotSize, HotspotSize);
            go.GetComponent<LongPressHotspot>().Pressed += OpenMenu;
        }

        /// <summary>Asks for the PIN, then shows the menu. Freezes the task clock meanwhile.</summary>
        public void OpenMenu()
        {
            if (IsOpen) return;
            if (!_holdsPause)
            {
                PauseLock.Acquire();
                _holdsPause = true;
            }
            ShowPinThen(ShowActions);
        }

        private void OnDestroy()
        {
            if (_holdsPause) PauseLock.Release();
            if (_canvas != null) DestroyObject(_canvas.gameObject);
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = UiKit.CreateCanvas("GuardianCanvas", CanvasOrder);
            _screen = UiKit.Panel(_canvas.transform, "GuardianScreen", UiKit.Bg);
            _screen.gameObject.SetActive(false);
        }

        private RectTransform BeginScreen(string title)
        {
            UiKit.Clear(_screen);
            _screen.gameObject.SetActive(true);
            _message = null;
            _dots = null;
            RectTransform column = UiKit.Column(_screen);
            UiKit.Label(column, title, 40f, FontStyles.Bold);
            return column;
        }

        private void ShowStatus(string text) => BeginScreen(text);

        // ---------- PIN ----------

        private void ShowPinThen(Action next)
        {
            _afterPin = next;
            _firstEntry = null;
            ShowKeypad(_pins.HasPin ? "Guardian PIN" : "Create a guardian PIN", null);
        }

        private void ShowKeypad(string title, string message)
        {
            _entry = string.Empty;
            RectTransform column = BeginScreen(title);
            _dots = UiKit.Label(column, RenderDots(), 40f);
            _message = UiKit.Label(column, message ?? string.Empty, 26f);

            var gridGo = new GameObject("Keypad", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            gridGo.transform.SetParent(column, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200f, 88f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;
            gridGo.GetComponent<LayoutElement>().minHeight = 4 * 88f + 3 * 12f;

            for (int digit = 1; digit <= 9; digit++) AddDigit(grid.transform, digit);
            UiKit.Button(grid.transform, "Clear", UiKit.Wait, ClearEntry);
            AddDigit(grid.transform, 0);
            if (_holdsPause) UiKit.Button(grid.transform, "Cancel", UiKit.Surface, CloseMenu);
        }

        private void AddDigit(Transform parent, int digit)
        {
            string text = digit.ToString();
            UiKit.Button(parent, text, UiKit.Surface, () => PressDigit(text));
        }

        private void ClearEntry()
        {
            _entry = string.Empty;
            _dots.text = RenderDots();
        }

        private string RenderDots()
        {
            var dots = new System.Text.StringBuilder();
            for (int i = 0; i < GuardianPinStore.PinLength; i++) dots.Append(i < _entry.Length ? "● " : "○ ");
            return dots.ToString().TrimEnd();
        }

        private void PressDigit(string digit)
        {
            if (_entry.Length >= GuardianPinStore.PinLength) return;
            _entry += digit;
            _dots.text = RenderDots();
            if (_entry.Length == GuardianPinStore.PinLength) SubmitPin();
        }

        private void SubmitPin()
        {
            string entry = _entry;
            if (!_pins.HasPin)
            {
                if (_firstEntry == null)
                {
                    _firstEntry = entry;
                    ShowKeypad("Enter the same PIN again", null);
                }
                else if (_firstEntry != entry)
                {
                    _firstEntry = null;
                    ShowKeypad("Create a guardian PIN", "Those PINs did not match. Please start again.");
                }
                else
                {
                    _pins.SetPin(entry);
                    _firstEntry = null;
                    _afterPin();
                }
                return;
            }

            switch (_pins.Verify(entry))
            {
                case PinResult.Ok:
                    _afterPin();
                    break;
                case PinResult.LockedOut:
                    ClearEntry();
                    _message.text = $"Too many tries. Wait {_pins.LockoutSecondsRemaining} seconds.";
                    break;
                default:
                    ClearEntry();
                    _message.text = "That PIN did not match.";
                    break;
            }
        }

        // ---------- Firebase sign-in ----------

        private void ShowLogin() => ShowLogin(_auth.FirebaseAvailable ? null : "Firebase is not available on this device.");

        private void ShowLogin(string message)
        {
            RectTransform column = BeginScreen("Sign in this tablet");
            TMP_InputField email = UiKit.Input(column, "Email", TMP_InputField.ContentType.EmailAddress);
            TMP_InputField password = UiKit.Input(column, "Password", TMP_InputField.ContentType.Password);
            _message = UiKit.Label(column, message ?? string.Empty, 26f);

            Button signIn = UiKit.Button(column, "Sign in", UiKit.Go, null);
            signIn.interactable = _auth.FirebaseAvailable;
            signIn.onClick.AddListener(() => SubmitLogin(email, password, signIn));

            if (message != null)
                UiKit.Button(column, "Continue without saving progress", UiKit.Surface, () => Finish(null));
        }

        private void SubmitLogin(TMP_InputField email, TMP_InputField password, Button signIn)
        {
            if (_busy) return;
            if (string.IsNullOrWhiteSpace(email.text) || string.IsNullOrEmpty(password.text))
            {
                _message.text = "Enter the tablet account's email and password.";
                return;
            }

            _busy = true;
            signIn.interactable = false;
            _message.text = "Signing in...";
            string pass = password.text;
            password.text = string.Empty;
            _auth.SignIn(email.text.Trim(), pass, (session, error) =>
            {
                _busy = false;
                if (session != null) Finish(session);
                else ShowLogin(error);
            });
        }

        private void Finish(DeviceSession session)
        {
            UiKit.Clear(_screen);
            _screen.gameObject.SetActive(false);
            Action<DeviceSession> done = _unlocked;
            _unlocked = null;
            done?.Invoke(session);
        }

        // ---------- In-task menu ----------

        private void ShowActions()
        {
            RectTransform column = BeginScreen("Guardian menu");
            if (_actions != null)
            {
                foreach (MenuAction action in _actions)
                {
                    MenuAction captured = action;
                    UiKit.Button(column, captured.Label(), UiKit.Surface, () =>
                    {
                        if (captured.KeepOpen)
                        {
                            captured.Run();
                            ShowActions();
                        }
                        else
                        {
                            CloseMenu();
                            captured.Run();
                        }
                    }, 72f);
                }
            }
            UiKit.Button(column, "Close menu", UiKit.Go, CloseMenu, 72f);
        }

        private void CloseMenu()
        {
            UiKit.Clear(_screen);
            _screen.gameObject.SetActive(false);
            if (!_holdsPause) return;
            _holdsPause = false;
            PauseLock.Release();
        }

        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>Invisible corner target. A long press keeps a child's tap from opening the PIN screen.</summary>
        private sealed class LongPressHotspot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            private const float HoldSeconds = 3f;

            public event Action Pressed;
            private float _downAt = -1f;

            public void OnPointerDown(PointerEventData eventData) => _downAt = Time.unscaledTime;
            public void OnPointerUp(PointerEventData eventData) => _downAt = -1f;
            public void OnPointerExit(PointerEventData eventData) => _downAt = -1f;

            private void Update()
            {
                if (_downAt < 0f || Time.unscaledTime - _downAt < HoldSeconds) return;
                _downAt = -1f;
                Pressed?.Invoke();
            }
        }
    }
}
