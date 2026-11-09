using System;
using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// Single source of truth for task progress. AR, Voice and UI only raise inputs;
    /// this class decides transitions and announces them. Order is strict: a marker only
    /// advances the task when it is the expected next marker.
    /// </summary>
    public class TaskStateMachine : MonoBehaviour
    {
        [SerializeField] private TaskDefinition task;

        /// <summary>(stepIndex, status, autonomous) — consumed by Telemetry.</summary>
        public event Action<int, TaskStatus, bool> OnStepEvent;
        /// <summary>Fired on every state change — consumed by AR presenters and UI.</summary>
        public event Action<MachineState, int> OnStateChanged;
        /// <summary>Wrong marker scanned; UI shows a gentle hint toward the expected one.</summary>
        public event Action<int> OnUnexpectedMarker;

        public TaskDefinition Task => task;
        public MachineState State { get; private set; } = MachineState.Idle;
        public int StepIndex { get; private set; }
        public float ProgressPct => task == null || task.Count == 0 ? 0f : 100f * StepIndex / task.Count;
        /// <summary>True while the current step's hold timer runs. Marker scans are ignored until it ends.</summary>
        public bool IsHolding => State == MachineState.Active && _holdTimer > 0f;
        /// <summary>Seconds left on the current hold, for UI feedback.</summary>
        public float HoldSecondsRemaining => IsHolding ? _holdTimer : 0f;

        private MachineState _stateBeforePause;
        private bool _assistedThisStep;
        private float _holdTimer;

        /// <summary>Begin (or restart) the configured task.</summary>
        public void Begin(TaskDefinition definition = null)
        {
            if (definition != null) task = definition;
            StepIndex = 0;
            _assistedThisStep = false;
            SetState(MachineState.Awaiting);
        }

        /// <summary>Called by AR when a TRACKED image target is found.</summary>
        public void HandleMarker(string markerId)
        {
            // Idle: the guardian menu is still open, so a scan must not show a wrong-marker hint.
            if (State is MachineState.Idle or MachineState.Paused or MachineState.Complete || task == null) return;

            // Time lock: a hold step only ends when its timer does, so no scan can skip it.
            // Guardian overrides go through Apply() and can still move on.
            if (IsHolding) return;

            var expected = task[StepIndex];
            if (markerId != expected.markerId)
            {
                // Next step's marker while current is active = implicit completion of current step.
                bool isNext = State == MachineState.Active && StepIndex + 1 < task.Count && task[StepIndex + 1].markerId == markerId;
                if (isNext) { CompleteStep(); HandleMarker(markerId); }
                else OnUnexpectedMarker?.Invoke(StepIndex);
                return;
            }

            if (State == MachineState.Awaiting)
            {
                _holdTimer = expected.holdSeconds;
                OnStepEvent?.Invoke(StepIndex, TaskStatus.Started, !_assistedThisStep);
                SetState(MachineState.Active);
                // Navigation checkpoints complete on arrival, including hallway arrows inside a routine.
                if (task.TaskKind == TaskDefinition.Kind.Nav || TaskDefinition.IsArrival(expected.cue)) CompleteStep();
            }
        }

        /// <summary>Apply a guardian override from any source.</summary>
        public void Apply(OverrideCommand cmd)
        {
            switch (cmd)
            {
                case OverrideCommand.Pause when State != MachineState.Paused:
                    _stateBeforePause = State;
                    OnStepEvent?.Invoke(StepIndex, TaskStatus.Paused, false);
                    SetState(MachineState.Paused);
                    break;
                case OverrideCommand.Resume when State == MachineState.Paused:
                    OnStepEvent?.Invoke(StepIndex, TaskStatus.Resumed, false);
                    SetState(_stateBeforePause);
                    break;
                case OverrideCommand.Skip when State is MachineState.Awaiting or MachineState.Active:
                    OnStepEvent?.Invoke(StepIndex, TaskStatus.Skipped, false);
                    Advance();
                    break;
                case OverrideCommand.Reset:
                    OnStepEvent?.Invoke(StepIndex, TaskStatus.Reset, false);
                    Begin();
                    break;
            }
        }

        /// <summary>Guardian tap/assist on current step: marks it non-autonomous.</summary>
        public void MarkAssisted() => _assistedThisStep = true;

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            if (!IsHolding) return;
            _holdTimer -= deltaTime;
            if (_holdTimer <= 0f) CompleteStep();
        }

        private void CompleteStep()
        {
            OnStepEvent?.Invoke(StepIndex, TaskStatus.Completed, !_assistedThisStep);
            Advance();
        }

        private void Advance()
        {
            StepIndex++;
            _assistedThisStep = false;
            SetState(StepIndex >= task.Count ? MachineState.Complete : MachineState.Awaiting);
        }

        private void SetState(MachineState next)
        {
            State = next;
            OnStateChanged?.Invoke(State, StepIndex);
        }
    }
}
