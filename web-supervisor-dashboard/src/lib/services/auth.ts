import { onAuthStateChanged, signInWithEmailAndPassword, signOut as firebaseSignOut } from 'firebase/auth';
import { get, onValue, ref } from 'firebase/database';
import { auth, db } from '../firebase/client';

export interface SupervisorAccess {
  role: string | null;
  studentIds: string[];
}

/** Calls back with the signed-in uid, or null. Returns an unsubscribe function. */
export function watchAuth(cb: (uid: string | null) => void): () => void {
  return onAuthStateChanged(auth, (user) => cb(user?.uid ?? null));
}

export async function signIn(email: string, password: string): Promise<void> {
  await signInWithEmailAndPassword(auth, email, password);
}

export function signOut(): Promise<void> {
  return firebaseSignOut(auth);
}

/** The learners this supervisor may see. An admin sees everyone. Returns an unsubscribe function. */
export function watchAccess(
  uid: string,
  cb: (access: SupervisorAccess) => void,
  onError: (e: Error) => void,
): () => void {
  return onValue(
    ref(db, `supervisors/${uid}`),
    async (snap) => {
      const role = snap.child('role').val() as string | null;
      const assigned = Object.keys((snap.child('students').val() as Record<string, unknown> | null) ?? {});
      if (role !== 'admin') {
        cb({ role, studentIds: assigned });
        return;
      }
      try {
        const all = await get(ref(db, 'students'));
        cb({ role, studentIds: Object.keys((all.val() as Record<string, unknown> | null) ?? {}) });
      } catch (e) {
        onError(e as Error);
      }
    },
    onError,
  );
}
