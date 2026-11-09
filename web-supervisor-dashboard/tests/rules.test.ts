// Simulated checks of firebase/database.rules.json using targaryen. The emulator run required by
// docs/TESTING.md is still needed before deploy, because this simulator is not Google's engine.
// @ts-expect-error targaryen ships no types.
import targaryen from 'targaryen';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const rules = JSON.parse(readFileSync(resolve(__dirname, '../../firebase/database.rules.json'), 'utf8'));
const seed = JSON.parse(readFileSync(resolve(__dirname, '../../firebase/seed.json'), 'utf8'));

const NOW = 1_800_000_000_000;
const TABLET = { uid: 'dev_uid_tablet01' };
const TEACHER = { uid: 'sup_uid_teacher01' };
const ADMIN = { uid: 'sup_uid_admin01' };
const STRANGER = { uid: 'self_registered_account' };

const db = (data: unknown = seed) => targaryen.database(rules, data, NOW);
const canRead = (auth: unknown, path: string, data?: unknown) => db(data).as(auth).read(path).allowed as boolean;
const canWrite = (auth: unknown, path: string, value: unknown, data?: unknown) =>
  db(data).as(auth).write(path, value, { now: NOW }).allowed as boolean;

const log = (over: object = {}) => ({
  studentId: 'stu_001', sessionId: 's1', routeId: 'rt_handwash', checkpointId: 'cp_wet', markerId: 'MK_SINK',
  taskStatus: 'completed', autonomous: true, deviceId: TABLET.uid, timestamp: NOW, ...over,
});
const live = { sessionId: 's1', routeId: 'rt_handwash', checkpointId: 'cp_wet', progressPct: 25, state: 'active', lastSeen: NOW };

describe('authentication is required', () => {
  it('denies anonymous reads of routines and telemetry', () => {
    for (const path of ['/routes/rt_handwash', '/sessions/stu_001', '/checkpointLogs/stu_001', '/liveStatus/stu_001', '/']) {
      expect(canRead(null, path), path).toBe(false);
    }
  });

  it('denies anonymous telemetry writes', () => {
    expect(canWrite(null, '/checkpointLogs/stu_001/new', log())).toBe(false);
    expect(canWrite(null, '/liveStatus/stu_001', live)).toBe(false);
  });
});

describe('routine data', () => {
  it('is readable by mapped tablets and supervisors', () => {
    expect(canRead(TABLET, '/routes/rt_handwash')).toBe(true);
    expect(canRead(TEACHER, '/routes/rt_handwash')).toBe(true);
  });

  it('is not readable by a signed-in account nobody has mapped', () => {
    expect(canRead(STRANGER, '/routes/rt_handwash')).toBe(false);
  });
});

describe('tablet telemetry', () => {
  it('writes its own learner only', () => {
    expect(canWrite(TABLET, '/checkpointLogs/stu_001/new', log())).toBe(true);
    expect(canWrite(TABLET, '/checkpointLogs/stu_002/new', log({ studentId: 'stu_002' }))).toBe(false);
    expect(canWrite(TABLET, '/liveStatus/stu_001', live)).toBe(true);
    expect(canWrite(TABLET, '/liveStatus/stu_002', live)).toBe(false);
  });

  it('cannot overwrite or delete a log', () => {
    expect(canWrite(TABLET, '/checkpointLogs/stu_001/log_0001', log())).toBe(false);
    expect(canWrite(TABLET, '/checkpointLogs/stu_001/log_0001', null)).toBe(false);
  });

  it('cannot delete sessions or overrides', () => {
    expect(canWrite(TABLET, '/sessions/stu_001/ses_0001', null)).toBe(false);
    expect(canWrite(TABLET, '/overrides/stu_001/ovr_0001', null)).toBe(false);
  });

  it('rejects a spoofed device ID, a client timestamp and unknown fields', () => {
    expect(canWrite(TABLET, '/checkpointLogs/stu_001/new', log({ deviceId: 'other' }))).toBe(false);
    expect(canWrite(TABLET, '/checkpointLogs/stu_001/new', log({ timestamp: NOW - 5 }))).toBe(false);
    expect(canWrite(TABLET, '/checkpointLogs/stu_001/new', log({ name: 'A' }))).toBe(false);
    expect(canWrite(TABLET, '/liveStatus/stu_001', { ...live, name: 'A' })).toBe(false);
  });
});

describe('roles', () => {
  it('stops self-assignment and supervisor telemetry writes', () => {
    expect(canWrite(TABLET, '/supervisors/dev_uid_tablet01', { role: 'admin' })).toBe(false);
    expect(canWrite(STRANGER, '/devices/self_registered_account', { studentId: 'stu_001' })).toBe(false);
    expect(canWrite(TEACHER, '/checkpointLogs/stu_001/new', log({ deviceId: TEACHER.uid }))).toBe(false);
  });

  it('keeps supervisors to assigned learners', () => {
    const data = structuredClone(seed);
    data.supervisors.sup_uid_teacher01.students = { stu_002: true };
    expect(canRead(TEACHER, '/liveStatus/stu_001', data)).toBe(false);
    expect(canRead(TEACHER, '/liveStatus/stu_002', data)).toBe(true);
    expect(canRead(ADMIN, '/liveStatus/stu_001', data)).toBe(true);
  });
});

