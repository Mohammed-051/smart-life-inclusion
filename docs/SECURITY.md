# Security & Privacy
Users are minors with disabilities: treat all student data as sensitive.

## Data minimisation
- Store `alias` (e.g. "Student A") not legal names. No photos, audio recordings, or camera frames leave the device.
- Voice recognition: process on-device where possible; never store audio or transcripts, only the resolved command.
- Logs/analytics/console: IDs only.

## Auth
- Firebase Auth email/password for supervisors; one dedicated account per tablet (mapped in `/devices/{uid}`).
- Roles in `/supervisors/{uid}.role`; created by admin only (console or admin script), never self-assigned.
- Guardian menu on tablet protected by 4-digit PIN (stored hashed locally, not in RTDB).

## Database rules (see `firebase/database.rules.json`)
- Default deny at root.
- Tablet may write only its own `studentId` subtree; logs append-only and server-timestamped.
- Supervisors read only students in their `students` map; admin reads all.
- Validate types, enums, and field sets on every write (`$other: .validate false`).

## Secrets
- Never commit: `google-services.json`, `.env*`, Vuforia license key, service-account JSON. Use `.env.example` with placeholders.
- Vuforia key loaded from a git-ignored `VuforiaConfiguration` asset or CI secret.
- Firebase web config is public by design; security relies on rules + Auth, not hiding it.

## Process
- Rules tested in emulator before deploy; deploys by a human only.
- Ethics: guardian consent form before field test; data deleted at project end.
- Dependabot / `npm audit` on dashboard each sprint.
