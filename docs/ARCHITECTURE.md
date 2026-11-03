# Architecture

```
[Vuforia Image Target] --OnTargetFound--> AR/MarkerObserver
        |                                        |
        v                                        v (MarkerDetected event)
  AR/AvatarPresenter <-- StateChanged -- Core/TaskStateMachine <-- OverrideCommand -- Voice/VoiceCommandRouter
        ^                                        |                                         ^
  UI/InstructionCard <-- StateChanged -----------+--> Telemetry/TelemetryService --> Firebase RTDB
                                                                                    |
                                     web-supervisor-dashboard (React) <-- onValue --+
```

## Unity modules (`Assets/_Project/Scripts/`)
| Module | Key types | Responsibility |
|---|---|---|
| `AR` | `MarkerObserver : DefaultObserverEventHandler`, `AvatarPresenter`, `ArrowPresenter` | Detect markers (TRACKED only, 2 s debounce), raise `MarkerDetected(markerId)`, render cue for current state. |
| `Core` | `TaskStateMachine`, `TaskDefinition` (SO), `RouteDefinition` (SO), `SessionContext` | Single source of truth for step index & status. Validates marker order. |
| `Telemetry` | `TelemetryService`, `CheckpointLog`, `FirebaseBootstrap` | Auth, offline persistence, append logs, update `/liveStatus`, listen to `/overrides`. |
| `Voice` | `VoiceCommandRouter`, `IVoiceRecognizer`, `AndroidSpeechRecognizer` | Map guardian phrases to `OverrideCommand {Skip, Reset, Pause, Resume}`. |
| `UI` | `InstructionCard`, `ProgressDots`, `GuardianMenu` | Low-sensory overlays; guardian PIN menu (student select, voice toggle). |

## State machine
States: `Idle → AwaitingMarker(step i) → StepActive(i) → StepComplete(i) → … → TaskComplete`; `Paused` reachable from any state, returns to previous.
- `MarkerDetected(m)`: if `m == expected(i)` → `StepActive(i)`, log `started`; else ignore + hint.
- Step done (timer/tap/next marker) → `StepComplete(i)`, log `completed`.
- `Skip` → log `skipped`, `autonomous=false`, advance. `Reset` → i=0, log `reset`. `Pause/Resume` → toggle `Paused`, log override.
Navigation routes reuse the same machine: each hallway marker is a step whose cue is an arrow.

## Voice
Android `SpeechRecognizer` via a small Java/Kotlin plugin (offline language pack preferred), wrapped by `IVoiceRecognizer` so it can be stubbed in Editor. Keyword match with synonyms; confidence threshold 0.6. Active only when guardian enables it.

## Firebase
Unity Firebase SDK (Auth + Database). `SetPersistenceEnabled(true)`; writes are fire-and-forget with error log. Dashboard uses Firebase JS SDK v10 modular. Schema: `DATABASE.md`.

## Web dashboard (`web-supervisor-dashboard/src`)
- `lib/firebase/` init + auth; `lib/services/` (`students.ts`, `checkpoints.ts`, `metrics.ts`); `lib/validation/` zod schemas; `types/` shared models; `components/` (StudentCard, RouteProgress, ActivityLog, KpiTile); `app/` routes/pages.

## Environments
`dev` (emulator), `staging` (Firebase project `smart-life-dev`), `prod` (field test). Config via `.env` (`VITE_FIREBASE_*`) and `google-services.json` (not committed).
