# Firebase Realtime Database schema
All timestamps are server ms epoch. Keys are opaque IDs (`stu_001`, `rt_hall_a`). No names in logs.

```
/students/{studentId}         { alias, grade, routeIds: {rt_x: true}, active: bool }
/devices/{authUid}            { studentId, label }
/supervisors/{authUid}        { role: "teacher"|"guardian"|"admin", students: {stu_x: true} }
/routes/{routeId}             { name, kind: "nav"|"adl", checkpoints: {cpId: {order, markerId, label, cue}} }
/sessions/{studentId}/{sessionId}
                              { routeId, startedAt, completedAt?, status: "active"|"completed"|"aborted",
                                checkpointsReached, autonomousCount }
/checkpointLogs/{studentId}/{logId}        (append-only, push id)
                              { studentId, sessionId, routeId, checkpointId, markerId,
                                taskStatus: "started"|"completed"|"skipped"|"reset"|"paused"|"resumed",
                                autonomous: bool, deviceId, timestamp }
/liveStatus/{studentId}       { sessionId, routeId, checkpointId, progressPct, state, lastSeen }
/overrides/{studentId}/{id}   { command: "skip"|"reset"|"pause"|"resume", source: "voice"|"menu"|"dashboard",
                                issuedBy, timestamp, consumed: bool }
```

## Indexes
- `checkpointLogs/$studentId`: `.indexOn: ["timestamp"]`
- `sessions/$studentId`: `.indexOn: ["startedAt", "status"]`
- `overrides/$studentId`: `.indexOn: ["consumed"]`

## Write paths
| Writer | Paths |
|---|---|
| Tablet (device uid) | `checkpointLogs`, `liveStatus`, `sessions`, `overrides` (voice/menu), set `overrides/*/consumed` |
| Supervisor | `overrides` (dashboard source), `students`, `routes` (admin) |
| Admin only | `devices`, `supervisors` |

## Metrics derivation
- Autonomous % (per student) = Σ`autonomousCount` / Σ`checkpointsReached` over sessions.
- Avg completion time = mean(`completedAt - startedAt`) for `status == completed`.
- Location = `/liveStatus/{id}.checkpointId` → `routes/{routeId}/checkpoints/{cp}.label`.

## Change protocol
Update this file, `firebase/database.rules.json`, `firebase/seed.json`, `web-supervisor-dashboard/src/types/` in one PR.
