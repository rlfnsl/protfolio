// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\SeatData\SheetDataController.cs
// Lines: full file

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class MainSheetDatas
{
    public string sheet_name;
    public string gid;
    public int versionNum;
}

[System.Serializable]
public class SheetVersion
{
    public string sheetName;
    public int version;
}

[System.Serializable]
public class SheetVersionList
{
    public List<SheetVersion> versions = new List<SheetVersion>();

    public int GetVersion(string sheetName)
    {
        var entry = versions.Find(v => v.sheetName == sheetName);
        return entry != null ? entry.version : -1;
    }

    public void UpdateVersion(string sheetName, int newVersion)
    {
        var entry = versions.Find(v => v.sheetName == sheetName);
        if (entry != null) entry.version = newVersion;
        else versions.Add(new SheetVersion { sheetName = sheetName, version = newVersion });
    }
}

public class SheetDataController : MonoBehaviour
{
    public static SheetDataController instance;

    const int SheetRequestTimeoutSeconds = 15;

    string mainSheetURL = "<REDACTED_URL>";
    string fixedURL = "<REDACTED>";

    private string savePath => Application.persistentDataPath;
    private string versionFilePath => Path.Combine(savePath, "data_versions.json");

    [SerializeField] private List<MainSheetDatas> mainSheetDatas;
    [SerializeField] private int maxParallelSheetDownloads = 4;
    [SerializeField] private bool logSheetProfile = true;

    public bool isLoadingDone = false;

    public event Action TestUpdateSheet = () => { };
    private readonly Dictionary<string, string> tsvCache = new Dictionary<string, string>();

    void Awake()
    {
        instance = this;
    }

    IEnumerator Start()
    {
        yield return new WaitUntil(() => APIData.Instance);
        yield return StartCoroutine(DownloadMetaAndUpdateSheets());
    }

    void GetMainSheetData(string tsv)
    {
        mainSheetDatas = new List<MainSheetDatas>();

        string[] seatArr = tsv.Split("\n");
        int row = seatArr.Length;

        for (int i = 1; i < row; i++)
        {
            string[] cols = seatArr[i].Split('\t');
            if (cols.Length < 3) continue;

            MainSheetDatas msd = new MainSheetDatas();
            msd.sheet_name = cols[0];
            msd.gid = cols[1];

            if (int.TryParse(cols[2], out int v))
                msd.versionNum = v;
            else
                msd.versionNum = 0;

            mainSheetDatas.Add(msd);
        }
    }

    IEnumerator DownloadMetaAndUpdateSheets()
    {
        float totalStartTime = Time.realtimeSinceStartup;
        UnityWebRequest www = UnityWebRequest.Get(mainSheetURL);
        www.timeout = SheetRequestTimeoutSeconds;
        float metaStartTime = Time.realtimeSinceStartup;
        yield return www.SendWebRequest();
        float metaSeconds = Time.realtimeSinceStartup - metaStartTime;

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Meta download failed: " + www.error);
            isLoadingDone = true;
            yield break;
        }

        GetMainSheetData(www.downloadHandler.text);
        LogSheetProfile($"meta network {metaSeconds:0.000}s, sheets {mainSheetDatas.Count}");

        SheetVersionList localVersions = LoadLocalVersions();
        bool anyUpdated = false;
        List<MainSheetDatas> sheetsToUpdate = new List<MainSheetDatas>();

        foreach (var sheet in mainSheetDatas)
        {
            int localVersion = localVersions.GetVersion(sheet.sheet_name);
            if (localVersion < sheet.versionNum || GameManager.gInstance.IsDev)
                sheetsToUpdate.Add(sheet);
        }

        int nextIndex = 0;
        int activeCount = 0;
        int completedCount = 0;
        int successCount = 0;
        int downloadLimit = Mathf.Max(1, maxParallelSheetDownloads);

        while (completedCount < sheetsToUpdate.Count)
        {
            while (activeCount < downloadLimit && nextIndex < sheetsToUpdate.Count)
            {
                MainSheetDatas sheet = sheetsToUpdate[nextIndex++];
                activeCount++;
                StartCoroutine(DownloadAndSaveEncrypted(sheet, success =>
                {
                    activeCount--;
                    completedCount++;
                    if (!success)
                        return;

                    localVersions.UpdateVersion(sheet.sheet_name, sheet.versionNum);
                    anyUpdated = true;
                    successCount++;
                }));
            }

            yield return null;
        }

        if (anyUpdated)
            SaveLocalVersions(localVersions);

