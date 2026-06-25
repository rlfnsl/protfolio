using UnityEngine;

namespace PortfolioSamples.Skills
{
    public interface IDamageable
    {
        Transform Transform { get; }
        void TakeDamage(int damage, int elementType, int debuffType);
    }

    public readonly struct SkillDamageContext
    {
        public SkillDamageContext(
            int baseDamage,
            int elementType,
            float damageBuff,
            float elementalBuff,
            int debuffType)
        {
            BaseDamage = baseDamage;
            ElementType = elementType;
            DamageBuff = Mathf.Max(1f, damageBuff);
            ElementalBuff = Mathf.Max(1f, elementalBuff);
            DebuffType = debuffType;
        }

        public int BaseDamage { get; }
        public int ElementType { get; }
        public float DamageBuff { get; }
        public float ElementalBuff { get; }
        public int DebuffType { get; }
    }

    public static class SkillDamageResolverSample
    {
        public static bool DealDamage(
            in SkillDamageContext context,
            IDamageable target,
            float damageMultiplier = 1f)
        {
            if (target == null)
                return false;

            float finalDamage =
                context.BaseDamage *
                context.DamageBuff *
                context.ElementalBuff *
                Mathf.Max(0f, damageMultiplier);

            target.TakeDamage((int)finalDamage, context.ElementType, context.DebuffType);
            return true;
        }
    }
}

