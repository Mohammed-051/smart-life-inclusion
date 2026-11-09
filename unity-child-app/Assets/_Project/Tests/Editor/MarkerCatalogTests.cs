using System;
using NUnit.Framework;
using SmartLife.AR;
using SmartLife.Core;
using UnityEditor;
using UnityEngine;

namespace SmartLife.Tests
{
    /// <summary>The shipped marker catalog must cover every task marker with an image Vuforia can use.</summary>
    public class MarkerCatalogTests
    {
        private const string TaskFolder = "Assets/_Project/Data";

        [Test]
        public void Parse_ReadsWidthAndIds()
        {
            MarkerCatalog catalog = MarkerCatalog.Parse("{\"printedWidthMeters\":0.18,\"markers\":[\"MK_A\",\"MK_B\"]}");
            Assert.AreEqual(0.18f, catalog.PrintedWidthMeters, 1e-6f);
            CollectionAssert.AreEqual(new[] { "MK_A", "MK_B" }, catalog.MarkerIds);
        }

        [TestCase("{\"printedWidthMeters\":0,\"markers\":[\"MK_A\"]}")]
        [TestCase("{\"printedWidthMeters\":0.18,\"markers\":[]}")]
        [TestCase("{\"printedWidthMeters\":0.18,\"markers\":[\"MK_A\",\"MK_A\"]}")]
        public void Parse_RejectsBadCatalogs(string json)
        {
            Assert.Throws<FormatException>(() => MarkerCatalog.Parse(json));
        }

        [Test]
        public void ShippedCatalog_HasAReadableImageForEveryMarker()
        {
            MarkerCatalog catalog = MarkerCatalog.LoadFromResources();
            Assert.IsNotNull(catalog, "Run tools/markers/generate_markers.py to create Resources/Markers.");

            foreach (string id in catalog.MarkerIds)
            {
                Texture2D image = MarkerCatalog.LoadImage(id);
                Assert.IsNotNull(image, $"{id} has no usable image.");
                Assert.That(image.format, Is.EqualTo(TextureFormat.RGBA32).Or.EqualTo(TextureFormat.RGB24), id);
                Assert.That(image.width, Is.InRange(256, 2048), id);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void EveryTaskMarker_IsInTheShippedCatalog()
        {
            MarkerCatalog catalog = MarkerCatalog.LoadFromResources();
            Assert.IsNotNull(catalog);

            string[] guids = AssetDatabase.FindAssets("t:TaskDefinition", new[] { TaskFolder });
            Assert.IsNotEmpty(guids, "No TaskDefinition assets found.");
            foreach (string guid in guids)
            {
                var task = AssetDatabase.LoadAssetAtPath<TaskDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                for (int i = 0; i < task.Count; i++)
                    CollectionAssert.Contains(catalog.MarkerIds, task[i].markerId, $"{task.name} step {i + 1}");
            }
        }

        [Test]
        public void ScanWhileIdle_IsIgnored()
        {
            var task = ScriptableObject.CreateInstance<TaskDefinition>();
            var taskSo = new SerializedObject(task);
            SerializedProperty steps = taskSo.FindProperty("steps");
            steps.arraySize = 1;
            steps.GetArrayElementAtIndex(0).FindPropertyRelative("markerId").stringValue = "MK_SINK";
            taskSo.ApplyModifiedPropertiesWithoutUndo();

            var host = new GameObject("machine");
            var machine = host.AddComponent<TaskStateMachine>();
            var machineSo = new SerializedObject(machine);
            machineSo.FindProperty("task").objectReferenceValue = task;
            machineSo.ApplyModifiedPropertiesWithoutUndo();
            int hints = 0;
            machine.OnUnexpectedMarker += _ => hints++;

            machine.HandleMarker("MK_SOAP"); // Before Begin: the guardian menu is still open.
            machine.HandleMarker("MK_SINK");

            Assert.AreEqual(MachineState.Idle, machine.State);
            Assert.AreEqual(0, hints);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(task);
        }
    }
}
