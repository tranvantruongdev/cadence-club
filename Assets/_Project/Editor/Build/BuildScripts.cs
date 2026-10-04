using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Template.EditorTools.Build
{
    /// <summary>
    /// Local and command-line builds. Keystore secrets come from environment variables, never from
    /// the repo: ANDROID_KEYSTORE_PATH, ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME, ANDROID_KEYALIAS_PASS.
    /// Optional BUILD_VERSION sets the version name shown to players for that build.
    /// CI uses GameCI's builder instead (see .github/workflows/release.yml).
    /// </summary>
    public static class BuildScripts
    {
        private static string[] EnabledScenes => EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        private static string SafeProductName => new string(PlayerSettings.productName.Where(char.IsLetterOrDigit).ToArray());

        [MenuItem("Template/Build/Android APK")]
        public static void AndroidApk() => BuildAndroid(false);

        [MenuItem("Template/Build/Android App Bundle (Google Play)")]
        public static void AndroidAab() => BuildAndroid(true);

        [MenuItem("Template/Build/Windows")]
        public static void Windows()
        {
            Build(BuildTarget.StandaloneWindows64, $"Builds/Windows/{SafeProductName}.exe");
        }

        private static void BuildAndroid(bool appBundle)
        {
            EditorUserBuildSettings.buildAppBundle = appBundle;

            string keystore = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");
            }
            else
            {
                Debug.LogWarning("[Build] ANDROID_KEYSTORE_PATH not set: building with the debug key (fine for testing, not for Google Play).");
            }

            Build(BuildTarget.Android, $"Builds/Android/{SafeProductName}.{(appBundle ? "aab" : "apk")}");
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Builds");
            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes,
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
            };

            // BUILD_VERSION (e.g. 0.1.0-preview) labels this build only; the project setting is restored after.
            string savedVersion = PlayerSettings.bundleVersion;
            string version = Environment.GetEnvironmentVariable("BUILD_VERSION");
            if (!string.IsNullOrEmpty(version))
            {
                PlayerSettings.bundleVersion = version;
            }

            BuildSummary summary;
            try
            {
                summary = BuildPipeline.BuildPlayer(options).summary;
            }
            finally
            {
                PlayerSettings.bundleVersion = savedVersion;
            }

            Debug.Log($"[Build] {target}: {summary.result}, {summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s → {outputPath}");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}
