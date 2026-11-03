---
description: Get-Shit-Done. Execute task end-to-end without pausing.
argument-hint: <task>
---
Task: $ARGUMENTS

Execute in one pass:
1. Restate goal in one line. Pick the most likely reading of anything ambiguous; note it.
2. Locate touched files with Grep/Glob (no broad reads). Open only the doc from AGENTS.md doc map that applies.
3. Implement. Follow path rules. Keep diff minimal.
4. Run the area's checks (AGENTS.md › Commands). Fix failures.
5. Report: files changed, checks run + result, any assumption. <10 lines.
Never ask for confirmation except for: deploys, deleting data, force-push.
