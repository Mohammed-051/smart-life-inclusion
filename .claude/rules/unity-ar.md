---
paths:
  - "unity-child-app/**/*.cs"
  - "unity-child-app/**/*.unity"
  - "unity-child-app/**/*.prefab"
  - "unity-child-app/**/*.asset"
---
# Unity AR rules (Unity 6 + URP + Vuforia)
- Namespace `SmartLife.<Module>` matching folder: AR, Core, Telemetry, Voice, UI.
- `Core/TaskStateMachine` is the only writer of task state. Others raise C# events or call its public API.
- Marker detection: extend/subscribe to `DefaultObserverEventHandler` (`OnTargetFound`/`OnTargetLost`). Debounce: ignore re-detections of the same marker within 2 s.
- Only act on `TargetStatus.Status == TRACKED` (not EXTENDED_TRACKED) for state transitions.
- Marker → step mapping lives in ScriptableObjects (`TaskDefinition`, `RouteDefinition`), never hard-coded strings.
- Perf: target >=30 FPS on mid-range Android 10+. No `Find*`/`GetComponent` in `Update`; cache refs in `Awake`. No per-frame allocations (LINQ, string concat) in hot paths.
- Avatar animations: idle + one demo clip per step. No looping attention-grab motion, no particle bursts, no camera shake.
- Firebase calls only via `Telemetry/TelemetryService`. Never block the main thread; use async + `ContinueWithOnMainThread`.
- Voice: `Voice/VoiceCommandRouter` maps phrases → `OverrideCommand` enum → state machine. Recognition runs only while a guardian toggle is on.
- Serialize with `[SerializeField] private`; no public fields.
- Every new script: EditMode test in `Assets/_Project/Tests/Editor/` when logic is non-trivial.
