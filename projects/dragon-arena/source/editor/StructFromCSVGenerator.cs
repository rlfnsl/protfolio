using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

public class GoogleSheetStructFromTSV : EditorWindow
{
    [Serializable]
    public class SheetInfo
    {
        public string SheetName;
        public string gid;
    }

    private static bool isFirstOpen = true;
    private string sheetId = "<REDACTED_GOOGLE_SHEET_ID>";
    private string DefaultSheetGid = "624080340";
    private List<SheetInfo> sheetList = new List<SheetInfo>();
    private int selectedSheetIndex = 0;

    private const string LocalizationGid = "1923282301";

    [MenuItem("Tools/Google Sheet TSV To Class")]
    public static void ShowWindow()
    {
        GetWindow(typeof(GoogleSheetStructFromTSV));
    }

    void OnGUI()
    {
        if (isFirstOpen)
        {
            string path = "Assets/Editor/Data/SheetIndex.csv";
            if (File.Exists(path))
                LoadSheetListFromCSV();
            isFirstOpen = false;
        }

        GUILayout.Label("Google Sheet Generator", EditorStyles.boldLabel);

        if (sheetList.Count == 0)
        {
            EditorGUILayout.HelpBox("시트 목록이 로드되지 않았습니다. 시트 목록 불러오기를 눌러주세요.", MessageType.Info);
        }
        else
        {
            string[] sheetNames = sheetList.Select(s => s.SheetName).ToArray();
            selectedSheetIndex = EditorGUILayout.Popup("Sheet", selectedSheetIndex, sheetNames);
        }

        if (GUILayout.Button("시트 목록 불러오기"))
        {
            GenerateSheetIndexCSV();
            LoadSheetListFromCSV();
        }

        GUI.enabled = sheetList.Count > 0;

        GUILayout.Space(8);

        if (GUILayout.Button("모든 시트 Class 생성 + Binary 생성 + 번역 CSV 생성"))
        {
            GenerateAllSheetsClassAndBinary();
            GenerateLocalizationCSV();
        }

        if (GUILayout.Button("모든 시트 Binary만 생성 + 번역 CSV 생성"))
        {
            GenerateAllSheetsBinaryOnly();
            GenerateLocalizationCSV();
        }

        GUILayout.Space(8);

        if (GUILayout.Button("선택 시트 Class 생성 + Binary 생성"))
        {
            GenerateSelectedSheetClassAndBinary();
        }

        if (GUILayout.Button("선택 시트 Binary만 생성"))
        {
            GenerateSelectedSheetBinaryOnly();
        }

        if (GUILayout.Button("선택 시트 Class만 생성"))
        {
            GenerateSelectedSheetClassOnly();
        }

        GUI.enabled = true;

        GUILayout.Space(16);
        GUILayout.Label("Localization", EditorStyles.boldLabel);

        if (GUILayout.Button("번역 CSV 생성"))
        {
            GenerateLocalizationCSV();
        }
    }

    void GenerateSheetIndexCSV()
    {
        string url = $"<REDACTED_GOOGLE_SHEET_URL>";
        try
        {
            StringBuilder csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("SheetName,gid");

            using (WebClient client = new WebClient())
            {
                string tsv = client.DownloadString(url);
                string[] lines = tsv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length >= 2)
                {
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string[] data = lines[i].Trim().Split('\t');
                        if (data.Length >= 2)
                            csvBuilder.AppendLine($"{data[0]},{data[1]}");
                    }
                }
            }

