# Firebase Realtime Database schema
All timestamps are server ms epoch. Keys are opaque IDs (`stu_001`, `rt_hall_a`). No names in logs.

```
/students/{studentId}         { alias, grade, routeIds: {rt_x: true}, active: bool }
/devices/{authUid}            { studentId, label }
/supervisors/{authUid}        { role: "teacher"|"guardian"|"admin", students: {stu_x: true} }
/routes/{routeId}             { name, kind: "nav"|"adl", version, checkpoints: {cpId: {order, markerId, label, cue,
                                enabled?: bool (default true), prompt?, holdSeconds?, aacBoardId?}} }
/sessions/{studentId}/{sessionId}
                              { routeId, startedAt, completedAt?, status: "active"|"completed"|"aborted",
                                checkpointsReached, autonomousCount, activeDurationMs?, hesitationTotalMs?, wrongScanCount? }
/nodeMetrics/{studentId}/{sessionId}/{id}  (append-only, one per step attempt)
                              { checkpointId, stepIndex, hesitationMs, holdMs, wrongScans, aacOpened, aacSelection?,
                                outcome: "completed"|"skipped"|"reset", autonomous, timestamp }
/checkpointLogs/{studentId}/{logId}        (append-only, push id)
                              { studentId, sessionId, routeId, checkpointId, markerId,
                                taskStatus: "started"|"completed"|"skipped"|"reset"|"paused"|"resumed",
                                autonomous: bool, deviceId, timestamp }
/liveStatus/{studentId}       { sessionId, routeId, checkpointId, progressPct, state, lastSeen, stepHesitationMs?, wrongScanCount?,
                                aacRequest?: "too_loud"|"need_help"|"show_me"|"break" (current step only) }
/overrides/{studentId}/{id}   { command: "skip"|"reset"|"pause"|"resume", source: "voice"|"menu"|"dashboard",
                                issuedBy, timestamp, consumed: bool }
```

## Indexes
- `checkpointLogs/$studentId`: `.indexOn: ["timestamp"]`
- `sessions/$studentId`: `.indexOn: ["startedAt", "status"]`
- `overrides/$studentId`: `.indexOn: ["consumed"]`
- `nodeMetrics/$studentId/$sessionId`: `.indexOn: ["timestamp"]`

## Write paths
| Writer | Paths |
|---|---|
| Tablet (device uid) | `checkpointLogs`, `nodeMetrics`, `liveStatus`, `sessions`, `overrides` (voice/menu), set `overrides/*/consumed` |
| Supervisor | `overrides` (dashboard source), `routes` (any supervisor publishes, admin deletes), `students` (admin) |
| Admin only | `devices`, `supervisors` |

## Metrics derivation
- Autonomous % (per student) = Σ`autonomousCount` / Σ`checkpointsReached` over sessions.
- Avg completion time = mean(`completedAt - startedAt`) for `status == completed`.
- Session duration = `completedAt - startedAt`, or server now minus `startedAt` while open. `activeDurationMs` excludes pauses.
- Hesitation = sum of `nodeMetrics.hesitationMs` for finished steps, plus `liveStatus.stepHesitationMs` for the current step.
- Location = `/liveStatus/{id}.checkpointId` → `routes/{routeId}/checkpoints/{cp}.label`.

## Change protocol
Update this file, `firebase/database.rules.json`, `firebase/seed.json`, `web-supervisor-dashboard/src/types/` in one PR.
