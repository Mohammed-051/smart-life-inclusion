namespace SmartLife.Telemetry
{
    /// <summary>RTDB paths. Keep in sync with docs/DATABASE.md.</summary>
    public static class DbPaths
    {
        public static string CheckpointLogs(string studentId) => $"checkpointLogs/{studentId}";
        public static string LiveStatus(string studentId) => $"liveStatus/{studentId}";
        public static string Sessions(string studentId) => $"sessions/{studentId}";
        public static string Overrides(string studentId) => $"overrides/{studentId}";
        public static string Device(string uid) => $"devices/{uid}";
    }
}
