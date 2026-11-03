---
description: Inspect a built Android APK/AAB for size, perms, perf red flags.
argument-hint: <path-to-apk>
---
Target: $ARGUMENTS
Run (skip any tool not installed, say so):
1. `ls -lh` size; flag >150 MB.
2. `aapt dump badging` → package, minSdk (must be 29 = Android 10), targetSdk, ABIs (expect arm64-v8a).
3. `aapt dump permissions` → expect CAMERA, INTERNET, RECORD_AUDIO only; flag extras (location, contacts, storage).
4. `unzip -l` → top 15 largest entries; flag uncompressed textures, duplicate Vuforia DBs, debug symbols.
5. Check `google-services.json` / license keys are not embedded in plain text beyond what the SDK needs.
Report as a table + top 3 actions.
