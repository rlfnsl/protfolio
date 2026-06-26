// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\InGame\SKILL_WS\SkillDamageResolver.cs
// Lines: full file

using UnityEngine;

public static class SkillDamageResolver
{
    static SkillManager_WS skillManager;
    public static void DealDamage(SkillInterFace skill, IDamageable damageable, float damageMultiplier = 1f, System.Action<Transform> onHit = null)
    {
        if (skill == null || damageable == null || skill.player == null)
            return;

        BuffManager buffManager = skill.player.BuffManager;
        if (buffManager == null)
            return;

        Transform targetTransform = damageable.GetTransform();
        if (onHit != null)
            onHit.Invoke(targetTransform);
        else
            skill.HitAction?.Invoke(targetTransform);

        float elementalAddDamage = GetElementalAddDamage(skill, buffManager);
        DeBuffType elementalDebuff = buffManager.GetMaxValue(BuffType.EnemyStiffness) > 0f
            ? DeBuffType.StiffnessPlusDamage
            : DeBuffType.None;

        float baseDamage = skill.CurDamage;
        float bonusDamage = Mathf.Max(1f, buffManager.GetMaxValue(BuffType.DamageBuff));
        float elementalBonus = Mathf.Max(1f, elementalAddDamage);
        float finalDamage = baseDamage * bonusDamage * elementalBonus * Mathf.Max(0f, damageMultiplier);

        damageable.TakeDamage((int)finalDamage, skill.ElementalType, elementalDebuff);
    }

    private static float GetElementalAddDamage(SkillInterFace skill, BuffManager buffManager)
    {
        if(skillManager == null)
            skillManager = skill.Manager;
        bool synergyActive =
            skillManager != null &&
            skillManager.SynergySkill != null &&
            skillManager.IsMatchingSynergy(skill.ElementalType) &&
            skillManager.SynergySkill.IsUseSkill;

        if (!synergyActive)
            return 1f;

        switch (skill.ElementalType)
        {
            case ElementType.fire:
                return buffManager.GetMaxValue(BuffType.FireDamageBuff);
            case ElementType.water:
                return buffManager.GetMaxValue(BuffType.WaterDamageBuff);
            case ElementType.electric:
                return buffManager.GetMaxValue(BuffType.ElectricDamageBuff);
            case ElementType.land:
                return buffManager.GetMaxValue(BuffType.LandDamageBuff);
            default:
                return 1f;
        }
    }
}
