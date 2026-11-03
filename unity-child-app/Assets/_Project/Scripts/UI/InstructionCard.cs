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

        private void Awake() => _group = GetComponent<CanvasGroup>();
        private void OnEnable()
        {
            stateMachine.OnStateChanged += Show;
            stateMachine.OnUnexpectedMarker += Hint;
        }
        private void OnDisable()
        {
            stateMachine.OnStateChanged -= Show;
            stateMachine.OnUnexpectedMarker -= Hint;
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

        private void Hint(int expectedStep) => SetText("Find the next sign");

        private void SetText(string text)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeTo(text));
        }

        private IEnumerator FadeTo(string text)
        {
            for (float t = 0; t < FadeSeconds; t += Time.unscaledDeltaTime) { _group.alpha = 1f - t / FadeSeconds; yield return null; }
            label.text = text;
            for (float t = 0; t < FadeSeconds; t += Time.unscaledDeltaTime) { _group.alpha = t / FadeSeconds; yield return null; }
            _group.alpha = 1f;
        }
    }
}
