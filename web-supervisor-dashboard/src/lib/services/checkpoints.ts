import { get, limitToLast, onValue, orderByChild, query, ref } from 'firebase/database';
import { db } from '../firebase/client';
import { checkpointLogSchema, liveStatusSchema, sessionSchema } from '../validation/schemas';
import type { CheckpointLog, LiveStatus, Session } from '../../types';

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
      if (p.success) out.push({ id: child.key!, ...p.data });
    });
    cb(out.reverse());
  });
}

/** One-shot fetch of all sessions for metric calculation. */
export async function fetchSessions(studentId: string): Promise<Session[]> {
  const snap = await get(ref(db, `sessions/${studentId}`));
  const out: Session[] = [];
  snap.forEach((child) => {
    const p = sessionSchema.safeParse(child.val());
    if (p.success) out.push({ id: child.key!, ...p.data });
  });
  return out;
}
