namespace SmartLife.Core
{
    /// <summary>Hands-free guardian overrides (voice, guardian menu or dashboard).</summary>
    public enum OverrideCommand { Skip, Reset, Pause, Resume }

    /// <summary>Checkpoint status values written to RTDB (must match docs/DATABASE.md).</summary>
    public enum TaskStatus { Started, Completed, Skipped, Reset, Paused, Resumed }

    /// <summary>High-level machine states mirrored to /liveStatus.state.</summary>
    public enum MachineState { Idle, Awaiting, Active, Paused, Complete }
}
