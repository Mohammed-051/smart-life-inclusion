import { describe, expect, it } from 'vitest';
import { autonomousPct, avgCompletionMs, formatDuration, sessionDurationMs, summarizeNodes } from './metrics';
import type { NodeMetric, Session } from '../../types';

const extra = { activeDurationMs: 0, hesitationTotalMs: 0, wrongScanCount: 0 };

// Mirrors firebase/seed.json sessions for stu_001.
const sessions: Session[] = [
  { id: 'ses_0001', routeId: 'rt_hall_a', startedAt: 1762160400000, completedAt: 1762160580000, status: 'completed', checkpointsReached: 4, autonomousCount: 3, ...extra },
  { id: 'ses_0002', routeId: 'rt_handwash', startedAt: 1762161000000, completedAt: 1762161150000, status: 'completed', checkpointsReached: 4, autonomousCount: 4, ...extra },
];

const node = (id: string, checkpointId: string, hesitationMs: number, wrongScans: number): NodeMetric => ({
  id, checkpointId, stepIndex: 0, hesitationMs, holdMs: 0, wrongScans, aacOpened: false, outcome: 'completed', autonomous: true, timestamp: 1,
});

describe('metrics', () => {
  it('computes autonomous %', () => expect(autonomousPct(sessions)).toBeCloseTo(87.5));
  it('computes avg completion', () => expect(avgCompletionMs(sessions)).toBe(165000));
  it('handles empty input', () => {
    expect(autonomousPct([])).toBe(0);
    expect(avgCompletionMs([])).toBeNull();
    expect(summarizeNodes([])).toEqual([]);
  });

  it('counts an open session up to now, and a closed one to its end', () => {
    expect(sessionDurationMs({ startedAt: 1000 }, 4000)).toBe(3000);
    expect(sessionDurationMs({ startedAt: 1000, completedAt: 2000 }, 4000)).toBe(1000);
    expect(sessionDurationMs({ startedAt: 5000 }, 4000)).toBe(0);
  });

  it('totals hesitation and wrong scans per checkpoint', () => {
    const rows = summarizeNodes([node('a', 'cp_wet', 4000, 1), node('b', 'cp_soap', 1000, 0), node('c', 'cp_wet', 2000, 2)]);
    expect(rows.map((r) => r.checkpointId)).toEqual(['cp_wet', 'cp_soap']);
    expect(rows[0]).toEqual({ checkpointId: 'cp_wet', attempts: 2, hesitationMs: 6000, avgHesitationMs: 3000, wrongScans: 3 });
  });

  it('formats durations', () => {
    expect(formatDuration(42000)).toBe('42 s');
    expect(formatDuration(65000)).toBe('1 m 05 s');
  });
});