            string folderPath = "Assets/Editor/Data";
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, "SheetIndex.csv");
            File.WriteAllText(filePath, csvBuilder.ToString());
            AssetDatabase.Refresh();

            LoadSheetListFromCSV();
            selectedSheetIndex = 0;
            Repaint();
        }
        catch (Exception ex)
        {
            Debug.LogError("시트 목록 생성 실패: " + ex.Message);

            string folderPath = "Assets/Editor/Data";
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, "SheetIndex.csv");
            File.WriteAllText(filePath, "SheetName,gid\n");
            AssetDatabase.Refresh();

            LoadSheetListFromCSV();
            selectedSheetIndex = 0;
            Repaint();
        }
    }

    void LoadSheetListFromCSV()
    {
        string path = "Assets/Editor/Data/SheetIndex.csv";
        if (!File.Exists(path))
        {
            Debug.LogWarning("시트 목록 파일이 없습니다: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path);
        sheetList.Clear();

        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length >= 2)
            {
                sheetList.Add(new SheetInfo
                {
                    SheetName = cols[0],
                    gid = cols[1]
                });
            }
        }

        selectedSheetIndex = 0;
    }

    void GenerateAllSheetsClassAndBinary()
    {
        foreach (var sheet in sheetList)
        {
            GenerateStructFromTSV(sheet.gid, sheet.SheetName);
            GenerateBinaryFromTSV(sheet.gid, sheet.SheetName);
        }
        AssetDatabase.Refresh();
    }

    void GenerateAllSheetsBinaryOnly()
    {
        foreach (var sheet in sheetList)
        {
            GenerateBinaryFromTSV(sheet.gid, sheet.SheetName);
        }
        AssetDatabase.Refresh();
    }

    void GenerateSelectedSheetClassAndBinary()
    {
        if (selectedSheetIndex < 0 || selectedSheetIndex >= sheetList.Count) return;
        var sheet = sheetList[selectedSheetIndex];

        GenerateStructFromTSV(sheet.gid, sheet.SheetName);
        GenerateBinaryFromTSV(sheet.gid, sheet.SheetName);
        AssetDatabase.Refresh();
    }

    void GenerateSelectedSheetBinaryOnly()
    {
        if (selectedSheetIndex < 0 || selectedSheetIndex >= sheetList.Count) return;
        var sheet = sheetList[selectedSheetIndex];

        GenerateBinaryFromTSV(sheet.gid, sheet.SheetName);
        AssetDatabase.Refresh();
    }

    void GenerateSelectedSheetClassOnly()
    {
        if (selectedSheetIndex < 0 || selectedSheetIndex >= sheetList.Count) return;
        var sheet = sheetList[selectedSheetIndex];

        GenerateStructFromTSV(sheet.gid, sheet.SheetName);
        AssetDatabase.Refresh();
    }

    void GenerateStructFromTSV(string gid, string className)
    {
        string url = $"<REDACTED_GOOGLE_SHEET_URL>";
        try
        {
            using (WebClient client = new WebClient())
            {
                string tsv = client.DownloadString(url);
                string[] lines = tsv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length < 3)
                {
                    Debug.LogError("시트에 충분한 데이터가 없습니다.");
                    return;
                }

                string[] fieldDefs = lines[0].Trim().Split('\t');
                List<string> fieldNames = new List<string>();
                List<string> fieldTypes = new List<string>();

                for (int i = 0; i < fieldDefs.Length; i++)
                {
                    var def = fieldDefs[i];
                    if (def.StartsWith("#") || !def.Contains(":"))
                        continue;

                    var split = def.Split(':');
                    fieldNames.Add(split[0]);
                    fieldTypes.Add(split[1]);
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("using UnityEngine;");
                sb.AppendLine();
                sb.AppendLine("[System.Serializable]");
                sb.AppendLine($"public class {className}");
                sb.AppendLine("{");

                for (int i = 0; i < fieldNames.Count; i++)
                {
                    sb.AppendLine($"    public {fieldTypes[i]} {fieldNames[i]};");
                }

                sb.AppendLine();
                sb.AppendLine($"    public {className}() {{ }}");
                sb.AppendLine();
                sb.AppendLine($"    public {className}({className} source)");
                sb.AppendLine("    {");
                for (int i = 0; i < fieldNames.Count; i++)
                {
                    sb.AppendLine($"        this.{fieldNames[i]} = source.{fieldNames[i]};");
                }
                sb.AppendLine("    }");
                sb.AppendLine("}");

                string folderPath = "Assets/GeneratedClass";
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, $"{className}.cs");
                File.WriteAllText(filePath, sb.ToString());
                AssetDatabase.Refresh();
            }
        }
        catch (WebException ex)
        {
            Debug.LogError("TSV 다운로드 실패 클래스 생성: " + ex.Message);
        }
    }

    void GenerateLocalizationCSV()
    {
        string url = $"<REDACTED_GOOGLE_SHEET_URL>";
        try
        {
            using (WebClient client = new WebClient())
            {
                string tsv = client.DownloadString(url);
                string[] lines = tsv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                {
                    Debug.LogWarning("번역시트에 충분한 데이터가 없습니다.");
                    return;
                }

                string[] header = lines[0].Trim().Split('\t');
                List<int> validIndexes = new List<int>();
                List<string> fieldNames = new List<string>();
                for (int i = 0; i < header.Length; i++)
                {
                    fieldNames.Add(header[i]);
                    validIndexes.Add(i);
                }

                StringBuilder csvBuilder = new StringBuilder();
                csvBuilder.AppendLine(string.Join(",", fieldNames.Select(EscapeCsvField)));

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] row = lines[i].Trim().Split('\t');
                    List<string> validRow = new List<string>();
                    foreach (var idx in validIndexes)
                    {
                        string value = row.Length > idx ? row[idx] : "";
                        validRow.Add(EscapeCsvField(value));
                    }
                    csvBuilder.AppendLine(string.Join(",", validRow));
                }

                string folderPath = "Assets/Resources/Data";
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, "Localization.csv");
                File.WriteAllText(filePath, csvBuilder.ToString());
                AssetDatabase.Refresh();
            }
        }
        catch (WebException ex)
        {
            Debug.LogError("TSV 다운로드 실패 Localization: " + ex.Message);
        }
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        bool containsSpecial = field.Contains(",") || field.Contains("\"") || field.Contains("\n");
        if (containsSpecial)
        {
            field = field.Replace("\"", "\"\"");
            return "\"" + field + "\"";
        }
        return field;
    }

    void GenerateBinaryFromTSV(string gid, string className)
    {
        string url = $"<REDACTED_GOOGLE_SHEET_URL>";

        try
        {
            using (WebClient client = new WebClient())
            {
                string tsv = client.DownloadString(url);
                string[] lines = tsv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 3)
                {
                    Debug.LogWarning($"{className} 시트에 데이터가 없습니다.");
                    return;
                }

                string[] fieldDefs = lines[0].Trim().Split('\t');
                List<int> validIndexes = new List<int>();
                List<string> headerNames = new List<string>();
                List<string> headerTypes = new List<string>();

                for (int i = 0; i < fieldDefs.Length; i++)
                {
                    var def = fieldDefs[i];
                    if (def.StartsWith("#") || !def.Contains(":")) continue;

                    var sp = def.Split(':');
                    headerNames.Add(sp[0]);
                    headerTypes.Add(sp[1]);
                    validIndexes.Add(i);
                }

                // 1. 메모리 스트림에 바이너리 데이터를 먼저 작성합니다.
                byte[] finalBinaryData;
                using (var ms = new MemoryStream())
                {
                    using (var bw = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        bw.Write(1);
                        bw.Write(headerNames.Count);

                        for (int i = 0; i < headerNames.Count; i++)
                        {
                            bw.Write(headerNames[i]);
                            bw.Write(headerTypes[i]);
                        }

                        int rowCount = Mathf.Max(0, lines.Length - 2);
                        bw.Write(rowCount);

                        for (int r = 2; r < lines.Length; r++)
                        {
                            string[] row = lines[r].Trim().Split('\t');
                            for (int c = 0; c < validIndexes.Count; c++)
                            {
                                int idx = validIndexes[c];
                                string value = row.Length > idx ? row[idx] : "";
                                WriteValueByType(bw, value, headerTypes[c]);
                            }
                        }
                    }
                    // 2. 작성된 바이너리 데이터를 배열로 가져옵니다.
                    finalBinaryData = ms.ToArray();
                }

                // 3. 데이터를 암호화합니다.
                byte[] encryptedData = Encrypt(finalBinaryData);

                // 4. 암호화된 데이터를 파일로 저장합니다.
                string folderPath = "Assets/Resources/DataBin";
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string filePath = Path.Combine(folderPath, $"{className}.bytes");
                File.WriteAllBytes(filePath, encryptedData);

                Debug.Log($"{className} 바이너리 암호화 완료.");
                AssetDatabase.Refresh();
            }
        }
        catch (WebException ex)
        {
            Debug.LogError("TSV 다운로드 실패 바이너리: " + ex.Message);
        }
    }

    void WriteValueByType(BinaryWriter bw, string value, string typeName)
    {
        if (typeName == "int")
        {
            int v = 0;
            int.TryParse(value, out v);
            bw.Write(v);
            return;
        }

        if (typeName == "float")
        {
            float v = 0f;
            float.TryParse(value, out v);
            bw.Write(v);
            return;
        }

        if (typeName == "bool")
        {
            bool v = false;
            if (value == "1") v = true;
            else if (value == "0") v = false;
            else bool.TryParse(value, out v);
            bw.Write(v);
            return;
        }

        if (typeName == "string")
        {
            WriteString(bw, value);
            return;
        }

        if (typeName == "int[]")
        {
            var arr = SplitSafe(value);
            bw.Write(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                int v = 0;
                int.TryParse(arr[i], out v);
                bw.Write(v);
            }
            return;
        }

        if (typeName == "float[]")
        {
            var arr = SplitSafe(value);
            bw.Write(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                float v = 0f;
                float.TryParse(arr[i], out v);
                bw.Write(v);
            }
            return;
        }

        if (typeName == "bool[]")
        {
            var arr = SplitSafe(value);
            bw.Write(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                string t = arr[i].Trim().ToLower();
                bool v = false;
                if (t == "1" || t == "true") v = true;
                else if (t == "0" || t == "false") v = false;
                bw.Write(v);
            }
            return;
        }

        if (typeName == "string[]")
        {
            var arr = SplitSafe(value);
            bw.Write(arr.Length);
            for (int i = 0; i < arr.Length; i++)
                WriteString(bw, arr[i]);
            return;
        }

        WriteString(bw, value);
    }

    void WriteString(BinaryWriter bw, string s)
    {
        if (s == null)
        {
            bw.Write(-1);
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(s);
        bw.Write(bytes.Length);
        bw.Write(bytes);
    }

    string[] SplitSafe(string s)
    {
        if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
        return s.Split('|');
    }
    private byte[] Encrypt(byte[] data)
    {
        using (var aes = System.Security.Cryptography.Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(EncryptionConfig.Key);
            aes.IV = Encoding.UTF8.GetBytes(EncryptionConfig.IV);
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                return encryptor.TransformFinalBlock(data, 0, data.Length);
            }
        }
    }
}
