using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class RuntimeAssetResourcesExporter
{
    private const string ResourcesRoot = "Assets/Resources/RuntimeAssets";
    private const string ManifestPath = ResourcesRoot + "/_RuntimeAssetsManifest.txt";
    private const string BackendConfigPath = "Assets/Resources/RuntimeAssetBackend.txt";

    [MenuItem("Tools/Runtime Assets/Use Addressables Backend")]
    public static void UseAddressablesBackend()
    {
        WriteBackendConfig(RuntimeAssetBackend.Addressables);
    }

    [MenuItem("Tools/Runtime Assets/Use Resources Backend")]
    public static void UseResourcesBackend()
    {
        WriteBackendConfig(RuntimeAssetBackend.Resources);
    }

    [MenuItem("Tools/Runtime Assets/Copy Addressables To Resources")]
    public static void CopyAddressablesToResources()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("Addressables settings were not found. RuntimeAssets were not copied.");
            return;
        }

        Directory.CreateDirectory(ResourcesRoot);

        Dictionary<string, string> assetPathsByKey = new Dictionary<string, string>();
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null)
                continue;

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.address) || string.IsNullOrEmpty(entry.AssetPath))
                    continue;

                assetPathsByKey[entry.address] = entry.AssetPath;
            }
        }

        List<string> manifestLines = new List<string>();
        int copiedCount = 0;
        foreach (KeyValuePair<string, string> pair in assetPathsByKey.OrderBy(item => item.Key))
        {
            string sourcePath = pair.Value;
            string extension = Path.GetExtension(sourcePath);
            if (string.Equals(extension, ".cs", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string targetPath = Path.Combine(ResourcesRoot, BuildResourceRelativePath(pair.Key, extension)).Replace('\\', '/');
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            if (AssetDatabase.LoadAssetAtPath<Object>(targetPath) != null)
            {
                AssetDatabase.DeleteAsset(targetPath);
            }
            else if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                string metaPath = targetPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
            }

            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                Debug.LogWarning($"Failed to copy RuntimeAsset: {pair.Key} ({sourcePath})");
                continue;
            }

            manifestLines.Add($"{pair.Key}\t{sourcePath}\t{targetPath}");
            copiedCount++;
        }

        File.WriteAllLines(ManifestPath, manifestLines, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(ManifestPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Copied {copiedCount} Addressables to {ResourcesRoot}.");
    }

    private static string BuildResourceRelativePath(string key, string extension)
    {
        string[] parts = key.Replace('\\', '/').Split('/');
        for (int i = 0; i < parts.Length; i++)
            parts[i] = SanitizePathPart(parts[i]);

        string relativePath = string.Join("/", parts);
        return relativePath + extension;
    }

    private static string SanitizePathPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "_";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char item in value.Trim())
        {
            builder.Append(invalidChars.Contains(item) ? '_' : item);
        }

        string sanitized = builder.ToString().TrimEnd('.');
        return string.IsNullOrEmpty(sanitized) ? "_" : sanitized;
    }

    private static void WriteBackendConfig(RuntimeAssetBackend backend)
    {
        Directory.CreateDirectory("Assets/Resources");
        File.WriteAllText(BackendConfigPath, backend.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(BackendConfigPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Runtime asset backend set to {backend}.");
    }
}
