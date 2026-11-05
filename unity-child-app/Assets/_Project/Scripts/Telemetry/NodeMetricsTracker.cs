namespace SmartLife.Telemetry
{
    /// <summary>
    /// Adds up per-step and per-session measures for /nodeMetrics and /sessions. No Firebase or Unity
    /// calls, so it is unit-tested. Spans come from the frame clock, so they exclude frozen time.
    /// </summary>
    public sealed class NodeMetricsTracker
    {
        public float StepHesitationSeconds { get; private set; }
        public float StepHoldSeconds { get; private set; }
        public int StepWrongScans { get; private set; }
        public bool StepAacOpened { get; private set; }
        public string StepAacSelection { get; private set; }

        public float SessionActiveSeconds { get; private set; }
        public float SessionHesitationSeconds { get; private set; }
        public int SessionWrongScans { get; private set; }

        /// <summary>Call each frame while a session runs and the task is not paused.</summary>
        public void Tick(float deltaSeconds, bool waitingOnChild, bool holding)
        {
            SessionActiveSeconds += deltaSeconds;
            if (holding)
            {
                StepHoldSeconds += deltaSeconds;
            }
            else if (waitingOnChild)
            {
                StepHesitationSeconds += deltaSeconds;
                SessionHesitationSeconds += deltaSeconds;
            }
        }

        public void RecordWrongScan()
        {
            StepWrongScans++;
            SessionWrongScans++;
        }

        public void RecordAacOpened() => StepAacOpened = true;

        /// <summary>The last choice on the board is the one kept for this step.</summary>
        public void RecordAacSelection(string selection)
        {
            StepAacOpened = true;
            StepAacSelection = selection;
        }

        /// <summary>Starts counting for the next step. Session totals carry on.</summary>
        public void NextStep()
        {
            StepHesitationSeconds = 0f;
            StepHoldSeconds = 0f;
            StepWrongScans = 0;
            StepAacOpened = false;
            StepAacSelection = null;
        }

        public void NewSession()
        {
            NextStep();
            SessionActiveSeconds = 0f;
            SessionHesitationSeconds = 0f;
            SessionWrongScans = 0;
        }
    }
}
