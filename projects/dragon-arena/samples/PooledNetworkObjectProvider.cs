// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\Dragon-Arena\Assets\Scripts\Photon\PooledNetworkObjectProvider.cs
// Lines: full file

using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using UnityEngine;

public class PooledNetworkObjectProvider : NetworkObjectProviderDefault
{
    private readonly Dictionary<NetworkPrefabId, Queue<NetworkObject>> _pool = new();

    public override NetworkObjectAcquireResult AcquirePrefabInstance(NetworkRunner runner, in NetworkPrefabAcquireContext context, out NetworkObject instance)
    {
        instance = null;

        if (DelayIfSceneManagerIsBusy && runner.SceneManager.IsBusy)
        {
            return NetworkObjectAcquireResult.Retry;
        }

        NetworkObject prefab;
        try
        {
            prefab = runner.Prefabs.Load(context.PrefabId, isSynchronous: context.IsSynchronous);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Pool] Failed to load prefab: {ex}");
            return NetworkObjectAcquireResult.Failed;
        }

        if (!prefab)
            return NetworkObjectAcquireResult.Retry;

        if (_pool.TryGetValue(context.PrefabId, out var queue) && queue.Count > 0)
        {
            instance = queue.Dequeue();
            instance.gameObject.SetActive(true);
        }
        else
        {
            instance = InstantiatePrefab(runner, prefab);
        }

        if (context.DontDestroyOnLoad)
            runner.MakeDontDestroyOnLoad(instance.gameObject);
        else
            runner.MoveToRunnerScene(instance.gameObject);

        runner.Prefabs.AddInstance(context.PrefabId);
        return NetworkObjectAcquireResult.Success;
    }

    public override void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
    {
        var instance = context.Object;

        if (!context.IsBeingDestroyed)
        {
            instance.gameObject.SetActive(false);

            if (!_pool.TryGetValue(context.TypeId.AsPrefabId, out var queue))
            {
                queue = new Queue<NetworkObject>();
                _pool[context.TypeId.AsPrefabId] = queue;
            }

            queue.Enqueue(instance);
        }

        if (context.TypeId.IsPrefab)
        {
            runner.Prefabs.RemoveInstance(context.TypeId.AsPrefabId);
        }
    }
}
