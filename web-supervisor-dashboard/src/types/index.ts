// Shared RTDB models. Source of truth: docs/DATABASE.md
export type TaskStatus = 'started' | 'completed' | 'skipped' | 'reset' | 'paused' | 'resumed';
export type MachineState = 'idle' | 'awaiting' | 'active' | 'paused' | 'complete';
export type OverrideCommand = 'skip' | 'reset' | 'pause' | 'resume';
export type AacRequest = 'too_loud' | 'need_help' | 'show_me' | 'break';

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
  stepHesitationMs: number;
  aacRequest?: AacRequest;
  wrongScanCount: number;
}

export interface Session {
  id: string;
  routeId: string;
  startedAt: number;
  completedAt?: number;
  status: 'active' | 'completed' | 'aborted';
  checkpointsReached: number;
  autonomousCount: number;
  activeDurationMs: number;
  hesitationTotalMs: number;
  wrongScanCount: number;
}

/** One step attempt, appended by the tablet when the step ends. */
export interface NodeMetric {
  id: string;
  checkpointId: string;
  stepIndex: number;
  hesitationMs: number;
  holdMs: number;
  wrongScans: number;
  aacOpened: boolean;
  aacSelection?: string;
  outcome: 'completed' | 'skipped' | 'reset';
  autonomous: boolean;
  timestamp: number;
}

export interface RouteCheckpoint {
  order: number;
  markerId: string;
  label: string;
  cue: string;
  enabled: boolean;
  prompt?: string;
  holdSeconds?: number;
  aacBoardId?: string;
}

export interface Route {
  name: string;
  kind: 'adl' | 'nav';
  version: number;
  checkpoints: Record<string, RouteCheckpoint>;
}

/** A route checkpoint while it is being edited in the Routine Builder. */
export interface RoutineStep extends RouteCheckpoint {
  id: string;
}
