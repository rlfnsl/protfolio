using UnityEditor;
using System.IO;
using System.Diagnostics;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.Build.Content;

public class CustomBuild
{
    static string GameName = "MonsterVillage.";
    [MenuItem("Build/Live")]
    private static void Build_Android()
    {
        EditorUserBuildSettings.buildAppBundle = true;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(GetBuildTarget(), "Live");
        string originpath = null;
        if (PlayerPrefs.HasKey("buildpath"))
        {
            originpath = PlayerPrefs.GetString("buildpath");
        }
        if (!System.IO.Directory.Exists(originpath))
        {
            originpath = null;
        }
        string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", originpath, "");

        if (string.IsNullOrEmpty(buildPath))
        {
            return;
        }
        PlayerPrefs.SetString("buildpath", buildPath);

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = Path.Combine(buildPath, GameName + "aab");
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;
        PlayerSettings.Android.bundleVersionCode += 1;
        PlayerSettings.bundleVersion = IncrementVersionString(PlayerSettings.bundleVersion);
        bool buildSuccess = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (buildSuccess)
        {
            if (System.IO.Directory.Exists(buildPath))
            {
                Process.Start(buildPath);
            }
        }
        else
        {
            PlayerSettings.Android.bundleVersionCode -= 1;
            PlayerSettings.bundleVersion = IncrementVersionStringMinus(PlayerSettings.bundleVersion);
        }
    }
    [MenuItem("Build/Dev")]
    private static void Build_AndroidDev()
    {
        EditorUserBuildSettings.buildAppBundle = true;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(GetBuildTarget(), "Dev");
        string originpath = null;
        if (PlayerPrefs.HasKey("buildpath"))
        {
            originpath = PlayerPrefs.GetString("buildpath");
        }
        if (!System.IO.Directory.Exists(originpath))
        {
            originpath = null;
        }
        string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", originpath, "");

        if (string.IsNullOrEmpty(buildPath))
        {
            return;
        }
        PlayerPrefs.SetString("buildpath", buildPath);

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = Path.Combine(buildPath, GameName + "aab");
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;
        PlayerSettings.Android.bundleVersionCode += 1;
        PlayerSettings.bundleVersion = IncrementVersionString(PlayerSettings.bundleVersion);
        bool buildSuccess = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (buildSuccess)
        {
            if (System.IO.Directory.Exists(buildPath))
            {
                Process.Start(buildPath);
            }
        }
        else
        {
            PlayerSettings.Android.bundleVersionCode -= 1;
            PlayerSettings.bundleVersion = IncrementVersionStringMinus(PlayerSettings.bundleVersion);
        }
    }
    [MenuItem("Build/Live 출시안하는버전")]
    private static void Build_Live()
    {
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(GetBuildTarget(), "Live");
        string originpath = null;
        if (PlayerPrefs.HasKey("buildpath"))
        {
            originpath = PlayerPrefs.GetString("buildpath");
        }
        if (!System.IO.Directory.Exists(originpath))
        {
            originpath = null;
        }
        string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", originpath, "");

        if (string.IsNullOrEmpty(buildPath))
        {
            return;
        }
        PlayerPrefs.SetString("buildpath", buildPath);

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = Path.Combine(buildPath, GameName + "apk");
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;
        bool buildSuccess = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (buildSuccess)
        {
            if (System.IO.Directory.Exists(buildPath))
            {
                Process.Start(buildPath);
            }
        }
    }
    [MenuItem("Build/Dev 출시안하는버전")]
    private static void Build_Dev()
    {
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.SetScriptingDefineSymbolsForGroup(GetBuildTarget(), "Dev");
        string originpath = null;
        if (PlayerPrefs.HasKey("buildpath"))
        {
            originpath = PlayerPrefs.GetString("buildpath");
        }
        if (!System.IO.Directory.Exists(originpath))
        {
            originpath = null;
        }
        string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", originpath, "");

        if (string.IsNullOrEmpty(buildPath))
        {
            return;
        }
        PlayerPrefs.SetString("buildpath", buildPath);

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = Path.Combine(buildPath, GameName + "apk");
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;
        bool buildSuccess = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (buildSuccess)
        {
            if (System.IO.Directory.Exists(buildPath))
            {
                Process.Start(buildPath);
            }
        }
    }
    private static BuildTargetGroup GetBuildTarget()
    {
#if UNITY_ANDROID
        return BuildTargetGroup.Android;
#elif UNITY_IOS
        return BuildTargetGroup.iOS;
#endif
        return BuildTargetGroup.Standalone;
    }
    static string IncrementVersionString(string version)
    {
        string[] parts = version.Split('.');
        int major = int.Parse(parts[0]);
        int minor = int.Parse(parts[1]);
        int build = int.Parse(parts[2]) + 1;

        return $"{major}.{minor}.{build}";
    }
    static string IncrementVersionStringMinus(string version)
    {
        string[] parts = version.Split('.');
        int major = int.Parse(parts[0]);
        int minor = int.Parse(parts[1]);
        int build = int.Parse(parts[2]) - 1;

        return $"{major}.{minor}.{build}";
    }
    static string[] GetScenePaths()
    {
        string[] scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }
        return scenes;
    }
}