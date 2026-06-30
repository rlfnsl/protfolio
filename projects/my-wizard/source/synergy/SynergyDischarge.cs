using UnityEngine;
public class SynergyDischarge : SkillInterFace
{
    public SynergyDischarge()
    {
        IsSynergySkill = true;
        SynergyType = SpiritSynergy.Discharge; // 불 + 번개
        ElementalType = ElementType.fire;
        ElementalType2 = ElementType.electric;
        SetSynergyValues(1.3f, 5, 5);
        SkillBehavior = new AreaOfEffectSkillBehavior();
    }
    public override void SkillEffect()
    {
        BuffManager.ApplyBuff(BuffType.FireDamageBuff, CurTime, CurDamage);
        BuffManager.ApplyBuff(BuffType.ElectricDamageBuff, CurTime, CurDamage);
    }
    protected override void ConfigureValueMappings() { }
}