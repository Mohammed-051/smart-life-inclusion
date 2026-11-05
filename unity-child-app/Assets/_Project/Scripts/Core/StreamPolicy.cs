namespace SmartLife.Core
{
    public enum StreamDecision
    {
        Allowed,
        NoOpenEmergency,
        NotSos,
        WrongEmergency,
        NoConsent,
        Expired,
        AlreadyStreaming,
    }

    /// <summary>
    /// The tablet's own check before any capture starts (docs/SECURITY.md). Database rules and the
    /// dashboard check too, but this one protects the child if those two fail. Pure, so it is unit-tested.
    /// </summary>
    public static class StreamPolicy
    {
        /// <summary>Decides whether a supervisor's stream request may be answered. Detection alone never reaches this.</summary>
        public static StreamDecision Evaluate(
            string openEmergencyId,
            EmergencySource? openSource,
            string requestedEmergencyId,
            bool consentRecorded,
            long nowMs,
            long expiresAtMs,
            bool alreadyStreaming)
        {
            if (alreadyStreaming) return StreamDecision.AlreadyStreaming;
            if (string.IsNullOrEmpty(openEmergencyId)) return StreamDecision.NoOpenEmergency;
            if (openSource != EmergencySource.Sos) return StreamDecision.NotSos;
            if (requestedEmergencyId != openEmergencyId) return StreamDecision.WrongEmergency;
            if (!consentRecorded) return StreamDecision.NoConsent;
            if (nowMs >= expiresAtMs) return StreamDecision.Expired;
            return StreamDecision.Allowed;
        }

        /// <summary>A route marked private, such as a washroom, sends audio only. The default is private.</summary>
        public static bool AllowVideo(bool routeIsPrivate) => !routeIsPrivate;
    }
}
