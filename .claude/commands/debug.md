---
description: Structured root-cause debugging.
argument-hint: <symptom / error>
---
Symptom: $ARGUMENTS

1. Reproduce: state exact steps/command. If can't reproduce, say what's needed.
2. Hypotheses: max 3, ranked by likelihood, each with a cheap test.
3. Test cheapest first (logs, Grep, single test). Unity: check Console + `adb logcat -s Unity`. Web: browser console + emulator UI.
4. Fix root cause, not symptom. Add a regression test where feasible.
5. Report: cause (1 line), fix (diff), proof (test/log output).
Common suspects: Vuforia license/key missing, marker DB not in StreamingAssets, RTDB rules rejecting write, listener not unsubscribed, main-thread Firebase callback.
