using System.Collections.Generic;
using UnityEngine;

public class ObjectPool_WS<T> where T : MonoBehaviour
{
    private Queue<T> pooledObjects = new Queue<T>();
    private HashSet<T> activeObjects = new HashSet<T>();
    private T prefab;
    private Transform parentTransform;
    private System.Action<T> InitializeAction;

    public ObjectPool_WS(T prefab, int initialSize, Transform parentTransform, System.Action<T> initializeAction = null)
    {
        this.prefab = prefab;
        this.parentTransform = parentTransform;
        InitializeAction = initializeAction;

        for (int i = 0; i < initialSize; i++)
        {
            CreateAndEnqueueObject();
        }
    }

    private T CreateAndEnqueueObject(bool enqueueAfterCreation = true)
    {
        T obj = Object.Instantiate(prefab, parentTransform);
        obj.gameObject.SetActive(false);

        InitializeAction?.Invoke(obj);

        // 풀 오너 연결
        var submit = obj.GetComponent<SkillSubmit>();
        if (submit != null)
        {
            submit.SetPool(this as ObjectPool_WS<SkillSubmit>);
        }

        if (enqueueAfterCreation)
        {
            pooledObjects.Enqueue(obj);
        }

        return obj;
    }

    public T GetPooledObject(bool isActive = true)
    {
        T obj;
        if (pooledObjects.Count > 0)
        {
            obj = pooledObjects.Dequeue();
        }
        else
        {
            obj = CreateAndEnqueueObject(false);
        }

        if (isActive)
        {
            var hitRange = obj.GetComponent<SkillHitRange>();
            if (hitRange != null)
            {
                hitRange.SetActive(true);
            }
            else
            {
                obj.gameObject.SetActive(true);
            }
        }

        activeObjects.Add(obj);
        return obj;
    }

    public void ReturnObject(T obj)
    {
        if (obj == null)
            return;

        var hitRange = obj.GetComponent<SkillHitRange>();
        if (hitRange != null)
        {
            if (hitRange.PlayParticle != null)
            {
                hitRange.SetActive(false);
            }
            else
            {
                obj.gameObject.SetActive(false);
            }
        }
        else
        {
            obj.gameObject.SetActive(false);
        }

        activeObjects.Remove(obj);
        pooledObjects.Enqueue(obj);
    }

    public void SetActive(bool active)
    {
        while (pooledObjects.Count > 0)
        {
            var obj = pooledObjects.Peek();
            if (obj == null)
            {
                pooledObjects.Dequeue();
                continue;
            }

            var hitRange = obj.GetComponent<SkillHitRange>();
            if (hitRange != null)
            {
                hitRange.SetActive(active);
            }
            else
            {
                obj.gameObject.SetActive(active);
            }

            break;
        }
    }

    public bool HasActiveObject()
    {
        return activeObjects.Count > 0;
    }
}
