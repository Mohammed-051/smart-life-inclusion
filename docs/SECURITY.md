# Security & Privacy
Users are minors with disabilities: treat all student data as sensitive.

## Data minimisation
- Store `alias` (e.g. "Student A") not legal names. No photos, audio recordings, or camera frames leave the device, except under the emergency streaming exception below.
- Voice recognition: process on-device where possible; never store audio or transcripts, only the resolved command.
- Distress detection: microphone audio is processed in memory on the device by edge AI and level checks. It is never stored or sent.
- Logs/analytics/console: IDs only.

## Emergency streaming exception
Two-way encrypted WebRTC audio and video may leave the tablet only when **all** of these hold:
1. `EmergencyDetector` raised an emergency (audio, IMU or SOS source) that is still open. Other pauses, including the voice "emergency pause", do not qualify.
2. The student's guardian has signed the streaming section of `docs/GUARDIAN_CONSENT.md`, and an admin has recorded it.
3. A supervisor assigned to that student started the stream. Detection unlocks streaming but never starts it.

Requirements while a stream runs:
- Encrypted with DTLS-SRTP. Any TURN relay carries encrypted media only.
- No recording, screenshots, stored frames or transcripts on the tablet, dashboard or backend.
- The tablet shows a steady, non-flashing "camera on" indicator, and an adult at the tablet can end the stream.
- The stream ends, and the camera and microphone are released, when the emergency is resolved, when either side disconnects, or after 15 minutes. A new stream needs a new supervisor action.
- Logs hold metadata only: student ID, supervisor ID, emergency ID, start and end time.

**Status:** policy only. No streaming code exists, and the database rules do not enforce these conditions yet. Do not enable streaming until they do (`docs/clinical_architecture.md` section 2.4).

## Auth
- Firebase Auth email/password for supervisors; one dedicated account per tablet (mapped in `/devices/{uid}`).
- Roles in `/supervisors/{uid}.role`; created by admin only (console or admin script), never self-assigned.
- Guardian menu on tablet protected by 4-digit PIN: salted PBKDF2-SHA256 hash in local app storage, never in RTDB or source. Five wrong tries lock it for 30 s, doubling up to 15 min. A forgotten PIN needs the app's data cleared, followed by a new tablet sign-in.
- The tablet account's password is typed by a guardian and goes only to Firebase Auth. The Firebase SDK keeps the session on the device, and later boots go straight to the task. Only the learner's opaque ID is cached.
- Disable public email sign-up in the Firebase console if possible. Rules only trust accounts listed in `/devices` or `/supervisors`, so a self-registered account reads nothing.

## Database rules (see `firebase/database.rules.json`)
- Default deny at root.
- Tablet may write only its own `studentId` subtree; logs append-only and server-timestamped. Telemetry cannot be deleted through rules.
- Routine data under `/routes` is readable only by accounts in `/devices` or `/supervisors`.
- Supervisors read only students in their `students` map; admin reads all.
- Validate types, enums, and field sets on every write (`$other: .validate false`).

## Secrets
- Never commit: `google-services.json`, `.env*`, Vuforia license key, service-account JSON. Use `.env.example` with placeholders.
- Vuforia key loaded from a git-ignored `VuforiaConfiguration` asset or CI secret.
- Firebase web config is public by design; security relies on rules + Auth, not hiding it.

## Process
- Rules tested in emulator before deploy; deploys by a human only.
- Ethics: signed guardian consent (`docs/GUARDIAN_CONSENT.md`) before field test; data deleted at project end.
- Dependabot / `npm audit` on dashboard each sprint.
