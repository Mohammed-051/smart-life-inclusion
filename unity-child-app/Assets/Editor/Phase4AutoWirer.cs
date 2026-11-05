using System;
using SmartLife.AR;
using SmartLife.Core;
using SmartLife.Telemetry;
using SmartLife.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SmartLife.EditorTools
{
    /// <summary>
    /// Adds the Phase 4 components to GameController and links them, then reloads the saved scene
    /// and checks the result. Safe to run twice. Run from the menu, or by CLI with
    /// -executeMethod SmartLife.EditorTools.Phase4AutoWirer.Run. An exception gives a non-zero exit.
    /// Not [InitializeOnLoadMethod], because that would rewrite the scene on every recompile.
    /// </summary>
    public static class Phase4AutoWirer
    {
        // The scene whose SceneBootstrapper runs the ADL_Handwashing task.
        private const string ScenePath = "Assets/_Project/Scenes/ADL_Handwashing.unity";
        private const string ControllerName = "GameController";
        private const float HesitationSeconds = 15f;

        [MenuItem("SMART-LIFE/Wire Phase 4 Components")]
        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Wire(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {ScenePath}.");

            // Check the saved file, not the objects still held in memory.
            Verify(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
            Debug.Log($"[Phase4AutoWirer] OK: {ControllerName} wired and verified in {ScenePath}.");
        }

        private static void Wire(Scene scene)
        {
            GameObject controller = FindController(scene);
            TaskStateMachine machine = Require<TaskStateMachine>(controller);

            HesitationMonitor hesitation = GetOrAdd<HesitationMonitor>(controller);
            SetReference(hesitation, "stateMachine", machine);
            SetFloat(hesitation, "hesitationSeconds", HesitationSeconds);
            SetReference(GetOrAdd<EmergencyDetector>(controller), "stateMachine", machine);
            SetReference(GetOrAdd<MascotStateManager>(controller), "stateMachine", machine);

            TelemetryService telemetry = FindRequired<TelemetryService>();
            SetReference(telemetry, "stateMachine", machine);

            SceneBootstrapper bootstrapper = Require<SceneBootstrapper>(controller);
            SetReference(bootstrapper, "telemetry", telemetry);
            SetReference(bootstrapper, "voiceRouter", FindRequired<VoiceCommandRouter>());
        }

        private static void Verify(Scene scene)
        {
            GameObject controller = FindController(scene);
            TaskStateMachine machine = Require<TaskStateMachine>(controller);

            HesitationMonitor hesitation = Require<HesitationMonitor>(controller);
            Check(hesitation, "stateMachine", machine);
            Check(Require<EmergencyDetector>(controller), "stateMachine", machine);
            Check(Require<MascotStateManager>(controller), "stateMachine", machine);

            TelemetryService telemetry = FindRequired<TelemetryService>();
            Check(telemetry, "stateMachine", machine);

            SceneBootstrapper bootstrapper = Require<SceneBootstrapper>(controller);
            Check(bootstrapper, "telemetry", telemetry);
            Check(bootstrapper, "voiceRouter", FindRequired<VoiceCommandRouter>());

            float seconds = new SerializedObject(hesitation).FindProperty("hesitationSeconds").floatValue;
            if (!Mathf.Approximately(seconds, HesitationSeconds))
                throw new InvalidOperationException($"hesitationSeconds is {seconds}, expected {HesitationSeconds}.");
        }

        private static void Check(Object owner, string field, Object expected)
        {
            if (new SerializedObject(owner).FindProperty(field).objectReferenceValue != expected)
                throw new InvalidOperationException($"{owner.GetType().Name}.{field} is not linked to {expected.GetType().Name}.");
        }

        private static GameObject FindController(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == ControllerName) return root;
            throw new InvalidOperationException($"{ControllerName} not found in {ScenePath}.");
        }

        private static T FindRequired<T>() where T : Object
        {
            T found = Object.FindAnyObjectByType<T>();
            if (found == null) throw new InvalidOperationException($"{typeof(T).Name} not found in {ScenePath}.");
            return found;
        }

        private static T Require<T>(GameObject target) where T : Component
        {
            if (target.TryGetComponent(out T existing)) return existing;
            throw new InvalidOperationException($"{target.name} has no {typeof(T).Name}.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();

        private static void SetReference(Object owner, string field, Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object owner, string field, float value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
