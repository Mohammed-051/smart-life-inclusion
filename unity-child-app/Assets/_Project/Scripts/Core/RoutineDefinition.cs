using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// The shared default routine: an ordered list of tasks (wake up, toilet, handwash...). Each task's
    /// steps carry their own scene, so the routine runs one scene per step. A learner's profile can
    /// reorder or skip whole tasks; steps inside a task stay as authored.
    /// </summary>
    [CreateAssetMenu(menuName = "SmartLife/Routine Definition", fileName = "RD_NewRoutine")]
    public class RoutineDefinition : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Stable id used in /profiles, e.g. task_wake")] public string taskId;
            public TaskDefinition task;
        }

        public const float FinalStepHoldSeconds = 10f;

        [Tooltip("RTDB route id for the whole routine, e.g. rt_morning")]
        [SerializeField] private string routineId = "rt_morning";
        [SerializeField] private Entry[] tasks = Array.Empty<Entry>();

        public string RoutineId => routineId;
        public int TaskCount => tasks.Length;
        public Entry this[int i] => tasks[i];

        /// <summary>
        /// True if any task is private, such as near a washroom. Emergency calls then send audio only,
        /// so one private step makes the whole routine audio-only by default.
        /// </summary>
        public bool PrivateSpace
        {
            get
            {
                foreach (Entry entry in tasks)
                    if (entry.task == null || entry.task.PrivateSpace) return true;
                return false;
            }
        }

        /// <summary>
        /// Flattens the routine into one task for the state machine, after the learner's task order
        /// and skips. Tasks the profile does not mention keep their default place after the listed ones.
        /// Falls back to the full default order if the profile would leave nothing to do.
        /// </summary>
        public TaskDefinition Build(StudentProfile profile)
        {
            List<TaskDefinition> ordered = OrderedTasks(profile);
            if (ordered.Count == 0) ordered = OrderedTasks(StudentProfile.Default);

            var steps = new List<TaskDefinition.Step>();
            foreach (TaskDefinition task in ordered)
                for (int i = 0; i < task.Count; i++)
                    steps.Add(task[i]);

            // A step with no hold only ends when the next marker is scanned. If a reorder leaves such a
            // step last, nothing could end it, so give it a short hold instead.
            int last = steps.Count - 1;
            if (last >= 0 && steps[last].holdSeconds <= 0f && !TaskDefinition.IsArrival(steps[last].cue))
            {
                TaskDefinition.Step final = steps[last];
                final.holdSeconds = FinalStepHoldSeconds;
                steps[last] = final;
            }

            return TaskDefinition.Compose(name, routineId, TaskDefinition.Kind.Adl, PrivateSpace, steps.ToArray());
        }

        private List<TaskDefinition> OrderedTasks(StudentProfile profile)
        {
            var overrides = new Dictionary<string, TaskOverride>();
            foreach (TaskOverride o in profile.Tasks) overrides[o.TaskId] = o;

            var keyed = new List<(int order, int index, TaskDefinition task)>();
            for (int i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].task == null) continue;
                if (overrides.TryGetValue(tasks[i].taskId, out TaskOverride o))
                {
                    if (o.Enabled) keyed.Add((o.Order, i, tasks[i].task));
                }
                else keyed.Add((int.MaxValue, i, tasks[i].task));
            }

            keyed.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.index.CompareTo(b.index));
            var result = new List<TaskDefinition>(keyed.Count);
            foreach (var entry in keyed) result.Add(entry.task);
            return result;
        }
    }
}
