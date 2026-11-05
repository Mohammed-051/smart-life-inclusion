import type { NodeMetric, Session } from '../../types';

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

/** Wall-clock length of a session. An open session counts up to `nowMs`, and a skewed clock never goes negative. */
export function sessionDurationMs(session: Pick<Session, 'startedAt' | 'completedAt'>, nowMs: number): number {
  return Math.max(0, (session.completedAt ?? nowMs) - session.startedAt);
}

export interface NodeSummary {
  checkpointId: string;
  attempts: number;
  hesitationMs: number;
  avgHesitationMs: number;
  wrongScans: number;
}

/** Totals per checkpoint, in the order each checkpoint first appeared. */
export function summarizeNodes(metrics: NodeMetric[]): NodeSummary[] {
  const byCheckpoint = new Map<string, NodeSummary>();
  for (const m of metrics) {
    const row = byCheckpoint.get(m.checkpointId) ?? {
      checkpointId: m.checkpointId, attempts: 0, hesitationMs: 0, avgHesitationMs: 0, wrongScans: 0,
    };
    row.attempts += 1;
    row.hesitationMs += m.hesitationMs;
    row.wrongScans += m.wrongScans;
    row.avgHesitationMs = row.hesitationMs / row.attempts;
    byCheckpoint.set(m.checkpointId, row);
  }
  return [...byCheckpoint.values()];
}

/** "1 m 05 s", or "42 s" under a minute. */
export function formatDuration(ms: number): string {
  const total = Math.round(ms / 1000);
  if (total < 60) return `${total} s`;
  return `${Math.floor(total / 60)} m ${String(total % 60).padStart(2, '0')} s`;
}
