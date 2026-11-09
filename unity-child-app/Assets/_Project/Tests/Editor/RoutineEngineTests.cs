using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SmartLife.Core;
using SmartLife.UI;
using UnityEditor;
using UnityEngine;

namespace SmartLife.Tests
{
    /// <summary>Phase 2 routine engine: profiles, routine building, step scenes and the shipped Morning Routine.</summary>
    public class RoutineEngineTests
    {
        private const string MorningRoutinePath = "Assets/_Project/Data/MorningRoutine/RD_MorningRoutine.asset";
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ---------- StudentProfile ----------

        [Test]
        public void Profile_MissingOrMalformed_IsDefault()
        {
            Assert.AreSame(StudentProfile.Default, StudentProfile.FromDatabase(null));
            Assert.AreSame(StudentProfile.Default, StudentProfile.FromDatabase("oops"));
        }

        [Test]
        public void Profile_ReadsOverridesFromDatabaseShape()
        {
            var value = new Dictionary<string, object>
            {
                ["tasks"] = new Dictionary<string, object>
                {
                    ["task_toilet"] = new Dictionary<string, object> { ["order"] = 2L, ["enabled"] = false },
                    ["task_wake"] = new Dictionary<string, object> { ["order"] = 1L },
                },
                ["voicePrompts"] = false,
                ["mascot"] = false,
                ["hesitationSeconds"] = 30L,
                ["aacOptions"] = new Dictionary<string, object> { ["too_loud"] = false, ["need_help"] = false },
            };

            StudentProfile profile = StudentProfile.FromDatabase(value);

            Assert.IsFalse(profile.VoicePrompts);
            Assert.IsFalse(profile.Mascot);
            Assert.AreEqual(30f, profile.HesitationSeconds);
            Assert.AreEqual(2, profile.Tasks.Count);
            Assert.IsFalse(profile.Tasks.Single(t => t.TaskId == "task_toilet").Enabled);
            Assert.IsTrue(profile.Tasks.Single(t => t.TaskId == "task_wake").Enabled);
            CollectionAssert.DoesNotContain(profile.AacOptions, "too_loud");
            CollectionAssert.Contains(profile.AacOptions, "need_help", "Need help can never be switched off.");
        }

        [TestCase(1L)]
        [TestCase(500L)]
        public void Profile_OutOfRangeHesitation_FallsBackTo15s(long seconds)
        {
            var value = new Dictionary<string, object> { ["hesitationSeconds"] = seconds };
            Assert.AreEqual(StudentProfile.DefaultHesitationSeconds, StudentProfile.FromDatabase(value).HesitationSeconds);
        }

        [Test]
        public void AacBoard_AlwaysShowsNeedHelp()
        {
            List<string> ids = AacBoard.VisibleOptionIds(new HashSet<string> { "break" });
            CollectionAssert.AreEquivalent(new[] { "need_help", "break" }, ids);
            Assert.AreEqual(4, AacBoard.VisibleOptionIds(null).Count);
        }

        // ---------- RoutineDefinition.Build ----------

        [Test]
        public void Build_Default_RunsEveryTaskInOrder()
        {
            RoutineDefinition routine = Routine(("a", Task("A1", "A2")), ("b", Task("B1")), ("c", Task("C1")));
            CollectionAssert.AreEqual(new[] { "A1", "A2", "B1", "C1" }, Checkpoints(routine.Build(StudentProfile.Default)));
            Assert.AreEqual("rt_test", routine.Build(StudentProfile.Default).RouteId);
        }

        [Test]
        public void Build_Profile_ReordersAndSkipsWholeTasks()
        {
            RoutineDefinition routine = Routine(("a", Task("A1", "A2")), ("b", Task("B1")), ("c", Task("C1")));
            StudentProfile profile = Profile(new TaskOverride("c", 1, true), new TaskOverride("a", 2, false));

            // c first, a skipped, b not mentioned so it keeps its default place after the listed tasks.
            CollectionAssert.AreEqual(new[] { "C1", "B1" }, Checkpoints(routine.Build(profile)));
        }

        [Test]
        public void Build_ProfileThatSkipsEverything_FallsBackToDefault()
        {
            RoutineDefinition routine = Routine(("a", Task("A1")), ("b", Task("B1")));
            StudentProfile profile = Profile(new TaskOverride("a", 1, false), new TaskOverride("b", 2, false));
            CollectionAssert.AreEqual(new[] { "A1", "B1" }, Checkpoints(routine.Build(profile)));
        }

        [Test]
        public void Build_LastStepWithNoHold_GetsAShortHoldSoItCanFinish()
        {
            RoutineDefinition routine = Routine(("a", Task("A1")));
            TaskDefinition built = routine.Build(StudentProfile.Default);
            Assert.AreEqual(RoutineDefinition.FinalStepHoldSeconds, built[built.Count - 1].holdSeconds);
        }

        // ---------- State machine and scenes ----------

        [Test]
        public void ArrowStep_InsideAdlRoutine_CompletesOnArrival()
        {
            TaskDefinition task = Task("NAV", "NEXT");
            SetStep(task, 0, "NAV_MK", TaskDefinition.Cue.ArrowStraight);
            SetStep(task, 1, "NEXT_MK", TaskDefinition.Cue.Avatar);
            var host = new GameObject("machine");
            _created.Add(host);
            var machine = host.AddComponent<TaskStateMachine>();
            machine.Begin(task);

            machine.HandleMarker("NAV_MK");

            Assert.AreEqual(1, machine.StepIndex);
            Assert.AreEqual(MachineState.Awaiting, machine.State);
        }

