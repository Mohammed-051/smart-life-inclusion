# PRD — SMART-LIFE INCLUSION
*An AI-AR Powered Ecosystem for Holistic Special Education, Vocational Training, and Assisted Daily Living (ADL).*
Stack locked by Phase-I Progress Report: Unity 6 (`6000.6.8f1`, URP) + Vuforia Engine 11.4.4 (Android tablet), Firebase RTDB, web supervisor dashboard. WebAR is out of scope.

## Users
| Persona | Need |
|---|---|
| Child / learner (neurodivergent, special-needs) | Calm, single-step guidance to finish a daily task or reach a room independently. |
| Teacher / guardian (on-site) | Help without taking the tablet; skip/reset/pause by voice. |
| Supervisor (school staff, remote) | See where each learner is and how independent they are becoming. |

## Features & acceptance criteria
### F1 Deterministic AR guidance + sequential state machine
- Each Vuforia Image Target maps to exactly one step/checkpoint (ScriptableObject config).
- ADL: 4-step handwashing (wet → soap → rub 20 s → rinse/dry); trash sorting (identify → choose bin → drop → confirm).
- Navigation: hallway markers show a single arrow (left/right/straight) or "You arrived".
- AC: avatar anchored jitter-free on TRACKED; scanning an out-of-order marker never advances state (shows "Find the <next> sign").
### F2 Low-sensory UI (Pavlov's heuristics)
- Pastel high-contrast palette; one cue on screen; no flashing, no mini-map; prompts <=6 words.
- AC: `/ui-audit` passes on every child-facing screen.
### F3 Automatic path tracking
- On marker found, write `{studentId, checkpointId, markerId, taskStatus, timestamp}` to `/checkpointLogs`.
- AC: p95 write latency <500 ms on school Wi-Fi; offline writes queue and sync.
### F4 Guardian voice override
- Commands: "skip step", "reset task", "emergency pause", "resume".
- AC: >=90% recognition on the 4 phrases in a quiet classroom; pause freezes UI within 1 s; every override logged.
### F5 Supervisor dashboard
- Live location (last checkpoint), route progress %, autonomous navigation %, average completion time, recent checkpoint log (last 50).
- AC: updates appear <2 s after a tablet write; usable at 360 px width.

## Metrics
- Autonomous navigation % = checkpoints reached with `autonomous=true` / total checkpoints reached.
- Avg completion time = mean(`completedAt - startedAt`) over completed sessions per task.

## Non-functional
- >=30 FPS on mid-range Android 10+ tablet (e.g. 4 GB RAM, Helio G-class / SD 6xx).
- APK <150 MB. Permissions: CAMERA, INTERNET, RECORD_AUDIO only.
- Minors' privacy: IDs only in telemetry; see SECURITY.md.

## Out of scope (prototype)
WebAR, markerless/SLAM navigation, 2D maps, AI-generated content at runtime, iOS build, parent mobile app.

## Milestones
| Sprint | Deliverable |
|---|---|
| S1 | Repo, schema, rules, Vuforia scene with 1 marker + avatar |
| S2 | State machine + handwashing task + telemetry |
| S3 | Hallway navigation route + voice override |
| S4 | Dashboard metrics + live view |
| S5 | Field test, perf tuning, Phase-II report |
