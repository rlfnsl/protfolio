using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeAssetCatalog", menuName = "Runtime Assets/Catalog")]
public class RuntimeAssetCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;
        public UnityEngine.Object asset;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, UnityEngine.Object> lookup;

    public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
    public void SetEntries(List<Entry> newEntries)
    {
        entries = newEntries ?? new List<Entry>();
        lookup = null;
    }
#endif

    public bool TryGetAsset<T>(string key, out T asset) where T : UnityEngine.Object
    {
        BuildLookup();

        asset = null;
        if (string.IsNullOrEmpty(key) || !lookup.TryGetValue(key, out UnityEngine.Object value))
            return false;

        asset = value as T;
        return asset != null;
    }

    private void BuildLookup()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<string, UnityEngine.Object>();
        foreach (Entry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key) || entry.asset == null)
                continue;

            lookup[entry.key] = entry.asset;
        }
    }
}
