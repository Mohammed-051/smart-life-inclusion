import type { Emergency } from '../../types';

/** Why the Start button is locked, or null when it may be used. Mirrors the tablet's StreamPolicy (docs/SECURITY.md). */
export function streamBlockReason(emergency: Emergency, consentRecorded: boolean, streaming: boolean): string | null {
  if (streaming) return 'A call is already running.';
  if (emergency.status === 'resolved') return 'This emergency is resolved.';
  if (emergency.source !== 'sos') return 'Only an SOS press unlocks a call.';
  if (!consentRecorded) return "No guardian consent is recorded for this learner.";
  return null;
}
