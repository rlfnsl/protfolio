using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MetalBotManager : MonoBehaviour
{
    public Dictionary<string, MetalBot> metalbots;

    private void Awake()
    {
        // Scene에서 모든 GameObject를 찾습니다.
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        metalbots = new Dictionary<string, MetalBot>();

        foreach (GameObject obj in allObjects)
        {
            MetalBot metalBot = obj.GetComponent<MetalBot>();
            if (metalBot != null)
            {
                // GameObject의 이름을 키로 사용하여 딕셔너리에 추가합니다.
                metalbots.Add(metalBot.Type, metalBot);
            }
        }
    }


    /// <summary>
    /// 애니메이션 트리거
    /// </summary>
    /// <param name="botName"></param>
    /// <param name="triggerName"></param>
    public void AnimateBot(string botName, string triggerName)
    {
        if (metalbots.TryGetValue(botName, out MetalBot metalBot))
        {
            metalBot.InitParameter();
            metalBot.botani.SetTrigger(triggerName);
        }
        else
        {
            Debug.LogError($"MetalBot with the name '{botName}' not found!");
        }
    }
    /// <summary>
    /// 애니메이션 기다려주기.
    /// </summary>
    /// <param name="botName"></param>
    /// <param name="stateName"></param>
    /// <param name="normalizedTime"></param>
    /// <returns></returns>
    public IEnumerator WaitForAnimationEnd(string botName, string stateName, float normalizedTime = 1f)
    {
        if (metalbots.TryGetValue(botName, out MetalBot metalBot))
        {

            AnimatorStateInfo stateInfo = metalBot.botani.GetCurrentAnimatorStateInfo(0);

            while (!stateInfo.IsName(stateName) || stateInfo.normalizedTime < normalizedTime)
            {
                yield return null;
                stateInfo = metalBot.botani.GetCurrentAnimatorStateInfo(0);
            }
        }
        else
        {
            Debug.LogError($"MetalBot with the name '{botName}' not found!");
        }
    }



    /// <summary>
    /// 애니메이션 멈추기
    /// </summary>
    /// <param name="botName"></param>
    /// <param name="speed"></param>
    public void SetAnimationSpeed(string botName ,float speed)
    {
        if (metalbots.TryGetValue(botName, out MetalBot metalBot))
        {
            metalBot.botani.speed = speed;
        }
        else
        {
            Debug.LogError($"MetalBot with the name '{botName}' not found!");
        }
    }

    /// <summary>
    /// 중복되어있는 애니메이션은 한번에 처리
    /// </summary>
    /// <param name="_str"></param>
    public void DuplicationAnim(string _str)
    {
        foreach(MetalBot bot in metalbots.Values)
        {
            bot.botani.SetTrigger(_str);
        }
    }


}
