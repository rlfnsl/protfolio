using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SynergySteam : SkillInterFace
{
    public SynergySteam()
    {
        IsSynergySkill = true;
        SynergyType = SpiritSynergy.Steam;// 불 + 물
        SkillData.statsType = SkillValueType.dmgNslow;
        ElementalType = ElementType.water;
        ElementalType2 = ElementType.fire;
        SetSynergyValues(0.5f, 5, 10);
        SkillBehavior = new AreaOfEffectSkillBehavior();
    }
    public override void SkillEffect()
    {
        BuffManager.ApplyBuff(BuffType.SpeedDebuff, CurTime, CurDamage);
        BuffManager.ApplyBuff(BuffType.DamageDebuff, CurTime, CurDamage);
    }
    protected override void ConfigureValueMappings()
    {

    }
}
