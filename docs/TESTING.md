# Testing

## Gates per area
| Area | Must pass before merge |
|---|---|
| Unity | EditMode tests (state machine, marker ordering, voice phrase mapping); project compiles for Android |
| Web | `npm run lint && npm run build && npm test` (Vitest) |
| Firebase | Rules tests on emulator (`@firebase/rules-unit-testing`) |
| UI change | `/ui-audit` passes |

## Unity
- EditMode (`Assets/_Project/Tests/Editor`): `TaskStateMachine` transitions incl. out-of-order marker, skip/reset/pause; `VoiceCommandRouter` synonyms; payload builder produces schema-valid `CheckpointLog`.
- PlayMode: simulate `MarkerDetected` events through a full handwashing task.
- Device (manual, each sprint): printed markers at 30–100 cm, 3 lighting conditions; log FPS via Unity Profiler/`Application.targetFrameRate` overlay — **min 30 FPS, p95 frame time <33 ms** over a 5-min run.

## Telemetry latency
- Script logs client send → RTDB server timestamp delta; **p95 <500 ms** over 100 writes on school Wi-Fi. Test offline → online sync.

## Voice
- 4 commands × 20 utterances × 2 speakers in a quiet classroom; **>=90%** correct, **0** false "reset" triggers in 10 min of classroom noise.

## Web
- Unit: `metrics.ts` (autonomous %, avg time) with fixed fixtures from `firebase/seed.json`.
- Component: ActivityLog renders last 50, KpiTile formats.
- Manual: 360 px / 768 px / 1440 px; update visible <2 s after tablet write.

## Rules
- Tablet cannot write another student's logs; cannot overwrite a log; supervisor cannot read unassigned student; unauthenticated denied.

## Usability (field test)
- SUS questionnaire for teachers; task completion & assistance count per child, consent-based.

> Prototype uses Unity's predefined assemblies (no `.asmdef`) so the Vuforia/Firebase SDKs resolve without reference wiring. EditMode tests live in `Assets/_Project/Tests/Editor/` (compiled into Assembly-CSharp-Editor). Introduce asmdefs later if compile times grow.
