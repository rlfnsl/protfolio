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
            if (!TryGetSegmentColliderHit(hitCollider, start, end, safeRadius, out Vector3 hitPosition, out float along))
                continue;

            if (TryBreakItem(hitCollider))
                continue;

            IDamageable damageable = GetDamageable(hitCollider);
            if (damageable == null || damageable == ignored)
                continue;

            if (along >= closestAlong)
                continue;

            closestAlong = along;
            result = new RuntimeSkillHitResult(damageable, hitCollider, hitPosition);
        }

        return result.Damageable != null;
    }

    public static IDamageable GetDamageable(Collider2D collider)
    {
        if (collider == null)
            return null;

        IDamageable damageable = collider.GetComponent<IDamageable>();
        if (damageable != null)
            return damageable;

        damageable = collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
            return damageable;

        return collider.GetComponentInChildren<IDamageable>();
    }

    public static bool TryBreakItem(Collider2D collider)
    {
        if (collider == null || !collider.CompareTag("Item"))
            return false;

        GameObject itemObject = collider.gameObject;
        itemObject.tag = "InItem";
        itemObject.transform.localScale = Vector3.one * 0.75f;

        SpriteRenderer spriteRenderer = itemObject.GetComponent<SpriteRenderer>();
        Player player = SpawnManager.instance != null ? SpawnManager.instance.player : null;
        ItemObjInfo info = player != null ? player._iteminfo : null;
        if (spriteRenderer == null || info == null)
            return true;

        if (info.TryGetElementCoin(out ElementType type, out Sprite coinSprite))
        {
            spriteRenderer.sprite = coinSprite;
            itemObject.tag = "ElementCoin";

            ElementCoinData coinData = itemObject.GetComponent<ElementCoinData>();
            if (coinData == null)
                coinData = itemObject.AddComponent<ElementCoinData>();

            coinData.type = type;
        }
        else if (info.Randomimg != null && info.Randomimg.Length > 0)
        {
            spriteRenderer.sprite = info.Randomimg[Random.Range(0, info.Randomimg.Length)];
        }

        return true;
    }

    private static Vector3 GetClosestPoint(Collider2D collider, Vector3 point)
    {
        if (collider == null)
            return point;

        return collider.ClosestPoint(point);
    }

    private static bool TryGetSegmentColliderHit(Collider2D collider, Vector2 start, Vector2 end, float radius, out Vector3 hitPosition, out float along)
    {
        hitPosition = Vector3.zero;
        along = 0f;
        if (collider == null)
            return false;

        float safeRadius = Mathf.Max(0.01f, radius);
        Vector2 segment = end - start;
        float length = segment.magnitude;
        if (length < 0.001f)
        {
            Vector2 point = collider.ClosestPoint(start);
            if ((point - start).sqrMagnitude > safeRadius * safeRadius)
                return false;

            hitPosition = point;
            return true;
        }

        Vector2 direction = segment / length;
        Vector2 referencePoint = collider.bounds.center;
        Vector2 segmentPoint = ClosestPointOnSegment(referencePoint, start, end);
        Vector2 colliderPoint = collider.ClosestPoint(segmentPoint);

        segmentPoint = ClosestPointOnSegment(colliderPoint, start, end);
        colliderPoint = collider.ClosestPoint(segmentPoint);

        if ((colliderPoint - segmentPoint).sqrMagnitude > safeRadius * safeRadius)
            return false;

        along = Mathf.Clamp(Vector2.Dot(segmentPoint - start, direction), 0f, length);
        hitPosition = colliderPoint;
        return true;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float sqrLength = segment.sqrMagnitude;
        if (sqrLength < 0.0001f)
            return start;

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / sqrLength);
        return start + segment * t;
    }
}

public static class RuntimeSkillVisuals
{
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private static Material effectMaterial;
    private static Material particleMaterial;
    private static Texture2D particleTexture;

    public static Sprite LoadSprite(string key, float pixelsPerUnit = 100f)
    {
        if (spriteCache.TryGetValue(key, out Sprite cached))
            return cached;

        string resourcePath = "RuntimeAssets/" + key;
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );

