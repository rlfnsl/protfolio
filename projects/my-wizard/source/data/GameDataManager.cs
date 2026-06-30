using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    public SheetDataController sdc;

    private readonly List<GameDataTableBase> _tables = new List<GameDataTableBase>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        sdc = GetComponent<SheetDataController>();
        CollectTables();
    }

    private void CollectTables()
    {
        _tables.Clear();
        GetComponentsInChildren(true, _tables);
    }

    public IEnumerator InitAll()
    {
        if (sdc == null)
        {
            Debug.LogError("SheetDataController not found");
            yield break;
        }

        yield return new WaitUntil(() => sdc.isLoadingDone);

        for (int i = 0; i < _tables.Count; i++)
        {
            yield return StartCoroutine(_tables[i].InitAsync(sdc));
            yield return null;
        }

        sdc.TestUpdateSheet += OnSheetsUpdated;
    }

    private void OnDestroy()
    {
        if (sdc != null)
            sdc.TestUpdateSheet -= OnSheetsUpdated;
    }

    private void OnSheetsUpdated()
    {
        StartCoroutine(ReloadAllAsync());
    }

    private IEnumerator ReloadAllAsync()
    {
        for (int i = 0; i < _tables.Count; i++)
        {
            yield return StartCoroutine(_tables[i].ReloadAsync());
            yield return null;
        }
    }
}
