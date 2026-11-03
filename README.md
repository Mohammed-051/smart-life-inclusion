# SMART-LIFE INCLUSION
An AI-AR Powered Ecosystem for Holistic Special Education, Vocational Training, and Assisted Daily Living (ADL). Final Year Design Project.

| Folder | What |
|---|---|
| `unity-child-app/` | "Magic Lens" Android tablet app (Unity 2022.3 LTS + Vuforia) |
| `web-supervisor-dashboard/` | Supervisor dashboard (React + Vite + TS + Firebase) |
| `firebase/` | Realtime Database rules + seed data |
| `blender-assets/` | 3D avatars and props |
| `docs/` | PRD, design system, architecture, DB, security, style, testing, graph |

Team: Mohammed Mahmud Parvez (Unity/AR/Firebase/Voice) · Tahmidul Islam Mugdha (UI/UX/3D/Web).
AI agents: start at `AGENTS.md`.

## Quick start
```bash
cd web-supervisor-dashboard && cp .env.example .env && npm i && npm run dev
firebase emulators:start --only database,auth   # from repo root
```
Unity: install Vuforia + Firebase packages per `unity-child-app/Packages/README.md`, then open the folder in Unity Hub.
