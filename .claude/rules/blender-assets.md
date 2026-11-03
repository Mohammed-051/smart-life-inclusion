---
paths:
  - "blender-assets/**"
  - "unity-child-app/Assets/_Project/Prefabs/**"
---
# Blender asset rules
- Units: metric, 1 unit = 1 m, +Y up in Unity (export FBX with "Apply Transform", forward -Z, up Y).
- Budget per avatar: <=15k tris, 1 material, 1 texture atlas <=2048 px, <=60 bones (Humanoid-compatible).
- Pastel, flat or soft-shaded materials per `docs/DESIGN_SYSTEM.md`. No emissive pulsing, no high-frequency patterns.
- Naming: `AV_<Name>_v<NN>.fbx`, `PR_<Name>_v<NN>.fbx`; animation clips `<Avatar>@<Action>`.
- Source `.blend` in `Avatars/` or `Props/`; exports only in `Exports_FBX/`. Both via Git LFS.
- Agents must not open `.blend`/`.fbx`; describe changes in text and let the owner apply them.
