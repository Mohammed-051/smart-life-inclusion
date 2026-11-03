# Knowledge graph
File: `.claude/memory/knowledge-graph.json`. Read by the `memory` MCP server (`.mcp.json`) or directly with Grep — cheaper than re-reading docs.

## Format (MCP memory server compatible)
```json
{ "entities": [{ "name": "TaskStateMachine", "entityType": "unity_script", "observations": ["Core/TaskStateMachine.cs", "single writer of task state"] }],
  "relations":  [{ "from": "MarkerObserver", "to": "TaskStateMachine", "relationType": "depends_on" }] }
```

## Entity types
`app`, `module`, `unity_script`, `scriptable_object`, `scene`, `rtdb_path`, `web_component`, `web_service`, `asset`, `doc`, `person`, `feature`.

## Relation types
`depends_on`, `writes_to`, `reads_from`, `renders`, `owned_by`, `specified_in`, `implements`, `part_of`.

## Rules
- Max 3 observations per entity, each <80 chars (path + role).
- Update with `/graphify` after adding/renaming/deleting modules, RTDB paths, scenes, assets.
- Query before exploring: `grep -n '"name": "X"' .claude/memory/knowledge-graph.json`.
