#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PortfolioSamples.EditorTools
{
    public static class AndroidBuildPipelineSample
    {
        private const string BuildPathPrefsKey = "portfolio.sample.build.path";
        private const string ProductName = "PortfolioGame";

        [MenuItem("Portfolio Build/Android APK")]
        private static void BuildApk()
        {
            BuildAndroid(buildAppBundle: false, defineSymbol: "DEV");
        }

        [MenuItem("Portfolio Build/Android AAB")]
        private static void BuildAab()
        {
            BuildAndroid(buildAppBundle: true, defineSymbol: "LIVE");
        }

        private static void BuildAndroid(bool buildAppBundle, string defineSymbol)
        {
            string previousPath = PlayerPrefs.GetString(BuildPathPrefsKey, string.Empty);
            string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", previousPath, string.Empty);
            if (string.IsNullOrEmpty(buildPath))
                return;

            PlayerPrefs.SetString(BuildPathPrefsKey, buildPath);
            Directory.CreateDirectory(buildPath);

            EditorUserBuildSettings.buildAppBundle = buildAppBundle;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, defineSymbol);
            PlayerSettings.Android.bundleVersionCode += 1;
            PlayerSettings.bundleVersion = IncrementPatchVersion(PlayerSettings.bundleVersion);

            string extension = buildAppBundle ? "aab" : "apk";
            string output = Path.Combine(buildPath, $"{ProductName}.{extension}");

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(output);
                return;
            }

            PlayerSettings.Android.bundleVersionCode -= 1;
            Debug.LogError($"Build failed: {report.summary.result}");
        }

        private static string[] GetEnabledScenes()
        {
            return System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
                scene => scene.path);
        }

        private static string IncrementPatchVersion(string version)
        {
            string[] parts = version.Split('.');
            if (parts.Length < 3)
                return "1.0.1";

            if (!int.TryParse(parts[2], out int patch))
                patch = 0;

            return $"{parts[0]}.{parts[1]}.{patch + 1}";
        }
    }
}
#endif

