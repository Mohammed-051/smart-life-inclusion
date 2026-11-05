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
        private const string PlaceholderMaterial = "PlaceholderMascot";
        private const float PlaceholderScale = 0.25f;
        private static readonly Vector3 PlaceholderOffset = new Vector3(0f, -0.1f, 1.2f);

        [SerializeField] private TaskStateMachine stateMachine;
        [Tooltip("Full 3D rig shown while instructing")]
        [SerializeField] private GameObject instructionModel;
        [Tooltip("Low-poly model shown during task execution")]
        [SerializeField] private GameObject ambientModel;

        /// <summary>Hook for the voice layer: speak the prompt once on entering Ambient.</summary>
        public event Action<MascotMode> OnModeChanged;

        public MascotMode Mode { get; private set; } = MascotMode.Hidden;

        private void Awake()
        {
            if (instructionModel == null) instructionModel = CreatePlaceholder(PrimitiveType.Capsule, "Instruction Model (placeholder)", new Color32(0x7F, 0xC8, 0xA9, 0xFF));
            if (ambientModel == null) ambientModel = CreatePlaceholder(PrimitiveType.Sphere, "Ambient Model (placeholder)", new Color32(0x8F, 0xB8, 0xDE, 0xFF));
        }

        // shortcut: primitives stand in until the Blender models arrive. Assign them in the Inspector and these are skipped.
        private static GameObject CreatePlaceholder(PrimitiveType shape, string objectName, Color color)
        {
            GameObject model = GameObject.CreatePrimitive(shape);
            model.name = objectName;
            Destroy(model.GetComponent<Collider>());

            // Built from a Resources material, because a runtime primitive's default material is not URP and draws pink.
            Material template = Resources.Load<Material>(PlaceholderMaterial);
            var renderer = model.GetComponent<Renderer>();
            if (template != null) renderer.material = new Material(template) { color = color };
            else Debug.LogWarning("[Mascot] Placeholder material is missing.", model);

            Camera view = Camera.main;
            if (view != null) model.transform.SetParent(view.transform, false);
            model.transform.localPosition = PlaceholderOffset;
            model.transform.localScale = Vector3.one * PlaceholderScale;
            model.SetActive(false);
            return model;
        }

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

        /// <summary>
        /// "Show me" on the AAC board: back to the demonstrating model until the next state change.
        /// Reads no state beyond the current step, and only on avatar steps, so arrows stay the single cue.
        /// </summary>
        public void ShowDemo()
        {
            var task = stateMachine.Task;
            int step = stateMachine.StepIndex;
            if (task == null || step >= task.Count || task[step].cue != TaskDefinition.Cue.Avatar) return;

            if (instructionModel != null) instructionModel.SetActive(true);
            if (ambientModel != null) ambientModel.SetActive(false);
            if (Mode == MascotMode.Instruction) return;
            Mode = MascotMode.Instruction;
            OnModeChanged?.Invoke(Mode);
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
