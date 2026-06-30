using UnityEditor;
using System.IO;
using System.Diagnostics;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class CustomBuild
{
    [MenuItem("Build/Computer")]
    private static void Build_Kor()
    {
        
        string jsonFolderPath = Path.Combine(Application.streamingAssetsPath, "Json");

        if (Directory.Exists(jsonFolderPath))
        {
            string[] jsonFiles = Directory.GetFiles(jsonFolderPath, "*.json");

            foreach (string filePath in jsonFiles)
            {
                File.Delete(filePath);
               UnityEngine.Debug.Log("Deleted file: " + filePath);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("JSON folder not found.");
        }
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, "Com");
        string originpath = null;
        if (PlayerPrefs.HasKey("buildpath"))
        {
            originpath = PlayerPrefs.GetString("buildpath");
        }
        if (!System.IO.Directory.Exists(originpath))
        {
            originpath = null;
        }
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", originpath, "");

        if (string.IsNullOrEmpty(buildPath))
        {
            return;
            buildPath = Path.Combine(desktopPath, "MyGame/Com");
        }
        PlayerPrefs.SetString("buildpath", buildPath);

        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = Path.Combine(buildPath, "MetalCardBotCom.exe");


        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;


        buildPlayerOptions.options = BuildOptions.None;

        BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (System.IO.Directory.Exists(buildPath))
        {
            Process.Start(buildPath);
        }

    }

    [MenuItem("Build/ARCADE")]
    private static void Build_Eng()
    {
        string jsonFolderPath = Path.Combine(Application.streamingAssetsPath, "Json");

        if (Directory.Exists(jsonFolderPath))
        {
            string[] jsonFiles = Directory.GetFiles(jsonFolderPath, "*.json");

            foreach (string filePath in jsonFiles)
            {
                File.Delete(filePath);
                UnityEngine.Debug.Log("Deleted file: " + filePath);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("JSON folder not found.");
        }
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, "Arcade");

        string originpath = null;
        if (PlayerPrefs.HasKey("buildpath"))
        {
            originpath = PlayerPrefs.GetString("buildpath");
        }
        if(!System.IO.Directory.Exists(originpath))
        {
            originpath = null;
        }
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", originpath, "");

        if (string.IsNullOrEmpty(buildPath))
        {
            return;
            buildPath = Path.Combine(desktopPath, "MyGame/Arcade");
        }
        PlayerPrefs.SetString("buildpath",buildPath);
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths(); 
        buildPlayerOptions.locationPathName = Path.Combine(buildPath, "MetalCardBotArcade.exe");

        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None; 

        BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (System.IO.Directory.Exists(buildPath))
        {
            Process.Start(buildPath);
        }
    }
    [MenuItem("Build/ARCADETest")]
    private static void Build_Test()
    {
        string jsonFolderPath = Path.Combine(Application.streamingAssetsPath, "Json");

        if (Directory.Exists(jsonFolderPath))
        {
            string[] jsonFiles = Directory.GetFiles(jsonFolderPath, "*.json");

            foreach (string filePath in jsonFiles)
            {
                File.Delete(filePath);
                UnityEngine.Debug.Log("Deleted file: " + filePath);
            }
        }
        else
        {
            UnityEngine.Debug.LogWarning("JSON folder not found.");
        }
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, "ArcadeTest");

        string originpath = null;
        if (PlayerPrefs.HasKey("buildpath"))
        {
            originpath = PlayerPrefs.GetString("buildpath");
        }
        if (!System.IO.Directory.Exists(originpath))
        {
            originpath = null;
        }
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string buildPath = EditorUtility.OpenFolderPanel("Select Build Folder", originpath, "");

        if (string.IsNullOrEmpty(buildPath))
        {
            return;
            buildPath = Path.Combine(desktopPath, "MyGame/ArcadeTest");
        }
        PlayerPrefs.SetString("buildpath", buildPath);
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = Path.Combine(buildPath, "MetalCardBotArcade.exe");

        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;

        BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (System.IO.Directory.Exists(buildPath))
        {
            Process.Start(buildPath);
        }
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