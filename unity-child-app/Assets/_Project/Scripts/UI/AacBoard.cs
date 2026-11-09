using System.Collections.Generic;
using System.Linq; // Enumerable.Contains: IReadOnlyCollection has no Contains of its own.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SmartLife.AR;
using SmartLife.Core;
using SmartLife.Telemetry;

namespace SmartLife.UI
{
    /// <summary>
    /// AAC picture board, opened by HesitationMonitor after 15 s without action. It freezes the task
    /// clock and fills the screen, so it stays the only cue. See docs/clinical_architecture.md 3.2.
    /// </summary>
    public class AacBoard : MonoBehaviour
    {
        private const int CanvasOrder = 50;
        private const float TileHeight = 200f;

        private struct Option
        {
            public string Id;
            public string Label;
            public Color Color;
        }

        // shortcut: colored tiles stand in for picture symbols until the final icons exist.
        private static readonly Option[] Options =
        {
            new Option { Id = "too_loud", Label = "Too loud", Color = UiKit.Wait },
            new Option { Id = "need_help", Label = "Need help", Color = UiKit.Help },
            new Option { Id = "show_me", Label = "Show me", Color = UiKit.Go },
            new Option { Id = "break", Label = "Break", Color = UiKit.Turn },
        };

        private TaskStateMachine _machine;
        private HesitationMonitor _monitor;
        private TelemetryService _telemetry;
        private MascotStateManager _mascot;
        private Canvas _canvas;
        private bool _holdsPause;
        private IReadOnlyCollection<string> _shown;

        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        /// <summary>Telemetry and the mascot are optional. The board still works without them.</summary>
        public void Init(TaskStateMachine machine, HesitationMonitor monitor, TelemetryService telemetry, MascotStateManager mascot)
        {
            _machine = machine;
            _monitor = monitor;
            _telemetry = telemetry;
            _mascot = mascot;
            if (_monitor != null) _monitor.OnHesitation += Open;
            _machine.OnStateChanged += HandleStateChanged;
        }

        /// <summary>The learner's AAC options from their profile. Call before the board first opens.</summary>
        public void SetOptions(IReadOnlyCollection<string> optionIds) => _shown = optionIds;

        /// <summary>The options the board shows, in board order.</summary>
        public static List<string> VisibleOptionIds(IReadOnlyCollection<string> shown)
        {
            var ids = new List<string>();
            foreach (Option option in Options)
                if (shown == null || shown.Contains(option.Id) || option.Id == StudentProfile.AlwaysOnAacOption)
                    ids.Add(option.Id);
            return ids;
        }

        /// <summary>Shows the board and freezes the clock. stepIndex is reserved for per-step boards.</summary>
        public void Open(int stepIndex)
        {
            if (IsOpen) return;
            if (_canvas == null) Build();
            _canvas.gameObject.SetActive(true);
            if (!_holdsPause)
            {
                PauseLock.Acquire();
                _holdsPause = true;
            }
            if (_telemetry != null) _telemetry.RecordAacOpened();
        }

        private void Build()
        {
            _canvas = UiKit.CreateCanvas("AacBoardCanvas", CanvasOrder);
            RectTransform panel = UiKit.Panel(_canvas.transform, "AacBoardPanel", UiKit.Bg);
            RectTransform column = UiKit.Column(panel, 900f);
            UiKit.Label(column, "What do you need?", 48f, FontStyles.Bold);

            var gridGo = new GameObject("Symbols", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            gridGo.transform.SetParent(column, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(420f, TileHeight);
            grid.spacing = new Vector2(24f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;
            gridGo.GetComponent<LayoutElement>().minHeight = 2 * TileHeight + 24f;

            List<string> visible = VisibleOptionIds(_shown);
            foreach (Option option in Options)
            {
                if (!visible.Contains(option.Id)) continue;
                Option captured = option;
                Button button = UiKit.Button(grid.transform, option.Label, option.Color, () => Select(captured.Id), TileHeight);
                button.GetComponentInChildren<TMP_Text>().fontSize = 44f;
            }
        }

        private void Select(string id)
        {
            if (_telemetry != null) _telemetry.RecordAacSelection(id);

            switch (id)
            {
                case "need_help":
                    _machine.MarkAssisted();
                    break;
                case "show_me":
                    if (_mascot != null) _mascot.ShowDemo();
                    break;
                case "break":
                    _machine.Apply(OverrideCommand.Pause);
                    break;
            }
            Close();
        }

        // An override or emergency that moves the task also closes the board, so it never hides a help card.
        private void HandleStateChanged(MachineState state, int stepIndex)
        {
            if (IsOpen) Close();
        }

        private void Close()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_holdsPause)
            {
                _holdsPause = false;
                PauseLock.Release();
            }
            if (_monitor != null) _monitor.Rearm();
        }

        private void OnDestroy()
        {
            if (_monitor != null) _monitor.OnHesitation -= Open;
            if (_machine != null) _machine.OnStateChanged -= HandleStateChanged;
            if (_holdsPause) PauseLock.Release();
            if (_canvas == null) return;
            if (Application.isPlaying) Destroy(_canvas.gameObject);
            else DestroyImmediate(_canvas.gameObject);
        }
    }
}
