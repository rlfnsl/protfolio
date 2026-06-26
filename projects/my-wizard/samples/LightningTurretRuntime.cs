// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\InGame\SKILL_WS\StandaloneRuntimeSkill.cs
// Lines: 1271-1400

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
            Vector3 end = transform.position + (Vector3)(direction * lineLength);
            RuntimeSkillVisuals.SpawnLightningBolt(transform.position, end, lineWidth, 0.32f, 8);
            RuntimeSkillDamageUtility.DamageLine(skill, transform.position, direction, lineLength, lineWidth, 1f);
        }
        else
        {
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
