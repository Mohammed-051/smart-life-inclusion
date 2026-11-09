using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartLife.Core
{
    /// <summary>
    /// Keeps exactly one step scene loaded next to the persistent boot scene: the scene of the current
    /// step. The boot scene holds the AR camera, marker targets, state machine, telemetry and menus, so
    /// nothing restarts between steps. Listens to the state machine and never changes task state.
    /// </summary>
    public class StepSceneLoader : MonoBehaviour
    {
        /// <summary>The loader of the running routine, for step scenes that need the learner's profile.</summary>
        public static StepSceneLoader Current { get; private set; }

        private TaskStateMachine _machine;
        private string _loaded;
        private string _wanted;
        private bool _working;

        public StudentProfile Profile { get; private set; } = StudentProfile.Default;
        public string LoadedScene => _loaded;

        public void Init(TaskStateMachine machine, StudentProfile profile)
        {
            _machine = machine;
            Profile = profile ?? StudentProfile.Default;
            Current = this;
            _machine.OnStateChanged += HandleStateChanged;
            HandleStateChanged(_machine.State, _machine.StepIndex);
        }

        /// <summary>The scene the given state should show. Pure, so it is unit-tested.</summary>
        public static string SceneFor(TaskDefinition task, MachineState state, int stepIndex)
        {
            if (task == null || state == MachineState.Idle || state == MachineState.Complete) return null;
            if (stepIndex < 0 || stepIndex >= task.Count) return null;
            string scene = task[stepIndex].sceneName;
            return string.IsNullOrEmpty(scene) ? null : scene;
        }

        private void HandleStateChanged(MachineState state, int stepIndex)
        {
            // A pause keeps the current scene, so the learner sees the same thing on resume.
            if (state == MachineState.Paused) return;
            _wanted = SceneFor(_machine.Task, state, stepIndex);
            if (!_working && isActiveAndEnabled) StartCoroutine(Sync());
        }

        // One load or unload at a time; a newer request simply changes _wanted while this runs.
        private IEnumerator Sync()
        {
            _working = true;
            while (_loaded != _wanted)
            {
                if (_loaded != null)
                {
                    Scene old = SceneManager.GetSceneByName(_loaded);
                    if (old.isLoaded) yield return SceneManager.UnloadSceneAsync(old);
                    _loaded = null;
                    continue;
                }

                string next = _wanted;
                if (!Application.CanStreamedLevelBeLoaded(next))
                {
                    Debug.LogError($"[StepSceneLoader] Scene '{next}' is not in Build Settings. Run SMART-LIFE > Build Morning Routine Scenes.", this);
                    _loaded = next; // Do not retry every frame; the step still runs without its scene.
                    continue;
                }
                yield return SceneManager.LoadSceneAsync(next, LoadSceneMode.Additive);
                _loaded = next;
            }
            _working = false;
        }

        private void OnDestroy()
        {
            if (_machine != null) _machine.OnStateChanged -= HandleStateChanged;
            if (Current == this) Current = null;
        }
    }
}