describe('routine publishing', () => {
  const route = { name: 'Handwashing', kind: 'adl', version: 2, checkpoints: { cp_wet: { order: 1, markerId: 'MK_SINK', label: 'Wet', cue: 'avatar_wet', enabled: false } } };

  it('lets a supervisor publish, but not a tablet or a stranger', () => {
    expect(canWrite(TEACHER, '/routes/rt_handwash', route)).toBe(true);
    expect(canWrite(TABLET, '/routes/rt_handwash', route)).toBe(false);
    expect(canWrite(STRANGER, '/routes/rt_handwash', route)).toBe(false);
    expect(canWrite(null, '/routes/rt_handwash', route)).toBe(false);
  });

  it('leaves deleting to an admin', () => {
    expect(canWrite(TEACHER, '/routes/rt_handwash', null)).toBe(false);
    expect(canWrite(ADMIN, '/routes/rt_handwash', null)).toBe(true);
  });

  it('rejects unknown fields and a bad kind', () => {
    expect(canWrite(TEACHER, '/routes/rt_handwash', { ...route, secret: 1 })).toBe(false);
    expect(canWrite(TEACHER, '/routes/rt_handwash', { ...route, kind: 'other' })).toBe(false);
  });
});

describe('node metrics', () => {
  const metric = { checkpointId: 'cp_wet', stepIndex: 0, hesitationMs: 10, holdMs: 0, wrongScans: 1, aacOpened: false, outcome: 'completed', autonomous: true, timestamp: NOW };

  it('are appended by the mapped tablet only', () => {
    expect(canWrite(TABLET, '/nodeMetrics/stu_001/s1/new', metric)).toBe(true);
    expect(canWrite(TABLET, '/nodeMetrics/stu_002/s1/new', metric)).toBe(false);
    expect(canWrite(TEACHER, '/nodeMetrics/stu_001/s1/new', metric)).toBe(false);
    expect(canWrite(TABLET, '/nodeMetrics/stu_001/ses_0002/nm_0001', metric)).toBe(false);
  });

  it('are readable by the assigned supervisor, not anonymously', () => {
    expect(canRead(TEACHER, '/nodeMetrics/stu_001/ses_0002')).toBe(true);
    expect(canRead(null, '/nodeMetrics/stu_001/ses_0002')).toBe(false);
  });
});

describe('emergency streaming', () => {
  const withEmergency = (source: string, status: string, consent: boolean) => {
    const data = structuredClone(seed);
    data.students.stu_001.streamingConsent = consent;
    data.emergencies = { stu_001: { em1: { source, status, timestamp: NOW - 1000 } } };
    return data;
  };
  const request = (over: object = {}) => ({
    emergencyId: 'em1', requestedBy: TEACHER.uid, status: 'requested', offer: 'v=0', startedAt: NOW, expiresAt: NOW + 60000, ...over,
  });
  const running = () => {
    const data = withEmergency('sos', 'open', true);
    data.interventions = { stu_001: { in1: request() } };
    return data;
  };

  it('lets an assigned supervisor start a call for an open SOS with consent', () => {
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request(), withEmergency('sos', 'open', true))).toBe(true);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request(), withEmergency('sos', 'acknowledged', true))).toBe(true);
  });

  it('refuses without consent, without an open SOS, or for a missing emergency', () => {
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request(), withEmergency('sos', 'open', false))).toBe(false);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request(), withEmergency('audio', 'open', true))).toBe(false);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request(), withEmergency('imu', 'open', true))).toBe(false);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request(), withEmergency('sos', 'resolved', true))).toBe(false);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request({ emergencyId: 'nope' }), withEmergency('sos', 'open', true))).toBe(false);
  });

  it('refuses the tablet, a stranger, another supervisor name and a long expiry', () => {
    const data = withEmergency('sos', 'open', true);
    expect(canWrite(TABLET, '/interventions/stu_001/in1', request({ requestedBy: TABLET.uid }), data)).toBe(false);
    expect(canWrite(STRANGER, '/interventions/stu_001/in1', request({ requestedBy: STRANGER.uid }), data)).toBe(false);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request({ requestedBy: ADMIN.uid }), data)).toBe(false);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1', request({ expiresAt: NOW + 900000 }), data)).toBe(false);
    expect(canWrite(null, '/interventions/stu_001/in1', request(), data)).toBe(false);
  });

  it('lets each side write only its own part of a call', () => {
    const data = running();
    expect(canWrite(TABLET, '/interventions/stu_001/in1/answer', 'v=0', data)).toBe(true);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1/answer', 'v=0', data)).toBe(false);
    expect(canWrite(TABLET, '/interventions/stu_001/in1/candidates/tablet/c1', '{}', data)).toBe(true);
    expect(canWrite(TABLET, '/interventions/stu_001/in1/candidates/supervisor/c1', '{}', data)).toBe(false);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1/candidates/supervisor/c1', '{}', data)).toBe(true);
    expect(canWrite(TABLET, '/interventions/stu_001/in1/status', 'ended', data)).toBe(true);
    expect(canWrite(TEACHER, '/interventions/stu_001/in1/status', 'ended', data)).toBe(true);
    expect(canWrite(STRANGER, '/interventions/stu_001/in1/status', 'ended', data)).toBe(false);
    expect(canWrite(TABLET, '/interventions/stu_001/in1/offer', 'x', data)).toBe(false);
  });

  it('lets only the tablet raise an emergency, and supervisors acknowledge it', () => {
    const fresh = { source: 'sos', status: 'open', timestamp: NOW };
    expect(canWrite(TABLET, '/emergencies/stu_001/em2', fresh)).toBe(true);
    expect(canWrite(TEACHER, '/emergencies/stu_001/em2', fresh)).toBe(false);
    expect(canWrite(TABLET, '/emergencies/stu_002/em2', fresh)).toBe(false);
    const data = withEmergency('sos', 'open', true);
    expect(canWrite(TEACHER, '/emergencies/stu_001/em1/status', 'resolved', data)).toBe(true);
    expect(canWrite(TEACHER, '/emergencies/stu_001/em1/acknowledgedBy', TEACHER.uid, data)).toBe(true);
    expect(canWrite(TEACHER, '/emergencies/stu_001/em1/acknowledgedBy', ADMIN.uid, data)).toBe(false);
    expect(canWrite(TEACHER, '/emergencies/stu_001/em1/source', 'audio', data)).toBe(false);
    expect(canRead(TEACHER, '/emergencies/stu_001', data)).toBe(true);
    expect(canRead(null, '/emergencies/stu_001', data)).toBe(false);
  });
});

