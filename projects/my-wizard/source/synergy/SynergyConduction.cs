using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SynergyConduction : SkillInterFace
{
    public SynergyConduction()
    {
        IsSynergySkill = true;
        SynergyType = SpiritSynergy.Conduction; // 물 + 번개
        ElementalType = ElementType.water;
        ElementalType2 = ElementType.electric;
        SetSynergyValues(1.1f, 3, 10);
        SkillBehavior = new AreaOfEffectSkillBehavior();
    }
    public override void SkillEffect()
    {
        BuffManager.ApplyBuff(BuffType.EnemyStiffness, CurTime, CurDamage);
    }
    protected override void ConfigureValueMappings() { }
}