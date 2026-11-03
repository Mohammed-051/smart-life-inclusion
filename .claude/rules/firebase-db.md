---
paths:
  - "firebase/**"
  - "**/Telemetry/**"
  - "web-supervisor-dashboard/src/lib/services/**"
  - "web-supervisor-dashboard/src/types/**"
---
# Firebase RTDB rules
- Schema source of truth: `docs/DATABASE.md`. Change it, `database.rules.json`, `seed.json`, and `src/types` together.
- Checkpoint logs are append-only (`push()`), never overwritten. Rules enforce `!data.exists()`.
- Timestamps: `ServerValue.TIMESTAMP` (Unity) / `serverTimestamp()` (web). Rules validate `newData.val() == now`.
- Keep payloads flat and small (<1 KB) to stay under the 500 ms write budget.
- Add `.indexOn` for every field the dashboard orders or queries by.
- Test rules against the emulator before any deploy. Never deploy from an agent session.
