using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Callbacks;
using UnityEngine;

[InitializeOnLoad]
public static class RuntimeAssetCatalogBuilder
{
    private const string CatalogPath = "Assets/Resources/RuntimeAssetCatalog.asset";

    static RuntimeAssetCatalogBuilder()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        QueueRebuildIfMissing();
    }

    [MenuItem("Tools/Runtime Assets/Rebuild Resources Catalog")]
    public static void Rebuild()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("Addressables 설정을 찾을 수 없어 RuntimeAssetCatalog를 만들 수 없습니다.");
            return;
        }

        Directory.CreateDirectory("Assets/Resources");

        RuntimeAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<RuntimeAssetCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RuntimeAssetCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        Dictionary<string, Object> assetsByKey = new Dictionary<string, Object>();
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null)
                continue;

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.address))
                    continue;

                Object asset = LoadBestAsset(entry.AssetPath, entry.address);
                if (asset == null)
                    continue;

                assetsByKey[entry.address] = asset;
            }
        }

        List<RuntimeAssetCatalog.Entry> entries = assetsByKey
            .OrderBy(pair => pair.Key)
            .Select(pair => new RuntimeAssetCatalog.Entry
            {
                key = pair.Key,
                asset = pair.Value
            })
            .ToList();

        catalog.SetEntries(entries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"RuntimeAssetCatalog 생성 완료: {entries.Count}개 에셋");
    }

    [DidReloadScripts]
    private static void RebuildIfMissing()
    {
        QueueRebuildIfMissing();
    }

    private static void QueueRebuildIfMissing()
    {
        if (File.Exists(CatalogPath))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.delayCall -= Rebuild;
        EditorApplication.delayCall += Rebuild;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            QueueRebuildIfMissing();
    }

    private static Object LoadBestAsset(string assetPath, string address)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets != null && assets.Length > 0)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            Sprite sprite = assets
                .OfType<Sprite>()
                .FirstOrDefault(item => item.name == address || item.name == fileName);

            if (sprite != null)
                return sprite;
        }

        return AssetDatabase.LoadMainAssetAtPath(assetPath);
    }
}
