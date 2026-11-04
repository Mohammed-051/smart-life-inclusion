using System;
using UnityEngine;
using SmartLife.Core;

namespace SmartLife.AR
{
    public enum MascotMode { Hidden, Instruction, Ambient }

    /// <summary>
    /// Swaps the full 3D mascot (demonstrating a step) for the low-poly ambient mascot (child is
    /// acting). Only reads task state. See docs/clinical_architecture.md section 3.1.
    /// </summary>
    public class MascotStateManager : MonoBehaviour
    {
        [SerializeField] private TaskStateMachine stateMachine;
        [Tooltip("Full 3D rig shown while instructing")]
        [SerializeField] private GameObject instructionModel;
        [Tooltip("Low-poly model shown during task execution")]
        [SerializeField] private GameObject ambientModel;

        /// <summary>Hook for the voice layer: speak the prompt once on entering Ambient.</summary>
        public event Action<MascotMode> OnModeChanged;

        public MascotMode Mode { get; private set; } = MascotMode.Hidden;

        private void OnEnable()
        {
            stateMachine.OnStateChanged += HandleStateChanged;
            HandleStateChanged(stateMachine.State, stateMachine.StepIndex);
        }

        private void OnDisable() => stateMachine.OnStateChanged -= HandleStateChanged;

        /// <summary>Pure mapping, unit-tested in EditMode.</summary>
        public static MascotMode Resolve(MachineState state, TaskDefinition.Cue cue)
        {
            // The arrow is the single cue on navigation steps, so the mascot stays out of view.
            if (cue != TaskDefinition.Cue.Avatar) return MascotMode.Hidden;
            return state switch
            {
                MachineState.Awaiting => MascotMode.Instruction,
                MachineState.Active or MachineState.Paused => MascotMode.Ambient,
                _ => MascotMode.Hidden,
            };
        }

        private void HandleStateChanged(MachineState state, int stepIndex)
        {
            var task = stateMachine.Task;
            // StepIndex equals Count once the task completes, so there is no step to read.
            bool hasStep = task != null && stepIndex < task.Count;
            var next = hasStep ? Resolve(state, task[stepIndex].cue) : MascotMode.Hidden;

            if (instructionModel != null) instructionModel.SetActive(next == MascotMode.Instruction);
            if (ambientModel != null) ambientModel.SetActive(next == MascotMode.Ambient);

            if (next == Mode) return;
            Mode = next;
            OnModeChanged?.Invoke(next);
        }
    }
}
