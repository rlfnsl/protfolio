using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public GameObject prefabs;

    List<GameObject> pools;
    private int nextSearchIndex;


    [Header("🔧 초기 생성 수")]
    public int preloadCount = 10; // 시작 시 미리 생성할 오브젝트 수

    private void Awake()
    {
        pools = new List<GameObject>(preloadCount);
        PreloadObjects(preloadCount); // 초기 오브젝트 생성
    }

    private void PreloadObjects(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefabs, transform);
            obj.SetActive(false);      // 비활성화된 상태로 생성
            pools.Add(obj);            // 풀에 추가
        }
    }

    public GameObject Get()//오브젝트 풀링 구현 코드
    {
        GameObject select = null;

        for (int i = 0; i < pools.Count; i++)//이미 생성되었던 오브젝트가 비활성화 상태로 있는경우 활용
        {
            int index = (nextSearchIndex + i) % pools.Count;
            GameObject item = pools[index];
            if(!item.activeSelf)
            {
                select = item;
                select.SetActive(true);//오브젝트 활성화
                nextSearchIndex = (index + 1) % pools.Count;
                break;
            }
        }

        if(!select)//재활용할 오브젝트가 없는 경우
        {
            select = Instantiate(prefabs, transform);//새 오브젝트 생성
            pools.Add(select);//풀링에 활용할 리스트에 추가
            nextSearchIndex = 0;
        }

        return select;
    }

    public void AllObjectRelease()
    {
        foreach (GameObject item in pools)
        {
            if(item.activeSelf)
            {
                item.GetComponent<Enemy>().Die();
                item.SetActive(false);
            }
        }
    }
}