        [Test]
        public void SceneFor_ShowsCurrentStepScene_AndNothingWhenIdleOrDone()
        {
            TaskDefinition task = Task("A1", "A2");
            Assert.AreEqual("Scene_A1", StepSceneLoader.SceneFor(task, MachineState.Awaiting, 0));
            Assert.AreEqual("Scene_A2", StepSceneLoader.SceneFor(task, MachineState.Active, 1));
            Assert.IsNull(StepSceneLoader.SceneFor(task, MachineState.Idle, 0));
            Assert.IsNull(StepSceneLoader.SceneFor(task, MachineState.Complete, 2));
            Assert.IsNull(StepSceneLoader.SceneFor(null, MachineState.Awaiting, 0));
        }

        // ---------- The shipped Morning Routine ----------

        [Test]
        public void MorningRoutine_StepsAreWellFormed()
        {
            var routine = AssetDatabase.LoadAssetAtPath<RoutineDefinition>(MorningRoutinePath);
            Assert.IsNotNull(routine);
            Assert.AreEqual("rt_morning", routine.RoutineId);
            Assert.IsTrue(routine.PrivateSpace, "Washroom tasks must keep emergency calls audio-only.");

            TaskDefinition all = routine.Build(StudentProfile.Default);
            var scenes = new HashSet<string>();
            var checkpoints = new HashSet<string>();
            for (int i = 0; i < all.Count; i++)
            {
                TaskDefinition.Step step = all[i];
                Assert.IsTrue(scenes.Add(step.sceneName), $"Scene {step.sceneName} is used twice.");
                Assert.IsTrue(checkpoints.Add(step.checkpointId), $"Checkpoint {step.checkpointId} is used twice.");
                Assert.LessOrEqual(step.prompt.Split(' ').Length, 6, $"{step.checkpointId} prompt is over 6 words.");

                // Two steps on the same card: the first must end on its own, or the learner would be stuck.
                if (i + 1 < all.Count && all[i + 1].markerId == step.markerId)
                    Assert.Greater(step.holdSeconds, 0f, $"{step.checkpointId} shares its marker with the next step but has no hold.");
            }
        }

        [Test]
        public void MorningRoutine_EveryStepSceneIsInBuildSettings()
        {
            var routine = AssetDatabase.LoadAssetAtPath<RoutineDefinition>(MorningRoutinePath);
            var listed = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)));
            TaskDefinition all = routine.Build(StudentProfile.Default);
            for (int i = 0; i < all.Count; i++)
                Assert.IsTrue(listed.Contains(all[i].sceneName),
                    $"{all[i].sceneName} is not in Build Settings. Run SMART-LIFE > Build Morning Routine Scenes.");
        }

        [Test]
        public void MorningRoutine_MatchesTheSeededRoute()
        {
            string seed = File.ReadAllText(Path.Combine(Application.dataPath, "../../firebase/seed.json"));
            var routine = AssetDatabase.LoadAssetAtPath<RoutineDefinition>(MorningRoutinePath);
            StringAssert.Contains("\"rt_morning\"", seed);
            for (int t = 0; t < routine.TaskCount; t++)
            {
                StringAssert.Contains($"\"{routine[t].taskId}\"", seed);
                TaskDefinition task = routine[t].task;
                for (int s = 0; s < task.Count; s++) StringAssert.Contains($"\"{task[s].checkpointId}\"", seed);
            }
        }

        // ---------- helpers ----------

        private TaskDefinition Task(params string[] checkpointIds)
        {
            var task = ScriptableObject.CreateInstance<TaskDefinition>();
            _created.Add(task);
            var so = new SerializedObject(task);
            SerializedProperty steps = so.FindProperty("steps");
            steps.arraySize = checkpointIds.Length;
            for (int i = 0; i < checkpointIds.Length; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("checkpointId").stringValue = checkpointIds[i];
                step.FindPropertyRelative("markerId").stringValue = "MK_" + checkpointIds[i];
                step.FindPropertyRelative("sceneName").stringValue = "Scene_" + checkpointIds[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return task;
        }

        private static void SetStep(TaskDefinition task, int index, string markerId, TaskDefinition.Cue cue)
        {
            var so = new SerializedObject(task);
            SerializedProperty step = so.FindProperty("steps").GetArrayElementAtIndex(index);
            step.FindPropertyRelative("markerId").stringValue = markerId;
            step.FindPropertyRelative("cue").enumValueIndex = (int)cue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private RoutineDefinition Routine(params (string id, TaskDefinition task)[] tasks)
        {
            var routine = ScriptableObject.CreateInstance<RoutineDefinition>();
            _created.Add(routine);
            var so = new SerializedObject(routine);
            so.FindProperty("routineId").stringValue = "rt_test";
            SerializedProperty list = so.FindProperty("tasks");
            list.arraySize = tasks.Length;
            for (int i = 0; i < tasks.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("taskId").stringValue = tasks[i].id;
                entry.FindPropertyRelative("task").objectReferenceValue = tasks[i].task;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return routine;
        }

        private static StudentProfile Profile(params TaskOverride[] tasks) =>
            new StudentProfile(tasks, true, true, StudentProfile.DefaultHesitationSeconds, new HashSet<string>(StudentProfile.AacOptionIds));

        private static string[] Checkpoints(TaskDefinition task)
        {
            var ids = new string[task.Count];
            for (int i = 0; i < task.Count; i++) ids[i] = task[i].checkpointId;
            return ids;
        }
    }
}
