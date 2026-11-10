# AGENTS.md — SMART-LIFE INCLUSION

AI-AR ecosystem for special education, vocational training and Assisted Daily Living (ADL).
Locked stack (Phase-I Progress Report wins over Concept Note; **no WebAR**):
- `unity-child-app/` — "Magic Lens" child app. Unity 6 (6000.6) with URP, Vuforia Engine (marker Image Targets), C#, Android 11+ tablets, >=30 FPS.
- `firebase/` — Realtime Database rules + seed. Write latency budget <500 ms.
- `web-supervisor-dashboard/` — React + Vite + TypeScript, Firebase JS SDK v10 (modular).
- `blender-assets/` — avatars/props, exported FBX into Unity.

## Team / ownership
| Area | Owner |
|---|---|
| Unity core, Vuforia, Firebase, Voice override | Mohammed Mahmud Parvez (0242310005101514) |
| UI/UX, Figma, Blender avatars/rigging, Web dashboard | Tahmidul Islam Mugdha (0242310005101913) |
Touching the other owner's area: keep the diff small and note it in the PR.

## Token budget (both devs on Claude Pro)
- Do NOT read `docs/*` up front. Open only the doc the task needs (map below).
- Path rules in `.claude/rules/` load only when matching files are touched.
- Prefer `Grep`/`Glob` over full-file reads. Never read `Library/`, `Temp/`, `*.fbx`, `*.blend`, `node_modules/`, `dist/`.
- Slash commands in `.claude/commands/` are on-demand. `/caveman` = terse replies.
- End long sessions with `/handoff` so the next session starts cold and cheap.

## Doc map (open on demand)
| Need | File |
|---|---|
| Scope, features, acceptance | `docs/PRD.md` |
| Colors, type, motion, low-sensory rules | `docs/DESIGN_SYSTEM.md` |
| Modules, data flow, state machine | `docs/ARCHITECTURE.md` |
| RTDB schema, paths, indexes | `docs/DATABASE.md` |
| Auth, rules, child data privacy | `docs/SECURITY.md` |
| Mascot, AAC board, hesitation/emergency flow, streaming | `docs/clinical_architecture.md` |
| Guardian consent (audio monitoring, emergency streaming) | `docs/GUARDIAN_CONSENT.md` |
| C# / TS conventions | `docs/CODE_STYLE.md` |
| Test strategy, FPS/latency gates | `docs/TESTING.md` |
| First APK, printing markers, on-device gate | `docs/DEVICE_BUILD.md` |
| Entity graph, how to update it | `docs/GRAPH.md` + `.claude/memory/knowledge-graph.json` |

## Core features (MVP)
1. **AR guidance state machine** — Vuforia Image Target → avatar + step overlay. ADL tasks (4-step handwashing, trash sorting) and hallway navigation arrows (left/right/straight). Deterministic: one marker = one state transition.
2. **Low-sensory UI** (Pavlov's heuristics) — pastel high-contrast palette, ONE cue on screen, zero flashing/looping attention animations, no 2D mini-maps.
3. **Automatic path tracking** — on marker found (`DefaultObserverEventHandler.OnTargetFound`) push `{studentId, checkpointId, markerId, taskStatus, timestamp}` to RTDB.
4. **Guardian voice override** — hands-free `skip step`, `reset task`, `emergency pause`, `resume`. Logged to `/overrides`.
5. **Supervisor dashboard** — live location, route progress, autonomous-navigation %, avg completion time, recent checkpoint log.

## Hard rules
- No flashing, strobing, or auto-playing looping animation anywhere (child safety, not style).
- One instructional cue visible at a time. Never add mini-maps.
- Never commit secrets: `google-services.json`, `.env*`, Vuforia license key, service-account JSON.
- Timestamps are server-side (`ServerValue.TIMESTAMP` / `serverTimestamp()`), never device clock.
- Student data is minors' data: no names in logs or analytics, IDs only. See `docs/SECURITY.md`.
- Unity scripts live under `Assets/_Project/Scripts/<Module>/`, namespace `SmartLife.<Module>`.
- State changes go through `Core/TaskStateMachine` only. AR, Voice and UI raise events; they never mutate state directly.
- RTDB schema changes: update `docs/DATABASE.md`, `firebase/database.rules.json`, `firebase/seed.json` and `web-supervisor-dashboard/src/types` in the same PR.

## Commands
```bash
# Web dashboard
cd web-supervisor-dashboard && npm i && npm run dev     # local
npm run build && npm run lint && npm test               # CI gate
# Firebase (needs firebase-tools)
firebase emulators:start --only database                # local RTDB on :9000
firebase database:set / firebase/seed.json --project <id>   # seed (dev project only)
firebase deploy --only database                         # rules
# Unity: open unity-child-app/ in Unity Hub (Unity 6, 6000.6.8f1). Tests: Window > General > Test Runner.
# CLI tests: Unity -batchmode -projectPath unity-child-app -runTests -testPlatform EditMode
```

## Git
- Branches: `feat/<area>-<topic>`, `fix/<area>-<topic>`; area ∈ `unity|web|firebase|assets|docs`.
- Conventional commits: `feat(unity): ...`, `fix(web): ...`.
- Unity: Force Text serialization + Visible Meta Files. Always commit `.meta` files.
- Large binaries (`*.fbx`, `*.blend`, textures) go through Git LFS.

## Definition of done
- Builds clean; tests in `docs/TESTING.md` pass for the touched area.
- Low-sensory checklist (`/ui-audit`) passes for any UI change.
- Knowledge graph updated if a new module, path, or entity was added (`/graphify`).