            spriteCache[key] = sprite;
            return sprite;
        }

        Sprite[] spriteAssets = Resources.LoadAll<Sprite>(resourcePath);
        if (spriteAssets != null && spriteAssets.Length > 0 && spriteAssets[0] != null)
        {
            spriteCache[key] = spriteAssets[0];
            return spriteAssets[0];
        }

        Sprite spriteAsset = Resources.Load<Sprite>(resourcePath);
        if (spriteAsset != null)
        {
            spriteCache[key] = spriteAsset;
            return spriteAsset;
        }

        Debug.LogWarning($"Runtime skill sprite not found: {key}");
        return null;
    }

    public static SpriteRenderer CreateSpriteRenderer(string spriteKey, Vector3 position, int sortingOrder)
    {
        GameObject obj = new GameObject(spriteKey);
        obj.transform.position = position;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSprite(spriteKey);
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    public static SpriteRenderer PrepareSpriteRenderer(GameObject owner, string spriteKey, int sortingOrder)
    {
        if (owner == null)
            return null;

        SpriteRenderer renderer = owner.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null)
        {
            GameObject obj = new GameObject(spriteKey);
            obj.transform.SetParent(owner.transform, false);
            renderer = obj.AddComponent<SpriteRenderer>();
        }

        renderer.gameObject.SetActive(true);
        if (renderer.transform.parent != owner.transform)
            renderer.transform.SetParent(owner.transform, false);

        renderer.sprite = LoadSprite(spriteKey);
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.white;
        renderer.transform.localPosition = Vector3.zero;
        renderer.transform.localRotation = Quaternion.identity;
        return renderer;
    }

    public static void ScaleToWorld(SpriteRenderer renderer, float width, float height)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        renderer.transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
    }

    public static void SpawnTransientSprite(string spriteKey, Vector3 position, float worldSize, float lifetime, int sortingOrder, float rotation = 0f, bool grow = true)
    {
        SpriteRenderer renderer = CreateSpriteRenderer(spriteKey, position, sortingOrder);
        if (renderer == null)
            return;

        renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        ScaleToWorld(renderer, worldSize, worldSize);

        RuntimeSkillSpriteEffect effect = renderer.gameObject.AddComponent<RuntimeSkillSpriteEffect>();
        effect.Initialize(lifetime, grow);
    }

    public static void SpawnBeam(Vector3 start, Vector3 end, float width, float lifetime, int sortingOrder)
    {
        SpawnLightningBolt(start, end, width, lifetime, sortingOrder);
    }

    public static void SpawnLightningBolt(Vector3 start, Vector3 end, float width, float lifetime, int sortingOrder)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f)
            return;

        GameObject obj = new GameObject("RuntimeLightningBolt");
        RuntimeLightningBoltEffect effect = obj.AddComponent<RuntimeLightningBoltEffect>();
        effect.Initialize(start, end, Mathf.Max(0.08f, width), lifetime, sortingOrder);
    }

    public static void SpawnLightningImpact(Vector3 position, float radius, int sortingOrder)
    {
        SpawnTransientSprite("ws_lightning_aoe", position, Mathf.Max(0.5f, radius * 2f), 0.28f, sortingOrder, 0f, true);
        SpawnParticleBurst("RuntimeLightningImpactSparks", position, radius, 16, 0.28f, sortingOrder, new Color(0.45f, 0.95f, 1f, 1f));

        GameObject obj = new GameObject("RuntimeLightningImpactRing");
        RuntimeLightningRingEffect effect = obj.AddComponent<RuntimeLightningRingEffect>();
        effect.Initialize(position, Mathf.Max(0.25f, radius), 0.28f, sortingOrder);
    }

    public static void SpawnLightningExplosion(Vector3 position, float radius, int sortingOrder)
    {
        float safeRadius = Mathf.Max(0.5f, radius);
        SpawnTransientSprite("ws_lightning_explosion", position, safeRadius * 2.1f, 0.35f, sortingOrder, 0f, true);
        SpawnParticleBurst("RuntimeLightningExplosionSparks", position, safeRadius, 34, 0.55f, sortingOrder + 1, new Color(0.55f, 0.95f, 1f, 1f));

        GameObject obj = new GameObject("RuntimeLightningExplosion");
        RuntimeLightningExplosionEffect effect = obj.AddComponent<RuntimeLightningExplosionEffect>();
        effect.Initialize(position, safeRadius, 0.5f, sortingOrder + 1);
    }

    public static void SpawnTowerSpawnEffect(Vector3 position, float radius, int sortingOrder)
    {
        float safeRadius = Mathf.Max(0.4f, radius);
        SpawnTransientSprite("ws_lightning_aoe", position, safeRadius * 1.55f, 0.28f, sortingOrder, 0f, true);
        SpawnParticleBurst("RuntimeLightningTowerSpawnSparks", position, safeRadius, 24, 0.42f, sortingOrder + 1, new Color(0.45f, 0.95f, 1f, 1f));

        GameObject obj = new GameObject("RuntimeLightningTowerSpawnRing");
        RuntimeLightningRingEffect effect = obj.AddComponent<RuntimeLightningRingEffect>();
        effect.Initialize(position, safeRadius * 0.7f, 0.36f, sortingOrder + 1);
    }

    public static RuntimeLightningAuraEffect AttachLightningAura(GameObject target, float radius, int sortingOrder)
    {
        if (target == null)
            return null;

        RuntimeLightningAuraEffect aura = target.GetComponentInChildren<RuntimeLightningAuraEffect>(true);
        if (aura == null)
        {
            GameObject obj = new GameObject("RuntimeLightningAura");
            obj.transform.SetParent(target.transform, false);
            aura = obj.AddComponent<RuntimeLightningAuraEffect>();
        }

        aura.gameObject.SetActive(true);
        aura.Initialize(target.transform, radius, sortingOrder);
        return aura;
    }

    public static RuntimeStoneFlameTrailEffect AttachStoneFlameTrail(GameObject target, Vector2 direction, float size, int sortingOrder)
    {
        if (target == null)
            return null;

        RuntimeStoneFlameTrailEffect flameTrail = target.GetComponentInChildren<RuntimeStoneFlameTrailEffect>(true);
        if (flameTrail == null)
        {
            GameObject obj = new GameObject("RuntimeStoneBlueFlameTrail");
            obj.transform.SetParent(target.transform, false);
            flameTrail = obj.AddComponent<RuntimeStoneFlameTrailEffect>();
        }

        flameTrail.gameObject.SetActive(true);
        flameTrail.Initialize(target.transform, direction, size, sortingOrder);
        return flameTrail;
    }

    public static void AnimateTrail(TrailRenderer trail, float baseWidth, float pulseSpeed, float phase)
    {
        if (trail == null)
            return;

        float pulse = 0.78f + Mathf.Sin(Time.time * pulseSpeed + phase) * 0.22f;
        trail.startWidth = Mathf.Max(0.01f, baseWidth * pulse);
    }

    public static void SpawnStoneDust(Vector3 position, float radius, int sortingOrder)
    {
        SpawnParticleBurst("RuntimeStoneDust", position, Mathf.Max(0.12f, radius), 5, 0.22f, sortingOrder, new Color(0.55f, 0.48f, 0.38f, 0.75f));
    }

    public static TrailRenderer AttachTrail(GameObject target, float width, float lifetime, int sortingOrder, Color startColor, Color endColor)
    {
        if (target == null)
            return null;

        TrailRenderer trail = target.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = target.AddComponent<TrailRenderer>();

        trail.time = Mathf.Max(0.03f, lifetime);
        trail.minVertexDistance = 0.04f;
        trail.startWidth = Mathf.Max(0.01f, width);
        trail.endWidth = 0.01f;
        trail.numCapVertices = 3;
        trail.numCornerVertices = 2;
        trail.sortingOrder = sortingOrder;
        trail.autodestruct = false;
        trail.emitting = true;

        Material material = GetEffectMaterial();
        if (material != null)
            trail.material = material;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = gradient;
        trail.Clear();

        return trail;
    }

    public static void PreparePooledSkillObject(HitCollider hit, SkillInterFace skill)
    {
        if (hit == null)
            return;

        if (hit.SpriteAnimator != null)
        {
            hit.SpriteAnimator.Stop(false);
            hit.SpriteAnimator.enabled = false;
            hit.SpriteAnimator = null;
        }

        hit.enabled = false;
        hit.SkillSetting(skill);
        hit.transform.localScale = Vector3.one;

        if (hit.skillHitrange != null)
            hit.skillHitrange.enabled = false;

        if (hit.Collider == null)
            hit.Collider = hit.GetComponent<Collider2D>();

        if (hit.Collider != null)
            hit.Collider.enabled = false;
    }

    public static bool TrySpawnPooledSubmitEffect(SkillInterFace skill, string effectKey, Vector3 position, float scale)
    {
        SkillManager_WS manager = skill != null && skill.Manager != null ? skill.Manager : SkillManager_WS.instance;
        ObjectPool_WS<SkillSubmit> pool = manager != null ? manager.GetSkillSubmit(effectKey) : null;
        if (pool == null)
            return false;

        SkillSubmit submit = pool.GetPooledObject(false);
        if (submit == null)
            return false;

        submit.transform.position = position;
        submit.transform.rotation = Quaternion.identity;
        submit.transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        submit.gameObject.SetActive(true);
        return true;
    }

    public static void ApplyRuntimeSkillObjectSetup(GameObject target, float hitboxSize)
    {
        if (target == null)
            return;

        try
        {
            target.tag = "Effects";
        }
        catch (UnityException)
        {
        }

        Rigidbody2D rigidbody = target.GetComponent<Rigidbody2D>();
        if (rigidbody == null)
            rigidbody = target.AddComponent<Rigidbody2D>();

        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.gravityScale = 0f;
        rigidbody.simulated = false;

        BoxCollider2D collider = target.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = target.AddComponent<BoxCollider2D>();

        collider.isTrigger = true;
        collider.enabled = false;
        collider.size = Vector2.one * Mathf.Max(0.1f, hitboxSize);
    }

    public static Material GetEffectMaterial()
    {
        if (effectMaterial != null)
            return effectMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        if (shader != null)
            effectMaterial = new Material(shader);

        return effectMaterial;
    }

    public static Material GetParticleMaterial()
    {
        if (particleMaterial != null)
            return particleMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        if (shader == null)
            return null;

        particleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        particleTexture.name = "RuntimeLightningParticle";
        particleTexture.wrapMode = TextureWrapMode.Clamp;
        particleTexture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < particleTexture.height; y++)
        {
            for (int x = 0; x < particleTexture.width; x++)
            {
                float nx = (x + 0.5f) / particleTexture.width * 2f - 1f;
                float ny = (y + 0.5f) / particleTexture.height * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha;
                particleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        particleTexture.Apply();
        particleTexture.hideFlags = HideFlags.HideAndDontSave;

        particleMaterial = new Material(shader);
        particleMaterial.name = "RuntimeLightningParticleMaterial";
        particleMaterial.hideFlags = HideFlags.HideAndDontSave;
        particleMaterial.SetTexture("_MainTex", particleTexture);

        return particleMaterial;
    }

    public static LineRenderer CreateLineRenderer(GameObject parent, string name, int sortingOrder, float width, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);

        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = sortingOrder;

        Material material = GetEffectMaterial();
        if (material != null)
            line.material = material;

        return line;
    }

    public static void ApplyLineAlpha(LineRenderer line, float alpha)
    {
        if (line == null)
            return;

        Color start = line.startColor;
        Color end = line.endColor;
        start.a = alpha;
        end.a = alpha;
        line.startColor = start;
        line.endColor = end;
    }

    private static void SpawnParticleBurst(string name, Vector3 position, float radius, int count, float lifetime, int sortingOrder, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = position;

        ParticleSystem particles = obj.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.Clear(true);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.05f, lifetime);
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, Mathf.Max(0.18f, lifetime));
        main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Max(1.5f, radius * 2f), Mathf.Max(3f, radius * 5f));
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, Mathf.Max(0.07f, radius * 0.08f));
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(8, count);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.04f, radius * 0.12f);
        shape.arc = 360f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = sortingOrder;

        Material material = GetParticleMaterial();
        if (material != null)
            renderer.material = material;

        particles.Play();
        UnityEngine.Object.Destroy(obj, lifetime + 0.75f);
    }
}

public class RuntimeSkillSpriteEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector3 startScale;
    private float lifetime;
    private float timer;
    private bool grow;

    public void Initialize(float lifetime, bool grow)
    {
        this.lifetime = Mathf.Max(0.05f, lifetime);
        this.grow = grow;
        spriteRenderer = GetComponent<SpriteRenderer>();
        startScale = transform.localScale;
    }

    private void Update()
    {
        if (PauseManager.freeze)
            return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / lifetime);

        if (grow)
            transform.localScale = Vector3.Lerp(startScale * 0.75f, startScale * 1.15f, t);

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f - t;
            spriteRenderer.color = color;
        }

        if (timer >= lifetime)
            Destroy(gameObject);
    }
}

public class RuntimeLightningBoltEffect : MonoBehaviour
{
    private readonly List<LineRenderer> branchLines = new List<LineRenderer>();
    private LineRenderer glowLine;
    private LineRenderer coreLine;
    private Vector3 start;
    private Vector3 end;
    private Vector3[] boltPoints;
    private float width;
    private float lifetime;
    private float timer;
    private float rebuildTimer;

    public void Initialize(Vector3 start, Vector3 end, float width, float lifetime, int sortingOrder)
    {
        this.start = start;
        this.end = end;
        this.width = Mathf.Max(0.04f, width);
        this.lifetime = Mathf.Max(0.08f, lifetime);

        glowLine = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "LightningGlow", sortingOrder, this.width * 1.8f, new Color(0.2f, 0.75f, 1f, 0.45f));
        coreLine = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "LightningCore", sortingOrder + 1, this.width * 0.45f, Color.white);

        int branchCount = Mathf.Clamp(Mathf.RoundToInt((end - start).magnitude * 0.8f), 2, 5);
        for (int i = 0; i < branchCount; i++)
        {
            LineRenderer branch = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "LightningBranch", sortingOrder, this.width * 0.28f, new Color(0.55f, 0.95f, 1f, 0.75f));
            branchLines.Add(branch);
        }

        RebuildBolt();
    }

    private void Update()
    {
        if (PauseManager.freeze)
            return;

        timer += Time.deltaTime;
        rebuildTimer -= Time.deltaTime;

        if (rebuildTimer <= 0f && timer < lifetime * 0.85f)
            RebuildBolt();

        float t = Mathf.Clamp01(timer / lifetime);
        float alpha = Mathf.Pow(1f - t, 0.65f);
        RuntimeSkillVisuals.ApplyLineAlpha(glowLine, alpha * 0.45f);
        RuntimeSkillVisuals.ApplyLineAlpha(coreLine, alpha);

        for (int i = 0; i < branchLines.Count; i++)
            RuntimeSkillVisuals.ApplyLineAlpha(branchLines[i], alpha * 0.75f);

        if (timer >= lifetime)
            Destroy(gameObject);
    }

    private void RebuildBolt()
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f)
            return;

        Vector3 direction = delta / length;
        Vector3 normal = new Vector3(-direction.y, direction.x, 0f);
        int segmentCount = Mathf.Clamp(Mathf.CeilToInt(length * 5f), 5, 14);
        float jitter = Mathf.Min(0.55f, Mathf.Max(0.08f, length * 0.1f));
        boltPoints = new Vector3[segmentCount + 1];

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float taper = Mathf.Sin(t * Mathf.PI);
            float offset = Random.Range(-jitter, jitter) * taper;
            boltPoints[i] = Vector3.Lerp(start, end, t) + normal * offset;
        }

        ApplyPositions(glowLine, boltPoints);
        ApplyPositions(coreLine, boltPoints);
        RebuildBranches(direction, normal, length);
        rebuildTimer = Random.Range(0.025f, 0.045f);
    }

    private void RebuildBranches(Vector3 direction, Vector3 normal, float length)
    {
        if (boltPoints == null || boltPoints.Length < 4)
            return;

        for (int i = 0; i < branchLines.Count; i++)
        {
            int index = Random.Range(1, boltPoints.Length - 2);
            float side = Random.value > 0.5f ? 1f : -1f;
            float branchLength = Random.Range(length * 0.08f, length * 0.2f);
            Vector3 branchDirection = (direction + normal * Random.Range(0.65f, 1.15f) * side).normalized;

            Vector3 p0 = boltPoints[index];
            Vector3 p2 = p0 + branchDirection * branchLength;
            Vector3 p1 = (p0 + p2) * 0.5f + normal * Random.Range(-0.12f, 0.12f);
            ApplyPositions(branchLines[i], new Vector3[] { p0, p1, p2 });
        }
    }

    private void ApplyPositions(LineRenderer line, Vector3[] positions)
    {
        if (line == null || positions == null)
            return;

        line.positionCount = positions.Length;
        line.SetPositions(positions);
    }
}

