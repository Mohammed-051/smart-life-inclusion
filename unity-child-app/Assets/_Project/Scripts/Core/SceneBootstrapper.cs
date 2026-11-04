using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// Starts the configured task when the scene loads, so the state machine leaves Idle
    /// and begins accepting markers.
    /// </summary>
    [RequireComponent(typeof(TaskStateMachine))]
    public class SceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private TaskDefinition _taskDefinition;

        private void Start()
        {
            // Begin() would make InstructionCard read a prompt from a missing task, so stop here instead.
            if (_taskDefinition == null)
            {
                Debug.LogError("[SceneBootstrapper] Assign a TaskDefinition before entering Play mode.", this);
                return;
            }

            GetComponent<TaskStateMachine>().Begin(_taskDefinition);
        }
    }
}
