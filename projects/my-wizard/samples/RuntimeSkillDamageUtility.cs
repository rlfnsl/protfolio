// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\InGame\SKILL_WS\StandaloneRuntimeSkill.cs
// Lines: 1-183

using System.Collections.Generic;
using UnityEngine;

public interface IStandaloneSkillBehavior : ISkillBehavior
{
    bool RequiresSkillObjectPool { get; }
}

public struct RuntimeSkillHitResult
{
    public IDamageable Damageable;
    public Collider2D Collider;
    public Vector3 HitPosition;

    public RuntimeSkillHitResult(IDamageable damageable, Collider2D collider, Vector3 hitPosition)
    {
        Damageable = damageable;
        Collider = collider;
        HitPosition = hitPosition;
    }
}

public static class RuntimeSkillDamageUtility
{
    private const int QueryBufferSize = 256;
    private static readonly Collider2D[] QueryBuffer = new Collider2D[QueryBufferSize];
    private static readonly List<IDamageable> QueryDamageables = new List<IDamageable>(64);
    private static readonly ContactFilter2D QueryContactFilter = CreateQueryContactFilter();

    public static void DealDamage(SkillInterFace skill, IDamageable damageable, float damageMultiplier = 1f)
    {
        SkillDamageResolver.DealDamage(skill, damageable, damageMultiplier);
    }

    private static ContactFilter2D CreateQueryContactFilter()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        return filter;
    }

    public static void DamageCircle(SkillInterFace skill, Vector3 center, float radius, float damageMultiplier = 1f)
    {
        int hitCount = Physics2D.OverlapCircle(center, Mathf.Max(0.05f, radius), QueryContactFilter, QueryBuffer);
        QueryDamageables.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = QueryBuffer[i];
            if (TryBreakItem(hitCollider))
                continue;

            IDamageable damageable = GetDamageable(hitCollider);
            if (damageable == null || QueryDamageables.Contains(damageable))
                continue;

            QueryDamageables.Add(damageable);
            DealDamage(skill, damageable, damageMultiplier);
        }

        QueryDamageables.Clear();
    }

    public static void DamageLine(SkillInterFace skill, Vector3 origin, Vector2 direction, float length, float width, float damageMultiplier = 1f)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        direction.Normalize();
        float safeLength = Mathf.Max(0.1f, length);
        float halfWidth = Mathf.Max(0.05f, width * 0.5f);
        Vector3 end = origin + (Vector3)(direction * safeLength);
        Vector3 queryCenter = origin + (Vector3)(direction * (safeLength * 0.5f));
        float queryRadius = Mathf.Sqrt((safeLength * safeLength * 0.25f) + (halfWidth * halfWidth)) + 0.5f;

        int hitCount = Physics2D.OverlapCircle(queryCenter, queryRadius, QueryContactFilter, QueryBuffer);
        QueryDamageables.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = QueryBuffer[i];
            if (!TryGetSegmentColliderHit(hitCollider, origin, end, halfWidth, out _, out _))
                continue;

            if (TryBreakItem(hitCollider))
                continue;

            IDamageable damageable = GetDamageable(hitCollider);
            if (damageable == null || QueryDamageables.Contains(damageable))
                continue;

            QueryDamageables.Add(damageable);
            DealDamage(skill, damageable, damageMultiplier);
        }

        QueryDamageables.Clear();
    }

    public static Transform FindNearestEnemy(Vector3 origin, float range)
    {
        int hitCount = Physics2D.OverlapCircle(origin, Mathf.Max(0.05f, range), QueryContactFilter, QueryBuffer);
        Transform closest = null;
        float closestSqr = float.MaxValue;
        QueryDamageables.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageable = GetDamageable(QueryBuffer[i]);
            if (damageable == null || QueryDamageables.Contains(damageable))
                continue;

            QueryDamageables.Add(damageable);
            Transform t = damageable.GetTransform();
            if (t == null)
                continue;

            float sqr = (t.position - origin).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = t;
            }
        }

        QueryDamageables.Clear();
        return closest;
    }

    public static IDamageable FindFirstDamageable(Vector3 center, float radius, IDamageable ignored = null)
    {
        return TryFindFirstDamageable(center, radius, ignored, out RuntimeSkillHitResult result)
            ? result.Damageable
            : null;
    }

    public static bool TryFindFirstDamageable(Vector3 center, float radius, IDamageable ignored, out RuntimeSkillHitResult result)
    {
        result = default;
        int hitCount = Physics2D.OverlapCircle(center, Mathf.Max(0.05f, radius), QueryContactFilter, QueryBuffer);
        float closestSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = QueryBuffer[i];
            if (TryBreakItem(hitCollider))
                continue;

            IDamageable damageable = GetDamageable(hitCollider);
            if (damageable == null || damageable == ignored)
                continue;

            Vector3 hitPosition = GetClosestPoint(hitCollider, center);
            float sqr = (hitPosition - center).sqrMagnitude;
            if (sqr >= closestSqr)
                continue;

            closestSqr = sqr;
            result = new RuntimeSkillHitResult(damageable, hitCollider, hitPosition);
        }

        return result.Damageable != null;
    }

    public static bool TryFindFirstDamageableAlongPath(Vector3 start, Vector3 end, float radius, IDamageable ignored, out RuntimeSkillHitResult result)
    {
        result = default;

        Vector2 start2 = start;
        Vector2 end2 = end;
        Vector2 delta = end2 - start2;
        float length = delta.magnitude;
        if (length < 0.001f)
            return TryFindFirstDamageable(end, radius, ignored, out result);

        float safeRadius = Mathf.Max(0.05f, radius);
        Vector3 queryCenter = (start + end) * 0.5f;
        float queryRadius = (length * 0.5f) + safeRadius + 0.25f;
        int hitCount = Physics2D.OverlapCircle(queryCenter, queryRadius, QueryContactFilter, QueryBuffer);
        float closestAlong = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = QueryBuffer[i];
