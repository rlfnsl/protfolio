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
    const string KEY_STEAM_LOGIN_ID = "SteamBuild_SteamLoginId";
    const string KEY_PASSWORD = "SteamBuild_Password";
    const string KEY_BATCH_PATH = "SteamBuild_BatchPath";
    const string KEY_IS_DEV = "SteamBuild_IsDev";
    const string KEY_DESC = "SteamBuild_Desc";

    const string EXE_NAME = "Dragon Arena.exe";
    const string STEAM_APP_ID_FILE_NAME = "steam_appid.txt";

    const string APP_ID = "4371940";
    const string VDF_NAME = "app_build_4371940.vdf";
    const string BAT_NAME = "build_dragonarena.bat";

    string steamLoginId;
    string steamPassword;
    string batchPath;
    bool isDevBuild = true;
    string descText = "";

    [MenuItem("Tools/Steam Build")]
    public static void OpenWindow()
    {
        var _window = GetWindow<SteamBuildEditorWindow>("Steam Build");
        _window.minSize = new Vector2(420, 240);
    }

    void OnEnable()
    {
        steamLoginId = EditorPrefs.GetString(KEY_STEAM_LOGIN_ID, "");
        steamPassword = EditorPrefs.GetString(KEY_PASSWORD, "");
        batchPath = EditorPrefs.GetString(KEY_BATCH_PATH, "");
        isDevBuild = EditorPrefs.GetInt(KEY_IS_DEV, 1) == 1;
        descText = EditorPrefs.GetString(KEY_DESC, "");
    }

    void OnGUI()
    {
        GUILayout.Label("Steam 로그인 정보", EditorStyles.boldLabel);
        steamLoginId = EditorGUILayout.TextField("Steam Login ID", steamLoginId);
        steamPassword = EditorGUILayout.PasswordField("Steam 비밀번호", steamPassword);

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        batchPath = EditorGUILayout.TextField("배치 파일 경로", batchPath);
        if (GUILayout.Button("찾기", GUILayout.Width(60)))
        {
            string _path = EditorUtility.OpenFilePanel("배치 파일 선택", "", "bat");
            if (!string.IsNullOrEmpty(_path))
                batchPath = _path;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.Label("빌드 설명", EditorStyles.boldLabel);
        descText = EditorGUILayout.TextField("Desc 내용", descText);

        GUILayout.Space(10);

        GUILayout.Label("빌드 옵션", EditorStyles.boldLabel);
        isDevBuild = EditorGUILayout.Toggle("개발용 빌드 (DEV 심볼)", isDevBuild);

        GUILayout.Space(6);

        string _contentPath = GetContentRootFromBatch();
        EditorGUILayout.LabelField("Content 폴더", string.IsNullOrEmpty(_contentPath) ? "-" : _contentPath);
        EditorGUILayout.LabelField("App VDF", VDF_NAME);
        EditorGUILayout.LabelField("AppId", APP_ID);

        GUILayout.Space(10);

        if (GUILayout.Button("빌드 후 Steam 업로드"))
        {
            SavePrefs();

            if (EnsureBatchAndContent())
            {
                UpdateSteamAppIdFile();
                UpdateVdfDesc(descText);

                bool _ok = BuildPcForSteam(isDevBuild, _contentPath);
                if (_ok)
                    RunBatch();
            }
        }
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(KEY_STEAM_LOGIN_ID, steamLoginId);
        EditorPrefs.SetString(KEY_PASSWORD, steamPassword);
        EditorPrefs.SetString(KEY_BATCH_PATH, batchPath);
        EditorPrefs.SetInt(KEY_IS_DEV, isDevBuild ? 1 : 0);
        EditorPrefs.SetString(KEY_DESC, descText);
    }

    string GetContentRootFromBatch()
    {
        if (string.IsNullOrEmpty(batchPath)) return "";
        string _baseDir = Path.GetDirectoryName(batchPath);
        if (string.IsNullOrEmpty(_baseDir)) return "";
        return Path.Combine(_baseDir, "tools", "ContentBuilder", "content");
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

        string _fileName = Path.GetFileName(batchPath);
        if (!string.IsNullOrEmpty(_fileName) && _fileName != BAT_NAME)
        {
            string _dir = Path.GetDirectoryName(batchPath);
            if (!string.IsNullOrEmpty(_dir))
            {
                string _expected = Path.Combine(_dir, BAT_NAME);
                if (File.Exists(_expected))
                    batchPath = _expected;
            }
        }

        string _contentRoot = GetContentRootFromBatch();
        if (!Directory.Exists(_contentRoot))
            Directory.CreateDirectory(_contentRoot);

        return true;
    }

    bool BuildPcForSteam(bool _dev, string _contentRoot)
    {
        string _fullExePath = Path.Combine(_contentRoot, EXE_NAME);

        string[] _scenes = EditorBuildSettings.scenes
            .Where(_s => _s.enabled)
            .Select(_s => _s.path)
            .ToArray();

        if (_scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "Build Settings 에 등록된 씬이 없음", "확인");
            return false;
        }

        SetScriptingSymbols(_dev);

        BuildPlayerOptions _opt = new BuildPlayerOptions();
        _opt.scenes = _scenes;
        _opt.locationPathName = _fullExePath;
        _opt.target = BuildTarget.StandaloneWindows64;

        BuildReport _report = BuildPipeline.BuildPlayer(_opt);
        if (_report.summary.result != BuildResult.Succeeded)
        {
            UnityEngine.Debug.LogError("PC 빌드 실패");
            return false;
        }

        UnityEngine.Debug.Log("PC 빌드 성공: " + _fullExePath);
        return true;
    }

    static void SetScriptingSymbols(bool _isDev)
    {
        BuildTargetGroup _group = BuildTargetGroup.Standalone;
        string _defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(_group);

        List<string> _symbols = _defines.Split(';')
            .Select(_s => _s.Trim())
            .Where(_s =>
                !string.IsNullOrEmpty(_s) &&
                _s != "DEV" &&
                _s != "LIVE")
            .ToList();

        if (_isDev) _symbols.Add("DEV");
        else _symbols.Add("LIVE");

        string _result = string.Join(";", _symbols.Distinct());
        PlayerSettings.SetScriptingDefineSymbolsForGroup(_group, _result);

        UnityEngine.Debug.Log("BuildSymbol 설정됨: " + _result);
    }

    void UpdateVdfDesc(string _desc)
    {
        string _baseDir = Path.GetDirectoryName(batchPath);
        if (string.IsNullOrEmpty(_baseDir)) return;

        string _scriptDir = Path.Combine(_baseDir, "tools", "ContentBuilder", "scripts");
        string _vdfPath = Path.Combine(_scriptDir, VDF_NAME);

        if (!File.Exists(_vdfPath))
        {
            UnityEngine.Debug.LogError("VDF 파일을 찾을 수 없음: " + _vdfPath);
            return;
        }

        string[] _lines = File.ReadAllLines(_vdfPath);

        for (int _i = 0; _i < _lines.Length; _i++)
        {
            if (_lines[_i].TrimStart().StartsWith("\"Desc\""))
            {
                _lines[_i] = "    \"Desc\" \"" + _desc + "\"";
                break;
            }
        }

        File.WriteAllLines(_vdfPath, _lines);
        UnityEngine.Debug.Log("VDF Desc 업데이트됨: " + _desc);
    }

    void RunBatch()
    {
        var _psi = new ProcessStartInfo();
        _psi.FileName = batchPath;
        _psi.WorkingDirectory = Path.GetDirectoryName(batchPath);
        _psi.Arguments = steamLoginId + " " + steamPassword;
        _psi.UseShellExecute = false;
        _psi.RedirectStandardOutput = true;
        _psi.RedirectStandardError = true;
        _psi.CreateNoWindow = true;

        var _proc = new Process();
        _proc.StartInfo = _psi;

        _proc.OutputDataReceived += (_s, _e) =>
        {
            if (!string.IsNullOrEmpty(_e.Data))
                UnityEngine.Debug.Log("SteamCMD: " + _e.Data);
        };

        _proc.ErrorDataReceived += (_s, _e) =>
        {
            if (!string.IsNullOrEmpty(_e.Data))
                UnityEngine.Debug.LogError("SteamCMD ERROR: " + _e.Data);
        };

        _proc.Start();
        _proc.BeginOutputReadLine();
        _proc.BeginErrorReadLine();

        UnityEngine.Debug.Log("Steam 배치 실행 시작");
    }

    void UpdateSteamAppIdFile()
    {
        string _projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string _appIdPath = Path.Combine(_projectRoot, STEAM_APP_ID_FILE_NAME);

        try
        {
            File.WriteAllText(_appIdPath, APP_ID);
            UnityEngine.Debug.Log("steam_appid.txt 업데이트됨: " + APP_ID);
        }
        catch (System.Exception _e)
        {
            UnityEngine.Debug.LogError("steam_appid.txt 업데이트 실패: " + _e.Message);
        }
    }
}
#endif
