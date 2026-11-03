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
});

export const sessionSchema = z.object({
  routeId: z.string(),
  startedAt: z.number(),
  completedAt: z.number().optional(),
  status: z.enum(['active', 'completed', 'aborted']),
  checkpointsReached: z.number().int().min(0).default(0),
  autonomousCount: z.number().int().min(0).default(0),
});
