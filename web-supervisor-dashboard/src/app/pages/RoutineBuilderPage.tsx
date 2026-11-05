import { useEffect, useMemo, useState } from 'react';
import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import { SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { buildRoutePayload, moveStep, toggleStep, toSteps, validateRoutine } from '../../lib/services/routineBuilder';
import { publishRoutine, watchRoutes } from '../../lib/services/routines';
import type { Route, RoutineStep } from '../../types';

type Status = { kind: 'idle' } | { kind: 'saving' } | { kind: 'saved'; version: number } | { kind: 'error'; message: string };

/** Reorder and toggle a routine's steps, then publish it to /routes. Layout: clinical_architecture.md 2.2. */
export function Component() {
  const [routes, setRoutes] = useState<Record<string, Route> | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [routeId, setRouteId] = useState<string | null>(null);
  const [draft, setDraft] = useState<RoutineStep[] | null>(null);
  const [status, setStatus] = useState<Status>({ kind: 'idle' });

  useEffect(() => watchRoutes(setRoutes, () => setLoadFailed(true)), []);

  const ids = Object.keys(routes ?? {});
  const activeId = routeId && ids.includes(routeId) ? routeId : ids[0] ?? null;
  const route = activeId && routes ? routes[activeId] : undefined;
  // Live updates refresh the list until a caregiver starts editing, so edits are never overwritten.
  const steps = draft ?? (route ? toSteps(route) : []);

  const payload = useMemo(() => (route ? buildRoutePayload(route, steps) : null), [route, steps]);
  const problems = payload ? validateRoutine(payload) : [];

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  function edit(next: RoutineStep[]) {
    setDraft(next);
    setStatus({ kind: 'idle' });
  }

  function pickRoute(id: string) {
    setRouteId(id);
    setDraft(null);
    setStatus({ kind: 'idle' });
  }

  function onDragEnd({ active, over }: DragEndEvent) {
    if (!over || active.id === over.id) return;
    const from = steps.findIndex((s) => s.id === active.id);
    const to = steps.findIndex((s) => s.id === over.id);
    edit(moveStep(steps, from, to));
  }

  async function publish() {
    if (!activeId || !payload || problems.length > 0) return;
    setStatus({ kind: 'saving' });
    try {
      await publishRoutine(activeId, payload);
      setDraft(null);
      setStatus({ kind: 'saved', version: payload.version });
    } catch {
      setStatus({ kind: 'error', message: 'The database refused this change. Check that this account may publish routines.' });
    }
  }

  if (loadFailed) return <p role="alert" className="chip chip-help">Routines could not load. Check this account's access.</p>;
  if (!routes) return <p>Loading routines...</p>;
  if (ids.length === 0) return <p>No routines exist yet. Seed the database first.</p>;

  return (
    <>
      <h1>Routine Builder</h1>
      <label className="field">
        Routine{' '}
        <select value={activeId ?? ''} onChange={(e) => pickRoute(e.target.value)}>
          {ids.map((id) => <option key={id} value={id}>{routes[id]?.name ?? id}</option>)}
        </select>
      </label>

      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={steps.map((s) => s.id)} strategy={verticalListSortingStrategy}>
          <ol className="step-list">
            {steps.map((step, index) => (
              <StepRow
                key={step.id}
                step={step}
                index={index}
                count={steps.length}
                onMove={(to) => edit(moveStep(steps, index, to))}
                onToggle={() => edit(toggleStep(steps, step.id))}
              />
            ))}
          </ol>
        </SortableContext>
      </DndContext>

      {problems.map((p) => <p key={p} role="alert" className="chip chip-help">{p}</p>)}

      <div className="toolbar toolbar-start">
        <button className="btn btn-go" type="button" disabled={problems.length > 0 || status.kind === 'saving' || !draft} onClick={() => void publish()}>
          {status.kind === 'saving' ? 'Publishing...' : 'Publish'}
        </button>
        {draft && <button className="btn" type="button" onClick={() => pickRoute(activeId ?? '')}>Discard changes</button>}
        {status.kind === 'saved' && <span role="status" className="chip chip-go">Published version {status.version}</span>}
        {status.kind === 'error' && <span role="alert" className="chip chip-help">{status.message}</span>}
      </div>

      <details className="panel">
        <summary>Data sent to the tablet</summary>
        <pre className="payload">{JSON.stringify(payload, null, 2)}</pre>
      </details>
    </>
  );
}

interface StepRowProps {
  step: RoutineStep;
  index: number;
  count: number;
  onMove: (to: number) => void;
  onToggle: () => void;
}

function StepRow({ step, index, count, onMove, onToggle }: StepRowProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: step.id });

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`step${step.enabled ? '' : ' step-off'}${isDragging ? ' step-dragging' : ''}`}
    >
      <button className="btn step-handle" type="button" aria-label={`Drag ${step.label}`} {...attributes} {...listeners}>☰</button>
      <div className="step-body">
        <strong>{index + 1}. {step.label}</strong>
        <span className="muted">{step.markerId}</span>
      </div>
      <div className="step-actions">
        <button className="btn" type="button" disabled={index === 0} aria-label={`Move ${step.label} up`} onClick={() => onMove(index - 1)}>Up</button>
        <button className="btn" type="button" disabled={index === count - 1} aria-label={`Move ${step.label} down`} onClick={() => onMove(index + 1)}>Down</button>
        <label className="toggle">
          <input type="checkbox" checked={step.enabled} onChange={onToggle} /> {step.enabled ? 'On' : 'Off'}
        </label>
      </div>
    </li>
  );
}