public class RuntimeLightningRingEffect : MonoBehaviour
{
    private LineRenderer ringLine;
    private Vector3 center;
    private float radius;
    private float lifetime;
    private float timer;

    public void Initialize(Vector3 center, float radius, float lifetime, int sortingOrder)
    {
        this.center = center;
        this.radius = Mathf.Max(0.15f, radius);
        this.lifetime = Mathf.Max(0.08f, lifetime);

        ringLine = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "LightningImpactRing", sortingOrder, Mathf.Max(0.035f, this.radius * 0.035f), new Color(0.45f, 0.95f, 1f, 0.9f));
        ringLine.loop = true;
        UpdateRing(0f);
    }

    private void Update()
    {
        if (PauseManager.freeze)
            return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / lifetime);
        UpdateRing(t);
        RuntimeSkillVisuals.ApplyLineAlpha(ringLine, 1f - t);

        if (timer >= lifetime)
            Destroy(gameObject);
    }

    private void UpdateRing(float t)
    {
        if (ringLine == null)
            return;

        int points = 48;
        ringLine.positionCount = points;
        float currentRadius = Mathf.Lerp(radius * 0.25f, radius * 1.1f, t);
        float wobble = Mathf.Lerp(0.12f, 0.02f, t);

        for (int i = 0; i < points; i++)
        {
            float angle = (i / (float)points) * Mathf.PI * 2f;
            float noise = 1f + Mathf.Sin(angle * 5f + Time.time * 18f) * wobble;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentRadius * noise;
            ringLine.SetPosition(i, point);
        }
    }
}

public class RuntimeLightningExplosionEffect : MonoBehaviour
{
    private readonly List<LineRenderer> radialLines = new List<LineRenderer>();
    private LineRenderer shockRing;
    private Vector3 center;
    private float radius;
    private float lifetime;
    private float timer;

    public void Initialize(Vector3 center, float radius, float lifetime, int sortingOrder)
    {
        this.center = center;
        this.radius = Mathf.Max(0.35f, radius);
        this.lifetime = Mathf.Max(0.15f, lifetime);

        shockRing = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "LightningExplosionRing", sortingOrder, Mathf.Max(0.05f, this.radius * 0.06f), new Color(0.45f, 0.95f, 1f, 0.85f));
        shockRing.loop = true;