        LogSheetProfile($"sheet update total {Time.realtimeSinceStartup - totalStartTime:0.000}s, updated {successCount}/{sheetsToUpdate.Count}, dev {GameManager.gInstance.IsDev}");
        isLoadingDone = true;
    }

    IEnumerator DownloadAndSaveEncrypted(MainSheetDatas meta, Action<bool> completed = null)
    {
        UnityWebRequest req = UnityWebRequest.Get(fixedURL + meta.gid);
        req.timeout = SheetRequestTimeoutSeconds;
        float startTime = Time.realtimeSinceStartup;
        yield return req.SendWebRequest();
        float networkSeconds = Time.realtimeSinceStartup - startTime;

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(meta.sheet_name + " download failed: " + req.error);
            completed?.Invoke(false);
            yield break;
        }

        string tsv = req.downloadHandler.text;
        float encryptStartTime = Time.realtimeSinceStartup;
        string filePath = GetEncryptedFilePath(meta.sheet_name);
        Task encryptTask = Task.Run(() =>
        {
            byte[] encrypted = AesCryptoUtil.EncryptStringToBytes(tsv);
            File.WriteAllBytes(filePath, encrypted);
        });

        while (!encryptTask.IsCompleted)
            yield return null;

        float encryptSeconds = Time.realtimeSinceStartup - encryptStartTime;

        if (encryptTask.IsFaulted)
        {
            Debug.LogError("Sheet encrypt/write failed: " + meta.sheet_name + " " + encryptTask.Exception?.GetBaseException().Message);
            completed?.Invoke(false);
            yield break;
        }

        tsvCache[meta.sheet_name] = tsv;
        LogSheetProfile($"sheet {meta.sheet_name} network {networkSeconds:0.000}s, encrypt/write {encryptSeconds:0.000}s, bytes {tsv.Length}");
        completed?.Invoke(true);
    }

    private string GetEncryptedFilePath(string sheetName)
    {
        return Path.Combine(savePath, sheetName + ".bytes");
    }

    public string GetTSVData(string sheetName)
    {
        if (tsvCache.TryGetValue(sheetName, out string cachedTsv))
            return cachedTsv;

        string filePath = GetEncryptedFilePath(sheetName);
        if (!File.Exists(filePath))
            return string.Empty;

        float startTime = Time.realtimeSinceStartup;
        byte[] encrypted = File.ReadAllBytes(filePath);
        string tsv = AesCryptoUtil.DecryptBytesToString(encrypted);
        tsvCache[sheetName] = tsv;
        LogSheetProfile($"sheet {sheetName} decrypt {Time.realtimeSinceStartup - startTime:0.000}s, bytes {encrypted.Length}");
        return tsv;
    }

    public IEnumerator GetTSVDataAsync(string sheetName, Action<string> completed)
    {
        if (tsvCache.TryGetValue(sheetName, out string cachedTsv))
        {
            completed?.Invoke(cachedTsv);
            yield break;
        }

        string filePath = GetEncryptedFilePath(sheetName);
        if (!File.Exists(filePath))
        {
            completed?.Invoke(string.Empty);
            yield break;
        }

        float startTime = Time.realtimeSinceStartup;
        Task<string> decryptTask = Task.Run(() =>
        {
            byte[] encrypted = File.ReadAllBytes(filePath);
            return AesCryptoUtil.DecryptBytesToString(encrypted);
        });

        while (!decryptTask.IsCompleted)
            yield return null;

        if (decryptTask.IsFaulted)
        {
            Debug.LogError("Sheet decrypt failed: " + sheetName + " " + decryptTask.Exception?.GetBaseException().Message);
            completed?.Invoke(string.Empty);
            yield break;
        }

        string tsv = decryptTask.Result;
        tsvCache[sheetName] = tsv;
        LogSheetProfile($"sheet {sheetName} async decrypt {Time.realtimeSinceStartup - startTime:0.000}s, chars {tsv.Length}");
        completed?.Invoke(tsv);
    }

    SheetVersionList LoadLocalVersions()
    {
        if (!File.Exists(versionFilePath))
            return new SheetVersionList();

        string json = File.ReadAllText(versionFilePath);
        var data = JsonUtility.FromJson<SheetVersionList>(json);
        return data ?? new SheetVersionList();
    }

    void SaveLocalVersions(SheetVersionList versionList)
    {
        string json = JsonUtility.ToJson(versionList, true);
        File.WriteAllText(versionFilePath, json);
    }

    public IEnumerator ForceUpdateAll()
    {
        SheetVersionList localVersions = LoadLocalVersions();

        foreach (var sheet in mainSheetDatas)
        {
            yield return StartCoroutine(DownloadAndSaveEncrypted(sheet));
            localVersions.UpdateVersion(sheet.sheet_name, sheet.versionNum);
        }

        SaveLocalVersions(localVersions);
        TestUpdateSheet();
    }

    private void LogSheetProfile(string message)
    {
        if (!logSheetProfile)
            return;

        Debug.Log($"[SheetProfile] {message}");
    }
}
