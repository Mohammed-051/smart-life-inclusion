---
paths:
  - "web-supervisor-dashboard/**"
---
# Web dashboard rules (React + Vite + TS)
- TypeScript strict. No `any`; RTDB payloads validated with zod schemas in `src/lib/validation/`.
- Firebase access only in `src/lib/firebase/` (init) and `src/lib/services/` (reads/writes). Components never import `firebase/*` directly.
- Shared shapes in `src/types/` must match `docs/DATABASE.md`.
- Listeners: `onValue`/`onChildAdded` with `limitToLast(n)`; always unsubscribe in effect cleanup.
- Metrics (autonomous %, avg completion time) computed in `src/lib/services/metrics.ts` as pure functions with unit tests.
- Use design tokens from `src/app/tokens.css` (mirrors `docs/DESIGN_SYSTEM.md`). No raw hex in components.
- Responsive: works from 360 px phone to 1440 px desktop. WCAG AA contrast.
- Never render student names in URLs or console logs.
