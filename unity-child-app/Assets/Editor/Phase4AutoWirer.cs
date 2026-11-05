using System;
using SmartLife.AR;
using SmartLife.Core;
using SmartLife.Telemetry;
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
        private const string ScenePath = "Assets/smart-life-inclusion.unity";
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

            // The new components have no telemetry field, so this is the only link to make.
            SetReference(FindTelemetry(), "stateMachine", machine);
        }

        private static void Verify(Scene scene)
        {
            GameObject controller = FindController(scene);
            TaskStateMachine machine = Require<TaskStateMachine>(controller);

            HesitationMonitor hesitation = Require<HesitationMonitor>(controller);
            Check(hesitation, machine);
            Check(Require<EmergencyDetector>(controller), machine);
            Check(Require<MascotStateManager>(controller), machine);
            Check(FindTelemetry(), machine);

            float seconds = new SerializedObject(hesitation).FindProperty("hesitationSeconds").floatValue;
            if (!Mathf.Approximately(seconds, HesitationSeconds))
                throw new InvalidOperationException($"hesitationSeconds is {seconds}, expected {HesitationSeconds}.");
        }

        private static void Check(Object owner, TaskStateMachine machine)
        {
            if (new SerializedObject(owner).FindProperty("stateMachine").objectReferenceValue != machine)
                throw new InvalidOperationException($"{owner.GetType().Name}.stateMachine is not linked to {ControllerName}.");
        }

        private static GameObject FindController(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == ControllerName) return root;
            throw new InvalidOperationException($"{ControllerName} not found in {ScenePath}.");
        }

        private static TelemetryService FindTelemetry()
        {
            TelemetryService telemetry = Object.FindAnyObjectByType<TelemetryService>();
            if (telemetry == null) throw new InvalidOperationException($"TelemetryService not found in {ScenePath}.");
            return telemetry;
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
