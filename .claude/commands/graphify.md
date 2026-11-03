---
description: Update the local knowledge graph from recent changes.
argument-hint: [path]
---
Scope: ${ARGUMENTS:-files changed in `git diff --name-only main...HEAD`}.
Follow `docs/GRAPH.md`. For each new/renamed module, script, RTDB path, scene, asset or doc:
- add/update entity in `.claude/memory/knowledge-graph.json` (`name`, `entityType`, `observations` <=3 short strings),
- add relations (`depends_on`, `writes_to`, `reads_from`, `owned_by`, `renders`, `specified_in`).
Remove entities whose files were deleted. Keep JSON valid and sorted by name. Reply with counts: +added ~updated -removed.
