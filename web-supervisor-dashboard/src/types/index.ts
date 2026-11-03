// Shared RTDB models. Source of truth: docs/DATABASE.md
export type TaskStatus = 'started' | 'completed' | 'skipped' | 'reset' | 'paused' | 'resumed';
export type MachineState = 'idle' | 'awaiting' | 'active' | 'paused' | 'complete';
export type OverrideCommand = 'skip' | 'reset' | 'pause' | 'resume';

export interface CheckpointLog {
  id: string;
  studentId: string;
  sessionId: string;
  routeId: string;
  checkpointId: string;
  markerId: string;
  taskStatus: TaskStatus;
  autonomous: boolean;
  deviceId: string;
  timestamp: number;
}

export interface LiveStatus {
  sessionId: string;
  routeId: string;
  checkpointId: string;
  progressPct: number;
  state: MachineState;
  lastSeen: number;
}

export interface Session {
  id: string;
  routeId: string;
  startedAt: number;
  completedAt?: number;
  status: 'active' | 'completed' | 'aborted';
  checkpointsReached: number;
  autonomousCount: number;
}
