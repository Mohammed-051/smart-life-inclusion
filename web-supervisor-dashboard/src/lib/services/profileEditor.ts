import type { LearnerProfile, Route, TaskOverride } from '../../types';

export interface TaskRow {
  id: string;
  label: string;
  enabled: boolean;
}

export const DEFAULT_HESITATION_S = 15;

/** A fresh profile that changes nothing: every task on, in the routine's default order. */
export function defaultProfile(routineId: string): LearnerProfile {
  return {
    routineId,
    tasks: {},
    voicePrompts: true,
    mascot: true,
    hesitationSeconds: DEFAULT_HESITATION_S,
    aacOptions: { too_loud: true, need_help: true, show_me: true, break: true },
  };
}

/**
 * The routine's tasks as this learner will get them: tasks the profile lists come first in its order,
 * the rest keep their default order after them. Matches RoutineDefinition.Build on the tablet.
 */
export function taskRows(route: Route, profile: LearnerProfile): TaskRow[] {
  const tasks = Object.entries(route.tasks ?? {}).sort(([, a], [, b]) => a.order - b.order);
  const keyed = tasks.map(([id, task], index) => {
    const override: TaskOverride | undefined = profile.tasks[id];
    return {
      row: { id, label: task.label, enabled: override?.enabled ?? true },
      key: override ? override.order : Number.MAX_SAFE_INTEGER,
      index,
    };
  });
  keyed.sort((a, b) => (a.key !== b.key ? a.key - b.key : a.index - b.index));
  return keyed.map((k) => k.row);
}

export function moveRow(rows: TaskRow[], from: number, to: number): TaskRow[] {
  if (from < 0 || to < 0 || from >= rows.length || to >= rows.length) return rows;
  const next = [...rows];
  const [moved] = next.splice(from, 1);
  if (moved) next.splice(to, 0, moved);
  return next;
}

export function toggleRow(rows: TaskRow[], id: string): TaskRow[] {
  return rows.map((r) => (r.id === id ? { ...r, enabled: !r.enabled } : r));
}

/** Writes every task's place explicitly, so the saved profile reads the same on any tablet. */
export function withTasks(profile: LearnerProfile, rows: TaskRow[]): LearnerProfile {
  const tasks: Record<string, TaskOverride> = {};
  rows.forEach((r, i) => {
    tasks[r.id] = { order: i + 1, enabled: r.enabled };
  });
  return { ...profile, tasks };
}

export function profileProblems(rows: TaskRow[]): string[] {
  return rows.some((r) => r.enabled) ? [] : ['Keep at least one task on.'];
}
