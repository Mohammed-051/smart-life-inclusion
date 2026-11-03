---
description: Write a compact handoff so the next session (or teammate) starts cheap.
---
Write `.claude/memory/HANDOFF.md` (overwrite) with max 30 lines:
- Goal of this session (1 line)
- Done: bullet list with file paths
- In progress: exact next step + file:line
- Decisions made + why (1 line each)
- Open questions / blockers
- Commands to resume
Then update `.claude/memory/knowledge-graph.json` for any new module/entity (see `docs/GRAPH.md`).
Reply only: "Handoff written."