        int boltCount = 9;
        for (int i = 0; i < boltCount; i++)
        {
            LineRenderer line = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "LightningExplosionBolt", sortingOrder + 1, Mathf.Max(0.035f, this.radius * 0.035f), new Color(0.8f, 1f, 1f, 0.95f));
            radialLines.Add(line);
        }

        BuildRadials();
        UpdateShockRing(0f);
    }

    private void Update()
    {
        if (PauseManager.freeze)
            return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / lifetime);
        float alpha = Mathf.Pow(1f - t, 0.8f);

        UpdateShockRing(t);
        RuntimeSkillVisuals.ApplyLineAlpha(shockRing, alpha * 0.8f);

        for (int i = 0; i < radialLines.Count; i++)
            RuntimeSkillVisuals.ApplyLineAlpha(radialLines[i], alpha);

        if (timer >= lifetime)
            Destroy(gameObject);
    }

    private void BuildRadials()
    {
        for (int i = 0; i < radialLines.Count; i++)
        {
            float angle = (i / (float)radialLines.Count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 side = new Vector3(-dir.y, dir.x, 0f);
            float boltLength = radius * Random.Range(0.65f, 1.25f);
            Vector3 p0 = center + dir * radius * 0.12f;
            Vector3 p2 = center + dir * boltLength;
            Vector3 p1 = (p0 + p2) * 0.5f + side * Random.Range(-radius * 0.15f, radius * 0.15f);

            LineRenderer line = radialLines[i];
            line.positionCount = 3;
            line.SetPosition(0, p0);
            line.SetPosition(1, p1);
            line.SetPosition(2, p2);
        }
    }

    private void UpdateShockRing(float t)
    {
        if (shockRing == null)
            return;

        int points = 64;
        shockRing.positionCount = points;
        float currentRadius = Mathf.Lerp(radius * 0.2f, radius * 1.25f, t);

        for (int i = 0; i < points; i++)
        {
            float angle = (i / (float)points) * Mathf.PI * 2f;
            float crackle = 1f + Mathf.Sin(angle * 7f + Time.time * 22f) * 0.08f * (1f - t);
            Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentRadius * crackle;
            shockRing.SetPosition(i, point);
        }
    }
}

public class RuntimeLightningAuraEffect : MonoBehaviour
{
    private readonly List<LineRenderer> auraLines = new List<LineRenderer>();
    private Transform followTarget;
    private float radius;
    private float phase;
    private float rebuildTimer;

    public void Initialize(Transform followTarget, float radius, int sortingOrder)
    {
        this.followTarget = followTarget;
        this.radius = Mathf.Max(0.35f, radius);
        phase = Random.Range(0f, Mathf.PI * 2f);
        rebuildTimer = 0f;

        EnsureLines(sortingOrder);
        for (int i = 0; i < auraLines.Count; i++)
        {
            auraLines[i].enabled = true;
            RuntimeSkillVisuals.ApplyLineAlpha(auraLines[i], 0.8f);
        }
    }

    public void Stop()
    {
        for (int i = 0; i < auraLines.Count; i++)
        {
            if (auraLines[i] != null)
                auraLines[i].enabled = false;
        }

        gameObject.SetActive(false);
    }

    private void EnsureLines(int sortingOrder)
    {
        const int lineCount = 4;
        while (auraLines.Count < lineCount)
        {
            LineRenderer line = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "TowerAuraBolt", sortingOrder, radius * 0.055f, new Color(0.45f, 0.95f, 1f, 0.75f));
            auraLines.Add(line);
        }

        for (int i = 0; i < auraLines.Count; i++)
        {
            auraLines[i].sortingOrder = sortingOrder;
            auraLines[i].startWidth = radius * 0.055f;
            auraLines[i].endWidth = radius * 0.02f;
        }
    }

    private void Update()
    {
        if (PauseManager.freeze || followTarget == null)
            return;

        rebuildTimer -= Time.deltaTime;
        if (rebuildTimer <= 0f)
        {
            RebuildAura();
            rebuildTimer = Random.Range(0.035f, 0.065f);
        }

        float alpha = 0.52f + Mathf.Sin(Time.time * 12f + phase) * 0.2f;
        for (int i = 0; i < auraLines.Count; i++)
            RuntimeSkillVisuals.ApplyLineAlpha(auraLines[i], alpha);
    }

    private void RebuildAura()
    {
        Vector3 center = followTarget.position;
        for (int i = 0; i < auraLines.Count; i++)
        {
            LineRenderer line = auraLines[i];
            if (line == null)
                continue;

            int points = 4;
            line.positionCount = points;
            float startAngle = phase + Time.time * 1.3f + i * Mathf.PI * 0.5f + Random.Range(-0.22f, 0.22f);
            float arc = Random.Range(0.35f, 0.75f);

            for (int p = 0; p < points; p++)
            {
                float t = p / (float)(points - 1);
                float angle = startAngle + arc * t;
                float wobble = Mathf.Sin(Time.time * 18f + i * 2.1f + p) * radius * 0.07f;
                float currentRadius = radius * (0.78f + 0.2f * Mathf.Sin(Time.time * 3.4f + i)) + wobble;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentRadius;
                line.SetPosition(p, point);
            }
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < auraLines.Count; i++)
        {
            if (auraLines[i] != null)
                auraLines[i].enabled = false;
        }
    }
}

public class RuntimeStoneFlameTrailEffect : MonoBehaviour
{
    private readonly List<LineRenderer> flameLines = new List<LineRenderer>();
    private Transform followTarget;
    private Vector2 direction;
    private float size;
    private float phase;

    public void Initialize(Transform followTarget, Vector2 direction, float size, int sortingOrder)
    {
        this.followTarget = followTarget;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        this.size = Mathf.Max(0.25f, size);
        phase = Random.Range(0f, Mathf.PI * 2f);

        EnsureLines(sortingOrder);
        for (int i = 0; i < flameLines.Count; i++)
            flameLines[i].enabled = true;

        RebuildFlame();
    }

    public void SetDirection(Vector2 direction)
    {
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : this.direction;
    }

    public void Stop()
    {
        for (int i = 0; i < flameLines.Count; i++)
        {
            if (flameLines[i] != null)
                flameLines[i].enabled = false;
        }

        gameObject.SetActive(false);
    }

    private void EnsureLines(int sortingOrder)
    {
        const int lineCount = 3;
        while (flameLines.Count < lineCount)
        {
            LineRenderer line = RuntimeSkillVisuals.CreateLineRenderer(gameObject, "StoneBlueFlame", sortingOrder, size * 0.08f, new Color(0.2f, 0.9f, 1f, 0.72f));
            flameLines.Add(line);
        }

        for (int i = 0; i < flameLines.Count; i++)
        {
            flameLines[i].sortingOrder = sortingOrder;
            flameLines[i].startWidth = size * 0.11f;
            flameLines[i].endWidth = 0.01f;
        }
    }

    private void Update()
    {
        if (PauseManager.freeze || followTarget == null)
            return;

        RebuildFlame();
    }

