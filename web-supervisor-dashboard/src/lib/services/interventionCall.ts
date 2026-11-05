import { createIntervention, type CallSignaling } from './emergency';

const STUN_SERVERS: RTCIceServer[] = [{ urls: 'stun:stun.l.google.com:19302' }];
const MAX_CALL_MS = 15 * 60 * 1000;
const END_MESSAGE = 'end';

export type CallState = 'connecting' | 'connected' | 'ended';

export interface InterventionCall {
  /** Tablet video and audio. No video track arrives from a private route. */
  remoteStream: MediaStream;
  micAllowed: boolean;
  end: () => void;
}

export interface CallOptions {
  studentId: string;
  emergencyId: string;
  uid: string;
  nowMs: number;
  onState: (state: CallState) => void;
  onVideo: () => void;
}

/**
 * Starts a two-way call for an open SOS. Only a supervisor's button press calls this. Media is
 * encrypted peer to peer with DTLS-SRTP, and nothing is recorded or stored. The supervisor sends audio
 * only, and the 15-minute limit also applies on this side (docs/SECURITY.md).
 */
export async function startInterventionCall(options: CallOptions): Promise<InterventionCall> {
  const { studentId, emergencyId, uid, nowMs, onState, onVideo } = options;
  const peer = new RTCPeerConnection({ iceServers: STUN_SERVERS });
  const remoteStream = new MediaStream();
  const control = peer.createDataChannel('control');

  let local: MediaStream | null = null;
  try {
    local = await navigator.mediaDevices.getUserMedia({ audio: true });
    local.getTracks().forEach((track) => peer.addTrack(track, local!));
  } catch {
    // The supervisor can still see and hear the room without a working microphone.
    peer.addTransceiver('audio', { direction: 'recvonly' });
  }
  peer.addTransceiver('video', { direction: 'recvonly' });

  peer.ontrack = (e) => {
    remoteStream.addTrack(e.track);
    if (e.track.kind === 'video') onVideo();
  };

  let signaling: CallSignaling | null = null;
  let stopWatching: (() => void) | null = null;
  let limitTimer = 0;
  let ended = false;
  const earlyCandidates: string[] = [];

  const end = () => {
    if (ended) return;
    ended = true;
    window.clearTimeout(limitTimer);
    if (control.readyState === 'open') control.send(END_MESSAGE);
    stopWatching?.();
    local?.getTracks().forEach((track) => track.stop());
    remoteStream.getTracks().forEach((track) => track.stop());
    peer.close();
    void signaling?.end().catch(() => undefined);
    onState('ended');
  };

  peer.onicecandidate = (e) => {
    if (!e.candidate) return;
    const json = JSON.stringify(e.candidate.toJSON());
    if (signaling) signaling.writeCandidate(json);
    else earlyCandidates.push(json);
  };

  peer.onconnectionstatechange = () => {
    if (peer.connectionState === 'connected') onState('connected');
    if (['failed', 'closed', 'disconnected'].includes(peer.connectionState)) end();
  };

  try {
    const offer = await peer.createOffer();
    await peer.setLocalDescription(offer);
    signaling = await createIntervention(studentId, emergencyId, uid, offer.sdp ?? '', nowMs);
  } catch (e) {
    ended = true;
    local?.getTracks().forEach((track) => track.stop());
    peer.close();
    throw e;
  }
  earlyCandidates.forEach((json) => signaling!.writeCandidate(json));

  let answered = false;
  const queued: RTCIceCandidateInit[] = [];
  stopWatching = signaling.watch({
    onAnswer: (sdp) => {
      if (answered) return;
      answered = true;
      void peer.setRemoteDescription({ type: 'answer', sdp }).then(() => {
        queued.forEach((c) => void peer.addIceCandidate(c));
        queued.length = 0;
      });
    },
    onTabletCandidate: (json) => {
      const candidate = JSON.parse(json) as RTCIceCandidateInit;
      if (answered && peer.remoteDescription) void peer.addIceCandidate(candidate);
      else queued.push(candidate);
    },
    onStatus: (status) => {
      if (status === 'ended' || status === 'expired') end();
    },
  });

  limitTimer = window.setTimeout(end, MAX_CALL_MS);
  onState('connecting');
  return { remoteStream, micAllowed: local !== null, end };
}
