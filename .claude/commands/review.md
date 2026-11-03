---
description: Review current diff against project rules.
argument-hint: [branch|path]
---
Review `git diff ${ARGUMENTS:-main...HEAD}`. Report only real issues, ranked, as `file:line — issue — fix`:
1. Correctness bugs, null refs, unhandled async errors.
2. Child-safety: flashing/looping animation, >1 simultaneous cue, mini-map, PII in logs.
3. State only mutated via `TaskStateMachine`; Firebase only via services.
4. Schema drift between `docs/DATABASE.md`, rules, seed, `src/types`.
5. Perf: per-frame allocations, `Find`/`GetComponent` in `Update`, unbounded RTDB listeners.
6. Secrets committed.
No style nitpicks a linter would catch. If clean: "LGTM" + 1 line.
