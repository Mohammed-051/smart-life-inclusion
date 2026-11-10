using System;
using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// Raises OnStall when the child has not acted for the threshold (prompt ladder, guardian
    /// alert). It never opens the help board: the child opens help only by pressing Help.
    /// Only reads task state. See docs/clinical_architecture.md section 3.2.
    /// </summary>
    public class HesitationMonitor : MonoBehaviour
    {
        [SerializeField] private TaskStateMachine stateMachine;
        [SerializeField] private float hesitationSeconds = 15f;

        /// <summary>(stepIndex) — the child has stalled on this step. Informational only.</summary>
        public event Action<int> OnStall;

        public float ElapsedSeconds { get; private set; }

        private bool _fired;

        private void OnEnable() => stateMachine.OnStateChanged += HandleStateChanged;
        private void OnDisable() => stateMachine.OnStateChanged -= HandleStateChanged;
        private void Update() => Tick(Time.deltaTime);

        public float HesitationSeconds => hesitationSeconds;

        /// <summary>Per-learner delay from the profile, applied before the routine starts.</summary>
        public void SetHesitationSeconds(float seconds)
        {
            if (seconds > 0f) hesitationSeconds = seconds;
        }

        /// <summary>Call when the AAC board is dismissed to start a fresh wait.</summary>
        public void Rearm()
        {
            ElapsedSeconds = 0f;
            _fired = false;
        }

        private void Tick(float deltaTime)
        {
            if (_fired || !IsWaitingOnChild()) return;
            ElapsedSeconds += deltaTime;
            if (ElapsedSeconds < hesitationSeconds) return;
            _fired = true;
            OnStall?.Invoke(stateMachine.StepIndex);
        }

        // A running hold means the child is doing the step, which is not hesitation.
        private bool IsWaitingOnChild() =>
            stateMachine.State == MachineState.Awaiting
            || (stateMachine.State == MachineState.Active && !stateMachine.IsHolding);

        // Tick is frozen while Paused (IsWaitingOnChild is false). Leaving Paused starts a fresh wait,
        // so resuming never fires a stall at once.
        private void HandleStateChanged(MachineState state, int stepIndex)
        {
            if (state != MachineState.Paused) Rearm();
        }
    }
}
