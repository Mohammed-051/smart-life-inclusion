import { describe, expect, it } from 'vitest';
import { defaultProfile, moveRow, profileProblems, taskRows, toggleRow, withTasks } from './profileEditor';
import type { Route } from '../../types';

const route: Route = {
  name: 'Morning routine',
  kind: 'adl',
  version: 1,
  checkpoints: {},
  tasks: { task_wake: { order: 1, label: 'Wake up' }, task_toilet: { order: 2, label: 'Toilet' }, task_dress: { order: 3, label: 'Get dressed' } },
};

describe('learner profile editor', () => {
  it('shows the default order when the profile changes nothing', () => {
    expect(taskRows(route, defaultProfile('rt_morning')).map((r) => r.id)).toEqual(['task_wake', 'task_toilet', 'task_dress']);
  });

  it('puts listed tasks first in profile order and keeps the rest after them, like the tablet', () => {
    const profile = { ...defaultProfile('rt_morning'), tasks: { task_dress: { order: 1, enabled: true }, task_toilet: { order: 2, enabled: false } } };
    const rows = taskRows(route, profile);
    expect(rows.map((r) => r.id)).toEqual(['task_dress', 'task_toilet', 'task_wake']);
    expect(rows.find((r) => r.id === 'task_toilet')?.enabled).toBe(false);
  });

  it('writes every task with a contiguous order after edits', () => {
    let rows = taskRows(route, defaultProfile('rt_morning'));
    rows = moveRow(rows, 2, 0);
    rows = toggleRow(rows, 'task_toilet');
    expect(withTasks(defaultProfile('rt_morning'), rows).tasks).toEqual({
      task_dress: { order: 1, enabled: true },
      task_wake: { order: 2, enabled: true },
      task_toilet: { order: 3, enabled: false },
    });
  });

  it('refuses a profile with every task off', () => {
    const rows = taskRows(route, defaultProfile('rt_morning')).map((r) => ({ ...r, enabled: false }));
    expect(profileProblems(rows)).toEqual(['Keep at least one task on.']);
  });
});
