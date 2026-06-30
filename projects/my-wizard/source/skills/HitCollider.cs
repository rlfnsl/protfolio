using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(SkillHitRange))]
public class HitCollider : MonoBehaviour
{
    [AutoAssign]
    public SkillHitRange skillHitrange;
    [AutoAssign]
    public Collider2D Collider;
    private Coroutine colliderEnableCoroutine;

    private int poolSize = 10;
    private ParticleSystem.Particle[] particles;

    public SkillInterFace Skill => skill;
    private SkillInterFace skill;
    public bool IsSingleHit => skill.isSingleHit;
    [HideInInspector]
    public int HitCount;

    private Transform target; // 타겟
    private Vector3 direction; // 이동 방향
    private Vector2 movementDirection;
    private float speed = 0; // 이동 속도 기본값
    private float holdTime;
    private Transform orbitCenter;
    private float orbitAngle;
    private float orbitRadius;
    private float orbitSpeed;
    private bool isOrbiting = false;
    public float Interval { get; private set; }
    public float DamageMultiplier { get; private set; } = 1f;

    private List<Transform> Effect = new List<Transform>();

    public Action<Transform> OnHitEnemy;

    public SpriteFlipbookAnimatorBase SpriteAnimator;
    private void OnValidate()
    {
        if (SpriteAnimator == null)
            SpriteAnimator = GetComponentInChildren<SpriteFlipbookAnimatorBase>();
    }
    private void Awake()
    {
        Effect = this.transform.GetComponentsInChildren<ParticleSystem>().Select(a => a.transform).ToList();
    }
    #region 스킬 설정
    public void SkillSetting(SkillInterFace _skill)
    {
        skill = _skill;
        OnHitEnemy = skill.HitAction;
        holdTime = skill.CurTime;
        DamageMultiplier = 1f;

        if (Effect.Count > 0)
        {
            var firstParticleSystem = Effect[0].GetComponent<ParticleSystem>();
            if (firstParticleSystem != null && skill is ISkillParticleModifier particleModifier)
            {
                // ISkillParticleModifier가 있으면 첫 번째 파티클 시스템 수정
                particleModifier.ModifyParticle(firstParticleSystem);
            }
        }


        this.transform.localScale = Vector3.one * _skill.CurAttackSize;
    }

    public void SetTarget(Transform _target)
    {
        target = _target;
        if (target != null)
        {
            movementDirection = Vector2.zero;
            direction = (target.position - transform.position).normalized; // 타겟 방향 계산
        }
    }
    public void SetSpawnPos(Transform _target)
    {
        this.transform.position = _target.position;
    }
    public void SetRandomDirection(Vector2 direction)
    {
        // 방향 설정 로직
        target = null;
        movementDirection = direction.normalized; // 방향 벡터 정규화
    }

    public void SetInfo(float _speed, int _count)
    {
        speed = _speed;
        HitCount = _count;
    }
    public void SetInterval(float _Interval)
    {
        Interval = _Interval;
    }
    public void SetDamageMultiplier(float multiplier)
    {
        DamageMultiplier = Mathf.Max(0f, multiplier);
    }
    public void SetOrbit(Transform center, float radius, float startAngle, float speed)
    {
        orbitCenter = center;
        orbitRadius = radius;
        orbitAngle = startAngle;
        orbitSpeed = speed;
        isOrbiting = true;
    }
    public void EnableColliderAfterDelay(float delay)
    {
        Collider.enabled = false;
        if (colliderEnableCoroutine != null)
            StopCoroutine(colliderEnableCoroutine);

        colliderEnableCoroutine = StartCoroutine(EnableColliderCoroutine(delay));
    }

    private IEnumerator EnableColliderCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (Collider != null)
            Collider.enabled = true;
    }
    #endregion
    private void OnDisable()
    {
        //스프라이트 애니메이터에서 자동으로 꺼질경우
        if (SpriteAnimator != null && SpriteAnimator.AutoEnd)
        {
            if (Skill != null)
            {
                var _objectPool = Skill.ReturnPool(name);
                if (_objectPool != null) _objectPool.ReturnObject(this);
            }
        }
    }

    private void Update()
    {
        if (PauseManager.freeze) return;

        if (SpriteAnimator == null || !SpriteAnimator.AutoEnd)
        {
            if (holdTime > 0)
            {
                holdTime -= Time.deltaTime;
                if (holdTime <= 0)
                {
                    var _objectPool = Skill.ReturnPool(name);
                    if (_objectPool != null) _objectPool.ReturnObject(this);
                }
            }
        }

        if (isOrbiting && orbitCenter != null)
        {
            orbitAngle += orbitSpeed * Time.deltaTime; // 회전
            float rad = orbitAngle * Mathf.Deg2Rad;
            transform.position = orbitCenter.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orbitRadius;
        }
        else if (speed != 0) // 기존 이동 방식 유지
        {
            if (target != null && movementDirection == Vector2.zero)
            {
                movementDirection = (target.position - transform.position).normalized;
                target = null;
            }
            transform.Translate(movementDirection * speed * Time.deltaTime);
        }
        else
        {
            if (target != null)
                transform.position = target.position;
        }
    }

    //void LateUpdate()
    //{
    //    if (PauseManager.freeze) return;
    //    if (particleSystemObject == null) return;


    //    int aliveParticles = particleSystemObject.GetParticles(particles);
    //    for (int i = 0; i < poolSize; i++)
    //    {
    //        if (i < aliveParticles)
    //        {
    //            // 파티클의 위치를 즉시 적용
    //            objectPool[i].transform.position = particleSystemObject.transform.TransformPoint(particles[i].position);

    //            // 파티클의 크기 적용 (선택적)
    //            objectPool[i].transform.localScale = Vector3.one * particles[i].GetCurrentSize(particleSystemObject);
    //        }
    //    }
    //}

    #region 타겟팅 관련 로직
    //private void OnHitTarget()
    //{
    //    // 타겟에 도달했을 때 처리 로직
    //    Debug.Log($"Hit target: {target.name}");

    //    // 필요하면 타겟에 데미지를 적용하거나 효과 처리
    //    ApplyDamage();

    //    // 풀로 반환
    //    Skill.Manager.defaultSkillObjectPools[skill.id].ReturnObject(this);
    //    gameObject.SetActive(false);
    //}

    //private void ApplyDamage()
    //{
    //    // 타겟이 데미지를 받을 수 있는 컴포넌트가 있는 경우 처리
    //    var damageable = target.GetComponent<IDamageable>(); // IDamageable 인터페이스 필요
    //    if (damageable != null)
    //    {
    //        damageable.TakeDamage(skill.OriginDamage);
    //    }
    //}
    #endregion
}
