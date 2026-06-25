using System.Collections.Generic;
using UnityEngine;

namespace PortfolioSamples.Skills
{
    public static class RuntimeSkillHitUtilitySample
    {
        private static readonly Collider2D[] Buffer = new Collider2D[128];
        private static readonly HashSet<IDamageable> Damaged = new HashSet<IDamageable>();

        public static int DamageCircle(
            Vector2 center,
            float radius,
            LayerMask targetMask,
            in SkillDamageContext damageContext,
            float damageMultiplier = 1f)
        {
            int count = Physics2D.OverlapCircleNonAlloc(center, radius, Buffer, targetMask);
            int hitCount = 0;
            Damaged.Clear();

            for (int i = 0; i < count; i++)
            {
                if (!TryGetDamageable(Buffer[i], out IDamageable damageable))
                    continue;

                if (!Damaged.Add(damageable))
                    continue;

                if (SkillDamageResolverSample.DealDamage(damageContext, damageable, damageMultiplier))
                    hitCount++;
            }

            return hitCount;
        }

        public static int DamageLine(
            Vector2 origin,
            Vector2 direction,
            float length,
            float width,
            LayerMask targetMask,
            in SkillDamageContext damageContext)
        {
            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 end = origin + normalized * length;

            int count = Physics2D.OverlapCapsuleNonAlloc(
                (origin + end) * 0.5f,
                new Vector2(length, width),
                CapsuleDirection2D.Horizontal,
                Vector2.SignedAngle(Vector2.right, normalized),
                Buffer,
                targetMask);

            int hitCount = 0;
            Damaged.Clear();

            for (int i = 0; i < count; i++)
            {
                if (!TryGetDamageable(Buffer[i], out IDamageable damageable))
                    continue;

                if (!Damaged.Add(damageable))
                    continue;

                if (SkillDamageResolverSample.DealDamage(damageContext, damageable))
                    hitCount++;
            }

            return hitCount;
        }

        private static bool TryGetDamageable(Collider2D collider, out IDamageable damageable)
        {
            damageable = null;

            if (collider == null)
                return false;

            damageable = collider.GetComponentInParent<IDamageable>();
            return damageable != null;
        }
    }
}

