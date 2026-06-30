public class SynergyOverheat : SkillInterFace
{
    public SynergyOverheat()
    {
        IsSynergySkill = true;
        SynergyType = SpiritSynergy.Overheat; // 불 + 땅
        SkillData.statsType = SkillValueType.slowNelementalResist;
        ElementalType = ElementType.fire;
        ElementalType2 = ElementType.land;
        SetSynergyValues(0.8f, 5, 5);
        SkillBehavior = new FixedPointSkillBehavior(SkillSpawnLocationType.PlayerPosition);
    }
    public override void SkillEffect()
    {
        BuffManager.ApplyBuff(BuffType.SpeedDebuff, CurTime, CurDamage);
        BuffManager.ApplyBuff(BuffType.AllResistDebuff, CurTime, CurDamage);
    }
    protected override void ConfigureValueMappings() { }
}