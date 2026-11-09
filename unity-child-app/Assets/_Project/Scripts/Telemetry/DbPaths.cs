namespace SmartLife.Telemetry
{
    /// <summary>RTDB paths. Keep in sync with docs/DATABASE.md.</summary>
    public static class DbPaths
    {
        public static string CheckpointLogs(string studentId) => $"checkpointLogs/{studentId}";
        public static string LiveStatus(string studentId) => $"liveStatus/{studentId}";
        public static string Sessions(string studentId) => $"sessions/{studentId}";
        public static string Overrides(string studentId) => $"overrides/{studentId}";
        public static string NodeMetrics(string studentId, string sessionId) => $"nodeMetrics/{studentId}/{sessionId}";
        public static string RouteCheckpoints(string routeId) => $"routes/{routeId}/checkpoints";
        public static string Emergencies(string studentId) => $"emergencies/{studentId}";
        public static string Interventions(string studentId) => $"interventions/{studentId}";
        public static string Profile(string studentId) => $"profiles/{studentId}";
        public static string StreamingConsent(string studentId) => $"students/{studentId}/streamingConsent";
        public const string ServerTimeOffset = ".info/serverTimeOffset";
        public static string Device(string uid) => $"devices/{uid}";
        public static string DeviceStudent(string uid) => $"devices/{uid}/studentId";
    }
}
