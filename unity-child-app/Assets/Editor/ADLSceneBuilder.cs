using SmartLife.AR;
using SmartLife.Core;
using SmartLife.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SmartLife.EditorTools
{
    /// <summary>
    /// Scaffolds the Sprint 1 objects into the active scene. Safe to run twice: objects that
    /// already exist are reused. Changes support Undo and are left for you to save.
    /// </summary>
    public static class ADLSceneBuilder
    {
        // docs/DESIGN_SYSTEM.md tokens: --ink and --surface.
        private static readonly Color Ink = new Color32(0x1F, 0x2A, 0x37, 0xFF);
        private static readonly Color Surface = new Color(1f, 1f, 1f, 0.85f);
        private const float PromptFontSize = 48f; // Within the 40-56 sp prompt range.

        [MenuItem("SMART-LIFE/Scaffold Sprint 1 Scene")]
        public static void Scaffold()
        {
            Scene scene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Scaffold Sprint 1 Scene");
            int undoGroup = Undo.GetCurrentGroup();

            DeleteDefaultCamera(scene);

            GameObject controller = GetOrCreateRoot(scene, "GameController");
            TaskStateMachine stateMachine = GetOrAdd<TaskStateMachine>(controller);

            GameObject target = GetOrCreateRoot(scene, "ImageTarget_Sink");
            MarkerObserver observer = GetOrAdd<MarkerObserver>(target);
            SetReference(observer, "stateMachine", stateMachine);

            BuildCanvas(scene, stateMachine);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = controller;

            Debug.Log("[ADLSceneBuilder] Sprint 1 scene scaffolded. Save the scene, then do these by hand:\n" +
                      "1. Add the Vuforia ARCamera (GameObject > Vuforia Engine > AR Camera). The scene has no camera until you do.\n" +
                      "2. Add the Vuforia ImageTarget behaviour to ImageTarget_Sink and select your target. Remove any DefaultObserverEventHandler it adds, since MarkerObserver replaces it.\n" +
                      "3. Add TelemetryService and link its State Machine field to GameController.\n" +
                      "4. Create a TaskDefinition (Create > SmartLife > Task Definition) and assign it to TaskStateMachine.\n" +
                      "5. Call TaskStateMachine.Begin() when the task should start. No script calls it yet, so markers are ignored until then.");
        }

        private static void DeleteDefaultCamera(Scene scene)
        {
            GameObject camera = FindRoot(scene, "Main Camera");
            if (camera != null) Undo.DestroyObjectImmediate(camera);
        }

        private static void BuildCanvas(Scene scene, TaskStateMachine stateMachine)
        {
            GameObject canvasObject = GetOrCreateRoot(scene, "Canvas_UI");
            GetOrAdd<Canvas>(canvasObject).renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1200f); // Landscape tablet.
            GetOrAdd<GraphicRaycaster>(canvasObject);

            // InstructionCard needs its label assigned or it has nothing to write to.
            InstructionCard card = GetOrAdd<InstructionCard>(canvasObject);

            GameObject panel = GetOrCreateChild(canvasObject.transform, "CardPanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.1f, 0.05f);
            panelRect.anchorMax = new Vector2(0.9f, 0.25f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            GetOrAdd<Image>(panel).color = Surface;

            GameObject labelObject = GetOrCreateChild(panel.transform, "Label");
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = GetOrAdd<TextMeshProUGUI>(labelObject);
            label.color = Ink;
            label.fontSize = PromptFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            SetReference(card, "stateMachine", stateMachine);
            SetReference(card, "label", label);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static GameObject GetOrCreateRoot(Scene scene, string name)
        {
            GameObject existing = FindRoot(scene, name);
            if (existing != null) return existing;

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var created = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent(out T existing) ? existing : Undo.AddComponent<T>(target);
        }

        /// <summary>Assigns a private [SerializeField] so the link is saved with the scene.</summary>
        private static void SetReference(Object owner, string field, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[ADLSceneBuilder] {owner.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
