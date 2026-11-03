import { describe, expect, it } from 'vitest';
import { autonomousPct, avgCompletionMs } from './metrics';
import type { Session } from '../../types';

// Mirrors firebase/seed.json sessions for stu_001.
const sessions: Session[] = [
  { id: 'ses_0001', routeId: 'rt_hall_a', startedAt: 1762160400000, completedAt: 1762160580000, status: 'completed', checkpointsReached: 4, autonomousCount: 3 },
  { id: 'ses_0002', routeId: 'rt_handwash', startedAt: 1762161000000, completedAt: 1762161150000, status: 'completed', checkpointsReached: 4, autonomousCount: 4 },
];

describe('metrics', () => {
  it('computes autonomous %', () => expect(autonomousPct(sessions)).toBeCloseTo(87.5));
  it('computes avg completion', () => expect(avgCompletionMs(sessions)).toBe(165000));
  it('handles empty input', () => {
    expect(autonomousPct([])).toBe(0);
    expect(avgCompletionMs([])).toBeNull();
  });
});
