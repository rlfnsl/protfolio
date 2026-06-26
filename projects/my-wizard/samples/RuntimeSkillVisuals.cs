// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\InGame\SKILL_WS\StandaloneRuntimeSkill.cs
// Lines: 310-760

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
