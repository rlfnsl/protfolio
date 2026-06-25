using UnityEngine;

namespace PortfolioSamples.Skills
{
    public sealed class LightningTurretSkillSample : MonoBehaviour
    {
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private float searchRadius = 8f;
        [SerializeField] private float attackInterval = 3f;
        [SerializeField] private float impactRadius = 1.4f;
        [SerializeField] private float evolvedLineLength = 8f;
        [SerializeField] private float evolvedLineWidth = 1.1f;
        [SerializeField] private bool evolved;

        private float nextAttackTime;
        private SkillDamageContext damageContext;

        public void Initialize(SkillDamageContext context, bool isEvolved)
        {
            damageContext = context;
            evolved = isEvolved;
            nextAttackTime = Time.time + attackInterval;
        }

        private void Update()
        {
            if (Time.time < nextAttackTime)
                return;

            if (!TryFindNearestTarget(out Transform target))
                return;

            nextAttackTime = Time.time + attackInterval;
            Attack(target);
        }

        private void Attack(Transform target)
        {
            Vector2 origin = transform.position;
            Vector2 targetPosition = target.position;

            if (evolved)
            {
                Vector2 direction = (targetPosition - origin).normalized;
                RuntimeSkillHitUtilitySample.DamageLine(
                    origin,
                    direction,
                    evolvedLineLength,
                    evolvedLineWidth,
                    targetMask,
                    damageContext);
            }
            else
            {
                RuntimeSkillHitUtilitySample.DamageCircle(
                    targetPosition,
                    impactRadius,
                    targetMask,
                    damageContext);
            }

            // Visuals are intentionally omitted from the public sample.
            // In production this point triggers beam/impact/explosion VFX.
        }

        private bool TryFindNearestTarget(out Transform target)
        {
            target = null;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, searchRadius, targetMask);
            float bestDistance = float.MaxValue;

            foreach (Collider2D hit in hits)
            {
                if (hit == null)
                    continue;

                float distance = (hit.transform.position - transform.position).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                target = hit.transform;
            }

            return target != null;
        }
    }
}