    private void RebuildFlame()
    {
        Vector3 center = followTarget.position;
        Vector3 forward = new Vector3(direction.x, direction.y, 0f);
        Vector3 side = new Vector3(-direction.y, direction.x, 0f);
        Vector3 basePosition = center - forward * size * 0.42f;

        for (int i = 0; i < flameLines.Count; i++)
        {
            LineRenderer line = flameLines[i];
            if (line == null)
                continue;

            float sideOffset = (i - 1) * size * 0.11f;
            float wave = Mathf.Sin(Time.time * 22f + phase + i * 1.7f);
            float length = size * (0.34f + 0.11f * wave);
            Vector3 start = basePosition + side * sideOffset;
            Vector3 mid = start - forward * length * 0.55f + side * wave * size * 0.08f;
            Vector3 end = start - forward * length + side * Mathf.Sin(Time.time * 17f + i) * size * 0.12f;

            line.positionCount = 3;
            line.SetPosition(0, start);
            line.SetPosition(1, mid);
            line.SetPosition(2, end);

            float alpha = 0.42f + Mathf.Abs(wave) * 0.35f;
            RuntimeSkillVisuals.ApplyLineAlpha(line, alpha);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < flameLines.Count; i++)
        {
            if (flameLines[i] != null)
                flameLines[i].enabled = false;
        }
    }
}

public class LightningTurretRuntime : MonoBehaviour
{
    private const float AttackInterval = 3f;

    private SkillInterFace skill;
    private HitCollider ownerHit;
    private SpriteRenderer spriteRenderer;
    private bool upgraded;
    private float duration;
    private float attackTimer;
    private float detectionRange;
    private float attackRadius;
    private float lineLength;
    private float lineWidth;
    private RuntimeLightningAuraEffect aura;
    private bool returned;

    public void Initialize(HitCollider hit, SkillInterFace skill, Vector3 position, bool upgraded)
    {
        ownerHit = hit;
        RuntimeSkillVisuals.PreparePooledSkillObject(ownerHit, skill);
        Initialize(skill, position, upgraded);
    }

    public void Initialize(SkillInterFace skill, Vector3 position, bool upgraded)
    {
        this.skill = skill;
        this.upgraded = upgraded;
        returned = false;
        transform.position = position;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        string spriteKey = upgraded ? "ws_lightning_tower_upgrade" : "ws_lightning_tower_default";
        spriteRenderer = RuntimeSkillVisuals.PrepareSpriteRenderer(gameObject, spriteKey, 7);

        float towerHeight = upgraded ? 1.45f : 1.25f;
        float towerWidth = towerHeight * (upgraded ? 0.64f : 0.68f);
        RuntimeSkillVisuals.ScaleToWorld(spriteRenderer, towerWidth, towerHeight);
        RuntimeSkillVisuals.ApplyRuntimeSkillObjectSetup(gameObject, Mathf.Max(towerWidth, towerHeight));
        RuntimeSkillVisuals.SpawnTowerSpawnEffect(position, towerHeight, 8);
        SkillAudioPlayer.PlayLightningTurretSpawn(skill);
        aura = RuntimeSkillVisuals.AttachLightningAura(gameObject, towerHeight * 0.58f, 8);

        duration = Mathf.Max(0.5f, skill.CurTime);
        attackTimer = 0.2f;
        attackRadius = Mathf.Max(0.35f, skill.CurAttackSize);
        detectionRange = Mathf.Max(4f, attackRadius * 3.5f);
        lineLength = Mathf.Max(3f, attackRadius * 4f);
        lineWidth = Mathf.Max(0.55f, attackRadius * 0.65f);
    }

    private void Update()
    {
        if (PauseManager.freeze || skill == null)
            return;

        duration -= Time.deltaTime;
        attackTimer -= Time.deltaTime;

        if (duration <= 0f)
        {
            Explode();
            ReturnToPoolOrDestroy();
            return;
        }

        if (attackTimer <= 0f)
        {
            attackTimer = Attack() ? AttackInterval : 0.2f;
        }
    }

    private bool Attack()
    {
        Transform target = RuntimeSkillDamageUtility.FindNearestEnemy(transform.position, detectionRange);
        if (target == null)
            return false;

        Vector2 direction = target.position - transform.position;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;

        direction.Normalize();

        if (upgraded)
        {
            SkillAudioPlayer.PlayLightningTurretAttack(skill, true);
            Vector3 end = transform.position + (Vector3)(direction * lineLength);
            RuntimeSkillVisuals.SpawnLightningBolt(transform.position, end, lineWidth, 0.32f, 8);
            RuntimeSkillDamageUtility.DamageLine(skill, transform.position, direction, lineLength, lineWidth, 1f);
        }
        else
        {
            SkillAudioPlayer.PlayLightningTurretAttack(skill, false);
            Vector3 targetPos = target.position;
            RuntimeSkillVisuals.SpawnLightningBolt(transform.position, targetPos, 0.35f, 0.24f, 8);
            RuntimeSkillVisuals.SpawnLightningImpact(targetPos, attackRadius, 8);
            RuntimeSkillDamageUtility.DamageCircle(skill, targetPos, attackRadius, 1f);
        }

        return true;
    }

    private void Explode()
    {
        float radius = Mathf.Max(0.5f, attackRadius);
        SkillAudioPlayer.PlayLightningTurretExplode(skill);
        RuntimeSkillVisuals.SpawnLightningExplosion(transform.position, radius, 9);
        RuntimeSkillDamageUtility.DamageCircle(skill, transform.position, radius, 1f);
    }

    private void ReturnToPoolOrDestroy()
    {
        if (returned)
            return;

        returned = true;

        if (aura != null)
            aura.Stop();

        if (ownerHit != null && skill != null)
        {
            ObjectPool_WS<HitCollider> pool = skill.ReturnPool(ownerHit.name);
            if (pool != null)
            {
                pool.ReturnObject(ownerHit);
                return;
            }
        }

        Destroy(gameObject);
    }
}

public class StoneBulletProjectileRuntime : MonoBehaviour
{
    private const string ImpactEffectKey = "StoneBulletImpact";
    private const string FragmentEffectKey = "StoneBulletSubHit";

