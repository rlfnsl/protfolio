using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CodexAndroidBuild
{
    private const string OutputDirectory = @"C:\Users\qlwns\MVbuild";
    private const string ApkFileName = "MonsterVillage.apk";
    private const string ZipFileName = "MonsterVillage_apk.zip";

    public static void BuildApk()
    {
        Directory.CreateDirectory(OutputDirectory);

        string apkPath = Path.Combine(OutputDirectory, ApkFileName);
        string zipPath = Path.Combine(OutputDirectory, ZipFileName);
        if (File.Exists(apkPath))
            File.Delete(apkPath);
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false;

        string keystorePassword = ReadKeystorePassword();
        if (!string.IsNullOrEmpty(keystorePassword))
        {
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasPass = keystorePassword;
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray(),
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"Android APK build failed: {report.summary.result}");

        using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(apkPath, Path.GetFileName(apkPath), System.IO.Compression.CompressionLevel.Optimal);
        }

        Debug.Log($"[CodexAndroidBuild] APK: {apkPath}");
        Debug.Log($"[CodexAndroidBuild] ZIP: {zipPath}");
    }

    private static string ReadKeystorePassword()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "키스토어.txt");
        if (!File.Exists(path))
            return string.Empty;

        string text = File.ReadAllText(path).Trim();
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string[] tokens = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 0 ? string.Empty : tokens[tokens.Length - 1];
    }
}
