import { onValue, ref, set } from 'firebase/database';
import { db } from '../firebase/client';
import { routeSchema, routinePayloadSchema } from '../validation/schemas';
import type { Route } from '../../types';

/** Every published routine, keyed by route ID. Returns an unsubscribe function. */
export function watchRoutes(
  cb: (routes: Record<string, Route>) => void,
  onError: (e: Error) => void,
): () => void {
  return onValue(
    ref(db, 'routes'),
    (snap) => {
      const routes: Record<string, Route> = {};
      snap.forEach((child) => {
        const parsed = routeSchema.safeParse(child.val());
        if (parsed.success) routes[child.key] = parsed.data;
      });
      cb(routes);
    },
    onError,
  );
}

/** Validates again right before the write, then pushes the routine to /routes/{routeId}. */
export async function publishRoutine(routeId: string, payload: Route): Promise<void> {
  await set(ref(db, `routes/${routeId}`), routinePayloadSchema.parse(payload));
}
