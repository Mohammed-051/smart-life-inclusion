---
description: Audit UI (Unity or web) against low-sensory design system.
argument-hint: <file|screen>
---
Audit $ARGUMENTS against `docs/DESIGN_SYSTEM.md` (read only the "Low-sensory checklist" section). Output a pass/fail table:
| Check | Pass? | Evidence (file:line) |
Checks: palette tokens only; contrast >=4.5:1 text / 3:1 icons; one cue visible; no flashing (>3 Hz) or looping attention animations;
transitions <=300 ms ease-out, opacity/position only; no mini-map; touch targets >=64 dp (child) / 44 px (web);
audio prompts calm + optional; text <=6 words per child prompt; icons paired with text.
End with fixes for each fail.
