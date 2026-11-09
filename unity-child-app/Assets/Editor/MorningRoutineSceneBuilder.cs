using System.Collections.Generic;
using System.IO;
using SmartLife.AR;
using SmartLife.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartLife.EditorTools
{
    /// <summary>
    /// Builds the V1 Morning Routine scenes from RD_MorningRoutine, one scene per step, and registers them
    /// in Build Settings. The boot scene is a copy of ADL_Handwashing (AR camera, GameController, UI)
    /// that runs the routine; every step scene holds only a StepStage with placeholder primitives.
    /// Safe to run again: step scenes are regenerated, the boot scene is only patched.
    /// </summary>
    public static class MorningRoutineSceneBuilder
    {
        public const string RoutinePath = "Assets/_Project/Data/MorningRoutine/RD_MorningRoutine.asset";
        public const string BootScenePath = "Assets/_Project/Scenes/MorningRoutine_Boot.unity";
        public const string StepSceneFolder = "Assets/_Project/Scenes/MorningRoutine";
        private const string TemplateScenePath = "Assets/_Project/Scenes/ADL_Handwashing.unity";
        private const string MaterialPath = "Assets/_Project/Resources/PlaceholderMascot.mat";

        [MenuItem("SMART-LIFE/Build Morning Routine Scenes")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var routine = AssetDatabase.LoadAssetAtPath<RoutineDefinition>(RoutinePath);
            if (routine == null)
            {
                Debug.LogError($"[MorningRoutineSceneBuilder] {RoutinePath} is missing.");
                return;
            }

            BuildBootScene(routine);

            var stepScenes = new List<string>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!AssetDatabase.IsValidFolder(StepSceneFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(StepSceneFolder).Replace('\\', '/'), Path.GetFileName(StepSceneFolder));
            foreach (TaskDefinition.Step step in AllSteps(routine))
            {
                string path = $"{StepSceneFolder}/{step.sceneName}.unity";
                BuildStepScene(step, path, material);
                stepScenes.Add(path);
            }

            RegisterInBuildSettings(stepScenes);
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Debug.Log($"[MorningRoutineSceneBuilder] Boot scene and {stepScenes.Count} step scenes built and added to Build Settings.");
        }

        /// <summary>Every step of every task in the routine, in default order.</summary>
        public static IEnumerable<TaskDefinition.Step> AllSteps(RoutineDefinition routine)
        {
            for (int t = 0; t < routine.TaskCount; t++)
            {
                TaskDefinition task = routine[t].task;
                if (task == null) continue;
                for (int s = 0; s < task.Count; s++) yield return task[s];
            }
        }

        private static void BuildBootScene(RoutineDefinition routine)
        {
            if (!File.Exists(BootScenePath) && !AssetDatabase.CopyAsset(TemplateScenePath, BootScenePath))
            {
                Debug.LogError($"[MorningRoutineSceneBuilder] Could not copy {TemplateScenePath}.");
                return;
            }

            Scene boot = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            foreach (GameObject root in boot.GetRootGameObjects())
            {
                // The template's hand-placed targets never see Vuforia; ImageTargetRegistry creates real ones.
                if (root.name.StartsWith("ImageTarget_")) Object.DestroyImmediate(root);
            }

            foreach (GameObject root in boot.GetRootGameObjects())
            {
                var bootstrapper = root.GetComponentInChildren<SceneBootstrapper>(true);
                if (bootstrapper == null) continue;
                var so = new SerializedObject(bootstrapper);
                so.FindProperty("_routine").objectReferenceValue = routine;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(boot);
        }

        private static void BuildStepScene(TaskDefinition.Step step, string path, Material material)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var stageGo = new GameObject("StepStage");
            var content = new GameObject("Content").transform;
            content.SetParent(stageGo.transform, false);

            GameObject mascot = null;
            if (TaskDefinition.IsArrival(step.cue)) BuildArrow(content, step.cue, material);
            else mascot = BuildMascot(content, material);

            var stage = stageGo.AddComponent<StepStage>();
            var so = new SerializedObject(stage);
            so.FindProperty("markerId").stringValue = step.markerId;
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("mascot").objectReferenceValue = mascot;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, path);
        }

        // Sizes are in meters on an 18 cm card: the mascot stands about 12 cm tall beside the card centre.
        private static GameObject BuildMascot(Transform parent, Material material)
        {
            var mascot = new GameObject("Mascot").transform;
            mascot.SetParent(parent, false);
            mascot.localPosition = new Vector3(-0.05f, 0f, 0f);
            Primitive(PrimitiveType.Capsule, "Body", mascot, new Vector3(0f, 0.045f, 0f), new Vector3(0.05f, 0.045f, 0.05f), material);
            Primitive(PrimitiveType.Sphere, "Head", mascot, new Vector3(0f, 0.11f, 0f), new Vector3(0.045f, 0.045f, 0.045f), material);
            Primitive(PrimitiveType.Cube, "Prop", parent, new Vector3(0.04f, 0.015f, 0f), new Vector3(0.03f, 0.03f, 0.03f), material);
            return mascot.gameObject;
        }

        // A flat arrow lying on the card, pointing the way to walk. Arrived shows a flat disc instead.
        private static void BuildArrow(Transform parent, TaskDefinition.Cue cue, Material material)
        {
            var arrow = new GameObject("Arrow").transform;
            arrow.SetParent(parent, false);
            if (cue == TaskDefinition.Cue.Arrived)
            {
                Primitive(PrimitiveType.Cylinder, "Disc", arrow, new Vector3(0f, 0.005f, 0f), new Vector3(0.1f, 0.005f, 0.1f), material);
                return;
            }

            arrow.localRotation = Quaternion.Euler(0f, cue == TaskDefinition.Cue.ArrowLeft ? -90f : cue == TaskDefinition.Cue.ArrowRight ? 90f : 0f, 0f);
            Primitive(PrimitiveType.Cube, "Shaft", arrow, new Vector3(0f, 0.01f, -0.02f), new Vector3(0.03f, 0.01f, 0.09f), material);
            Transform head = Primitive(PrimitiveType.Cube, "Head", arrow, new Vector3(0f, 0.01f, 0.04f), new Vector3(0.06f, 0.01f, 0.06f), material).transform;
            head.localRotation = Quaternion.Euler(0f, 45f, 0f);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // Visual only.
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void RegisterInBuildSettings(List<string> stepScenes)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BootScenePath, true) };
            foreach (string path in stepScenes) scenes.Add(new EditorBuildSettingsScene(path, true));
            // The single-task scene stays listed but off, so it can still be opened and tested.
            scenes.Add(new EditorBuildSettingsScene(TemplateScenePath, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
