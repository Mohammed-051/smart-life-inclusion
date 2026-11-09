using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// Ordered list of steps for one ADL task or navigation route.
    /// Each step is bound to exactly one Vuforia Image Target (deterministic mapping).
    /// </summary>
    [CreateAssetMenu(menuName = "SmartLife/Task Definition", fileName = "TD_NewTask")]
    public class TaskDefinition : ScriptableObject
    {
        public enum Kind { Adl, Nav }
        public enum Cue { Avatar, ArrowLeft, ArrowRight, ArrowStraight, Arrived }

        [Serializable]
        public struct Step
        {
            [Tooltip("RTDB checkpoint id, e.g. cp_wet")] public string checkpointId;
            [Tooltip("Vuforia Image Target name, e.g. MK_SINK")] public string markerId;
            [Tooltip("Child prompt, max 6 words, positive phrasing")] public string prompt;
            public Cue cue;
            [Tooltip("Avatar animation clip for this step (Avatar cue only)")] public AnimationClip demoClip;
            [Tooltip("Seconds before the step auto-completes; 0 = wait for next marker")] public float holdSeconds;
            [Tooltip("Scene loaded additively while this step runs (one scene per step); empty = none")] public string sceneName;
        }

        [SerializeField] private string routeId = "rt_handwash";
        [SerializeField] private Kind kind = Kind.Adl;
        [SerializeField] private Step[] steps = Array.Empty<Step>();
        [Tooltip("On a private route, such as near a washroom, an emergency call sends audio only. Clear this only after a supervising adult confirms the space is not private.")]
        [SerializeField] private bool privateSpace = true;

        public string RouteId => routeId;
        public Kind TaskKind => kind;
        public bool PrivateSpace => privateSpace;
        public int Count => steps.Length;
        public Step this[int i] => steps[i];

        /// <summary>
        /// A copy of this task arranged by a routine from the Routine Builder: only listed, enabled
        /// steps, by order. The published routine changes order and on/off only, so prompts, markers
        /// and demo clips stay authored here. Returns this task when nothing would be left to do.
        /// </summary>
        public TaskDefinition Arranged(IReadOnlyList<RoutineEntry> routine)
        {
            if (routine == null) return this;

            var enabled = new List<RoutineEntry>();
            foreach (RoutineEntry entry in routine)
                if (entry.Enabled) enabled.Add(entry);
            enabled.Sort((a, b) => a.Order.CompareTo(b.Order));

            var arranged = new List<Step>();
            foreach (RoutineEntry entry in enabled)
                foreach (Step step in steps)
                    if (step.checkpointId == entry.CheckpointId) arranged.Add(step);
            if (arranged.Count == 0) return this;

            var copy = CreateInstance<TaskDefinition>();
            copy.name = name;
            copy.routeId = routeId;
            copy.kind = kind;
            copy.privateSpace = privateSpace;
            copy.steps = arranged.ToArray();
            return copy;
        }

        /// <summary>A runtime task built from other tasks' steps, such as a whole morning routine.</summary>
        public static TaskDefinition Compose(string name, string routeId, Kind kind, bool privateSpace, Step[] steps)
        {
            var composed = CreateInstance<TaskDefinition>();
            composed.name = name;
            composed.routeId = routeId;
            composed.kind = kind;
            composed.privateSpace = privateSpace;
            composed.steps = steps;
            return composed;
        }

        /// <summary>Arrow and Arrived steps are navigation checkpoints: reaching the marker completes them.</summary>
        public static bool IsArrival(Cue cue) => cue != Cue.Avatar;
    }

    /// <summary>One checkpoint's place in a routine published to /routes.</summary>
    public readonly struct RoutineEntry
    {
        public readonly string CheckpointId;
        public readonly int Order;
        public readonly bool Enabled;

        public RoutineEntry(string checkpointId, int order, bool enabled)
        {
            CheckpointId = checkpointId;
            Order = order;
            Enabled = enabled;
        }
    }
}
