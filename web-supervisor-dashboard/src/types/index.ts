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

export type EmergencySource = 'audio' | 'imu' | 'sos';

export interface Emergency {
  id: string;
  source: EmergencySource;
  status: 'open' | 'acknowledged' | 'resolved';
  acknowledgedBy?: string;
  sessionId?: string;
  timestamp: number;
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
  /** Set on routine checkpoints: the task this step belongs to. */
  taskId?: string;
}

/** A task inside a multi-task routine such as rt_morning. */
export interface RouteTask {
  order: number;
  label: string;
}

export interface Route {
  name: string;
  kind: 'adl' | 'nav';
  version: number;
  checkpoints: Record<string, RouteCheckpoint>;
  /** Present on routines (rt_morning). Their tasks are adjusted per learner in /profiles. */
  tasks?: Record<string, RouteTask>;
}

export type AacOptionId = 'too_loud' | 'need_help' | 'show_me' | 'break';

export interface TaskOverride {
  order: number;
  enabled: boolean;
}

/** /profiles/{studentId}: one learner's adjustments to the shared routine, applied at the next start. */
export interface LearnerProfile {
  routineId: string;
  tasks: Record<string, TaskOverride>;
  voicePrompts: boolean;
  mascot: boolean;
  hesitationSeconds: number;
  aacOptions: Record<AacOptionId, boolean>;
  updatedAt?: number;
  updatedBy?: string;
}

/** A route checkpoint while it is being edited in the Routine Builder. */
export interface RoutineStep extends RouteCheckpoint {
  id: string;
}
