using System;
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
        }

        [SerializeField] private string routeId = "rt_handwash";
        [SerializeField] private Kind kind = Kind.Adl;
        [SerializeField] private Step[] steps = Array.Empty<Step>();

        public string RouteId => routeId;
        public Kind TaskKind => kind;
        public int Count => steps.Length;
        public Step this[int i] => steps[i];
    }
}
