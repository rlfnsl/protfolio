#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using UnityEditor;
using UnityEngine;

public class SheetsEncryptAllTabsToResources : EditorWindow
{
    string spreadsheetId = "<REDACTED_SPREADSHEET_ID>";
    string serviceAccountJsonResourcePath = "<REDACTED_SERVICE_ACCOUNT_RESOURCE>";
    string password = "<REDACTED_EXPORT_PASSWORD>";

    string resourcesSubFolder = "EnemySheets";
    string SavePath => Path.Combine(Application.dataPath, "Resources", resourcesSubFolder);

    [MenuItem("Tools/Sheets/Encrypt All Tabs To Resources")]
    static void Open()
    {
        GetWindow<SheetsEncryptAllTabsToResources>("Sheets Encrypt");
    }

    void OnGUI()
    {
        GUILayout.Label("스프레드시트의 모든 시트 탭을 암호화해서 Resources에 저장", EditorStyles.boldLabel);

        spreadsheetId = EditorGUILayout.TextField("Spreadsheet Id", spreadsheetId);
        serviceAccountJsonResourcePath = EditorGUILayout.TextField("Json Resource Path", serviceAccountJsonResourcePath);
        password = EditorGUILayout.TextField("Password", password);
        resourcesSubFolder = EditorGUILayout.TextField("Resources Folder", resourcesSubFolder);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Save Path", SavePath);

        EditorGUILayout.Space(12);

        if (GUILayout.Button("실행"))
        {
            _ = Run();
        }
    }

    async Task Run()
    {
        EditorUtility.ClearProgressBar();

        try
        {
            var jsonAsset = Resources.Load<TextAsset>(serviceAccountJsonResourcePath);
            if (jsonAsset == null)
            {
                EditorUtility.DisplayDialog("오류", "Resources에서 JSON을 못 찾음", "확인");
                return;
            }

            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                EditorUtility.DisplayDialog("오류", "Spreadsheet Id가 비어있음", "확인");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                EditorUtility.DisplayDialog("오류", "Password가 비어있음", "확인");
                return;
            }

            Directory.CreateDirectory(SavePath);

            var service = CreateSheetsService(jsonAsset.text);

            var ss = await Task.Run(() =>
            {
                var req = service.Spreadsheets.Get(spreadsheetId);
                req.IncludeGridData = false;
                return req.Execute();
            });

            if (ss == null || ss.Sheets == null || ss.Sheets.Count == 0)
            {
                EditorUtility.DisplayDialog("오류", "시트 목록을 못 가져옴", "확인");
                return;
            }

            var titles = new List<string>();
            foreach (var s in ss.Sheets)
            {
                var t = s.Properties?.Title;
                if (!string.IsNullOrEmpty(t))
                    titles.Add(t);
            }

            int total = titles.Count;
            for (int i = 0; i < total; i++)
            {
                string title = titles[i];
                EditorUtility.DisplayProgressBar("다운로드 및 암호화", title, (float)i / total);

                ValueRange vr = await Task.Run(() =>
                {
                    string range = SheetRange(title, "A:Z");
                    var vreq = service.Spreadsheets.Values.Get(spreadsheetId, range);
                    return vreq.Execute();
                });

                string tsv = ToTsv(vr);
                byte[] encrypted = EncryptTsv(tsv, password);

                string safeFileName = MakeSafeFileName(title) + ".bytes";
                string outPath = Path.Combine(SavePath, safeFileName);
                File.WriteAllBytes(outPath, encrypted);
            }

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("완료", "Resources 저장 완료", "확인");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError(e);
            EditorUtility.DisplayDialog("오류", e.Message, "확인");
        }
    }

    static SheetsService CreateSheetsService(string json)
    {
        var credential = GoogleCredential.FromJson(json)
            .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "UnitySheetsEncryptAllTabs"
        });
    }

    static byte[] EncryptTsv(string tsv, string password)
    {
        byte[] plain = Encoding.UTF8.GetBytes(tsv ?? "");
        return AesCryptoUtil.EncryptBytes(plain, password);
    }

    static string SheetRange(string sheetTitle, string a1)
    {
        return EscapeSheetTitle(sheetTitle) + "!" + a1;
    }

    static string EscapeSheetTitle(string sheetTitle)
    {
        bool needQuotes = sheetTitle.IndexOfAny(new[] { ' ', '\t', '\'', '!', ':', '/', '\\', '[', ']', '(', ')', '{', '}', ',' }) >= 0;
        if (!needQuotes) return sheetTitle;

        string escaped = sheetTitle.Replace("'", "''");
        return "'" + escaped + "'";
    }

    static string ToTsv(ValueRange vr)
    {
        if (vr == null || vr.Values == null) return "";

        var sb = new StringBuilder(8192);
        var rows = vr.Values;

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (int c = 0; c < row.Count; c++)
            {
                if (c > 0) sb.Append('\t');
                string cell = row[c] == null ? "" : row[c].ToString();
                cell = cell.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                sb.Append(cell);
            }
            if (r < rows.Count - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    static string MakeSafeFileName(string name)
    {
        foreach (char ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return name;
    }
}
#endif
