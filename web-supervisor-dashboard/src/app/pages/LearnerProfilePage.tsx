import { useEffect, useState } from 'react';
import { watchRoutes } from '../../lib/services/routines';
import { saveProfile, watchProfile } from '../../lib/services/profiles';
import { defaultProfile, moveRow, profileProblems, taskRows, toggleRow, withTasks, type TaskRow } from '../../lib/services/profileEditor';
import { HESITATION_MAX_S, HESITATION_MIN_S } from '../../lib/validation/schemas';
import type { AacOptionId, LearnerProfile, Route } from '../../types';
import { useStudentId } from '../studentContext';

const ROUTINE_ID = 'rt_morning';

const AAC_LABELS: Record<AacOptionId, string> = {
  too_loud: 'Too loud',
  need_help: 'Need help (always on)',
  show_me: 'Show me',
  break: 'Break',
};

type Status = { kind: 'idle' } | { kind: 'saving' } | { kind: 'saved' } | { kind: 'error'; message: string };

/** One learner's adjustments to the shared Morning Routine. The tablet applies them at the next start. */
export function Component() {
  const studentId = useStudentId();
  const [route, setRoute] = useState<Route | null | undefined>(undefined);
  const [saved, setSaved] = useState<LearnerProfile | null | undefined>(undefined);
  const [failed, setFailed] = useState(false);
  const [draft, setDraft] = useState<LearnerProfile | null>(null);
  const [rows, setRows] = useState<TaskRow[] | null>(null);
  const [status, setStatus] = useState<Status>({ kind: 'idle' });

  useEffect(() => watchRoutes((all) => setRoute(all[ROUTINE_ID] ?? null), () => setFailed(true)), []);
  useEffect(() => {
    if (!studentId) return undefined;
    setDraft(null);
    setRows(null);
    setSaved(undefined);
    setStatus({ kind: 'idle' });
    return watchProfile(studentId, setSaved, () => setFailed(true));
  }, [studentId]);

  if (!studentId) return <p>No learner is assigned to this account.</p>;
  if (failed) return <p role="alert" className="chip chip-help">The profile could not load. Check this account's access.</p>;
  if (route === undefined || saved === undefined) return <p>Loading profile...</p>;
  if (route === null) return <p>The Morning Routine is not in the database yet. Seed it first.</p>;

  // Live updates refresh the form until someone starts editing, so edits are never overwritten.
  const profile = draft ?? saved ?? defaultProfile(ROUTINE_ID);
  const tasks = rows ?? taskRows(route, profile);
  const problems = profileProblems(tasks);
  const dirty = draft !== null || rows !== null;

  function change(next: Partial<LearnerProfile>) {
    setDraft({ ...profile, ...next });
    setStatus({ kind: 'idle' });
  }

  function changeTasks(next: TaskRow[]) {
    setRows(next);
    setStatus({ kind: 'idle' });
  }

  function discard() {
    setDraft(null);
    setRows(null);
    setStatus({ kind: 'idle' });
  }

  async function save() {
    if (!studentId || problems.length > 0) return;
    setStatus({ kind: 'saving' });
    try {
      await saveProfile(studentId, withTasks(profile, tasks));
      setDraft(null);
      setRows(null);
      setStatus({ kind: 'saved' });
    } catch {
      setStatus({ kind: 'error', message: 'The database refused this change. Check that this learner is assigned to you.' });
    }
  }

  return (
    <>
      <h1>Learner Profile</h1>
      <p className="muted">Changes apply when {studentId} next starts the Morning Routine.</p>

      <section className="panel">
        <h2>Tasks</h2>
        <ol className="step-list">
          {tasks.map((task, index) => (
            <li key={task.id} className={`step${task.enabled ? '' : ' step-off'}`}>
              <div className="step-body">
                <strong>{index + 1}. {task.label}</strong>
              </div>
              <div className="step-actions">
                <button className="btn" type="button" disabled={index === 0} aria-label={`Move ${task.label} up`} onClick={() => changeTasks(moveRow(tasks, index, index - 1))}>Up</button>
                <button className="btn" type="button" disabled={index === tasks.length - 1} aria-label={`Move ${task.label} down`} onClick={() => changeTasks(moveRow(tasks, index, index + 1))}>Down</button>
                <label className="toggle">
                  <input type="checkbox" checked={task.enabled} onChange={() => changeTasks(toggleRow(tasks, task.id))} /> {task.enabled ? 'On' : 'Skip'}
                </label>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <section className="panel form">
        <h2>Support</h2>
        <label className="toggle">
          <input type="checkbox" checked={profile.voicePrompts} onChange={(e) => change({ voicePrompts: e.target.checked })} /> Spoken prompts
        </label>
        <label className="toggle">
          <input type="checkbox" checked={profile.mascot} onChange={(e) => change({ mascot: e.target.checked })} /> 3D mascot
        </label>
        <label className="field">
          Wait before the help board opens: {profile.hesitationSeconds} s
          <input
            type="range"
            min={HESITATION_MIN_S}
            max={HESITATION_MAX_S}
            step={5}
            value={profile.hesitationSeconds}
            onChange={(e) => change({ hesitationSeconds: Number(e.target.value) })}
          />
        </label>
        <fieldset>
          <legend>Help board options</legend>
          {(Object.keys(AAC_LABELS) as AacOptionId[]).map((id) => (
            <label key={id} className="toggle">
              <input
                type="checkbox"
                checked={profile.aacOptions[id]}
                disabled={id === 'need_help'}
                onChange={(e) => change({ aacOptions: { ...profile.aacOptions, [id]: e.target.checked } })}
              />{' '}
              {AAC_LABELS[id]}
            </label>
          ))}
        </fieldset>
      </section>

      {problems.map((p) => <p key={p} role="alert" className="chip chip-help">{p}</p>)}

      <div className="toolbar toolbar-start">
        <button className="btn btn-go" type="button" disabled={!dirty || problems.length > 0 || status.kind === 'saving'} onClick={() => void save()}>
          {status.kind === 'saving' ? 'Saving...' : 'Save'}
        </button>
        {dirty && <button className="btn" type="button" onClick={discard}>Discard changes</button>}
        {status.kind === 'saved' && <span role="status" className="chip chip-go">Saved</span>}
        {status.kind === 'error' && <span role="alert" className="chip chip-help">{status.message}</span>}
      </div>
    </>
  );
}
