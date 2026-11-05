import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { buildRoutePayload, moveStep, toggleStep, toSteps, validateRoutine } from './routineBuilder';
import { routeSchema } from '../validation/schemas';
import type { Route } from '../../types';

const seed = JSON.parse(readFileSync(resolve(__dirname, '../../../../firebase/seed.json'), 'utf8'));
const handwash: Route = routeSchema.parse(seed.routes.rt_handwash);

describe('routine builder', () => {
  it('lists seed checkpoints by order', () => {
    expect(toSteps(handwash).map((s) => s.id)).toEqual(['cp_wet', 'cp_soap', 'cp_rub', 'cp_rinse']);
  });

  it('moves a step and leaves a bad move alone', () => {
    const steps = toSteps(handwash);
    expect(moveStep(steps, 0, 2).map((s) => s.id)).toEqual(['cp_soap', 'cp_rub', 'cp_wet', 'cp_rinse']);
    expect(moveStep(steps, 0, 9)).toBe(steps);
  });

  it('renumbers order, keeps seed fields and bumps version', () => {
    const payload = buildRoutePayload(handwash, moveStep(toSteps(handwash), 3, 0));
    expect(payload.version).toBe(handwash.version + 1);
    expect(payload.checkpoints.cp_rinse?.order).toBe(1);
    expect(payload.checkpoints.cp_wet).toEqual({
      order: 2, markerId: 'MK_SINK', label: 'Wet your hands', cue: 'avatar_wet', enabled: true,
    });
    expect(validateRoutine(payload)).toEqual([]);
  });

  it('keeps a turned-off step in the payload', () => {
    const payload = buildRoutePayload(handwash, toggleStep(toSteps(handwash), 'cp_soap'));
    expect(payload.checkpoints.cp_soap?.enabled).toBe(false);
    expect(validateRoutine(payload)).toEqual([]);
  });

  it('refuses a routine with every step off', () => {
    let steps = toSteps(handwash);
    for (const s of steps) steps = toggleStep(steps, s.id);
    expect(validateRoutine(buildRoutePayload(handwash, steps))).toContain('Turn on at least one step.');
  });

  it('refuses two active steps on one marker', () => {
    const steps = toSteps(handwash).map((s) => ({ ...s, markerId: 'MK_SINK' }));
    expect(validateRoutine(buildRoutePayload(handwash, steps))).toContain('Two active steps use the same marker.');
  });
});
