import { useEffect, useState } from 'react';
import { watchLiveStatus, watchRecentLogs, fetchSessions } from '../lib/services/checkpoints';
import { autonomousPct, avgCompletionMs } from '../lib/services/metrics';
import type { CheckpointLog, LiveStatus, Session } from '../types';

/** Prototype single-student view. Replace with routing once auth + student list land (S4). */
const STUDENT_ID = 'stu_001';

export default function App() {
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
    <main style={{ padding: 'var(--space)', maxWidth: 1200, margin: '0 auto' }}>
      <h1>Supervisor Dashboard</h1>
      <section style={{ display: 'grid', gap: 'var(--space)', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))' }}>
        <Kpi label="Current checkpoint" value={live?.checkpointId ?? '—'} />
        <Kpi label="Route progress" value={live ? `${live.progressPct}%` : '—'} />
        <Kpi label="Autonomous navigation" value={`${autonomousPct(sessions).toFixed(0)}%`} />
        <Kpi label="Avg completion" value={avgMs ? `${(avgMs / 1000).toFixed(0)} s` : '—'} />
      </section>
      <h2>Recent activity</h2>
      <ul>
        {logs.map((l) => (
          <li key={l.id}>
            {new Date(l.timestamp).toLocaleTimeString()} · {l.checkpointId} · {l.taskStatus}
            {l.autonomous ? '' : ' (assisted)'}
          </li>
        ))}
      </ul>
    </main>
  );
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ background: 'var(--surface)', borderRadius: 'var(--radius)', padding: 'var(--space)' }}>
      <div style={{ color: 'var(--muted)', fontSize: 14 }}>{label}</div>
      <div style={{ fontSize: 28, fontWeight: 600 }}>{value}</div>
    </div>
  );
}
