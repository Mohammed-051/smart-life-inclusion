import { useEffect, useState } from 'react';
import { watchLiveStatus, watchRecentLogs, fetchSessions } from '../../lib/services/checkpoints';
import { autonomousPct, avgCompletionMs } from '../../lib/services/metrics';
import type { CheckpointLog, LiveStatus, Session } from '../../types';

/** Prototype single-student view. Replace with a student picker once auth + student list land (S4). */
const STUDENT_ID = 'stu_001';

export function Component() {
  const [live, setLive] = useState<LiveStatus | null>(null);
  const [logs, setLogs] = useState<CheckpointLog[]>([]);
  const [sessions, setSessions] = useState<Session[]>([]);

  useEffect(() => {
    const offLive = watchLiveStatus(STUDENT_ID, setLive);
    const offLogs = watchRecentLogs(STUDENT_ID, 50, setLogs);
    fetchSessions(STUDENT_ID).then(setSessions).catch(console.error);
    return () => { offLive(); offLogs(); };
  }, []);

  const avgMs = avgCompletionMs(sessions);
  return (
    <>
      <h1>Live Telemetry</h1>
      <section className="kpi-grid">
        <Kpi label="Current checkpoint" value={live?.checkpointId ?? '—'} />
        <Kpi label="Route progress" value={live ? `${live.progressPct}%` : '—'} />
        <Kpi label="Autonomous navigation" value={`${autonomousPct(sessions).toFixed(0)}%`} />
        <Kpi label="Avg completion" value={avgMs ? `${(avgMs / 1000).toFixed(0)} s` : '—'} />
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