describe('morning routine and learner profiles', () => {
  const profile = (over: object = {}) => ({
    routineId: 'rt_morning',
    tasks: { task_toilet: { order: 1, enabled: false } },
    voicePrompts: true, mascot: false, hesitationSeconds: 20,
    aacOptions: { too_loud: false, need_help: true, show_me: true, break: true },
    updatedAt: NOW, updatedBy: TEACHER.uid, ...over,
  });
  const OTHER_TEACHER = { uid: 'sup_uid_teacher02' };
  const withOtherTeacher = {
    ...seed,
    supervisors: { ...seed.supervisors, [OTHER_TEACHER.uid]: { role: 'teacher', students: { stu_002: true } } },
  };

  it('seeds a morning route that publishes with tasks and task ids', () => {
    expect(canWrite(TEACHER, '/routes/rt_morning', seed.routes.rt_morning)).toBe(true);
    expect(canWrite(TEACHER, '/routes/rt_morning/tasks/task_wake', { order: 1, label: 'Wake up', extra: 1 })).toBe(false);
  });

  it('is readable by the learner tablet and assigned supervisor only', () => {
    expect(canRead(TABLET, '/profiles/stu_001')).toBe(true);
    expect(canRead(TABLET, '/profiles/stu_002')).toBe(false);
    expect(canRead(TEACHER, '/profiles/stu_002')).toBe(true);
    expect(canRead(STRANGER, '/profiles/stu_001')).toBe(false);
  });

  it('is written by an assigned supervisor, not the tablet or another supervisor', () => {
    expect(canWrite(TEACHER, '/profiles/stu_001', profile())).toBe(true);
    expect(canWrite(ADMIN, '/profiles/stu_001', profile({ updatedBy: ADMIN.uid }))).toBe(true);
    expect(canWrite(TABLET, '/profiles/stu_001', profile({ updatedBy: TABLET.uid }))).toBe(false);
    expect(canWrite(OTHER_TEACHER, '/profiles/stu_001', profile({ updatedBy: OTHER_TEACHER.uid }), withOtherTeacher)).toBe(false);
    expect(canWrite(TEACHER, '/profiles/stu_001', null)).toBe(false);
  });

  it('keeps Need help on, the delay in range, and the stamp honest', () => {
    expect(canWrite(TEACHER, '/profiles/stu_001', profile({ aacOptions: { need_help: false } }))).toBe(false);
    expect(canWrite(TEACHER, '/profiles/stu_001', profile({ hesitationSeconds: 2 }))).toBe(false);
    expect(canWrite(TEACHER, '/profiles/stu_001', profile({ hesitationSeconds: 121 }))).toBe(false);
    expect(canWrite(TEACHER, '/profiles/stu_001', profile({ updatedAt: NOW - 1 }))).toBe(false);
    expect(canWrite(TEACHER, '/profiles/stu_001', profile({ updatedBy: ADMIN.uid }))).toBe(false);
    expect(canWrite(TEACHER, '/profiles/stu_001', profile({ secret: 1 }))).toBe(false);
  });
});
