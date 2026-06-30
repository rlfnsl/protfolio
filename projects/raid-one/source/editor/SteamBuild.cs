#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class SteamBuildEditorWindow : EditorWindow
{
    const string KeySteamId = "SteamBuild_SteamId";
    const string KeyPassword = "SteamBuild_Password";
    const string KeyBatchPath = "SteamBuild_BatchPath";
    const string KeyIsDev = "SteamBuild_IsDev";
    const string KeyDesc = "SteamBuild_Desc";
    const string KeyIsDemo = "SteamBuild_IsDemo";

    const string ExeName = "RaidOne.exe";

    const string LiveVdfName = "app_build_3896480.vdf";
    const string DemoVdfName = "app_build_4317330.vdf";

    const string LiveBatName = "build_raidone.bat";
    const string DemoBatName = "build_raidone_Demo.bat";

    const string LiveAppId = "3896480";
    const string DemoAppId = "4317330";
    const string SteamAppIdFileName = "steam_appid.txt";

    string steamId;
    string steamPassword;
    string batchPath;
    bool isDevBuild = true;
    bool isDemoBuild = false;
    string descText = "";

    bool _prevDemo;
    bool _prevDev;

    [MenuItem("Tools/Steam Build")]
    public static void OpenWindow()
    {
        var window = GetWindow<SteamBuildEditorWindow>("Steam Build");
        window.minSize = new Vector2(420, 260);
    }

    void OnEnable()
    {
        steamId = PlayerPrefs.GetString(KeySteamId, "");
        steamPassword = PlayerPrefs.GetString(KeyPassword, "");
        batchPath = PlayerPrefs.GetString(KeyBatchPath, "");
        isDevBuild = PlayerPrefs.GetInt(KeyIsDev, 1) == 1;
        isDemoBuild = PlayerPrefs.GetInt(KeyIsDemo, 0) == 1;
        descText = PlayerPrefs.GetString(KeyDesc, "");

        _prevDemo = isDemoBuild;
        _prevDev = isDevBuild;
    }

    void OnGUI()
    {
        GUILayout.Label("Steam 로그인 정보", EditorStyles.boldLabel);
        steamId = EditorGUILayout.TextField("Steam ID", steamId);
        steamPassword = EditorGUILayout.PasswordField("Steam 비밀번호", steamPassword);

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        batchPath = EditorGUILayout.TextField("배치 파일 경로", batchPath);
        if (GUILayout.Button("찾기", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("배치 파일 선택", "", "bat");
            if (!string.IsNullOrEmpty(path))
                batchPath = path;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("빌드 설명", EditorStyles.boldLabel);
        descText = EditorGUILayout.TextField("Desc 내용", descText);

        GUILayout.Space(10);

        GUILayout.Label("빌드 옵션", EditorStyles.boldLabel);
        isDevBuild = EditorGUILayout.Toggle("개발용 빌드 (DEV 심볼)", isDevBuild);
        isDemoBuild = EditorGUILayout.Toggle("데모 빌드 (DEMO 심볼)", isDemoBuild);

        if (_prevDemo != isDemoBuild)
        {
            TryAutoSwitchBatchByDemo();
            _prevDemo = isDemoBuild;
        }

        if (_prevDev != isDevBuild)
        {
            _prevDev = isDevBuild;
        }

        string contentPath = GetContentRootFromBatch();
        EditorGUILayout.LabelField("Content 폴더", string.IsNullOrEmpty(contentPath) ? "-" : contentPath);

        string vdfName = isDemoBuild ? DemoVdfName : LiveVdfName;
        EditorGUILayout.LabelField("App VDF", vdfName);

        GUILayout.Space(10);

        if (GUILayout.Button("빌드 후 Steam 업로드"))
        {
            SavePrefs();

            if (EnsureBatchAndContent())
            {
                UpdateSteamAppIdFile(isDemoBuild);
                UpdateVdfDesc(descText);

                bool ok = BuildPcForSteam(isDevBuild, contentPath);
                if (ok)
                    RunBatch();
            }
        }
    }

    void SavePrefs()
    {
        PlayerPrefs.SetString(KeySteamId, steamId);
        PlayerPrefs.SetString(KeyPassword, steamPassword);
        PlayerPrefs.SetString(KeyBatchPath, batchPath);
        PlayerPrefs.SetInt(KeyIsDev, isDevBuild ? 1 : 0);
        PlayerPrefs.SetInt(KeyIsDemo, isDemoBuild ? 1 : 0);
        PlayerPrefs.SetString(KeyDesc, descText);
        PlayerPrefs.Save();
    }

    string GetContentRootFromBatch()
    {
        if (string.IsNullOrEmpty(batchPath)) return "";
        string baseDir = Path.GetDirectoryName(batchPath);
        if (string.IsNullOrEmpty(baseDir)) return "";
        return Path.Combine(baseDir, "tools", "ContentBuilder", "content");
    }

    bool EnsureBatchAndContent()
    {
        if (string.IsNullOrEmpty(batchPath))
        {
            EditorUtility.DisplayDialog("오류", "배치 파일 경로가 비어 있음", "확인");
            return false;
        }

        if (!File.Exists(batchPath))
        {
            EditorUtility.DisplayDialog("오류", "배치 파일을 찾을 수 없음\n" + batchPath, "확인");
            return false;
        }

        string contentRoot = GetContentRootFromBatch();
        if (!Directory.Exists(contentRoot))
            Directory.CreateDirectory(contentRoot);

        return true;
    }

    bool BuildPcForSteam(bool dev, string contentRoot)
    {
        string fullExePath = Path.Combine(contentRoot, ExeName);

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "Build Settings 에 등록된 씬이 없음", "확인");
            return false;
        }

        SetScriptingSymbols(dev, isDemoBuild);

        BuildPlayerOptions opt = new BuildPlayerOptions();
        opt.scenes = scenes;
        opt.locationPathName = fullExePath;
        opt.target = BuildTarget.StandaloneWindows64;

        BuildReport report = BuildPipeline.BuildPlayer(opt);
        if (report.summary.result != BuildResult.Succeeded)
        {
            UnityEngine.Debug.LogError("PC 빌드 실패");
            return false;
        }

        UnityEngine.Debug.Log("PC 빌드 성공 → " + fullExePath);
        return true;
    }

    static void SetScriptingSymbols(bool isDev, bool isDemo)
    {
        BuildTargetGroup group = BuildTargetGroup.Standalone;
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

        List<string> symbols = defines.Split(';')
            .Select(s => s.Trim())
            .Where(s =>
                !string.IsNullOrEmpty(s) &&
                s != "DEV" &&
                s != "LIVE" &&
                s != "DEMO")
            .ToList();

        if (isDev) symbols.Add("DEV");
        else symbols.Add("LIVE");

        if (isDemo) symbols.Add("DEMO");

        string result = string.Join(";", symbols.Distinct());
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, result);

        UnityEngine.Debug.Log("[BuildSymbol] 설정됨: " + result);
    }

    void UpdateVdfDesc(string desc)
    {
        string baseDir = Path.GetDirectoryName(batchPath);
        string scriptDir = Path.Combine(baseDir, "tools", "ContentBuilder", "scripts");

        string vdfName = isDemoBuild ? DemoVdfName : LiveVdfName;
        string vdfPath = Path.Combine(scriptDir, vdfName);

        if (!File.Exists(vdfPath))
        {
            UnityEngine.Debug.LogError("VDF 파일을 찾을 수 없음: " + vdfPath);
            return;
        }

        string[] lines = File.ReadAllLines(vdfPath);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("\"Desc\""))
            {
                lines[i] = "    \"Desc\" \"" + desc + "\"";
                break;
            }
        }

        File.WriteAllLines(vdfPath, lines);

        UnityEngine.Debug.Log("[VDF] Desc 업데이트 → " + desc);
    }

    void TryAutoSwitchBatchByDemo()
    {
        if (string.IsNullOrEmpty(batchPath)) return;

        string dir = Path.GetDirectoryName(batchPath);
        if (string.IsNullOrEmpty(dir)) return;

        string targetBat = Path.Combine(dir, isDemoBuild ? DemoBatName : LiveBatName);

        if (File.Exists(targetBat))
        {
            batchPath = targetBat;
            GUI.FocusControl(null);
        }
    }

    void RunBatch()
    {
        var psi = new ProcessStartInfo();
        psi.FileName = batchPath;
        psi.WorkingDirectory = Path.GetDirectoryName(batchPath);
        psi.Arguments = steamId + " " + steamPassword;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        var proc = new Process();
        proc.StartInfo = psi;

        proc.OutputDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log("[SteamCMD] " + e.Data);
        };

        proc.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.LogError("[SteamCMD ERROR] " + e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        UnityEngine.Debug.Log("Steam 배치 실행 시작");
    }
    void UpdateSteamAppIdFile(bool isDemo)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string appIdPath = Path.Combine(projectRoot, SteamAppIdFileName);

        string appId = isDemo ? DemoAppId : LiveAppId;

        try
        {
            File.WriteAllText(appIdPath, appId);
            UnityEngine.Debug.Log("[Steam] steam_appid.txt 업데이트됨: " + appId);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("[Steam] steam_appid.txt 업데이트 실패: " + e.Message);
        }
    }
}
#endif
