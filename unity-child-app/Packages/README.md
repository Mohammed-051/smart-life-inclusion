# Packages not in the public registry (add locally, do not commit tarballs)
1. **Vuforia Engine** 10.x: download `com.ptc.vuforia.engine-<ver>.tgz` from developer.vuforia.com,
   put it in `Packages/` (git-ignored via LFS or kept local) and add to manifest:
   `"com.ptc.vuforia.engine": "file:com.ptc.vuforia.engine-<ver>.tgz"`.
2. **Firebase Unity SDK** (Auth + Realtime Database) 12.x: import via `.tgz` with External Dependency Manager
   (`com.google.external-dependency-manager`, `com.google.firebase.app`, `com.google.firebase.auth`, `com.google.firebase.database`).
3. Place `google-services.json` in `Assets/` locally (git-ignored).
4. Vuforia license key: Project > Vuforia Engine > Configuration (keep the generated asset out of git or use CI secret).
