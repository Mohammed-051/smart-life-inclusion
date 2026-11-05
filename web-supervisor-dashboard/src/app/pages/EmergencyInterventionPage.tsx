import { useEffect, useRef, useState } from 'react';
import { watchAuth } from '../../lib/services/auth';
import { watchServerOffset } from '../../lib/services/checkpoints';
import {
  acknowledgeEmergency,
  readStreamingConsent,
  resolveEmergency,
  sendOverride,
  watchEmergencies,
} from '../../lib/services/emergency';
import { startInterventionCall, type CallState, type InterventionCall } from '../../lib/services/interventionCall';
import { streamBlockReason } from '../../lib/services/streamPolicy';
import { useStudentId } from '../studentContext';
import type { Emergency } from '../../types';

const SOURCE_LABEL: Record<Emergency['source'], string> = { sos: 'SOS button', imu: 'Fall detected', audio: 'Loud sound' };

/** Alert queue and supervisor-started calls. Rules: docs/SECURITY.md, flow: docs/clinical_architecture.md 2.4. */
export function Component() {
  const studentId = useStudentId();
  if (!studentId) return <p>No learners are assigned to this account yet.</p>;
  return <EmergencyPage key={studentId} studentId={studentId} />;
}

function EmergencyPage({ studentId }: { studentId: string }) {
  const [uid, setUid] = useState<string | null>(null);
  const [emergencies, setEmergencies] = useState<Emergency[]>([]);
  const [consent, setConsent] = useState(false);
  const [offsetMs, setOffsetMs] = useState(0);
  const [callState, setCallState] = useState<CallState | null>(null);
  const [hasVideo, setHasVideo] = useState(false);
  const [micAllowed, setMicAllowed] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const callRef = useRef<InterventionCall | null>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);

  useEffect(() => {
    const offs = [
      watchAuth(setUid),
      watchEmergencies(studentId, 20, setEmergencies),
      watchServerOffset(setOffsetMs),
    ];
    void readStreamingConsent(studentId).then(setConsent).catch(() => setConsent(false));
    return () => {
      offs.forEach((off) => off());
      callRef.current?.end();
    };
  }, [studentId]);

  const active = emergencies.filter((e) => e.status !== 'resolved');
  const streaming = callState === 'connecting' || callState === 'connected';

  // A resolved emergency ends its call, whichever side resolved it.
  useEffect(() => {
    if (streaming && active.length === 0) callRef.current?.end();
  }, [streaming, active.length]);

  async function run(action: () => Promise<void>) {
    setError(null);
    try {
      await action();
    } catch {
      setError('The database refused that action. Check this account has access to this learner.');
    }
  }

  async function startCall(emergency: Emergency) {
    if (!uid || streamBlockReason(emergency, consent, streaming) !== null) return;
    setError(null);
    setHasVideo(false);
    try {
      const call = await startInterventionCall({
        studentId,
        emergencyId: emergency.id,
        uid,
        nowMs: Date.now() + offsetMs,
        onState: setCallState,
        onVideo: () => setHasVideo(true),
      });
      callRef.current = call;
      setMicAllowed(call.micAllowed);
      if (videoRef.current) videoRef.current.srcObject = call.remoteStream;
    } catch {
      setCallState(null);
      setError('The call could not start. The database accepts it only for an open SOS with recorded consent.');
    }
  }

  return (
    <>
      <h1>Emergency Intervention</h1>
      {error && <p role="alert" className="chip chip-help">{error}</p>}

      {!consent && (
        <p className="chip chip-wait">No guardian consent is recorded for {studentId}, so calls stay locked. Alerts still work.</p>
      )}

      <section aria-label="Live call" className={streaming ? 'panel' : 'panel call-hidden'}>
        <p role="status">
          <span className={callState === 'connected' ? 'chip chip-go' : 'chip chip-wait'}>
            {callState === 'connected' ? 'Connected' : 'Connecting...'}
          </span>{' '}
          {callState === 'connected' && !hasVideo && <span className="chip">Private route: audio only</span>}
          {!micAllowed && <span className="chip chip-wait">Your microphone is off, so the learner cannot hear you</span>}
        </p>
        {/* Live view only: no recording, picture-in-picture or download. */}
        <video ref={videoRef} className="call-video" autoPlay playsInline disablePictureInPicture controlsList="nodownload" />
        <div className="toolbar toolbar-start">
          <button className="btn btn-help" type="button" onClick={() => callRef.current?.end()}>End call</button>
        </div>
      </section>

      <h2>Open alerts</h2>
      {active.length === 0 && <p>No open alerts for {studentId}. New alerts appear here automatically.</p>}
      <ul className="alert-list">
        {active.map((e) => {
          const blocked = streamBlockReason(e, consent, streaming);
          return (
            <li key={e.id} className="panel alert">
              <div>
                <strong>{SOURCE_LABEL[e.source]}</strong>{' '}
                <span className={e.status === 'open' ? 'chip chip-help' : 'chip chip-wait'}>{e.status}</span>
                <div className="muted">{new Date(e.timestamp).toLocaleTimeString()}</div>
              </div>
              <div className="toolbar toolbar-start">
                {e.status === 'open' && uid && (
                  <button className="btn" type="button" onClick={() => void run(() => acknowledgeEmergency(studentId, e.id, uid))}>Acknowledge</button>
                )}
                <button className="btn btn-go" type="button" disabled={blocked !== null} onClick={() => void startCall(e)}>Start call</button>
                <button className="btn" type="button" onClick={() => void run(() => resolveEmergency(studentId, e.id))}>Resolve</button>
              </div>
              {blocked && <p className="muted">{blocked}</p>}
            </li>
          );
        })}
      </ul>

      <h2>Task</h2>
      <p className="muted">Resolving an alert leaves the task paused. An adult with the learner decides when to continue.</p>
      {uid && <button className="btn" type="button" onClick={() => void run(() => sendOverride(studentId, 'resume', uid))}>Resume task on tablet</button>}
    </>
  );
}
