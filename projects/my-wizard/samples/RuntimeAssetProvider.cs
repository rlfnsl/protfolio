// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\Addressable\RuntimeAssetProvider.cs
// Lines: full file

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum RuntimeAssetBackend
{
    Addressables,
    Resources
}

public sealed class RuntimeAssetResult<T> where T : UnityEngine.Object
{
    public RuntimeAssetResult(string key, T result, bool succeeded, RuntimeAssetBackend backend, AsyncOperationHandle addressableHandle)
    {
        Key = key;
        Result = result;
        Succeeded = succeeded;
        Backend = backend;
        AddressableHandle = addressableHandle;
    }

    public string Key { get; }
    public T Result { get; }
    public bool Succeeded { get; }
    public RuntimeAssetBackend Backend { get; }
    public AsyncOperationHandle AddressableHandle { get; }
}

public class RuntimeAssetProvider : MonoBehaviour
{
    public static RuntimeAssetProvider Instance { get; private set; }

    private const RuntimeAssetBackend DefaultBackend = RuntimeAssetBackend.Resources;
    private const string BackendConfigResourceKey = "RuntimeAssetBackend";
    private const string CatalogResourceKey = "RuntimeAssetCatalog";
    private const string ResourceAssetRoot = "RuntimeAssets";
    private const bool DefaultFallbackToResourcesOnAddressablesFailure = true;

    [SerializeField] private RuntimeAssetBackend backend = DefaultBackend;
    [SerializeField] private bool fallbackToResourcesOnAddressablesFailure = DefaultFallbackToResourcesOnAddressablesFailure;

    private static RuntimeAssetBackend fallbackBackend = DefaultBackend;
    private static RuntimeAssetBackend configuredBackend = DefaultBackend;
    private static bool backendOverrideSet;
    private static bool backendConfigLoaded;
    private static bool fallbackToResourcesOnAddressablesFailureValue = DefaultFallbackToResourcesOnAddressablesFailure;
    private static readonly List<AsyncOperationHandle> loadedAddressableHandles = new List<AsyncOperationHandle>();
    private static readonly HashSet<GameObject> addressableInstances = new HashSet<GameObject>();
    private static readonly HashSet<GameObject> resourceInstances = new HashSet<GameObject>();
    private static RuntimeAssetCatalog runtimeCatalog;

    public static RuntimeAssetBackend Backend
    {
        get
        {
            if (Instance != null)
                return Instance.backend;

            return backendOverrideSet ? fallbackBackend : GetConfiguredBackend();
        }
        set
        {
            backendOverrideSet = true;
            fallbackBackend = value;
            if (Instance != null)
                Instance.backend = value;
        }
    }

    public static bool FallbackToResourcesOnAddressablesFailure
    {
        get => Instance != null ? Instance.fallbackToResourcesOnAddressablesFailure : fallbackToResourcesOnAddressablesFailureValue;
        set
        {
            fallbackToResourcesOnAddressablesFailureValue = value;
            if (Instance != null)
                Instance.fallbackToResourcesOnAddressablesFailure = value;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        fallbackBackend = backend;
        backendOverrideSet = true;
        fallbackToResourcesOnAddressablesFailureValue = fallbackToResourcesOnAddressablesFailure;
    }

    public static void LoadAssetAsync<T>(string key, Action<RuntimeAssetResult<T>> completed) where T : UnityEngine.Object
    {
        if (Backend == RuntimeAssetBackend.Resources)
        {
            LoadResourceAssetAsync(key, completed);
            return;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        handle.Completed += completedHandle =>
        {
            bool succeeded = completedHandle.Status == AsyncOperationStatus.Succeeded && completedHandle.Result != null;
            if (succeeded)
            {
                loadedAddressableHandles.Add(completedHandle);
                completed?.Invoke(new RuntimeAssetResult<T>(key, completedHandle.Result, true, RuntimeAssetBackend.Addressables, completedHandle));
                return;
            }

            if (completedHandle.IsValid())
                Addressables.Release(completedHandle);

            if (FallbackToResourcesOnAddressablesFailure)
            {
                LoadResourceAssetAsync(key, completed);
                return;
            }

            completed?.Invoke(new RuntimeAssetResult<T>(key, null, false, RuntimeAssetBackend.Addressables, completedHandle));
        };
    }

    public static IEnumerator LoadAssetCoroutine<T>(string key, Action<RuntimeAssetResult<T>> completed) where T : UnityEngine.Object
    {
        RuntimeAssetResult<T> result = null;
        bool isDone = false;

        LoadAssetAsync<T>(key, loaded =>
        {
            result = loaded;
            isDone = true;
        });

        while (!isDone)
            yield return null;

        completed?.Invoke(result);
    }

    public static void InstantiateAsync(string key, Action<RuntimeAssetResult<GameObject>> completed)
    {
        InstantiateAsync(key, null, completed);
    }

    public static void InstantiateAsync(string key, Transform parent, Action<RuntimeAssetResult<GameObject>> completed)
    {
        if (Backend == RuntimeAssetBackend.Resources)
        {
            InstantiateResourceAsync(key, parent, completed);
            return;
        }

        AsyncOperationHandle<GameObject> handle = parent != null
            ? Addressables.InstantiateAsync(key, parent)
            : Addressables.InstantiateAsync(key);

        handle.Completed += completedHandle =>
        {
            bool succeeded = completedHandle.Status == AsyncOperationStatus.Succeeded && completedHandle.Result != null;
            if (succeeded)
            {
                addressableInstances.Add(completedHandle.Result);
                completed?.Invoke(new RuntimeAssetResult<GameObject>(key, completedHandle.Result, true, RuntimeAssetBackend.Addressables, completedHandle));
                return;
            }

            if (completedHandle.IsValid())
                Addressables.Release(completedHandle);

            if (FallbackToResourcesOnAddressablesFailure)
            {
                InstantiateResourceAsync(key, parent, completed);
                return;
            }

            completed?.Invoke(new RuntimeAssetResult<GameObject>(key, null, false, RuntimeAssetBackend.Addressables, completedHandle));
        };
    }

    public static void ReleaseInstance(GameObject instance)
    {
        if (instance == null)
            return;

        if (addressableInstances.Remove(instance))
        {
            Addressables.ReleaseInstance(instance);
            return;
        }

        if (resourceInstances.Remove(instance))
        {
            DestroyRuntimeObject(instance);
            return;
        }

        if (Backend == RuntimeAssetBackend.Addressables && Addressables.ReleaseInstance(instance))
            return;

        DestroyRuntimeObject(instance);
    }

    public static void ReleaseLoadedAssets()
    {
        foreach (GameObject instance in addressableInstances)
        {
            if (instance != null)
                Addressables.ReleaseInstance(instance);
        }

        foreach (GameObject instance in resourceInstances)
        {
            if (instance != null)
                DestroyRuntimeObject(instance);
        }

        foreach (AsyncOperationHandle handle in loadedAddressableHandles)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        addressableInstances.Clear();
        resourceInstances.Clear();
        loadedAddressableHandles.Clear();
    }

    private static string ResolveResourceKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        string normalized = key.Replace('\\', '/');
        const string resourcesSegment = "/Resources/";
        int resourcesIndex = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0)
            normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);

