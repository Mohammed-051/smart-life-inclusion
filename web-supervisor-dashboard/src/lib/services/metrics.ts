import type { Session } from '../../types';

/** Autonomous navigation % = Σ autonomousCount / Σ checkpointsReached (docs/PRD.md › Metrics). */
export function autonomousPct(sessions: Session[]): number {
  const reached = sessions.reduce((a, s) => a + s.checkpointsReached, 0);
  if (reached === 0) return 0;
  return (100 * sessions.reduce((a, s) => a + s.autonomousCount, 0)) / reached;
}

/** Mean completion time (ms) across completed sessions, or null if none. */
export function avgCompletionMs(sessions: Session[]): number | null {
  const done = sessions.filter((s) => s.status === 'completed' && s.completedAt !== undefined);
  if (done.length === 0) return null;
  return done.reduce((a, s) => a + (s.completedAt! - s.startedAt), 0) / done.length;
}
