import { useEffect, useState } from 'react';
import { watchLiveStatus, watchNodeMetrics, watchRecentLogs, watchServerOffset, watchSessions } from '../../lib/services/checkpoints';
import { autonomousPct, avgCompletionMs, formatDuration, sessionDurationMs, summarizeNodes } from '../../lib/services/metrics';
import { useStudentId } from '../studentContext';
import type { CheckpointLog, LiveStatus, NodeMetric, Session } from '../../types';

const SESSION_WINDOW = 50;
const STATE_CHIP: Record<LiveStatus['state'], string> = {
  idle: 'chip', awaiting: 'chip chip-go', active: 'chip chip-go', paused: 'chip chip-wait', complete: 'chip',
};

export function Component() {
  const studentId = useStudentId();
  if (!studentId) return <p>No learners are assigned to this account yet.</p>;
  // A new key drops the previous learner's numbers at once.
  return <LiveTelemetry key={studentId} studentId={studentId} />;
}

function LiveTelemetry({ studentId }: { studentId: string }) {
  const [live, setLive] = useState<LiveStatus | null>(null);
  const [logs, setLogs] = useState<CheckpointLog[]>([]);
  const [sessions, setSessions] = useState<Session[]>([]);
  const [nodes, setNodes] = useState<NodeMetric[]>([]);
  const [offsetMs, setOffsetMs] = useState(0);
  const [nowMs, setNowMs] = useState(() => Date.now());

  useEffect(() => {
    const offs = [
      watchLiveStatus(studentId, setLive),
      watchRecentLogs(studentId, 50, setLogs),
      watchSessions(studentId, SESSION_WINDOW, setSessions),
      watchServerOffset(setOffsetMs),
    ];
    return () => offs.forEach((off) => off());
  }, [studentId]);

  // Show the session on the tablet, or the latest one once it has finished.
  const current = sessions.find((s) => s.id === live?.sessionId) ?? sessions[sessions.length - 1] ?? null;
  const currentId = current?.id;

  useEffect(() => {
    if (!currentId) return undefined;
    return watchNodeMetrics(studentId, currentId, 100, setNodes);
  }, [studentId, currentId]);

  // Only an open session needs a running clock.
  const running = current !== null && current.completedAt === undefined;
  useEffect(() => {
    if (!running) return undefined;
    const timer = window.setInterval(() => setNowMs(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [running]);

  const onCurrent = live !== null && live.sessionId === currentId;
  const summary = summarizeNodes(nodes);
  const finishedHesitation = nodes.reduce((sum, n) => sum + n.hesitationMs, 0);
  // Step metrics arrive when a step ends, so add the step in progress from the live status.
  const hesitationMs = finishedHesitation + (onCurrent && running ? live.stepHesitationMs : 0);
  const wrongScans = onCurrent ? live.wrongScanCount : current?.wrongScanCount ?? 0;
  const avgMs = avgCompletionMs(sessions);

  return (
    <>
      <h1>Live Telemetry</h1>
      <p className="toolbar-start">
        {live
          ? <span role="status" className={STATE_CHIP[live.state]}>Tablet: {live.state}</span>
          : <span role="status" className="chip">No live status yet</span>}
      </p>

      <section className="kpi-grid" aria-label="Current session">
        <Kpi label="Session duration" value={current ? formatDuration(sessionDurationMs(current, nowMs + offsetMs)) : '—'} />
        <Kpi label="Hesitation time" value={current ? formatDuration(hesitationMs) : '—'} />
        <Kpi label="Wrong marker scans" value={current ? String(wrongScans) : '—'} />
        <Kpi label="Current checkpoint" value={live?.checkpointId ?? '—'} />
        <Kpi label="Route progress" value={live ? `${live.progressPct}%` : '—'} />
      </section>

      <h2>Hesitation by step</h2>
      {summary.length === 0
        ? <p>No finished steps in this session yet.</p>
        : (
          <div className="table-wrap">
            <table>
              <thead><tr><th scope="col">Checkpoint</th><th scope="col">Attempts</th><th scope="col">Hesitation</th><th scope="col">Average</th><th scope="col">Wrong scans</th></tr></thead>
              <tbody>
                {summary.map((row) => (
                  <tr key={row.checkpointId}>
                    <th scope="row">{row.checkpointId}</th>
                    <td>{row.attempts}</td>
                    <td>{formatDuration(row.hesitationMs)}</td>
                    <td>{formatDuration(row.avgHesitationMs)}</td>
                    <td>{row.wrongScans}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

      <h2>Across the last {SESSION_WINDOW} sessions</h2>
      <section className="kpi-grid">
        <Kpi label="Autonomous navigation" value={`${autonomousPct(sessions).toFixed(0)}%`} />
        <Kpi label="Avg completion" value={avgMs ? formatDuration(avgMs) : '—'} />
      </section>

      <h2>Recent activity</h2>
      {logs.length === 0 && <p>No checkpoints yet. They appear here as the tablet scans markers.</p>}
      <ul>
        {logs.map((l) => (
          <li key={l.id}>
            {new Date(l.timestamp).toLocaleTimeString()} · {l.checkpointId} · {l.taskStatus}
            {l.autonomous ? '' : ' (assisted)'}
          </li>
        ))}
      </ul>
    </>
  );
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <div className="kpi">
      <div className="kpi-label">{label}</div>
      <div className="kpi-value">{value}</div>
    </div>
  );
}
