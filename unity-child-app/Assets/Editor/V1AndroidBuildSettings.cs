using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SmartLife.EditorTools
{
    /// <summary>
    /// V1 tablet build settings in one place. The menu item applies them, and every Android build
    /// checks them first, so a build can never ship with the wrong package or with anti-aliasing on.
    /// </summary>
    public class V1AndroidBuildSettings : IPreprocessBuildWithReport
    {
        public const string CompanyName = "NoCodeNomads";
        public const string ProductName = "Smart-Life Inclusion";
        public const string PackageId = "com.nocodenomads.smartlife";
        public const int MinSdk = 30; // Android 11, the oldest OS on the V1 target tablets.
        public const string Version = "0.0.1";
        public const string SentisAnalyticsDefine = "SENTIS_ANALYTICS_ENABLED"; // A package re-adds it; this app is used by minors.

        public int callbackOrder => 0;

        [MenuItem("SMART-LIFE/Apply V1 Android Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;

            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android,
                string.Join(";", defines.Split(';').Where(d => d != SentisAnalyticsDefine)));

            SerializedObject quality = QualityAsset();
            SerializedProperty levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("antiAliasing").intValue = 0;
            quality.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            List<string> left = Problems();
            if (left.Count == 0) Debug.Log("[V1AndroidBuildSettings] V1 Android settings applied.");
            else Debug.LogWarning("[V1AndroidBuildSettings] Fix these by hand:\n- " + string.Join("\n- ", left));
        }

        /// <summary>Batchmode entry: Unity.exe -batchmode -quit -buildTarget Android -executeMethod SmartLife.EditorTools.V1AndroidBuildSettings.BuildApk</summary>
        public static void BuildApk()
        {
            Apply();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled scenes in Build Settings.");
            Debug.Log("[V1AndroidBuildSettings] First scene: " + scenes[0]);

            EditorUserBuildSettings.buildAppBundle = false; // .apk, not .aab
            string output = "Builds/SmartLife-v" + Version + ".apk";

            // BuildOptions.None on purpose: a development build adds an on-screen watermark to the low-sensory child UI.
            BuildSummary summary = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.Android, BuildOptions.None).summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[V1AndroidBuildSettings] Build {summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings.");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"[V1AndroidBuildSettings] Built {output} ({summary.totalSize / (1024f * 1024f):F1} MB).");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            List<string> problems = Problems();
            if (problems.Count > 0)
                throw new BuildFailedException("V1 Android settings are wrong:\n- " + string.Join("\n- ", problems) +
                                               "\nRun SMART-LIFE > Apply V1 Android Settings, then build again.");
        }

        /// <summary>Everything that differs from the V1 tablet configuration. Empty means ready to build.</summary>
        public static List<string> Problems()
        {
            var problems = new List<string>();

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (id != PackageId) problems.Add($"Package name is '{id}', expected '{PackageId}'.");
            if (PlayerSettings.bundleVersion != Version)
                problems.Add($"Version is '{PlayerSettings.bundleVersion}', expected '{Version}'.");
            if ((int)PlayerSettings.Android.minSdkVersion < MinSdk)
                problems.Add($"Minimum API level is {(int)PlayerSettings.Android.minSdkVersion}, expected {MinSdk} or higher.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                problems.Add("Scripting backend must be IL2CPP (ARM64 needs it).");
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                problems.Add("Target architectures must include ARM64.");
            if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';').Contains(SentisAnalyticsDefine))
                problems.Add($"Android scripting defines contain {SentisAnalyticsDefine}; remove it (minors' app).");
            if (string.IsNullOrWhiteSpace(global::Vuforia.VuforiaConfiguration.Instance.Vuforia.LicenseKey))
                problems.Add("Vuforia App License Key is empty. Runtime Image Targets need it: paste a key in Assets/Resources/VuforiaConfiguration (never commit it).");

            // Guardrail: anti-aliasing stays off on every quality level to stop thermal throttling under AR load.
            SerializedProperty levels = QualityAsset().FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                int aa = level.FindPropertyRelative("antiAliasing").intValue;
                if (aa != 0) problems.Add($"Quality level '{level.FindPropertyRelative("name").stringValue}' has anti-aliasing {aa}, expected 0.");

                // A level's own pipeline asset overrides the global one, so its MSAA counts too.
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset levelUrp && levelUrp.msaaSampleCount > 1)
                    problems.Add($"URP asset '{levelUrp.name}' on quality level '{level.FindPropertyRelative("name").stringValue}' has MSAA {levelUrp.msaaSampleCount}x, expected off.");
            }

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp && urp.msaaSampleCount > 1)
                problems.Add($"Default URP asset '{urp.name}' has MSAA {urp.msaaSampleCount}x, expected off.");

            return problems;
        }

        private static SerializedObject QualityAsset() =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
    }
}
