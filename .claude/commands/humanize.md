---
description: Rewrite text for humans (report, child prompt, guardian UI, viva slide).
argument-hint: <audience> <text|file>
---
Rewrite: $ARGUMENTS
Audiences:
- `child`: <=6 words, present tense, positive ("Rub your hands"), no negatives, no idioms.
- `guardian`: plain, calm, action-first, <=2 sentences.
- `report`: academic but readable; active voice; cite Phase-I report terms; no buzzword stacking.
- `slide`: <=6 bullets, <=8 words each.
Keep meaning and technical facts exact. Output only the rewritten text.