    private SkillInterFace skill;
    private HitCollider ownerHit;
    private SpriteRenderer spriteRenderer;
    private Vector2 direction;
    private bool upgraded;
    private float speed;
    private float lifetime;
    private float hitRadius;
    private float visualSize;
    private float baseAngle;
    private float dustTimer;
    private float trailPhase;
    private float trailBaseWidth;
    private TrailRenderer trail;
    private RuntimeStoneFlameTrailEffect flameTrail;
    private bool returned;

    public void Initialize(HitCollider hit, SkillInterFace skill, Vector3 position, Vector2 direction, float speed, bool upgraded)
    {
        ownerHit = hit;
        RuntimeSkillVisuals.PreparePooledSkillObject(ownerHit, skill);
        Initialize(skill, position, direction, speed, upgraded);
    }

    public void Initialize(SkillInterFace skill, Vector3 position, Vector2 direction, float speed, bool upgraded)
    {
        this.skill = skill;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        this.speed = Mathf.Max(1f, speed);
        this.upgraded = upgraded;
        returned = false;
        lifetime = 5f;
        visualSize = Mathf.Max(0.75f, skill.CurAttackSize * 0.65f);
        hitRadius = Mathf.Max(0.2f, visualSize * 0.25f);
        trailPhase = Random.Range(0f, Mathf.PI * 2f);
        transform.position = position;
        baseAngle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);
        RuntimeSkillVisuals.ApplyRuntimeSkillObjectSetup(gameObject, hitRadius * 2f);
        trailBaseWidth = visualSize * 0.22f;
        trail = RuntimeSkillVisuals.AttachTrail(
            gameObject,
            trailBaseWidth,
            0.18f,
            6,
            new Color(0.15f, 0.85f, 1f, 0.72f),
            new Color(0.03f, 0.25f, 0.55f, 0f)
        );
        flameTrail = RuntimeSkillVisuals.AttachStoneFlameTrail(gameObject, this.direction, visualSize, 6);

        string projectileSpriteKey = upgraded ? "ws_stone_bullet_projectile_upgrade" : "ws_stone_bullet_projectile";
        spriteRenderer = RuntimeSkillVisuals.PrepareSpriteRenderer(gameObject, projectileSpriteKey, 7);
        RuntimeSkillVisuals.ScaleToWorld(spriteRenderer, visualSize, visualSize);
    }

    private void Update()
    {
        if (PauseManager.freeze || skill == null)
            return;

        lifetime -= Time.deltaTime;
        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + (Vector3)(direction * speed * Time.deltaTime);
        transform.position = nextPosition;
        dustTimer -= Time.deltaTime;
        RuntimeSkillVisuals.AnimateTrail(trail, trailBaseWidth, 22f, trailPhase);

        if (spriteRenderer != null)
        {
            spriteRenderer.transform.position = transform.position;
            spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);
        }

        if (flameTrail != null)
            flameTrail.SetDirection(direction);

        if (dustTimer <= 0f)
        {
            Vector3 dustPosition = transform.position - (Vector3)(direction * visualSize * 0.35f);
            RuntimeSkillVisuals.SpawnStoneDust(dustPosition, visualSize * 0.18f, 6);
            dustTimer = 0.055f;
        }

        if (RuntimeSkillDamageUtility.TryFindFirstDamageableAlongPath(previousPosition, nextPosition, hitRadius, null, out RuntimeSkillHitResult hit))
        {
            transform.position = hit.HitPosition;
            Hit(hit);
            return;
        }

        if (lifetime <= 0f)
            ReturnToPoolOrDestroy();
    }

    private void Hit(RuntimeSkillHitResult hit)
    {
        IDamageable damageable = hit.Damageable;
        if (damageable == null)
            return;

        Vector3 hitPos = hit.HitPosition;
        Transform targetTransform = damageable.GetTransform();
        Vector3 fragmentOrigin = upgraded && targetTransform != null ? targetTransform.position : hitPos;

        RuntimeSkillDamageUtility.DealDamage(skill, damageable, 1f);
        SkillAudioPlayer.PlayStoneBulletImpact(skill, upgraded);
        SpawnImpact(hitPos, visualSize * 0.9f);
        SpawnFragments(fragmentOrigin, damageable);
        ReturnToPoolOrDestroy();
    }

    private void SpawnFragments(Vector3 hitPos, IDamageable ignoredTarget)
    {
        if (upgraded)
        {
            int count = 5;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + (360f / count) * i;
                Vector2 fragmentDir = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );
                SpawnFragment(hitPos + (Vector3)(fragmentDir * 0.25f), fragmentDir, ignoredTarget);
            }

            return;
        }

        Vector2 side = new Vector2(-direction.y, direction.x);
        for (int i = 0; i < 3; i++)
        {
            float angleOffset = (i - 1) * 34f;
            Vector2 fragmentDirection = (Vector2)(Quaternion.Euler(0f, 0f, angleOffset) * direction);
            float offset = (i - 1) * visualSize * 0.26f;
            Vector3 spawnPos = hitPos + (Vector3)(direction * 0.22f) + (Vector3)(side * offset);
            SpawnFragment(spawnPos, fragmentDirection, ignoredTarget);
        }
    }

    private void SpawnFragment(Vector3 position, Vector2 fragmentDirection, IDamageable ignoredTarget)
    {
        float fragmentSize = Mathf.Max(0.55f, visualSize * 0.68f);
        SkillSubmit submit = GetPooledFragmentSubmit(position, fragmentSize);
        GameObject obj = submit != null ? submit.gameObject : new GameObject("StoneBulletFragment");

        StoneBulletFragmentRuntime fragment = obj.GetComponent<StoneBulletFragmentRuntime>();
        if (fragment == null)
            fragment = obj.AddComponent<StoneBulletFragmentRuntime>();

        if (submit != null)
        {
            obj.SetActive(true);
            submit.DisableTime = 0.75f;
        }

        fragment.Initialize(skill, position, fragmentDirection, speed * 0.95f, fragmentSize, ignoredTarget, upgraded, submit);
    }

    private SkillSubmit GetPooledFragmentSubmit(Vector3 position, float scale)
    {
        SkillManager_WS manager = skill != null && skill.Manager != null ? skill.Manager : SkillManager_WS.instance;
        ObjectPool_WS<SkillSubmit> pool = manager != null ? manager.GetSkillSubmit(FragmentEffectKey) : null;
        if (pool == null)
            return null;

        SkillSubmit submit = pool.GetPooledObject(false);
        if (submit == null)
            return null;

        submit.transform.position = position;
        submit.transform.rotation = Quaternion.identity;
        submit.transform.localScale = Vector3.one;
        return submit;
    }

    private void SpawnImpact(Vector3 position, float scale)
    {
        if (RuntimeSkillVisuals.TrySpawnPooledSubmitEffect(skill, ImpactEffectKey, position, scale))
            return;

        RuntimeSkillVisuals.SpawnTransientSprite("ws_stone_impact", position, visualSize * 1.35f, 0.45f, 8);
    }

    private void ReturnToPoolOrDestroy()
    {
        if (returned)
            return;

        returned = true;

        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }

        if (flameTrail != null)
            flameTrail.Stop();

        if (ownerHit != null && skill != null)
        {
            ObjectPool_WS<HitCollider> pool = skill.ReturnPool(ownerHit.name);
            if (pool != null)
            {
                pool.ReturnObject(ownerHit);
                return;
            }
        }

        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }

        if (flameTrail != null)
            flameTrail.Stop();
    }
}

