using System;
using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// Raises OnHesitation when the child has not acted for 15 s, so the UI can open the
    /// step's AAC board. Only reads task state. See docs/clinical_architecture.md section 3.2.
    /// </summary>
    public class HesitationMonitor : MonoBehaviour
    {
        [SerializeField] private TaskStateMachine stateMachine;
        [SerializeField] private float hesitationSeconds = 15f;

        /// <summary>(stepIndex) — the AAC board opens for this step.</summary>
        public event Action<int> OnHesitation;

        public float ElapsedSeconds { get; private set; }

        private bool _fired;
        private bool _wasPaused;
        private int _lastStep;

        private void OnEnable() => stateMachine.OnStateChanged += HandleStateChanged;
        private void OnDisable() => stateMachine.OnStateChanged -= HandleStateChanged;
        private void Update() => Tick(Time.deltaTime);

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
            OnHesitation?.Invoke(stateMachine.StepIndex);
        }

        // A running hold means the child is doing the step, which is not hesitation.
        private bool IsWaitingOnChild() =>
            stateMachine.State == MachineState.Awaiting
            || (stateMachine.State == MachineState.Active && !stateMachine.IsHolding);

        private void HandleStateChanged(MachineState state, int stepIndex)
        {
            // Pause and resume keep the clock, so a pause never hands the child a new 15 s.
            bool resuming = _wasPaused && stepIndex == _lastStep;
            _wasPaused = state == MachineState.Paused;
            _lastStep = stepIndex;
            if (!_wasPaused && !resuming) Rearm();
        }
    }
}
