using UnityEngine;

namespace PortfolioSamples.Optimization
{
    public sealed class EnemySeparationNonAllocSample : MonoBehaviour
    {
        private static readonly Collider2D[] OverlapBuffer = new Collider2D[32];

        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private float checkRadius = 0.45f;
        [SerializeField] private float checkInterval = 0.12f;
        [SerializeField] private float pushStrength = 1.2f;

        private float nextCheckTime;
        private Vector2 cachedPush;

        private void Update()
        {
            if (Time.time >= nextCheckTime)
            {
                nextCheckTime = Time.time + checkInterval + Random.Range(0f, checkInterval * 0.25f);
                cachedPush = CalculateSeparationPush();
            }

            transform.position += (Vector3)(cachedPush * Time.deltaTime);
        }

        private Vector2 CalculateSeparationPush()
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                checkRadius,
                OverlapBuffer,
                enemyMask);

            Vector2 push = Vector2.zero;

            for (int i = 0; i < count; i++)
            {
                Collider2D other = OverlapBuffer[i];
                if (other == null || other.transform == transform)
                    continue;

                Vector2 away = (Vector2)(transform.position - other.transform.position);
                float sqrMagnitude = Mathf.Max(away.sqrMagnitude, 0.0001f);
                push += away.normalized / sqrMagnitude;
            }

            return Vector2.ClampMagnitude(push, 1f) * pushStrength;
        }
    }
}

