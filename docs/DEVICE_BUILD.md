# Device build: first APK and the Phase 1 gate

How to build the V1 Android APK and prove AR tracking works on the physical tablet.

## 1. One-time setup
1. Unity 6 `6000.6.8f1` with **Android Build Support** (OpenJDK, SDK and NDK ticked). Run `git lfs pull`.
2. Put `google-services.json` in `unity-child-app/Assets/` (git-ignored).
3. Vuforia license: **Project > Vuforia Engine > Configuration > App License Key**. Paste your free Developer key. The generated `Assets/Resources/VuforiaConfiguration.asset` is git-ignored, so the key never reaches git.
4. **SMART-LIFE > Apply V1 Android Settings**. This sets:
   - package `com.nocodenomads.smartlife`, company NoCodeNomads, product "Smart-Life Inclusion"
   - minimum API 30 (Android 11), IL2CPP, ARM64, internet permission
   - anti-aliasing 0 on every quality level (the performance guardrail)

   Every Android build re-checks these and stops with a list if anything drifted.
5. **Assets > External Dependency Manager > Android Resolver > Resolve**.
6. **SMART-LIFE > Build Morning Routine Scenes**. Creates `MorningRoutine_Boot` and one scene per step (16) and writes Build Settings with the boot scene first. Run it again whenever a step is added or renamed, then commit the scenes.

## 2. Print the markers
- Morning Routine cards (7): `MK_BED`, `NAV_TO_BATH`, `MK_TOILET`, `MK_SINK`, `MK_TOOTHBRUSH`, `NAV_TO_BED`, `MK_WARDROBE` in `docs/markers/`.
  Place: bed card by the bed, `NAV_TO_BATH` in the hallway facing the learner on the way to the bathroom, toilet card by the toilet, sink card above the sink, toothbrush card by the cup, `NAV_TO_BED` in the hallway on the way back, wardrobe card on the wardrobe.
- The standalone handwashing scene also uses `MK_SOAP`, `MK_RUB` and `MK_TOWEL`.
- Print each one **exactly 18 cm wide**. The PNGs carry no DPI, so "100% scale" does not fix the size: place each image in a page-layout tool (Word, Google Docs, an image editor's print dialog) and set its width to 18 cm explicitly, then measure the printed card with a ruler. The app is told the cards are 0.18 m wide; a different size makes tracking unstable.
- Matte paper, no lamination glare. Mount them flat on stiff card.
- The app builds its Image Targets from the same images at start (`ImageTargetRegistry`), so there is no Vuforia database to download.
- To add or change markers: edit `MARKERS` in `tools/markers/generate_markers.py`, run `python -I tools/markers/generate_markers.py`, reprint, and commit every generated file.

## 3. Build and install
1. **File > Build Profiles > Android**, switch platform. Scene list: `MorningRoutine_Boot` first, then the 16 `ADL_*` step scenes (step 1.6 sets this).
2. Plug in the tablet with USB debugging on, then **Build And Run**. A debug build uses Unity's debug keystore. For the signed release, add your keystore in **Player > Publishing Settings** (keep it outside the repo).
3. Grant the camera permission when the app asks.

## 4. Phase 1 gate (all must pass)
| Check | How | Pass |
|---|---|---|
| Targets load | `adb logcat -s Unity` | `[ImageTargetRegistry] 10 of 10 marker targets ready.` |
| Routine runs | Walk the routine: bed, hallway, toilet, sink (5 steps on hold timers), toothbrush, hallway, wardrobe | Each card moves to the next step and its scene; no scene error in logcat; a wrong card shows the hint and does not advance |
| Profile applies | In the dashboard Learner Profile, skip Toilet and turn the mascot off, then restart the app | Toilet is skipped and no mascot appears |
| Distance | Hold each card at 30, 60 and 100 cm | Detected at all three |
| Light | Classroom light, dim, window backlight | Detected in all three |
| Frame rate | Profiler or `adb shell dumpsys gfxinfo`, 5-minute run | At least 30 FPS, p95 frame under 33 ms |
| Heat | Same 5-minute run | No thermal warning or visible slowdown |

## 5. If something fails
- **"License key invalid" on screen:** the key in Vuforia Configuration is empty or mistyped.
- **"0 of 4 marker targets ready" or an error per marker:** `Resources/Markers` is missing or out of date. Re-run the generator.
- **Black camera view:** in **Player > Other Settings > Graphics APIs**, untick Auto and put OpenGLES3 first, then rebuild.
- **Cards detected only up close:** check the print width (18 cm) and lighting. If a card still tracks badly, upload its PNG to the Vuforia Target Manager to see its star rating, and regenerate with a new id if it rates below 3 stars.
- **Build stops with "V1 Android settings are wrong":** run **SMART-LIFE > Apply V1 Android Settings**.

Anti-looping rule: if the same build or device check fails twice, stop and report the exact error before trying a third fix.
