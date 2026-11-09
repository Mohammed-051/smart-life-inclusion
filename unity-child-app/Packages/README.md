# Packages and SDKs
Editor: Unity 6 `6000.6.8f1` with Android Build Support. Registry packages are pinned in `manifest.json` (URP 17.6.0, WebRTC 3.0.0, Input System, Test Framework).

1. **Vuforia Engine** 11.4.4: `com.ptc.vuforia.engine-11.4.4.tgz` is committed here through Git LFS and referenced in
   `manifest.json` as `"file:com.ptc.vuforia.engine-11.4.4.tgz"`. Run `git lfs pull` after cloning. To upgrade, download
   the new `.tgz` from developer.vuforia.com, replace this file and update the manifest entry.
2. **Firebase Unity SDK** 14.1.0 (App, Auth, Realtime Database): imported as `.unitypackage` into `Assets/Firebase/`, with
   External Dependency Manager 1.2.190 in `Assets/ExternalDependencyManager/`. Both are committed. Upgrade by importing the
   newer packages over them.
3. Place `google-services.json` in `Assets/` locally (git-ignored).
4. Vuforia license key: Project > Vuforia Engine > Configuration. The generated `Assets/Resources/VuforiaConfiguration.asset`
   is git-ignored; use a CI secret for builds.
