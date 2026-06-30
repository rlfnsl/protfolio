using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildTool : EditorWindow
{
    private const string LastBuildPathKey = "LastPCBuildPath";

    private static readonly string DevBuildName = "KillTheDragon_DEV.exe";
    private static readonly string LiveBuildName = "KillTheDragon_LIVE.exe";

    private static bool isDevBuild = true;
    private static string selectedBuildPath = "";

    [MenuItem("Tools/🔨 Build PC Game")]
    public static void ShowWindow()
    {
        GetWindow<BuildTool>("PC Build Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("💻 PC Build 설정", EditorStyles.boldLabel);
        isDevBuild = EditorGUILayout.Toggle("개발용 빌드 (Dev)", isDevBuild);

        GUILayout.Space(5);
        GUILayout.Label(isDevBuild ? "# DEV 빌드" : "# LIVE 빌드", EditorStyles.helpBox);

        GUILayout.Space(10);

        if (GUILayout.Button("📁 빌드 폴더 선택"))
        {
            string defaultPath = EditorPrefs.GetString(LastBuildPathKey, Application.dataPath);
            string path = EditorUtility.OpenFolderPanel("빌드할 폴더 선택", defaultPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                selectedBuildPath = path;
                EditorPrefs.SetString(LastBuildPathKey, selectedBuildPath);
            }
        }

        if (!string.IsNullOrEmpty(selectedBuildPath))
        {
            GUILayout.Label("📂 현재 선택된 경로:", EditorStyles.boldLabel);
            EditorGUILayout.TextField(selectedBuildPath);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("✅ 빌드"))
        {
            if (string.IsNullOrEmpty(selectedBuildPath))
            {
                EditorUtility.DisplayDialog("⚠️ 경고", "먼저 빌드할 폴더를 선택하세요.", "확인");
                return;
            }
            BuildPCGame(isDevBuild, selectedBuildPath, false);
        }

        if (GUILayout.Button("🚀 빌드 & 실행"))
        {
            if (string.IsNullOrEmpty(selectedBuildPath))
            {
                EditorUtility.DisplayDialog("⚠️ 경고", "먼저 빌드할 폴더를 선택하세요.", "확인");
                return;
            }
            BuildPCGame(isDevBuild, selectedBuildPath, true);
        }
    }

    public static void BuildPCGame(bool isDev, string parentPath, bool isRunAfterBuild)
    {
        string exeName = isDev ? DevBuildName : LiveBuildName;
        string folderName = (isDev ? "DEV_" : "LIVE_") + "KillTheDragon";
        string targetPath = Path.Combine(parentPath, folderName);
        string fullExePath = Path.Combine(targetPath, exeName);

        if (!Directory.Exists(targetPath))
            Directory.CreateDirectory(targetPath);

        string[] scenesToBuild = GetEnabledScenes();
        if (scenesToBuild.Length == 0)
        {
            EditorUtility.DisplayDialog("❌ 오류", "Build Settings에 등록된 씬이 없습니다.", "확인");
            return;
        }

        SetScriptingSymbols(isDev);

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenesToBuild,
            locationPathName = fullExePath,
            target = BuildTarget.StandaloneWindows64
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("✅ 빌드 완료", $"성공적으로 빌드되었습니다.\n\n{fullExePath}", "확인");

            EditorPrefs.SetString(LastBuildPathKey, parentPath);
            Process.Start("explorer.exe", Path.GetFullPath(targetPath));

            if (isRunAfterBuild)
                Process.Start(fullExePath); // ← .exe 바로 실행
        }
        else
        {
            EditorUtility.DisplayDialog("❌ 빌드 실패", "빌드 중 오류가 발생했습니다.", "확인");
        }
    }


    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    private static void SetScriptingSymbols(bool isDev)
    {
        BuildTargetGroup group = BuildTargetGroup.Standalone;
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

        List<string> symbols = defines.Split(';')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s) && s != "DEV" && s != "LIVE")
            .ToList();

        if (isDev)
            symbols.Add("DEV");
        else
            symbols.Add("LIVE");

        string result = string.Join(";", symbols.Distinct());
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, result);

        UnityEngine.Debug.Log($"[BuildSymbol] 설정됨: {result}");
    }


    private void OnEnable()
    {
        selectedBuildPath = EditorPrefs.GetString(LastBuildPathKey, "");
    }
}