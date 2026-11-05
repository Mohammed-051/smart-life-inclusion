import { limitToLast, onValue, orderByChild, query, ref } from 'firebase/database';
import { db } from '../firebase/client';
import { checkpointLogSchema, liveStatusSchema, nodeMetricSchema, sessionSchema } from '../validation/schemas';
import type { CheckpointLog, LiveStatus, NodeMetric, Session } from '../../types';

/** Live location/progress of one student. Returns an unsubscribe function. */
export function watchLiveStatus(studentId: string, cb: (s: LiveStatus | null) => void): () => void {
  return onValue(ref(db, `liveStatus/${studentId}`), (snap) => {
    const parsed = liveStatusSchema.safeParse(snap.val());
    cb(parsed.success ? parsed.data : null);
  });
}

/** Most recent `n` checkpoint logs, newest first. Returns an unsubscribe function. */
export function watchRecentLogs(studentId: string, n: number, cb: (logs: CheckpointLog[]) => void): () => void {
  const q = query(ref(db, `checkpointLogs/${studentId}`), orderByChild('timestamp'), limitToLast(n));
  return onValue(q, (snap) => {
    const out: CheckpointLog[] = [];
    snap.forEach((child) => {
      const p = checkpointLogSchema.safeParse(child.val());
      if (p.success) out.push({ id: child.key, ...p.data });
    });
    cb(out.reverse());
  });
}

/** The most recent `n` sessions, oldest first. Returns an unsubscribe function. */
export function watchSessions(studentId: string, n: number, cb: (sessions: Session[]) => void): () => void {
  const q = query(ref(db, `sessions/${studentId}`), orderByChild('startedAt'), limitToLast(n));
  return onValue(q, (snap) => {
    const out: Session[] = [];
    snap.forEach((child) => {
      const p = sessionSchema.safeParse(child.val());
      if (p.success) out.push({ id: child.key, ...p.data });
    });
    cb(out);
  });
}

/** Per-step metrics for one session, oldest first. Returns an unsubscribe function. */
export function watchNodeMetrics(
  studentId: string,
  sessionId: string,
  n: number,
  cb: (metrics: NodeMetric[]) => void,
): () => void {
  const q = query(ref(db, `nodeMetrics/${studentId}/${sessionId}`), orderByChild('timestamp'), limitToLast(n));
  return onValue(q, (snap) => {
    const out: NodeMetric[] = [];
    snap.forEach((child) => {
      const p = nodeMetricSchema.safeParse(child.val());
      if (p.success) out.push({ id: child.key, ...p.data });
    });
    cb(out);
  });
}

/** Difference between server and browser clocks, so live durations match server timestamps. */
export function watchServerOffset(cb: (offsetMs: number) => void): () => void {
  return onValue(ref(db, '.info/serverTimeOffset'), (snap) => cb((snap.val() as number | null) ?? 0));
}
