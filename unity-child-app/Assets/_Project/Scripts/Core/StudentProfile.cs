using System;
using System.Collections.Generic;

namespace SmartLife.Core
{
    /// <summary>One task's place in a learner's routine: its order, and whether it runs at all.</summary>
    public readonly struct TaskOverride
    {
        public readonly string TaskId;
        public readonly int Order;
        public readonly bool Enabled;

        public TaskOverride(string taskId, int order, bool enabled)
        {
            TaskId = taskId;
            Order = order;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// A learner's adjustments to the shared default routine, read from /profiles/{studentId}.
    /// Applied when a routine starts, never mid-session. Anything missing or invalid falls back to
    /// the default, so a bad profile can never stop the routine.
    /// </summary>
    public sealed class StudentProfile
    {
        public const float DefaultHesitationSeconds = 15f;
        public const float MinHesitationSeconds = 5f;
        public const float MaxHesitationSeconds = 120f;

        /// <summary>AAC options the board can show. "need_help" can never be switched off.</summary>
        public static readonly string[] AacOptionIds = { "too_loud", "need_help", "show_me", "break" };
        public const string AlwaysOnAacOption = "need_help";

        public static readonly StudentProfile Default = new StudentProfile(
            Array.Empty<TaskOverride>(), true, true, DefaultHesitationSeconds, new HashSet<string>(AacOptionIds));

        public IReadOnlyList<TaskOverride> Tasks { get; }
        public bool VoicePrompts { get; }
        public bool Mascot { get; }
        public float HesitationSeconds { get; }
        public IReadOnlyCollection<string> AacOptions { get; }

        public StudentProfile(IReadOnlyList<TaskOverride> tasks, bool voicePrompts, bool mascot,
                              float hesitationSeconds, ISet<string> aacOptions)
        {
            Tasks = tasks ?? Array.Empty<TaskOverride>();
            VoicePrompts = voicePrompts;
            Mascot = mascot;
            HesitationSeconds = hesitationSeconds >= MinHesitationSeconds && hesitationSeconds <= MaxHesitationSeconds
                ? hesitationSeconds
                : DefaultHesitationSeconds;

            var options = new HashSet<string>();
            if (aacOptions != null)
                foreach (string id in AacOptionIds)
                    if (aacOptions.Contains(id)) options.Add(id);
            options.Add(AlwaysOnAacOption);
            AacOptions = options;
        }

        /// <summary>
        /// Reads the RTDB value of /profiles/{studentId} (nested dictionaries, as the Firebase SDK
        /// returns them). Null or a non-object gives <see cref="Default"/>.
        /// </summary>
        public static StudentProfile FromDatabase(object value)
        {
            if (!(value is IDictionary<string, object> root)) return Default;

            var tasks = new List<TaskOverride>();
            if (root.TryGetValue("tasks", out object tasksValue) && tasksValue is IDictionary<string, object> taskMap)
                foreach (KeyValuePair<string, object> pair in taskMap)
                {
                    if (!(pair.Value is IDictionary<string, object> task)) continue;
                    if (!task.TryGetValue("order", out object order) || !TryNumber(order, out double orderNumber)) continue;
                    bool enabled = !task.TryGetValue("enabled", out object on) || !(on is bool flag) || flag;
                    tasks.Add(new TaskOverride(pair.Key, (int)orderNumber, enabled));
                }

            var aac = new HashSet<string>(AacOptionIds);
            if (root.TryGetValue("aacOptions", out object aacValue) && aacValue is IDictionary<string, object> aacMap)
                foreach (KeyValuePair<string, object> pair in aacMap)
                    if (pair.Value is bool shown && !shown) aac.Remove(pair.Key);

            return new StudentProfile(
                tasks,
                Flag(root, "voicePrompts"),
                Flag(root, "mascot"),
                root.TryGetValue("hesitationSeconds", out object seconds) && TryNumber(seconds, out double s) ? (float)s : DefaultHesitationSeconds,
                aac);
        }

        private static bool Flag(IDictionary<string, object> root, string key) =>
            !root.TryGetValue(key, out object value) || !(value is bool flag) || flag;

        private static bool TryNumber(object value, out double number)
        {
            switch (value)
            {
                case long l: number = l; return true;
                case int i: number = i; return true;
                case double d: number = d; return true;
                case float f: number = f; return true;
                default: number = 0; return false;
            }
        }
    }
}
