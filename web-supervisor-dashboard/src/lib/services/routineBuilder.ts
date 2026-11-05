import { routinePayloadSchema } from '../validation/schemas';
import type { Route, RoutineStep } from '../../types';

/** Route checkpoints as an ordered list for editing. */
export function toSteps(route: Route): RoutineStep[] {
  return Object.entries(route.checkpoints)
    .map(([id, checkpoint]) => ({ ...checkpoint, id }))
    .sort((a, b) => a.order - b.order);
}

/** Moves one step. An out-of-range target returns the same list. */
export function moveStep(steps: RoutineStep[], from: number, to: number): RoutineStep[] {
  const moved = steps[from];
  if (moved === undefined || to < 0 || to >= steps.length || from === to) return steps;
  const next = steps.filter((_, i) => i !== from);
  next.splice(to, 0, moved);
  return next;
}

export function toggleStep(steps: RoutineStep[], id: string): RoutineStep[] {
  return steps.map((s) => (s.id === id ? { ...s, enabled: !s.enabled } : s));
}

/** Builds the /routes/{routeId} value, in the same shape as firebase/seed.json. */
export function buildRoutePayload(base: Route, steps: RoutineStep[]): Route {
  const checkpoints: Route['checkpoints'] = {};
  steps.forEach(({ id, ...checkpoint }, index) => {
    checkpoints[id] = { ...checkpoint, order: index + 1 };
  });
  return { name: base.name, kind: base.kind, version: base.version + 1, checkpoints };
}

/** Reasons this payload cannot be published. Empty when it is valid. */
export function validateRoutine(payload: Route): string[] {
  const result = routinePayloadSchema.safeParse(payload);
  return result.success ? [] : result.error.issues.map((issue) => issue.message);
}
