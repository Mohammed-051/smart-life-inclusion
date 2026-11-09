import { z } from 'zod';

export const taskStatus = z.enum(['started', 'completed', 'skipped', 'reset', 'paused', 'resumed']);

export const checkpointLogSchema = z.object({
  studentId: z.string(),
  sessionId: z.string(),
  routeId: z.string(),
  checkpointId: z.string().max(64),
  markerId: z.string().max(64),
  taskStatus,
  autonomous: z.boolean(),
  deviceId: z.string(),
  timestamp: z.number(),
});

export const liveStatusSchema = z.object({
  sessionId: z.string(),
  routeId: z.string(),
  checkpointId: z.string(),
  progressPct: z.number().min(0).max(100),
  state: z.enum(['idle', 'awaiting', 'active', 'paused', 'complete']),
  lastSeen: z.number(),
  stepHesitationMs: z.number().min(0).default(0),
  aacRequest: z.enum(['too_loud', 'need_help', 'show_me', 'break']).optional(),
  wrongScanCount: z.number().int().min(0).default(0),
});

export const sessionSchema = z.object({
  routeId: z.string(),
  startedAt: z.number(),
  completedAt: z.number().optional(),
  status: z.enum(['active', 'completed', 'aborted']),
  checkpointsReached: z.number().int().min(0).default(0),
  autonomousCount: z.number().int().min(0).default(0),
  activeDurationMs: z.number().min(0).default(0),
  hesitationTotalMs: z.number().min(0).default(0),
  wrongScanCount: z.number().int().min(0).default(0),
});

export const nodeMetricSchema = z.object({
  checkpointId: z.string().max(64),
  stepIndex: z.number().int().min(0),
  hesitationMs: z.number().min(0),
  holdMs: z.number().min(0),
  wrongScans: z.number().int().min(0),
  aacOpened: z.boolean(),
  aacSelection: z.string().max(32).optional(),
  outcome: z.enum(['completed', 'skipped', 'reset']),
  autonomous: z.boolean(),
  timestamp: z.number(),
});

export const emergencySchema = z.object({
  source: z.enum(['audio', 'imu', 'sos']),
  status: z.enum(['open', 'acknowledged', 'resolved']),
  acknowledgedBy: z.string().optional(),
  sessionId: z.string().optional(),
  timestamp: z.number(),
});

export const routeCheckpointSchema = z.object({
  order: z.number().int().min(1),
  markerId: z.string().min(1).max(64),
  label: z.string().min(1).max(64),
  cue: z.string().min(1).max(32),
  enabled: z.boolean().default(true),
  prompt: z.string().max(64).optional(),
  holdSeconds: z.number().min(0).optional(),
  aacBoardId: z.string().max(64).optional(),
  taskId: z.string().max(64).optional(),
});

export const routeTaskSchema = z.object({
  order: z.number().int().min(1),
  label: z.string().min(1).max(64),
});

/** Stored shape of /routes/{routeId}. Publishing adds the stricter checks in routinePayloadSchema. */
export const routeSchema = z.object({
  name: z.string().min(1).max(64),
  kind: z.enum(['adl', 'nav']),
  version: z.number().int().min(0).default(0),
  checkpoints: z.record(z.string().max(64), routeCheckpointSchema),
  tasks: z.record(z.string().max(64), routeTaskSchema).optional(),
});

export const HESITATION_MIN_S = 5;
export const HESITATION_MAX_S = 120;

/** Stored shape of /profiles/{studentId}. Missing fields fall back to the tablet defaults. */
export const learnerProfileSchema = z.object({
  routineId: z.string().min(1).max(64),
  tasks: z.record(z.string().max(64), z.object({ order: z.number().int().min(1).max(50), enabled: z.boolean() })).default({}),
  voicePrompts: z.boolean().default(true),
  mascot: z.boolean().default(true),
  hesitationSeconds: z.number().min(HESITATION_MIN_S).max(HESITATION_MAX_S).default(15),
  aacOptions: z
    .object({ too_loud: z.boolean(), need_help: z.literal(true), show_me: z.boolean(), break: z.boolean() })
    .default({ too_loud: true, need_help: true, show_me: true, break: true }),
  updatedAt: z.number().optional(),
  updatedBy: z.string().optional(),
});

export const routinePayloadSchema = routeSchema.superRefine((route, ctx) => {
  const steps = Object.values(route.checkpoints);
  if (!steps.some((s) => s.enabled)) {
    ctx.addIssue({ code: 'custom', message: 'Turn on at least one step.' });
  }

  // The state machine needs one marker per transition, so two steps cannot share a marker.
  const enabledMarkers = steps.filter((s) => s.enabled).map((s) => s.markerId);
  if (new Set(enabledMarkers).size !== enabledMarkers.length) {
    ctx.addIssue({ code: 'custom', message: 'Two active steps use the same marker.' });
  }

  const orders = steps.map((s) => s.order).sort((a, b) => a - b);
  if (orders.some((order, i) => order !== i + 1)) {
    ctx.addIssue({ code: 'custom', message: 'Step order must run from 1 with no gaps.' });
  }
});
