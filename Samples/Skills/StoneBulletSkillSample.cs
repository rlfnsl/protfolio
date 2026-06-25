using UnityEngine;

namespace PortfolioSamples.Skills
{
    public sealed class StoneBulletSkillSample : MonoBehaviour
    {
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private float speed = 9f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private float fragmentDamageMultiplier = 0.5f;
        [SerializeField] private int normalFragmentCount = 3;
        [SerializeField] private int evolvedFragmentCount = 5;

        private Vector2 direction;
        private bool evolved;
        private SkillDamageContext damageContext;

        public void Fire(Vector2 fireDirection, SkillDamageContext context, bool isEvolved)
        {
            direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.right;
            damageContext = context;
            evolved = isEvolved;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            Vector3 previous = transform.position;
            transform.position += (Vector3)(direction * speed * Time.deltaTime);

            Vector2 delta = (Vector2)transform.position - (Vector2)previous;
            RaycastHit2D hit = Physics2D.CircleCast(previous, 0.2f, delta.normalized, delta.magnitude, targetMask);
            if (!hit.collider)
                return;

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
                SkillDamageResolverSample.DealDamage(damageContext, damageable);

            SpawnFragments(hit.point);
            Destroy(gameObject);
        }

        private void SpawnFragments(Vector2 origin)
        {
            int count = evolved ? evolvedFragmentCount : normalFragmentCount;
            float spread = evolved ? 360f : 80f;
            float start = evolved ? 0f : -spread * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float angleOffset = evolved ? 360f * i / count : Mathf.Lerp(start, -start, t);
                Vector2 fragmentDirection = Quaternion.Euler(0f, 0f, angleOffset) * direction;
                CreateFragment(origin, fragmentDirection);
            }
        }

        private void CreateFragment(Vector2 origin, Vector2 fragmentDirection)
        {
            // In production this instantiates a pooled fragment prefab.
            // The fragment uses the same damage resolver with 50% damage.
            Debug.DrawRay(origin, fragmentDirection.normalized * 2f, Color.cyan, 0.5f);
            _ = fragmentDamageMultiplier;
        }
    }
}

