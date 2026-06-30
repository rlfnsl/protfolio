using UnityEngine;
using System.Collections;

public abstract class GameDataTableBase : MonoBehaviour
{
    protected SheetDataController sdc;

    public abstract string SheetName { get; }

    public void Init(SheetDataController controller)
    {
        sdc = controller;
        Reload();
    }

    public IEnumerator InitAsync(SheetDataController controller)
    {
        sdc = controller;
        yield return ReloadAsync();
    }

    public void Reload()
    {
        if (sdc == null) return;

        string tsv = sdc.GetTSVData(SheetName);
        if (string.IsNullOrEmpty(tsv)) return;

        float parseStartTime = Time.realtimeSinceStartup;
        Parse(tsv);
        LogParseTime(Time.realtimeSinceStartup - parseStartTime);
    }

    public IEnumerator ReloadAsync()
    {
        if (sdc == null)
            yield break;

        string tsv = string.Empty;
        yield return sdc.GetTSVDataAsync(SheetName, result => tsv = result);
        if (string.IsNullOrEmpty(tsv))
            yield break;

        yield return null;
        float parseStartTime = Time.realtimeSinceStartup;
        Parse(tsv);
        LogParseTime(Time.realtimeSinceStartup - parseStartTime);
        yield return null;
    }

    private void LogParseTime(float seconds)
    {
        Debug.Log($"[LoadingProfile] sheet {SheetName} parse {seconds:0.000}s");
    }

    protected abstract void Parse(string tsv);
}
