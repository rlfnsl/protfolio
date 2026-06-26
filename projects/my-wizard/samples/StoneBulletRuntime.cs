// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\InGame\SKILL_WS\StandaloneRuntimeSkill.cs
// Lines: 1403-1766

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
