import { get, limitToLast, onValue, orderByChild, push, query, ref, serverTimestamp, set, update } from 'firebase/database';
import { db } from '../firebase/client';
import { emergencySchema } from '../validation/schemas';
import type { Emergency, OverrideCommand } from '../../types';

const REQUEST_TTL_MS = 60_000;

/** Recent emergencies, newest first. Returns an unsubscribe function. */
export function watchEmergencies(studentId: string, n: number, cb: (emergencies: Emergency[]) => void): () => void {
  const q = query(ref(db, `emergencies/${studentId}`), orderByChild('timestamp'), limitToLast(n));
  return onValue(q, (snap) => {
    const out: Emergency[] = [];
    snap.forEach((child) => {
      const p = emergencySchema.safeParse(child.val());
      if (p.success) out.push({ id: child.key, ...p.data });
    });
    cb(out.reverse());
  });
}

export function acknowledgeEmergency(studentId: string, emergencyId: string, uid: string): Promise<void> {
  return update(ref(db, `emergencies/${studentId}/${emergencyId}`), { status: 'acknowledged', acknowledgedBy: uid });
}

export function resolveEmergency(studentId: string, emergencyId: string): Promise<void> {
  return set(ref(db, `emergencies/${studentId}/${emergencyId}/status`), 'resolved');
}

/** Whether an admin recorded the guardian's streaming consent. */
export async function readStreamingConsent(studentId: string): Promise<boolean> {
  const snap = await get(ref(db, `students/${studentId}/streamingConsent`));
  return snap.val() === true;
}

/** Sends a guardian override to the tablet, such as Resume after an emergency. */
export async function sendOverride(studentId: string, command: OverrideCommand, uid: string): Promise<void> {
  await set(push(ref(db, `overrides/${studentId}`)), {
    command, source: 'dashboard', issuedBy: uid, timestamp: serverTimestamp(), consumed: false,
  });
}

export interface CallSignaling {
  id: string;
  writeCandidate: (json: string) => void;
  end: () => Promise<void>;
  watch: (handlers: {
    onAnswer: (sdp: string) => void;
    onTabletCandidate: (json: string) => void;
    onStatus: (status: string) => void;
  }) => () => void;
}

/** Creates the /interventions record. Database rules refuse it without an open SOS and recorded consent. */
export async function createIntervention(
  studentId: string,
  emergencyId: string,
  uid: string,
  offerSdp: string,
  nowMs: number,
): Promise<CallSignaling> {
  const record = push(ref(db, `interventions/${studentId}`));
  const id = record.key!;
  await set(record, {
    emergencyId,
    requestedBy: uid,
    status: 'requested',
    offer: offerSdp,
    startedAt: serverTimestamp(),
    expiresAt: nowMs + REQUEST_TTL_MS,
  });

  return {
    id,
    writeCandidate: (json) => { void set(push(ref(db, `interventions/${studentId}/${id}/candidates/supervisor`)), json); },
    end: () => update(ref(db, `interventions/${studentId}/${id}`), { status: 'ended', endedAt: serverTimestamp() }),
    watch: ({ onAnswer, onTabletCandidate, onStatus }) => {
      const seen = new Set<string>();
      const offs = [
        onValue(ref(db, `interventions/${studentId}/${id}/answer`), (snap) => {
          const sdp = snap.val() as string | null;
          if (sdp) onAnswer(sdp);
        }),
        onValue(ref(db, `interventions/${studentId}/${id}/candidates/tablet`), (snap) => {
          snap.forEach((child) => {
            if (seen.has(child.key)) return;
            seen.add(child.key);
            onTabletCandidate(child.val() as string);
          });
        }),
        onValue(ref(db, `interventions/${studentId}/${id}/status`), (snap) => onStatus((snap.val() as string | null) ?? '')),
      ];
      return () => offs.forEach((off) => off());
    },
  };
}
