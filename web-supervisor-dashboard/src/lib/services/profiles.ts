import { onValue, ref, serverTimestamp, set } from 'firebase/database';
import { auth, db } from '../firebase/client';
import { learnerProfileSchema } from '../validation/schemas';
import type { LearnerProfile } from '../../types';

/**
 * The learner's profile, or null if none is saved yet (the tablet then uses its defaults).
 * Returns an unsubscribe function.
 */
export function watchProfile(
  studentId: string,
  cb: (profile: LearnerProfile | null) => void,
  onError: (e: Error) => void,
): () => void {
  return onValue(
    ref(db, `profiles/${studentId}`),
    (snap) => {
      if (!snap.exists()) {
        cb(null);
        return;
      }
      const parsed = learnerProfileSchema.safeParse(snap.val());
      cb(parsed.success ? parsed.data : null);
    },
    onError,
  );
}

/** Validates, then saves. The tablet applies it when the next routine starts. */
export async function saveProfile(studentId: string, profile: LearnerProfile): Promise<void> {
  const uid = auth.currentUser?.uid;
  if (!uid) throw new Error('Not signed in.');
  const { updatedAt: _ignoredAt, updatedBy: _ignoredBy, ...body } = learnerProfileSchema.parse(profile);
  await set(ref(db, `profiles/${studentId}`), { ...body, updatedAt: serverTimestamp(), updatedBy: uid });
}
