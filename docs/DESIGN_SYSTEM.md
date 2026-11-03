# Design System — Low-Sensory (Pavlov's heuristics)
Goal: lowest cognitive and sensory load that still guides. One cue, calm colour, predictable motion.

## Colour tokens (pastel, high contrast on text)
| Token | Hex | Use |
|---|---|---|
| `--bg` | `#F7F5F0` | App/background panels (warm off-white, no pure white glare) |
| `--ink` | `#1F2A37` | Text, icons (contrast 13:1 on bg) |
| `--go` | `#7FC8A9` | "Do this" / correct / forward arrow fill |
| `--turn` | `#8FB8DE` | Left/right navigation arrows |
| `--wait` | `#F3D27A` | Pause / hold on |
| `--help` | `#E8A0A0` | Needs adult (never "error red") |
| `--surface` | `#FFFFFF` @ 85% | Instruction card behind text |
| `--muted` | `#6B7280` | Dashboard secondary text only |
Arrows/icons always carry a `--ink` 3 px outline so pastel fills keep >=3:1 against camera feed.

## Typography
- Child app: Atkinson Hyperlegible (or Lexend), 40–56 sp prompts, sentence case, <=6 words.
- Dashboard: Inter / system UI, 14–16 px body, 24 px headings.

## Layout (child app)
- One instruction card, bottom third, centred. One 3D cue (avatar or arrow) on the marker.
- Progress shown as N filled dots (max 6), top-centre, no numbers needed.
- Touch targets >=64 dp. No text input for the child.

## Motion
- Allowed: fade/slide 200–300 ms ease-out on state change; avatar demo clip for current step.
- Banned: flashing (>3 Hz), blinking, pulsing glow, looping bounce, confetti/particles, camera shake, auto-rotating models.
- Success = soft chime (optional, volume-capped) + card fade. No celebration bursts.

## Audio
- Calm single-voice TTS/recorded prompt, optional, toggle in guardian menu. No background music.

## Navigation cues
- Single arrow: left, right, straight, or "arrived" check. Never a 2D mini-map or multiple arrows.

## Dashboard
- Same palette; status chips use `--go` (on track), `--wait` (paused), `--help` (needs help). Light theme default.

## Low-sensory checklist
- [ ] Only palette tokens used
- [ ] Text contrast >=4.5:1, icon contrast >=3:1
- [ ] Exactly one instructional cue visible
- [ ] No flashing / looping attention animation
- [ ] Transitions <=300 ms, opacity/position only
- [ ] No mini-map
- [ ] Touch targets >=64 dp (child) / 44 px (web)
- [ ] Child prompt <=6 words, positive phrasing, icon + text
