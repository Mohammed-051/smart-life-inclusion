using System.Collections;
using TMPro;
using UnityEngine;
using SmartLife.Core;

namespace SmartLife.UI
{
    /// <summary>
    /// Single low-sensory instruction card. One prompt at a time; state changes use a
    /// 250 ms opacity fade only (no flashing, no looping motion). See docs/DESIGN_SYSTEM.md.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InstructionCard : MonoBehaviour
    {
        private const float FadeSeconds = 0.25f;

        [SerializeField] private TaskStateMachine stateMachine;
        [SerializeField] private TMP_Text label;

        private CanvasGroup _group;
        private Coroutine _fade;

        /// <summary>The cue the card shows (or is fading to). Always the step instruction, never a hint.</summary>
        public string Cue { get; private set; }
        /// <summary>How many fades have started. One per content change.</summary>
        public int FadesStarted { get; private set; }

        private void Awake() => _group = GetComponent<CanvasGroup>();
        private void OnEnable() => Bind();
        private void OnDisable() => stateMachine.OnStateChanged -= Show;

        /// <summary>Wires the card without the scene. Edit mode does not run OnEnable.</summary>
        public void Init(TaskStateMachine machine, TMP_Text text)
        {
            stateMachine = machine;
            label = text;
            if (_group == null) _group = GetComponent<CanvasGroup>();
            Bind();
        }

        private void Bind()
        {
            stateMachine.OnStateChanged -= Show;
            stateMachine.OnStateChanged += Show;
        }

        private void Show(MachineState state, int step)
        {
            string text = state switch
            {
                MachineState.Paused => "Wait with your teacher",
                MachineState.Complete => "All done. Well done!",
                _ => stateMachine.Task[step].prompt
            };
            SetText(text);
        }

        // Wrong-marker hint dropped: one cue at a time, and the instruction must never be erased.
        private void SetText(string text)
        {
            if (text == Cue) return; // a repeat notification for the same content must not fade again
            Cue = text;
            FadesStarted++;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeTo(text));
        }

        // One fade = out then in. Starts from the current alpha, so a cancelled fade does not jump.
        private IEnumerator FadeTo(string text)
        {
            for (float a = _group.alpha; a > 0f; a -= Time.unscaledDeltaTime / FadeSeconds) { _group.alpha = a; yield return null; }
            label.text = text;
            for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / FadeSeconds) { _group.alpha = a; yield return null; }
            _group.alpha = 1f;
        }
    }
}
