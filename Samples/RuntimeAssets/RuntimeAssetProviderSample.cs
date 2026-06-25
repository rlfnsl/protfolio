using System;
using System.Collections;
using UnityEngine;

#if USE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace PortfolioSamples.RuntimeAssets
{
    public enum RuntimeAssetBackend
    {
        Addressables,
        Resources
    }

    public readonly struct RuntimeAssetResult<T> where T : UnityEngine.Object
    {
        public RuntimeAssetResult(string key, T asset, RuntimeAssetBackend backend, bool succeeded)
        {
            Key = key;
            Asset = asset;
            Backend = backend;
            Succeeded = succeeded;
        }

        public string Key { get; }
        public T Asset { get; }
        public RuntimeAssetBackend Backend { get; }
        public bool Succeeded { get; }
    }

    public sealed class RuntimeAssetProviderSample : MonoBehaviour
    {
        public static RuntimeAssetProviderSample Instance { get; private set; }

        [SerializeField] private RuntimeAssetBackend backend = RuntimeAssetBackend.Addressables;
        [SerializeField] private bool fallbackToResources = true;
        [SerializeField] private string resourcesRoot = "RuntimeAssets";

        public RuntimeAssetBackend Backend
        {
            get => backend;
            set => backend = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public IEnumerator LoadAsset<T>(string key, Action<RuntimeAssetResult<T>> completed)
            where T : UnityEngine.Object
        {
            if (backend == RuntimeAssetBackend.Resources)
            {
                yield return LoadFromResources(key, completed);
                yield break;
            }

#if USE_ADDRESSABLES
            var handle = Addressables.LoadAssetAsync<T>(key);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                completed?.Invoke(new RuntimeAssetResult<T>(key, handle.Result, RuntimeAssetBackend.Addressables, true));
                yield break;
            }

            if (handle.IsValid())
                Addressables.Release(handle);

            if (fallbackToResources)
            {
                yield return LoadFromResources(key, completed);
                yield break;
            }
#endif

            completed?.Invoke(new RuntimeAssetResult<T>(key, null, backend, false));
        }

        private IEnumerator LoadFromResources<T>(string key, Action<RuntimeAssetResult<T>> completed)
            where T : UnityEngine.Object
        {
            string resourceKey = $"{resourcesRoot}/{key}".Replace("\\", "/");
            ResourceRequest request = Resources.LoadAsync<T>(resourceKey);
            yield return request;

            var asset = request.asset as T;
            completed?.Invoke(new RuntimeAssetResult<T>(key, asset, RuntimeAssetBackend.Resources, asset != null));
        }
    }
}

