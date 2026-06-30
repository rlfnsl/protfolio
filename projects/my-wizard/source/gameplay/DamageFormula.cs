using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class DamageFormula : MonoBehaviour
{
    // 정령 시너지 버프 (속성별 추가 공격력)
    public static float fireBuff = 0f;
    public static float waterBuff = 0f;
    public static float electricBuff = 0f;
    public static float landBuff = 0f;

    public static Player _player;

    private void Start()
    {
        _player = SpawnManager.instance.player;
    }

    /// <summary>
    /// 유저의 데미지 계산식
    /// 기본 공격력 * 속성 추가수치(퍼센트) -> 스킬데미지(퍼센트)에 대입
    /// </summary>
    /// <param name="type">속성 타입 1:불 | 2:물 | 3:번개 | 4:땅</param>
    /// <param name="SkillDamage">퍼센트</param>
    public static float SetPlayerAttack(float SkillDamage, ElementType type = ElementType.None)
    {
        float Attack = _player.playerStats.TotalAtk;
        //플레이어의 기본 공격력

        float attributeValue = 1f; // 곱해야 할 속성 수치 값 (기본 1)
        switch (type)
        {
            case ElementType.None:
                //무속성
                break;

            case ElementType.fire:
                attributeValue += _player.playerStats.TotalDamageFire / 100f;
                attributeValue += fireBuff; // ?? 발화 버프 적용
                attributeValue *= _player.Add_Fire_Damage;
                break;

            case ElementType.water:
                attributeValue += _player.playerStats.TotalDamageWater / 100f;
                attributeValue += waterBuff; // ?? 가랑비 버프 적용
                attributeValue *= _player.Add_Water_Damage;
                break;

            case ElementType.electric:
                attributeValue += _player.playerStats.TotalDamageElectric / 100f;
                attributeValue += electricBuff; // ? 공명 버프 적용
                attributeValue *= _player.Add_Electric_Damage;
                break;

            case ElementType.land:
                attributeValue += _player.playerStats.TotalDamageLand / 100f;
                attributeValue += landBuff; // ?? 침식 버프 적용
                attributeValue *= _player.Add_Land_Damage;
                break;
        }
        Attack *= attributeValue; // 기본 공격력에 속성 수치 및 버프 적용
        return Attack + SkillDamage;
    }

    /// <summary>
    /// 유저가 데미지를 입는 부분
    /// 데미지(수치) - 방어력 * 속성 저항(퍼센트)
    /// </summary>
    /// <param name="type">속성 타입 1:불 | 2:물 | 3:번개 | 4:땅</param>
    /// <param name="Damage">피해받을 데미지 값(수치)</param>
    public static float SetPlayerDefense(float Damage, ElementType type = ElementType.None)
    {
        float Def = _player.playerStats.TotalDef;

        float attributeValue = 1f; // 곱해야 할 속성 저항 수치 값
        switch (type)
        {
            case ElementType.None:
                break;
            case ElementType.fire:
                attributeValue += _player.playerStats.TotalResistFire / 100f;
                break;
            case ElementType.water:
                attributeValue += _player.playerStats.TotalResistWater / 100f;
                break;
            case ElementType.electric:
                attributeValue += _player.playerStats.TotalResistElectric / 100f;
                break;
            case ElementType.land:
                attributeValue += _player.playerStats.TotalResistLand / 100f;
                break;
        }

        Def *= attributeValue;

        if (Def >= Damage)
            return 0;
        else
            return Damage - Def;
    }

    /// <summary>
    /// 특정 속성 버프를 추가하는 함수 (SpiritManager에서 호출)
    /// </summary>
    public static void AddElementBuff(ElementType type, float value)
    {
        switch (type)
        {
            case ElementType.fire:
                fireBuff += value;
                break;
            case ElementType.water:
                waterBuff += value;
                break;
            case ElementType.electric:
                electricBuff += value;
                break;
            case ElementType.land:
                landBuff += value;
                break;
        }

        Debug.Log($"[DamageFormula] {type} 버프 +{value * 100}% 적용됨");
    }

    /// <summary>
    /// 특정 속성 버프를 제거하는 함수 (정령 해제 시 호출 가능)
    /// </summary>
    public static void RemoveElementBuff(ElementType type, float value)
    {
        switch (type)
        {
            case ElementType.fire:
                fireBuff = Mathf.Max(0f, fireBuff - value);
                break;
            case ElementType.water:
                waterBuff = Mathf.Max(0f, waterBuff - value);
                break;
            case ElementType.electric:
                electricBuff = Mathf.Max(0f, electricBuff - value);
                break;
            case ElementType.land:
                landBuff = Mathf.Max(0f, landBuff - value);
                break;
        }

        Debug.Log($"[DamageFormula] {type} 버프 -{value * 100}% 제거됨");
    }
}