public class StoneBulletFragmentRuntime : MonoBehaviour
{
    private SkillInterFace skill;
    private SkillSubmit ownerSubmit;
    private SpriteRenderer spriteRenderer;
    private Vector2 direction;
    private IDamageable ignoredTarget;
    private float speed;
    private float lifetime;
    private float initialLifetime;
    private float hitRadius;
    private float visualSize;
    private float ignoreTimer;
    private float baseAngle;
    private float dustTimer;
    private float trailPhase;
    private float trailBaseWidth;
    private TrailRenderer trail;
    private RuntimeStoneFlameTrailEffect flameTrail;
    private bool upgraded;
    private bool returned;

    public void Initialize(SkillInterFace skill, Vector3 position, Vector2 direction, float speed, float visualSize, IDamageable ignoredTarget, bool upgraded, SkillSubmit ownerSubmit = null)
    {
        this.skill = skill;
        this.ownerSubmit = ownerSubmit;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        this.speed = Mathf.Max(1f, speed);
        this.ignoredTarget = ignoredTarget;
        this.visualSize = visualSize;
        this.upgraded = upgraded;
        returned = false;
        lifetime = 0.62f;
        initialLifetime = lifetime;
        ignoreTimer = 0.08f;
        hitRadius = Mathf.Max(0.12f, visualSize * 0.28f);
        trailPhase = Random.Range(0f, Mathf.PI * 2f);
        transform.position = position;
        baseAngle = Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);
        RuntimeSkillVisuals.ApplyRuntimeSkillObjectSetup(gameObject, hitRadius * 2f);
        trailBaseWidth = visualSize * 0.18f;
        trail = RuntimeSkillVisuals.AttachTrail(
            gameObject,
            trailBaseWidth,
            0.16f,
            6,
            new Color(0.18f, 0.8f, 1f, 0.55f),
            new Color(0.08f, 0.28f, 0.45f, 0f)
        );
        flameTrail = RuntimeSkillVisuals.AttachStoneFlameTrail(gameObject, this.direction, visualSize * 0.78f, 6);

        string fragmentSpriteKey = upgraded ? "ws_stone_bullet_fragment_upgrade" : "ws_stone_bullet_fragment";
        spriteRenderer = RuntimeSkillVisuals.PrepareSpriteRenderer(gameObject, fragmentSpriteKey, 7);
        RuntimeSkillVisuals.ScaleToWorld(spriteRenderer, visualSize, visualSize);
    }

    private void Update()
    {
        if (PauseManager.freeze || skill == null)
            return;

        lifetime -= Time.deltaTime;
        ignoreTimer -= Time.deltaTime;
        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + (Vector3)(direction * speed * Time.deltaTime);
        transform.position = nextPosition;
        dustTimer -= Time.deltaTime;
        RuntimeSkillVisuals.AnimateTrail(trail, trailBaseWidth, 24f, trailPhase);

        if (spriteRenderer != null)
        {
            spriteRenderer.transform.position = transform.position;
            spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle);
            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(lifetime / Mathf.Max(0.01f, initialLifetime));
            spriteRenderer.color = color;
        }

        if (flameTrail != null)
            flameTrail.SetDirection(direction);

        if (dustTimer <= 0f)
        {
            Vector3 dustPosition = transform.position - (Vector3)(direction * hitRadius * 0.9f);
            RuntimeSkillVisuals.SpawnStoneDust(dustPosition, hitRadius * 0.55f, 6);
            dustTimer = 0.075f;
        }

        IDamageable ignored = ignoreTimer > 0f ? ignoredTarget : null;
        if (RuntimeSkillDamageUtility.TryFindFirstDamageableAlongPath(previousPosition, nextPosition, hitRadius, ignored, out RuntimeSkillHitResult hit))
        {
            transform.position = hit.HitPosition;
            RuntimeSkillDamageUtility.DealDamage(skill, hit.Damageable, 0.5f);
            SkillAudioPlayer.PlayStoneBulletImpact(skill, upgraded);
            if (!RuntimeSkillVisuals.TrySpawnPooledSubmitEffect(skill, "StoneBulletImpact", hit.HitPosition, Mathf.Max(0.35f, visualSize * 0.45f)))
                RuntimeSkillVisuals.SpawnTransientSprite("ws_stone_impact", hit.HitPosition, hitRadius * 3.4f, 0.25f, 8);

            ReturnToPoolOrDestroy();
            return;
        }

        if (lifetime <= 0f)
            ReturnToPoolOrDestroy();
    }

    private void ReturnToPoolOrDestroy()
    {
        if (returned)
            return;

        returned = true;

        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }

        if (flameTrail != null)
            flameTrail.Stop();

        if (ownerSubmit != null)
        {
            gameObject.SetActive(false);
            return;
        }

        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }

        if (flameTrail != null)
            flameTrail.Stop();
    }
}