        int extensionIndex = normalized.LastIndexOf('.');
        if (extensionIndex > 0)
            normalized = normalized.Substring(0, extensionIndex);

        return normalized;
    }

    private static List<string> BuildResourceKeys(string key)
    {
        string normalized = ResolveResourceKey(key);
        List<string> keys = new List<string>();
        AddResourceKey(keys, CombineResourcePath(ResourceAssetRoot, normalized));
        AddResourceKey(keys, normalized);
        return keys;
    }

    private static void AddResourceKey(List<string> keys, string key)
    {
        if (string.IsNullOrEmpty(key) || keys.Contains(key))
            return;

        keys.Add(key);
    }

    private static string CombineResourcePath(string root, string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        if (key.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            return key;

        return root + "/" + key;
    }

    private static void LoadResourceAssetAsync<T>(string key, Action<RuntimeAssetResult<T>> completed) where T : UnityEngine.Object
    {
        List<string> resourceKeys = BuildResourceKeys(key);
        TryLoadResourceAtIndex(key, resourceKeys, 0, completed);
    }

    private static void TryLoadResourceAtIndex<T>(string key, List<string> resourceKeys, int index, Action<RuntimeAssetResult<T>> completed) where T : UnityEngine.Object
    {
        if (index >= resourceKeys.Count)
        {
            TryGetCatalogAsset(key, out T catalogAsset);
            completed?.Invoke(new RuntimeAssetResult<T>(key, catalogAsset, catalogAsset != null, RuntimeAssetBackend.Resources, default));
            return;
        }

        ResourceRequest request = Resources.LoadAsync<T>(resourceKeys[index]);
        request.completed += _ =>
        {
            T asset = request.asset as T;
            if (asset != null)
            {
                completed?.Invoke(new RuntimeAssetResult<T>(key, asset, true, RuntimeAssetBackend.Resources, default));
                return;
            }

            TryLoadResourceAtIndex(key, resourceKeys, index + 1, completed);
        };
    }

    private static void InstantiateResourceAsync(string key, Transform parent, Action<RuntimeAssetResult<GameObject>> completed)
    {
        LoadResourceAssetAsync<GameObject>(key, result =>
        {
            GameObject prefab = result.Result;
            GameObject instance = prefab != null ? UnityEngine.Object.Instantiate(prefab, parent) : null;
            if (instance != null)
                resourceInstances.Add(instance);

            completed?.Invoke(new RuntimeAssetResult<GameObject>(key, instance, instance != null, RuntimeAssetBackend.Resources, default));
        });
    }

    private static bool TryGetCatalogAsset<T>(string key, out T asset) where T : UnityEngine.Object
    {
        asset = null;
        RuntimeAssetCatalog catalog = GetRuntimeCatalog();
        return catalog != null && catalog.TryGetAsset(key, out asset);
    }

    private static RuntimeAssetCatalog GetRuntimeCatalog()
    {
        if (runtimeCatalog == null)
            runtimeCatalog = Resources.Load<RuntimeAssetCatalog>(CatalogResourceKey);

        return runtimeCatalog;
    }

    private static RuntimeAssetBackend GetConfiguredBackend()
    {
        if (backendConfigLoaded)
            return configuredBackend;

        backendConfigLoaded = true;
        TextAsset backendConfig = Resources.Load<TextAsset>(BackendConfigResourceKey);
        if (backendConfig != null && Enum.TryParse(backendConfig.text.Trim(), true, out RuntimeAssetBackend parsedBackend))
            configuredBackend = parsedBackend;

        fallbackBackend = configuredBackend;
        return configuredBackend;
    }

    private static void DestroyRuntimeObject(UnityEngine.Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(obj);
        else
            UnityEngine.Object.DestroyImmediate(obj);
    }
}
