using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using MalbersAnimations.Controller.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.AI;
public class AnimationOnOffTime
{
    public int Start;
    public int End;

    public AnimationOnOffTime(int start, int end)
    {
        Start = start;
        End = end;
    }
}
public partial class Player
{
    #region NavMesh 변수 AI전용

    // AI가 현재 이동하려는 목표 위치
    // 배회면 랜덤 위치
    // 추격이면 타겟 위치
    Vector3 _aiTargetPos;

    // 현재 목표 위치가 유효한지 여부
    bool _aiHasTarget;

    // 다음 배회 타겟을 다시 뽑을 시간
    float _aiNextPickTime;

    // 네비메시 경로를 다시 계산할 시간
    float _aiNextRepathTime;

    // 배회 시 랜덤 목표를 뽑는 최대 반경
    const float AI_WANDER_RADIUS = 20;

    // 배회 목표와 너무 가까우면 정지로 판단하는 거리
    const float AI_STOP_DIST = 0.6f;

    // 배회 타겟을 새로 뽑는 최소 시간
    const float AI_PICK_INTERVAL_MIN = 1.0f;

    // 배회 타겟을 새로 뽑는 최대 시간
    const float AI_PICK_INTERVAL_MAX = 2.5f;

    // 네비메시 경로 재계산 주기
    const float AI_REPATH_INTERVAL = 2;

    #region AI 추격

    // 현재 추격 중인 몬스터 타겟
    Monster _aiChaseTargetMonster;

    // 현재 추격 중인 레이드보스 플레이어 타겟
    Player _aiChaseTargetRaidBoss;

    // 추격 중인지 여부
    bool _aiIsChasing;

    // 다음 스캔 가능 시간
    float _aiNextScanTime;

    // 스캔 주기
    const float AI_SCAN_INTERVAL = 0.2f;

    // 우선 타겟 스캔 반경
    const float AI_PRIORITY_RADIUS = 12f;

    // 멈춘 거리 판정 여유값
    const float AI_ATTACK_EPS = 0.2f;

    // 근접 무기 정지 거리
    const float AI_STOP_MELEE = 2;

    // 원거리 무기 정지 거리
    const float AI_STOP_RANGED = 6.0f;

    // 타겟이 이 거리 이상 멀어지면 추격 포기
    const float AI_CHASE_FORGET_DIST = 18f;

    // 타겟이 이 높이 이상 위에 있으면 지상 추격을 하지 않음
    const float AI_HIGH_TARGET_Y = 1000;

    // 이전에 공격했는지 저장
    bool _aiWasAttacking;

    // 레이드 타겟의 NavMesh 투영 실패 시 마지막으로 성공한 지점
    Vector3 _aiLastValidRaidNavPos;
    bool _aiHasLastValidRaidNavPos;

    // 레이드 타겟 투영 반경
    const float AI_RAID_NAV_SAMPLE_RADIUS = 8f;

    // 경로 유효성 검사에 쓸 여유값
    const float AI_RAID_NAV_PATH_EPS = 0.1f;

    #region AI 스킬
    int _aiStatOrderIndex = 0;
    readonly int[][] STAT_ORDER_BY_WEAPON =
{
    // 무기 타입 0
    new int[] { 2, 1, 1, 4, 1 },
    // 무기 타입 1
    new int[] { 2, 1, 1, 4, 1 },
    // 무기 타입 2
    new int[] { 2, 9, 9, 4, 3 },
};

    // AI가 사용할 스킬 인덱스들
    [SerializeField] int[] _aiSkillIndex = { 0, 1, 26, 2, 25, 39 };
    [SerializeField] int[] _aiPassiveSkillIndex = { 1, 4, 9, 10, 11, 23, 36 };

    // 공격 중 스킬 시도 확률
    [SerializeField] float AI_SKILL_CHANCE = 0.18f;

    // 스킬 시도 최소 간격
    [SerializeField] float AI_SKILL_COOLDOWN_MIN = 2.0f;

    // 스킬 시도 최대 간격
    [SerializeField] float AI_SKILL_COOLDOWN_MAX = 4.0f;

    // 다음 스킬 시도 가능 시간
    float _aiNextSkillTryTime;

    #endregion

    #endregion

    #endregion

    #region Object
    private ParticleSystem _hpRegenParticle;
    public ParticleSystem HpRegenParticle
    {
        get
        {
            if (_hpRegenParticle == null)
            {
                var prefab = Resources.Load<ParticleSystem>("PlayerEffect/HPRegenEffect");
                if (prefab == null) return null;

                _hpRegenParticle = Instantiate(prefab, this.transform);
            }
            return _hpRegenParticle;
        }
    }
    private ParticleSystem _mpRegenParticle;
    public ParticleSystem MpRegenParticle
    {
        get
        {
            if (_mpRegenParticle == null)
            {
                var prefab = Resources.Load<ParticleSystem>("PlayerEffect/MPRegenEffect");
                if (prefab == null) return null;

                _mpRegenParticle = Instantiate(prefab, this.transform);
            }
            return _mpRegenParticle;
        }
    }

    public ShopObject shop;
    private CharacterAudioDataSet _characterAudio;
    public CharacterAudioDataSet CharacterAudio
    {
        get
        {
            if (_characterAudio == null)
            {
                if (DataManager.Instance.CharacterAudio.TryGetValue(HeroInfo.Index.ToString(), out var data))
                    _characterAudio = data;
                else
                    return null;
            }
            return _characterAudio;
        }
    }
    private CharacterAudioDataSet _characterCommonAudio;
    public CharacterAudioDataSet CharacterCommonAudio
    {
        get
        {
            if (_characterCommonAudio == null)
            {
                if (DataManager.Instance.CharacterAudio.TryGetValue("Common", out var data))
                    _characterCommonAudio = data;
                else
                    return null;
            }
            return _characterCommonAudio;
        }
    }

    #endregion

    #region ▶ Player Settings
    [Networked, OnChangedRender(nameof(OnChanged_NetStatCount))]
    public int NetLevel { get; set; } = 1;
    [Networked, OnChangedRender(nameof(OnChanged_NetCoin))]
    public int NetCoin { get; set; } = 0;
    public float AttackMoveSpeedRatio = 0.7f;
    public int ReRollCount = 3;
    //public int HealCount;
    //public int MPCount;
    [SerializeField]
    private Transform[] meleeEffectPostion;
    private bool canUseStaminaAgain = true;
    private Transform _modelTransform => FollowController.transform;
    private float ReviveTime = 30f;
    private CancellationTokenSource reviveCancellationTokenSource;
    private CancellationTokenSource _attackAnimCts;
    private CancellationTokenSource _attackLoopCts;
    private Dictionary<Data_Item, int> _ownedItems = new Dictionary<Data_Item, int>();
    public Dictionary<Data_Item, int> OwnedItems => _ownedItems;
    [Networked, OnChangedRender(nameof(OnChanged_GodMode))]
    public NetworkBool NetIsGodMode { get; set; }

    [Networked]
    private float NetGodModeEndTime { get; set; }
    public bool IsInvincible() => NetIsGodMode || Invincible;

    static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ID_ShadeColor1 = Shader.PropertyToID("_1st_ShadeColor");
    static readonly int ID_ShadeColor2 = Shader.PropertyToID("_2nd_ShadeColor");
    MaterialPropertyBlock _mpb;
    bool _isGodModeVFXRunning = false;
    CancellationTokenSource _godModeVFXToken;


    // 막기 관련 설정
    private float BlockDuration = 9999f;
    private float BlockDamageMultiplier = 0.5f;
    [Networked] private float NetLastBlockStartTime { get; set; }
    [Networked, OnChangedRender(nameof(OnChanged_NetParriedTime))] public float NetParriedTime { get; set; }
    [SerializeField] public float ParriedTimeDuration = 1;
    [Networked, OnChangedRender(nameof(OnChanged_NetParriedAttackCount))] private int NetParriedAttackCount { get; set; }
    private ParticleSystem ParriedParticle;

    private float BlockFrontAngle = 70f;
    private float ParryRadius = 3f;   // 패링 범위 반경
    [SerializeField]
    private float ParryWindow = 0.5f; // 기존에 쓰고 있던 패링 유효 시간이라면 그대로 유지

    private bool _prevBlockPressed;
    #endregion

    #region ▶ Hero / Weapon Info
    private List<SkinnedMeshRenderer> bodyRenderers
    {
        get
        {
            if (FollowController == null)
                return null;
            return FollowController.BodyRenderers;
        }
    }
    public Data_Hero HeroInfo;
    [Networked]
    public int WeaponIndex { get; set; } = -1;
    public Data_Weapon WeaponInfo;
    [Networked, OnChangedRender(nameof(OnChanged_NetIsJump))]
    public NetworkBool NetIsJump { get; set; }
    [Networked, OnChangedRender(nameof(OnChanged_NetIsRoll))]
    public NetworkBool NetIsRoll { get; set; }
    [Networked]
    public NetworkBool NetIsDeath { get; set; }
    [Networked, OnChangedRender(nameof(OnChanged_NetIsRollDirection))]
    public Vector3 NetRollDirection { get; set; }
    [Networked, OnChangedRender(nameof(OnChanged_NetAPS))]
    private float NetAPS { get; set; } = 1f;
    [Networked] private float NetNextAttackTime { get; set; }
    [Networked, OnChangedRender(nameof(OnChanged_NetAttackToken))]
    private int NetAttackToken { get; set; }
    private int _pendingAttackTokens = 0;
    private bool _processingAttackTokens = false;
    private bool AttackCooldownReady => Now >= NetNextAttackTime;

    private WeaponHitbox weaponHitBox;

    private int _weaponAttackType
    {
        get
        {
            if (WeaponInfo == null) return -1;
            return WeaponInfo.WeaponType;
        }
    }

    private readonly Dictionary<string, float> _clipLenCache = new Dictionary<string, float>();
    private string[] _comboSeq => GetAttackClipSequence();
    private int _comboIdx = 0;
    private string _rangedClip => GetAttackClipSequence()[0];
    private int _localAttackToken = 0;

    private Data_Weapon nextWeaponData;
    #endregion

    #region SettingValue
    #region Roll
    [SerializeField] private float RollMoveDuration = 0.6f; // 실제 이동 구간(초)
    [SerializeField] private float RollGetUpLock = 0.35f; // 기상 락 기본값(초)
    private float RollDistance => Speed * 1.2f;
    [SerializeField] private float RollRequestTimeoutSec = 0.4f; // RPC 왕복 안 올 때 잠깐 잡아두는 시간
    [SerializeField] private float MaxGetUpLockCap = 0.25f; // 기상 락 상한 (너무 오래 멈추는 것 방지)
    public float RollCooldown = 1.0f;

    // ====== 절대 "시간(초)" 동기화 ======
    [Networked] private float NetRollMoveEndTime { get; set; }
    [Networked] private float NetRollGetUpEndTime { get; set; }
    [Networked] private float NetRollCooldownEndTime { get; set; }

    private bool _rollRequestPending;     // 클라에서 RPC 보낸 직후 도배 방지
    private float _rollRequestTimeoutAt;   // 펜딩 타임아웃
    private bool _rollCostApplied;        // 스태미나 차감 1회 보장
    private bool _prevRollPressed;        // 엣지 트리거

    private float Now => (float)Runner.SimulationTime;

    private bool InRollMovePhase => NetIsRoll && Now < NetRollMoveEndTime;
    private bool InRollGetUpPhase => NetIsRoll && Now >= NetRollMoveEndTime && Now < NetRollGetUpEndTime;
    private bool InRollAnyLock => NetIsRoll;
    private bool RollCooldownReady => Now >= NetRollCooldownEndTime;
    #endregion
    #endregion

    #region ▶ Animation ID / Speed
    private const float _baseAnimSpeed = 1f;
    private int _pUpStairSpeed;

    private int _animIWeaponType;
    private int _animIBaseAttack;
    private int _animIUseSkill;
    private int _animIDIsMove;
    private int _animIDSpeed;
    private int _animIDIsJump;
    private int _animIDIsRoll;
    private int _animIMoveX;
    private int _animIMoveY;
    private int _animIDChanneling;
    private int _animIDDeath;
    private int _animBlock;
    #endregion

    #region ▶ Status Flags
    protected float _jumpTimer = 0f;
    private float _rollTimer = 0f;
    private bool _isRolling = false;
    private bool _isBaseAttacking = false;
    private bool _isSkillAttacking = false;
    private bool _isReadyToUseSkill = true;
    protected bool _wasGrounded = false;
    #endregion

    #region ▶ Managers
    private SkillButtonManager _skillButtonManager;
    #endregion

    #region ▶ Network Movement

    void Player_FixedUpdateNetwork()
    {
        TickGodModeServer();

        bool hasInput = GetInput(out NetworkInputData input);

        // AI는 여기서 입력을 만든다
        if (CanAIControl && IsAlive)
        {
            if (ingameManager != null && !ingameManager.CanAIMove) return;
            hasInput = AI_BuildInput(out input);

            // 전투 판단과 공격 발동은 여기서 한번만 처리
            AI_ServerUpdateCombat(ref input);
        }

        if (!hasInput || !IsAlive)
        {
            if (Object.HasStateAuthority)
                _kcc.Move(Vector3.zero);
            return;
        }

        bool rotationLocked = InRollAnyLock;
        if (!rotationLocked)
        {
            float yaw = GetUnifiedLookValue(input.lookDelta.x);
            transform.Rotate(Vector3.up, yaw, Space.World);
        }

        Player_ProcessMovement(input);

        if (Object.HasStateAuthority)
        {
            if (HpRegenPlayTime >= 0) HpRegenPlayTime -= Time.deltaTime;
            if (MpRegenPlayTime >= 0) MpRegenPlayTime -= Time.deltaTime;

            RegenerationProcess();
            ActivePassiveSkill();
            Server_ResolveRollPhases();

            NetPosition = transform.position;
            NetRotation = transform.rotation;

            if (transform.position.y < -50f)
            {
                if (!ingameManager.raidStarted)
                    TeleportToSpawnPoint();
            }
        }

        if (Object.HasInputAuthority)
            RPC_SetNetStamina(Stamina);
        if (CanAIControl)
            NetStamina = Stamina;

        ResetModelRotation();
    }

    private void TickGodModeServer()
    {
        if (!Object.HasStateAuthority) return;
        if (NetIsGodMode && Now >= NetGodModeEndTime)
            RPC_GodModeStop();
    }
    #endregion

    #region ▶ Input Update
    void PlayerLateUpdate()
    {
        if (!Object.HasInputAuthority || _camera == null) return;
        if (!IsAlive || NetIsDeath) return;
        // 기존 직접 배치 대신, 충돌 보정 컴포넌트 사용
        if (_tpsCam == null)
        {
            _tpsCam = _camera.GetComponent<TpsCameraCollision>() ?? _camera.gameObject.AddComponent<TpsCameraCollision>();
            _tpsCam.Init();
        }
        Transform pivot = CameraPivot != null ? CameraPivot : CameraHandle.parent;
        Vector3 desiredLocalOffset = (pivot != null) ? (pivot.InverseTransformPoint(CameraHandle.position)) : Vector3.zero;
        Quaternion desiredRot = CameraHandle.rotation;

        _tpsCam.UpdateCamera(pivot, desiredLocalOffset, desiredRot);
    }
    public void Player_Update()
    {
        if (!Object.HasInputAuthority) return;
        CameraPivot.localPosition = PlayerCameraPos;
        UpdateCooldownUI();
        CheckShopDistance();

        if (NetSpeed >= 1 && NetIsMoving)
        {
            if (Affect == null || !InfiniteStamina)
                Stamina -= Time.deltaTime * 20;
        }
        else
        {
            if (Stamina < HeroInfo.Stamina)
                Stamina += Time.deltaTime * HeroInfo.StaminaRegeneration;
            else
                Stamina = HeroInfo.Stamina;
        }
        if (Stamina <= 0)
        {
            canUseStaminaAgain = false;
        }

        if (_inputManager.ClickInteractable)
        {
            TryInteract();
        }
        if (ingameManager._KeyLock)
        {
            if (NetBaseAttacking)
            {
                RPC_SetBaseAttack(false);
                _comboIdx = 0;
            }
            return;
        }
        if (!IsAlive) return;

        UpdateCameraPivotPos();

        if (Stun) return;

        bool blockDown = _inputManager.blockoNflyPressed && !_prevBlockPressed;
        bool blockUp = !_inputManager.blockoNflyPressed && _prevBlockPressed;
        _prevBlockPressed = _inputManager.blockoNflyPressed;

        if (blockDown && !_isRolling && !NetBlock && IsAlive && !NetIsDeath && !NetUseSkill)
        {
            RPC_RequestBlockStart();
        }

        if (blockUp && NetBlock)
        {
            RPC_RequestBlockEnd();
        }

        bool rollDown = _inputManager.rollPressed && !_prevRollPressed;
        _prevRollPressed = _inputManager.rollPressed;

        if (rollDown
            && _kcc.Grounded
            && RollCooldownReady
            && !NetIsRoll
            && !_rollRequestPending
            && !NetBlock)
        {
            if (CanUseStamina())
            {
                // 방향 계산
                Vector2 inputDir = _inputManager.moveDir;
                Vector3 inputWorldDir;
                if (inputDir != Vector2.zero)
                {
                    Vector3 f = CameraHandle.forward; f.y = 0f; f.Normalize();
                    Vector3 r = CameraHandle.right; r.y = 0f; r.Normalize();
                    inputWorldDir = (f * inputDir.y + r * inputDir.x).normalized;
                }
                else inputWorldDir = transform.forward;

                _rollRequestPending = true;
                _rollRequestTimeoutAt = Now + RollRequestTimeoutSec;

                RPC_RequestRoll(inputWorldDir);
            }
        }

        if (_rollRequestPending && (NetIsRoll || Now >= _rollRequestTimeoutAt))
        {
            _rollRequestPending = false;
        }


        if (_inputManager.useSkillPressed && !_isBaseAttacking && !_isSkillAttacking && _isReadyToUseSkill && _skillButtonManager.CurSKill != null && !_isRolling && !NetBlock)
        {
            if (InGameManager.Instance.Guide.UseActiveSkillGuide.Flag || InGameManager.Instance.Guide.UseActiveSkillGuide.Active)
                InGameManager.Instance.Guide.UseActiveSkillGuide.Flag = false;

            if (InGameManager.Instance.SkillManager.TryUseSkillCost(this, _skillButtonManager.CurSKill.Index))
            {
                if (NetBaseAttacking)
                {
                    RPC_CancelBaseAttackKeepCooldown();
                }

                _isSkillAttacking = true;
                _isReadyToUseSkill = false;
                ingameManager.SkillManager.ActiveSkillFunc(this, _skillButtonManager.CurSKill.Index, transform.forward);
                RPC_SetSkill(true, _skillButtonManager.CurSKill.Index, _skillButtonManager.CurSKill.AnimationKey);
            }
        }

        bool pressed = _inputManager.basicAttackPressed && !BlockBaseAttack && !_isSkillAttacking && !_isRolling && IsAlive && !NetIsDeath && !NetBlock;
        if (pressed)
        {
            RPC_RequestAttackTick();
        }
        else
        {
            if (NetBaseAttacking)
            {
                RPC_SetBaseAttack(false);
                _comboIdx = 0;
            }
        }
    }

    public void UpdateCameraPivotPos()
    {
        if (!ingameManager.CanMove)
        {
            _inputManager.lookDelta = Vector2.zero;
            ResetCameraPivotLocal();
            return;
        }
        float pitchDelta = GetUnifiedLookValue(_inputManager.lookDelta.y);
        _pitch = Mathf.Clamp(_pitch - pitchDelta, -60f, 60f);
        CameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
    void TryInteract_Hero()
    {
        if (shop != null && ingameManager.ShopManager.gameObject.activeSelf)
        {
            ingameManager.ShopManager.OnClickCloseButton(ingameManager.ShopManager.gameObject);
            return;
        }
        if (shop != null)
        {
            shop.TryInteract(this);
            if (ingameManager.Guide.ShopOpenGuide.Flag)
                ingameManager.Guide.ShopOpenGuide.Flag = false;
        }
    }

    #endregion

    #region OnNetworkedChanged Handlers
    private void OnChanged_NetIsRollDirection()
    {
        Quaternion targetRot = Quaternion.LookRotation(NetRollDirection, Vector3.up);
        FollowController.ModelRoot.transform.rotation = targetRot;
        CharAnimator?.Play("Roll", 0, 0f);
    }
    private void OnChanged_NetIsRoll()
    {
        if (_isRolling != NetIsRoll)
            _isRolling = NetIsRoll;

        if (NetIsRoll)
        {
            if (Object.HasInputAuthority && !_rollCostApplied)
            {
                Stamina = Mathf.Max(0f, Stamina - 20f);
                _rollCostApplied = true;
            }

            var clip = PickOne_WithFlags(
                "roll",
                CharacterAudio?.roll, CharacterAudio?.useRandom_roll,
                CharacterCommonAudio?.roll, CharacterCommonAudio?.useRandom_roll
            );
            PlayOneShot(clip);
        }
        else
        {
            _rollCostApplied = false;
        }
    }
    private void OnChanged_NetIsJump()
    {
        if (NetIsJump)
        {
            var clip = PickOne_WithFlags(
                "jump",
                CharacterAudio?.jump, CharacterAudio?.useRandom_jump,
                CharacterCommonAudio?.jump, CharacterCommonAudio?.useRandom_jump
            );
            PlayOneShot(clip);
        }
        else
        {
            var clip = PickOne_WithFlags(
                "jumpLanding",
                CharacterAudio?.jumpLanding, CharacterAudio?.useRandom_jumpLanding,
                CharacterCommonAudio?.jumpLanding, CharacterCommonAudio?.useRandom_jumpLanding
            );
            PlayOneShot(clip);
        }

        OnChanged_NetIsWalkPlayer();
    }
    private void OnChanged_NetIsWalkPlayer()
    {
        if (!NetIsMoving || NetIsJump)
        {
            StopAudio();
            return;
        }

        if (Mathf.Approximately(NetSpeed, 0f))
        {
            var clip = PickOne_WithFlags(
                "walk",
                CharacterAudio?.walk, CharacterAudio?.useRandom_walk,
                CharacterCommonAudio?.walk, CharacterCommonAudio?.useRandom_walk
            );
            if (clip != null) PlayAudio(clip);
            return;
        }

        if (Mathf.Approximately(NetSpeed, 1f))
        {
            var clip = PickOne_WithFlags(
                "run",
                CharacterAudio?.run, CharacterAudio?.useRandom_run,
                CharacterCommonAudio?.run, CharacterCommonAudio?.useRandom_run
            );
            if (clip != null) PlayAudio(clip);
            return;
        }
    }

    private void OnChanged_NetAttackToken()
    {
        if (NetAttackToken <= _localAttackToken) return;

        int delta = NetAttackToken - _localAttackToken;
        _localAttackToken = NetAttackToken;

        _pendingAttackTokens += delta;
        if (!_processingAttackTokens)
            ProcessAttackTokens().Forget();
    }
    private void OnChanged_GodMode()
    {
        SetGodModeVisual(NetIsGodMode);
    }
    private void OnChanged_NetStatCount()
    {
        IngameData.Instance.SetLevel(Object.Id, NetLevel);
        if (PlayerInfoData.IsRaidBoss)
            return;
        ingameManager.SetAverageLevel();
        if (ingameManager.ShowItemManager.gameObject.activeSelf)
        {
            ingameManager.ShowItemManager.ResetStatCount();
        }
    }
    private void OnChanged_NetCoin()
    {
        if (!PlayerInfoData.IsRaidBoss && ingameManager != null)
            ingameManager.PlayerInfoManager.UpdateCoinAmount();
    }
    private void OnChanged_NetAPS()
    {
        _clipLenCacheDirty = true;
    }
    private void OnChanged_NetParriedTime()
    {
        if (ingameManager == null)
            ingameManager = InGameManager.Instance;
        if (ingameManager.EffectManager == null) return;
        if (Object.HasStateAuthority)
        {
            NetParriedAttackCount = 1;
        }
        if (ParriedParticle == null || !ParriedParticle.gameObject.activeSelf)
            ParriedParticle = ingameManager.EffectManager.PlayPooledEffect(
                "Effect/WeaponParriedLight", Vector3.zero, Quaternion.identity, ParriedTimeDuration,
                parent: weaponHitBox.transform
            );
    }
    private void OnChanged_NetParriedAttackCount()
    {
        if (NetParriedAttackCount <= 0)
        {
            if (ParriedParticle != null || ParriedParticle.gameObject.activeSelf)
            {
                ingameManager.EffectManager.StopPooledEffect(ParriedParticle);
                ParriedParticle = null;
            }
        }
    }
    #endregion

    #region ▶ Movement Logic
    private void CheckShopDistance()
    {
        if (shop == null)
            return;

        Vector3 diff = transform.position - shop.transform.position;

        if (diff.sqrMagnitude > 15)
        {
            shop = null;
        }
    }
    // 막기 전용 넉백
    public async UniTaskVoid BlockKnockback(float knockbackForce = 1f, float knockbackTime = 0.1f)
    {
        if (!Object.HasStateAuthority) return;

        // 앞으로 오는 공격을 막았다고 보고 뒤로 밀기
        Vector3 dir = -transform.forward.normalized * 3f * knockbackForce;

        KnockBack = true;
        KnockBackDirection = dir;

        await UniTask.Delay(TimeSpan.FromSeconds(knockbackTime));

        KnockBack = false;
        KnockBackDirection = Vector3.zero;
    }
    private float GetUnifiedLookValue(float axis)
    {
        return axis * NetRotationSpeed * Runner.DeltaTime;
    }
    private void Server_ResolveRollPhases()
    {
        if (!NetIsRoll) return;

        if (Now >= NetRollGetUpEndTime)
            NetIsRoll = false; // 이동/기상 모두 끝 → 해제
    }
    private float AttackingSpeed()
    {
        if (_weaponAttackType == 0) return 1f;

        if (CharAnimator == null) return 1f;

        var stateInfo = CharAnimator.GetCurrentAnimatorStateInfo(1);

        if (stateInfo.IsName("Idle"))
        {
            return 1f;
        }
        else
        {
            return AttackMoveSpeedRatio;
        }
    }
    private void Player_ProcessMovement(NetworkInputData input)
    {
        if (NetIsRoll)
        {
            if (InRollMovePhase)
            {
                float rollSpeed = RollDistance / Mathf.Max(0.01f, RollMoveDuration);
                _kcc.Move(NetRollDirection * rollSpeed);
            }
            else
            {
                _kcc.Move(Vector3.zero);
            }

            NetIsMoving = false;
            NetSpeed = 0f;
            return;
        }

        //// 막는 동안에는 이동하지 않음
        //if (NetBlock)
        //{
        //    _kcc.Move(Vector3.zero);
        //    NetIsMoving = false;
        //    NetSpeed = 0f;
        //    NetMoveX = 0f;
        //    NetMoveY = 0f;
        //    return;
        //}

        if (!KnockBack && !Stun)
        {
            Vector2 md = input.moveDir;
            if (md.sqrMagnitude > 1f) md.Normalize();
            float blockSpeed = NetBlock ? 0.3f : 1;
            float speed = (input.IsRun ? Speed * 1.5f : Speed) * AttackingSpeed() * blockSpeed;
            Vector3 moveDir = (transform.forward * md.y + transform.right * md.x) * speed;
            _kcc.Move(moveDir);

            NetIsMoving = md.sqrMagnitude > 0f;
            NetSpeed = input.IsRun ? 1 : 0;
            NetMoveX = md.x;
            NetMoveY = md.y;
        }
        else
        {
            _kcc.Move(KnockBackDirection);
            NetIsMoving = false;
            NetSpeed = 0f;
        }
    }



    private void ProcessJump()
    {
        if (_kcc.Grounded && !_wasGrounded)
        {
            NetIsJump = false;
            _jumpTimer = JumpCooldown;

            // 착지 시 중력 원상복귀
            _kcc.gravity = -500;

            RPC_ApplyJumpGravity_ClientRPC(-100);
        }

        _wasGrounded = _kcc.Grounded;
    }
    private void ResetModelRotation()
    {
        // 모델 로컬 회전값을 (0,0,0)으로 천천히 보간
        if (FollowController.ModelRoot.transform != null)
        {
            Quaternion targetLocalRotation = Quaternion.Euler(Vector3.zero);
            float rotateSpeed = 5f;

            FollowController.ModelRoot.transform.localRotation = Quaternion.RotateTowards(
                FollowController.ModelRoot.transform.localRotation,
                targetLocalRotation,
                rotateSpeed * Time.deltaTime * 60f
            );

            // 거의 0에 도달하면 확실히 0으로 고정
            if (Quaternion.Angle(FollowController.ModelRoot.transform.localRotation, targetLocalRotation) < 0.5f)
            {
                FollowController.ModelRoot.transform.localRotation = targetLocalRotation;
            }
        }
    }
    public bool CanUseStamina_Hero()
    {
        if (Object != null && Object.IsValid && PlayerInfoData.IsRaidBoss) return true;

        if ((!canUseStaminaAgain && NetStamina < 10) || NetStamina <= 0)
        {
            return false;
        }
        canUseStaminaAgain = true;
        return true;
    }
    #endregion

    #region ▶ Animation Handling
    public void Player_AssignAnimationIDs()
    {
        _animIDIsMove = Animator.StringToHash("IsMove");
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDIsJump = Animator.StringToHash("IsJump");
        _animBlock = Animator.StringToHash("IsBlock");
        _animIBaseAttack = Animator.StringToHash("IsAttack");
        _animIUseSkill = Animator.StringToHash("UseSkill");
        _animIDIsRoll = Animator.StringToHash("IsRoll");
        _animIMoveX = Animator.StringToHash("MoveX");
        _animIMoveY = Animator.StringToHash("MoveY");
        _pUpStairSpeed = Animator.StringToHash("UpStairSpeed");
        _animIWeaponType = Animator.StringToHash("WeaponType");
        _animIDChanneling = Animator.StringToHash("Channeling");
        _animIDDeath = Animator.StringToHash("Death");
    }

    public void Player_UpdateAnimations()
    {
        if (CharAnimator == null) return;

        if (!NetBaseAttacking && !NetUseSkill && CharAnimator.speed != _baseAnimSpeed)
            CharAnimator.speed = _baseAnimSpeed;

        CharAnimator.SetBool(_animIDDeath, NetIsDeath);
        if (!IsAlive)
        {
            return;
        }
        CharAnimator.SetBool(_animIDIsRoll, NetIsRoll);
        if (NetIsRoll)
        {
            CharAnimator.SetBool(_animIDIsMove, false);
            CharAnimator.SetBool(_animIDIsJump, false);
            CharAnimator.SetFloat(_animIDSpeed, 0f);
            return;
        }

        CharAnimator.SetBool(_animIDIsMove, NetIsMoving);
        //점프 -> 막기
        //CharAnimator.SetBool(_animIDIsJump, NetIsJump);
        CharAnimator.SetBool(_animBlock, NetBlock);
        CharAnimator.SetBool(_animIBaseAttack, NetBaseAttacking);
        CharAnimator.SetBool(_animIDChanneling, NetIsChanneling);
        CharAnimator.SetFloat(_animIDSpeed, NetSpeed);
        CharAnimator.SetFloat(_animIMoveY, NetMoveY);
        CharAnimator.SetFloat(_animIMoveX, NetMoveX);
    }
    #endregion

    #region ▶ Attack Execution

    void Server_RequestAttackTick_Common()
    {
        if (!Object.HasStateAuthority) return;

        if (NetIsDeath || !IsAlive) return;
        if (_isRolling) return;

        float cd = GetAttackCooldownSec();
        if (Now < NetNextAttackTime) return;

        NetNextAttackTime = Now + cd;
        NetBaseAttacking = true;
        NetAttackToken += 1;
    }
    private async UniTask PlayOneAttackClip(string clipName, int layer, CancellationToken token)
    {
        float clipLen = Mathf.Max(0.01f, GetClipLengthSec(clipName));
        float cd = GetAttackCooldownSec();

        const float MinAnimSpeed = 0.1f;
        const float MaxAnimSpeed = 20.0f;
        float animSpeed = Mathf.Clamp(clipLen / cd, MinAnimSpeed, MaxAnimSpeed);

        CharAnimator.SetFloat(_pUpStairSpeed, animSpeed);

        float trans = 0.02f;
        int stateHash = Animator.StringToHash(clipName);
        CharAnimator.CrossFade(stateHash, trans, layer, 0f);

        float endAt = Now + cd;
        while (Now < endAt && !token.IsCancellationRequested)
            await UniTask.Yield(token);

        var st = CharAnimator.GetCurrentAnimatorStateInfo(layer);
        if (st.fullPathHash == stateHash && st.normalizedTime < 1f)
            CharAnimator.CrossFade(Animator.StringToHash("Idle"), 0.02f, layer, 0f);
    }
    private async UniTaskVoid ProcessAttackTokens()
    {
        _processingAttackTokens = true;
        try
        {
            const int layerIndex = 1;

            while (_pendingAttackTokens > 0 && IsAlive && !NetIsDeath)
            {
                // 다음 하나 소비
                _pendingAttackTokens--;

                // 콤보 시퀀스에서 하나 선택
                if (_weaponAttackType == 0)
                {
                    if (_comboIdx >= _comboSeq.Length) _comboIdx = 0;
                    string clip = _comboSeq[_comboIdx];
                    _comboIdx = (_comboIdx + 1) % _comboSeq.Length;
                    await PlayOneAttackClip(clip, layerIndex, this.GetCancellationTokenOnDestroy());
                }
                else
                {
                    await PlayOneAttackClip(_rangedClip, layerIndex, this.GetCancellationTokenOnDestroy());
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _processingAttackTokens = false;
        }
    }

    public void SpawnProjecttile(string _name, Vector3 _spawnPos = default(Vector3), Vector3 _dir = default(Vector3), float _damageValue = -1)
    {
        var _damage = DamageCalculator.GetDamage(this);

        Vector3 dir;

        if (PlayerInfoData != null && PlayerInfoData.IsAI)
        {
            // AI일 경우 현재 플레이어 정면 방향으로 발사
            dir = transform.forward;
        }
        else
        {
            // 플레이어는 기존 조준 위치 기준
            dir = _dir == default(Vector3)
                ? (ingameManager.PlayerInfoManager.GetAimPos() - transform.position).normalized
                : _dir;
        }

        ingameManager.ProjectileSpawnManager.RPC_SpawnProjectile(
            playerID: this.Id,
            name: _name,
            SpawnPos: _spawnPos == default(Vector3) ? this.transform.position : _spawnPos,
            Direction: dir,
            Damage: _damageValue == -1 ? _damage.FinalDamage : _damageValue,
            _isCritical: _damage.IsCritical,
            Guiding: GuidedAttack
        );
    }
    public void PlayAttackSound()
    {
        bool isBow = (_weaponAttackType == 1);

        var clip = isBow
            ? PickOne_WithFlags(
                "bowAttack",
                CharacterAudio?.bowAttack, CharacterAudio?.useRandom_bowAttack,
                CharacterCommonAudio?.bowAttack, CharacterCommonAudio?.useRandom_bowAttack
              )
            : PickOne_WithFlags(
                "magicAttack",
                CharacterAudio?.magicAttack, CharacterAudio?.useRandom_magicAttack,
                CharacterCommonAudio?.magicAttack, CharacterCommonAudio?.useRandom_magicAttack
              );

        PlayOneShot(clip);
    }
    public void AttackCheck(bool _check)
    {
        weaponHitBox.SetActive(_check);
    }
    public void SpawnEffect(string _name)
    {
        Transform _parant = meleeEffectPostion.Where(a => a.name == _name).First();
        int index = int.Parse(_name.Split('_')[1]);

        var clip = PickByIndexOrOne_WithFlags(
            "meleeAttack", index,
            CharacterAudio?.meleeAttack, CharacterAudio?.useRandom_meleeAttack,
            CharacterCommonAudio?.meleeAttack, CharacterCommonAudio?.useRandom_meleeAttack
        );
        PlayOneShot(clip);

        ingameManager.EffectManager.PlayPooledEffect(
            $"Effect/NormalAttack/Melee/{_name}",
            Vector3.zero, Quaternion.identity, 1.5f, _parant
        );
    }
    private async UniTask RunSkillExecution(int skillIndex)
    {
        float delayTime = 0.2f;
        await UniTask.Delay((int)(delayTime * 1000));

        await MonitorSkillAnimation();
        if (NetIsChanneling)
            return;

        _isSkillAttacking = false;
        RPC_ResetSkill();
    }
    private async UniTask WaitNextSkillTerm()
    {
        float delayTime = 0.5f;
        await UniTask.Delay((int)(delayTime * 1000));
        _isReadyToUseSkill = true;
    }

    private async UniTask MonitorSkillAnimation()
    {
        if (CharAnimator == null) return;

        const int SkillLayer = 2;
        string stateName = "Skill_" + HeroInfo.HeroType;

        try
        {
            await UniTask.WaitUntil(() =>
            {
                if (CharAnimator == null) return true;
                var st = CharAnimator.GetCurrentAnimatorStateInfo(SkillLayer);
                return st.IsName(stateName);
            });

            float asMult = Mathf.Max(1f, GetAPS());
            float cancelPoint = Mathf.Clamp01(1f / (0.7f * asMult + 0.3f));
            cancelPoint = Mathf.Clamp(cancelPoint, 0.35f, 1f);

            await UniTask.WaitUntil(() =>
            {
                if (CharAnimator == null) return true;
                var st = CharAnimator.GetCurrentAnimatorStateInfo(SkillLayer);
                return !st.IsName(stateName) || st.normalizedTime >= cancelPoint;
            });

            return;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Skill] MonitorSkillAnimation 예외: {ex.Message}");
            return;
        }
    }

    private async UniTask CheckSkillState()
    {
        await MonitorSkillAnimationState();

        _isSkillAttacking = false;
        RPC_ResetSkill();
    }

    private async UniTask MonitorSkillAnimationState()
    {
        if (CharAnimator == null) return;

        const string skillStateName = "Skill_";

        try
        {
            // 1. "Skill" 상태가 아니라면 패스
            if (CharAnimator != null && CharAnimator.GetCurrentAnimatorStateInfo(2).IsName(skillStateName + HeroInfo.HeroType.ToString()))
            {
                // 2. "Skill" 상태가 끝날 때까지 대기
                await UniTask.WaitUntil(() =>
                {
                    if (CharAnimator == null) return true;
                    var state = CharAnimator.GetCurrentAnimatorStateInfo(2);
                    return !state.IsName(skillStateName + HeroInfo.HeroType.ToString());
                });
            }
            Debug.Log("[Skill] State End");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Skill] MonitorSkillAnimation 예외 발생: {ex.Message}");
        }
    }

    #endregion

    #region ▶ Hero&Weapon 관련
    private void Player_SetupCharacterModel()
    {
        if (ingameManager == null)
        {
            ingameManager = InGameManager.Instance;
        }
        NetCoin = 10;
        NetSkillResetCount = 3;
        NetStatCount = LevelUpStatCount;

        string path = $"Character/{PlayerInfoData.CharacterIndex}";
        if (PlayerInfoData.CharacterSkinIndex != 0)
        {
            path += $"_{PlayerInfoData.CharacterSkinIndex}";
        }
        FollowController = PlayerFollowController.CreateFor(Object.Id);
        FollowController.CreateCharacterModel(path);
        if (!ingameManager.PlayerFollowKeyValue.ContainsKey(Object.Id))
        {
            ingameManager.PlayerFollowKeyValue.Add(Object.Id, FollowController);
        }
        else
        {
            ingameManager.PlayerFollowKeyValue[Object.Id] = FollowController;
        }
        PlayerInfoData.PlayerHP.SetActive(ingameManager.MyPlayerDataInfo.IsRaidBoss);
        PlayerInfoData.PlayerHP.transform.localPosition = Vector3.zero;
        PlayerInfoData.DebuffCanvas.transform.localPosition = Vector3.zero;
        if (ingameManager.ImRaid)
        {
            PlayerInfoData.DebuffCanvas.transform.localPosition = new Vector3(0, PlayerInfoData.PlayerHP.transform.localPosition.y + 0.4f, 0);
        }
        _kcc.gravity = -500;

        if (CharAnimator != null && CharAnimator.GetComponent<PlayerAnimationEvent>() == null)
        {
            CharAnimator.gameObject.AddComponent<PlayerAnimationEvent>();
        }

        HeroInfo = DataManager.Instance.HeroData[PlayerInfoData.CharacterIndex];
        HP = HeroInfo.HP;
        MP = HeroInfo.MP;
        Stamina = HeroInfo.Stamina;
        WalkSpeed = HeroInfo.Speed;
        HPRegeneration = HeroInfo.HPRegeneration;
        MPRegeneration = HeroInfo.MPRegeneration;

        if (controller == null) controller = GetComponent<CharacterController>();
        controller.center = Vector3.zero;
        controller.radius = 0.5f;
        controller.height = 2;
        controller.stepOffset = 0.5f;
        controller.excludeLayers = 0;

        if (Object.HasInputAuthority)
        {
            UniTask.Void(async () =>
            {
                CameraPivot.localPosition = PlayerCameraPos;
                ingameManager.PlayerInfoManager.Init(this);
                ingameManager.MinimapCam.CreatePlayerIcon(transform, isMine: true);
                ingameManager.MinimapCam.SetFollowTarget(transform);
                ingameManager.CameraRenderSetting();

                await UniTask.WaitUntil(() => PlayerItemComponent != null && CharAnimator != null);

                //Cursor.lockState = CursorLockMode.Locked;
                //Cursor.visible = false;
                CustomCursor.Instance.SetCursorVisible(false);

                int weaponIndex = DataManager.Instance.WeaponData.Values
                    .Where(w => w.WeaponType == HeroInfo.HeroType)
                    .First().Index;

                RPC_SetRoationSpeed(GameManager.Instance.RotationSpeed);
                RPC_OnWeaponChanged(weaponIndex);

                ingameManager.SkillManager.Setting();
                ingameManager.LevelUpManager.DefaultSkillSetting(1, HeroInfo.SkillIndex, 0);
                ingameManager.skillButtonManager.SelectSkillSlot(0);
                if (HeroInfo.DefaultAbilities != 0)
                {
                    ingameManager.LevelUpManager.DefaultSkillSetting(HeroInfo.DefaultAbilities == 1 ? 2 : 1, HeroInfo.AbilitiesIndex, 1);
                }
            });
        }
        if (Object.HasStateAuthority)
            DragonBreathScale = 0;

        if (PlayerInfoData.IsAI)
        {
            int weaponIndex = DataManager.Instance.WeaponData.Values
                    .Where(w => w.WeaponType == HeroInfo.HeroType)
                    .First().Index;
            if (ingameManager.IsHost)
                RPC_OnWeaponChanged(weaponIndex);
            AI_EnsureAgent();
        }

        tag = "Hero";
    }
    public void SetGodModeVisual(bool on)
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();

        if (on)
        {
            if (_isGodModeVFXRunning) return;
            _godModeVFXToken = new CancellationTokenSource();
            BlinkGodColorLoop(_godModeVFXToken.Token).Forget();
        }
        else
        {
            _godModeVFXToken?.Cancel();
            _isGodModeVFXRunning = false;

            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(ID_BaseColor, Color.white);
                _mpb.SetColor(ID_ShadeColor1, Color.white);
                _mpb.SetColor(ID_ShadeColor2, Color.white);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
    private async UniTaskVoid BlinkGodColorLoop(CancellationToken token)
    {
        _isGodModeVFXRunning = true;
        float speed = 4f; // 깜빡임 속도

        while (!token.IsCancellationRequested)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
            Color godColor = Color.Lerp(Color.white, Color.red, pulse);

            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(ID_BaseColor, godColor);
                _mpb.SetColor(ID_ShadeColor1, godColor);
                _mpb.SetColor(ID_ShadeColor2, godColor);
                r.SetPropertyBlock(_mpb);
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }
    public void GetNextWeapon()
    {
        int weaponCount = DataManager.Instance.WeaponData
                    .Where(a => a.Value.WeaponType == WeaponInfo.WeaponType).Count(); // 해당 무기의 개수

        int minIndex = DataManager.Instance.WeaponData
            .Where(a => a.Value.WeaponType == WeaponInfo.WeaponType)
            .Min(a => a.Value.Index);

        var data = WeaponInfo;

        //최종무기
        if ((data.Index - minIndex) == (weaponCount - 1))
        {
            nextWeaponData = null;
            return;
        }

        nextWeaponData = DataManager.Instance.WeaponData[data.Index + 1];
    }
    #endregion

    #region ▶ Attack Speed

    public void RefreshLayerSpeeds()
    {
        CharAnimator.SetFloat(_pUpStairSpeed, 1f);
        CharAnimator.SetInteger(_animIWeaponType, _weaponAttackType);
    }
    private float GetClipLengthSec(string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || CharAnimator == null) return 1f;

        if (_clipLenCache.TryGetValue(clipName, out var len) && len > 0f)
            return len;

        var ctrl = CharAnimator.runtimeAnimatorController;
        if (ctrl == null || ctrl.animationClips == null || ctrl.animationClips.Length == 0)
            return 1f;

        var clip = ctrl.animationClips.FirstOrDefault(c => c != null && c.name == clipName);
        len = clip != null && clip.length > 0f ? clip.length : 1f;

        _clipLenCache[clipName] = len;
        return len;
    }

    private float GetAPS()
    {
        float baseAps = WeaponInfo != null ? WeaponInfo.AttackSpeed : 1.5f;
        float mult = Mathf.Max(0.1f, AttackSpeedRatio);
        if (float.IsNaN(mult) || mult <= 0f) mult = 1f;
        return Mathf.Clamp(baseAps * mult, 0.1f, 20f);
    }

    private string[] GetAttackClipSequence()
    {
        if (_weaponAttackType == 0)
            return new[] { "0_0", "0_1", "0_2" };
        if (_weaponAttackType == 1)
            return new[] { "1_0" };
        if (_weaponAttackType == 2)
            return new[] { "2_0" };
        return new[] { "Idle" };
    }
    private float GetASMultSafe()
    {
        var a = Mathf.Max(0.1f, AttackSpeedRatio);
        if (float.IsNaN(a) || a <= 0f) a = 1f;
        return Mathf.Clamp(a, 0.1f, 20f);
    }

    private float ComputeAPSFromWeapon()
    {
        float baseAps = WeaponInfo != null ? WeaponInfo.AttackSpeed : 1.5f;
        return Mathf.Max(0.01f, baseAps * GetASMultSafe());
    }

    private float GetAttackCooldownSec()
    {
        return 1f / Mathf.Max(0.01f, GetAPS());
    }

    public void Server_UpdateAPSAndBroadcast()
    {
        if (!Object.HasStateAuthority) return;
        NetAPS = ComputeAPSFromWeapon();
    }
    private bool _clipLenCacheDirty = false;

    private bool IsRangedWeapon()
    {
        return _weaponAttackType == 1 || _weaponAttackType == 2;
    }

    private void UpdateCooldownUI()
    {
        var img = ingameManager?.PlayerInfoManager?.CoolImage;
        if (img == null) return;

        if (!IsRangedWeapon())
        {
            img.enabled = false;
            return;
        }

        img.enabled = true;

        float cd = GetAttackCooldownSec();
        if (cd <= 0f) { img.fillAmount = 0f; return; }

        float remain = Mathf.Clamp(NetNextAttackTime - Now, 0f, cd);
        img.fillAmount = remain / cd;
    }
    #endregion

    #region ▶ RPC
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_UseCoin(int amount)
    {
        NetCoin -= amount;
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_LocalReviveReset()
    {
        LocalReviveReset();
    }
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_OnWeaponChanged(int _index)
    {
        string path = $"Weapon/{_index}";
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[Weapon] 프리팹을 찾을 수 없음 " + path);
            return;
        }
        if (PlayerItemComponent.WeaponPos.childCount > 0)
        {
            for (int i = 0; i < PlayerItemComponent.WeaponPos.childCount; i++)
            {
                Destroy(PlayerItemComponent.WeaponPos.GetChild(i).gameObject);
            }
        }
        if (Object.HasStateAuthority)
        {
            WeaponIndex = _index;
        }
        GameObject model = Instantiate(prefab, PlayerItemComponent.WeaponPos);
        weaponHitBox = model.GetComponent<WeaponHitbox>();

        WeaponInfo = DataManager.Instance.WeaponData[_index];
        GetNextWeapon();

        RefreshLayerSpeeds();

        if (Object.HasStateAuthority)
        {
            Server_UpdateAPSAndBroadcast();
            NetNextAttackTime = Mathf.Min(NetNextAttackTime, Now);
        }

        if (Object.HasInputAuthority)
        {
            ingameManager.PlayerInfoManager.NormalAttackIcon.sprite =
                AtlasManager.Instance.GetSprite(AtlasType.WeaponIcon, WeaponInfo.Index);
            if (ingameManager.ShopManager != null && ingameManager.ShopManager.gameObject.activeSelf)
                ingameManager.ShopManager.WeaponSetting();
        }
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestBlockStart()
    {
        if (NetIsDeath || !IsAlive) return;
        if (_kcc == null) return;
        if (NetIsRoll) return;

        if (!NetBlock)
        {
            NetBlock = true;
            NetLastBlockStartTime = Now;

            bool isAttackingNow = NetBaseAttacking || _pendingAttackTokens > 0 || _processingAttackTokens;

            if (isAttackingNow)
            {
                if (Now >= NetNextAttackTime)
                {
                    float cd = GetAttackCooldownSec();
                    NetNextAttackTime = Now + cd;
                }

                NetBaseAttacking = false;
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestBlockEnd()
    {
        if (!NetBlock) return;

        NetBlock = false;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestJump()
    {
        if (!_kcc.Grounded) return;

        // 점프 시작
        NetIsJump = true;

        // 중력 완화
        _kcc.gravity = -20;

        _kcc.Jump();
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ApplyJumpGravity_ClientRPC(float _gravity)
    {
        _kcc.gravity = _gravity;
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_Tel()
    {
        var p = ingameManager.Players.Values.ToList();
        var index = UnityEngine.Random.Range(0, p.Count);
        _kcc.Teleport(p[index].transform.position + p[index].transform.forward.normalized * 3);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestRoll(Vector3 rollDir)
    {
        if (!RollCooldownReady) return;
        if (!_kcc.Grounded) return;
        if (NetIsRoll) return;

        NetIsRoll = true;

        NetRollDirection = (rollDir != Vector3.zero ? rollDir : transform.forward).normalized;

        float start = Now;
        NetRollMoveEndTime = start + RollMoveDuration;

        // ✅ 기상 락을 캡핑해서 "너무 오래 멈춤" 방지
        float getup = Mathf.Min(RollGetUpLock, MaxGetUpLockCap);
        NetRollGetUpEndTime = NetRollMoveEndTime + getup;

        NetRollCooldownEndTime = NetRollGetUpEndTime + RollCooldown;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_SetBaseAttack(bool value)
    {
        if (!value)
        {
            NetBaseAttacking = false;
            _attackLoopCts?.Cancel();
            _attackLoopCts?.Dispose();
            _attackLoopCts = null;
            weaponHitBox?.SetActive(false);
        }

        if (Object.HasStateAuthority)
        {
            if (!AttackCooldownReady || _isRolling || !IsAlive || NetIsDeath)
            {
                NetBaseAttacking = false;
                return;
            }
            float cd = GetAttackCooldownSec();
            NetNextAttackTime = Now + cd;
            NetBaseAttacking = true;
            if (Stealth) Stealth = false;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_SetSkill(bool value, int skillSoundIndex, int skillIndex = -1)
    {
        NetUseSkill = value;
        if (HasStateAuthority && NetUseSkill)
            if (Stealth) Stealth = false;

        CharAnimator.SetFloat("SkillIndex", skillIndex);
        CharAnimator.SetTrigger(_animIUseSkill);

        var clip = PickByIndexOrOne_WithFlags("skills", skillSoundIndex, CharacterAudio.skills, CharacterAudio.useRandom_skills, CharacterCommonAudio.skills, CharacterCommonAudio.useRandom_skills);
        PlayOneShot(clip);

        if (Object.HasInputAuthority || CanAIControl)
        {
            RunSkillExecution(skillIndex).Forget();
            WaitNextSkillTerm().Forget();
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_Revive()
    {
        if (reviveCancellationTokenSource == null)
        {
            _isBaseAttacking = false;
            reviveCancellationTokenSource = new();
            Revive(reviveCancellationTokenSource.Token).Forget();
        }
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_DeathRevive()
    {
        NetIsDeath = false;
        RPC_ClearStatusUI(false);
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_GodModeStart(float seconds)
    {
        if (seconds <= 0f) return;
        NetIsGodMode = true;
        NetGodModeEndTime = Now + seconds;
    }

    // 무적 강제 종료
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GodModeStop()
    {
        NetIsGodMode = false;
        NetGodModeEndTime = 0f;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_RefreshLayerSpeeds()
    {
        RefreshLayerSpeeds();
    }
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestAttackTick()
    {
        Server_RequestAttackTick_Common();
    }
    public void DamageToHero(float _damage, UnitObject Attacker, bool _isCritical, bool ApplyHitSkill = true)
    {
        if (IsInvincible() || !IsAlive || Evaid())
            return;

        bool blocked = false;
        bool parried = false;
        BlockType blockType = BlockType.NoBlock;

        if (NetBlock && CharAnimator != null)
        {
            var st = CharAnimator.GetCurrentAnimatorStateInfo(1);
            if (st.IsName("Block"))
            {
                if (Attacker != null)
                {
                    Vector3 toAttacker = Attacker.transform.position - transform.position;
                    toAttacker.y = 0f;

                    if (toAttacker.sqrMagnitude > 0.0001f)
                    {
                        toAttacker.Normalize();
                        float angle = Vector3.Angle(transform.forward, toAttacker);

                        float timeSinceBlock = Now - NetLastBlockStartTime;

                        if (angle <= BlockFrontAngle)
                        {
                            blocked = true;

                            if (timeSinceBlock <= ParryWindow)
                            {
                                parried = true;
                            }
                        }
                    }
                }
                else
                {
                    blocked = true;
                }
            }
        }

        if (ApplyHitSkill)
            _damage = CalculationStatusDamage(Attacker, _damage);

        _damage -= _damage * ReduceDamageRatio;

        if (blocked)
        {
            if (parried)
            {
                blockType = BlockType.Parried;

                _damage = 0;

                ApplyParryAoE();

                if (Object.HasStateAuthority)
                {
                    NetBlock = false;

                    NetNextAttackTime = Now;
                    NetParriedTime = Now;

                    _pendingAttackTokens = 0;
                    _processingAttackTokens = false;

                    RPC_OnBlockSuccess();
                }
            }
            else
            {
                blockType = BlockType.Block;
                _damage *= BlockDamageMultiplier;
            }
        }

        var leftDamage = _damage - Shield;
        Shield = Mathf.Max(0, Shield - _damage);
        Affect.UseShieldAffect(_damage);

        HP -= Mathf.Max(0, leftDamage);
        HP = Mathf.Clamp(HP, 0, MaxHP);

        RPC_ShowDamageText(_damage, _isCritical, Attacker, blockType);

        if (ApplyHitSkill)
            HitPassiveSkill(Attacker, _damage);

        if (HP <= 0)
        {
            RPC_StopAllAttacks();

            DiePassiveSkill();

            if (!ingameManager.CheckEndGame())
            {
                NetIsDeath = true;
                NetBaseAttacking = false;
                if (CanAIControl)
                {
                    _isBaseAttacking = false;
                    reviveCancellationTokenSource = new();
                    ReviveAI(reviveCancellationTokenSource.Token).Forget();
                }
                else
                {
                    RPC_Revive();
                }
            }
            else
            {
                RPC_EndGame(1);
            }
        }

        if (!IsAlive)
        {
            var attacker = Attacker as Player;
            if (attacker != null && attacker.PlayerInfoData.IsRaidBoss)
            {
                attacker.AddExp(GameManager.Instance.HeroExp);
            }
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnBlockSuccess()
    {
        if (CharAnimator != null)
        {
            int hash = Animator.StringToHash("AttackBlock");
            CharAnimator.Play(hash, 1, 0f);
        }

        // 패링 성공 시 공격 때와 같은 쉐이크
        if (Object.HasInputAuthority)
        {
            CameraShake();
        }
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Heal(float _value, bool _canEffect = true)
    {
        Heal(_value, _canEffect);
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RestoreMana(float _value, bool _canEffect = true)
    {
        RestoreMP(_value, _canEffect);
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_UseMana(float _value)
    {
        MP -= _value;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void RPC_Shield(float _value)
    {
        Shield += _value;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ResetSkill()
    {
        NetUseSkill = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_StopAllAttacks()
    {
        _attackAnimCts?.Cancel();
        _attackAnimCts?.Dispose();
        _attackAnimCts = null;

        _attackLoopCts?.Cancel();
        _attackLoopCts?.Dispose();
        _attackLoopCts = null;

        _isBaseAttacking = false;
        NetBaseAttacking = false;
        _isSkillAttacking = false;
        _isReadyToUseSkill = true;
        NetUseSkill = false;
        NetIsChanneling = false;
        NetIsMoving = false;

        weaponHitBox?.SetActive(false);

        Affect?.ClearStatusUI(true);

        var img = ingameManager?.PlayerInfoManager?.CoolImage;
        if (img != null) img.fillAmount = 0f;

        if (CharAnimator != null)
        {
            CharAnimator.ResetTrigger("Skill");
            CharAnimator.SetBool(_animIBaseAttack, false);
            CharAnimator.SetBool(_animIDChanneling, false);
            if (!NetIsDeath)
                CharAnimator.Play("Idle", 1, 0f);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_CancelBaseAttackKeepCooldown()
    {
        if (Object.HasInputAuthority)
            RPC_SetBaseAttack(false);

        _attackAnimCts?.Cancel();
        _attackAnimCts?.Dispose();
        _attackAnimCts = null;
    }
    #endregion

    #region ▶ Monster관련
    //[Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    //public void RPC_RewardBoxCheck(int rewardType, float x, float y, float z)
    //{
    //    if (!Object.HasInputAuthority) return;

    //    Vector3 position = new Vector3(x, y, z);
    //    ingameManager.RewardBoxSpawner.Spawn(rewardType, position);
    //}
    private void ApplyParryAoE()
    {
        if (!Object.HasStateAuthority) return;

        Vector3 origin = transform.position;
        Vector3 dir = transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        dir.Normalize();

        float maxDistance = ParryRadius;
        float sphereRadius = 1f;
        float halfAngle = BlockFrontAngle;

        var hits = Physics.SphereCastAll(
            origin,
            sphereRadius,
            dir,
            maxDistance,
            1 << 20
        );

        if (hits == null || hits.Length == 0)
            return;

        HashSet<Monster> parriedMonsters = new HashSet<Monster>();

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            var monster = hit.collider.GetComponent<Monster>();
            if (monster == null) continue;
            if (monster.IsDying || monster.IsSummon) continue;

            if (!parriedMonsters.Add(monster))
                continue;

            Vector3 toM = monster.transform.position - transform.position;
            toM.y = 0f;
            if (toM.sqrMagnitude <= 0.0001f)
                continue;

            float angle = Vector3.Angle(dir, toM.normalized);
            if (angle > halfAngle)
                continue;

            monster.Parried_Server();
        }
    }
    #endregion

    #region ▶ Player Info
    public bool IsParried()
    {
        if (Now - NetParriedTime <= ParriedTimeDuration && NetParriedAttackCount > 0)
        {
            NetParriedAttackCount--;
            return true;
        }
        return false;
    }

    public void HitEffect()
    {
        CameraShake();
        ingameManager.PlayerInfoManager.ScreenEffect?.FlashRed();
    }
    public void ParriedHitEffect()
    {
        CameraShake();
    }
    public void HitEffectBlock()
    {
        BlockCameraShake();
        ingameManager.PlayerInfoManager.ScreenEffect?.FlashRedWeak();
    }
    private void BlockCameraShake()
    {
        if (!UserData.Instance.IsVibrationOn) return;
        CameraHandle.DOShakePosition(0.1f, 0.15f).OnComplete(() => CameraHandle.transform.localPosition = new Vector3(0, -0.5f, -3));
    }
    private async UniTaskVoid Revive(CancellationToken token)
    {
        // 입력 권한 없으면 로컬 코루틴/딜레이 돌 필요 없음
        if (!HasInputAuthority)
            return;

        EnableSpectateCamera(true);

        float reviveTime = ingameManager.raidStarted ? -10f : ReviveTime;
        ingameManager.PlayerInfoManager.SetRevive(reviveTime);

        if (reviveTime < 0f)
            return;

        try
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(reviveTime), cancellationToken: token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        // 여기까지 왔으면 정상 부활
        if (reviveCancellationTokenSource == null || token.IsCancellationRequested)
            return;

        RPC_DeathRevive();
        RPC_Heal(MaxHP, false);
        RPC_RestoreMana(MaxMP, false);
        RPC_TeleportToRandomSpawnPoint();
        RPC_GodModeStart(3f);

        LocalReviveReset();
    }
    private async UniTaskVoid ReviveAI(CancellationToken token)
    {
        // 호스트만 실행
        if (!CanAIControl)
            return;
        // 레이드 중이면 AI는 부활 대기 없이 종료 처리
        float reviveTime = ingameManager.raidStarted ? -10f : ReviveTime;

        if (reviveTime < 0f)
            return;

        try
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(reviveTime), cancellationToken: token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        // 여기까지 왔으면 정상 부활
        if (reviveCancellationTokenSource == null || token.IsCancellationRequested)
            return;

        // 부활 처리
        NetIsDeath = false;
        Affect?.ClearStatusUI(false);
        RPC_Heal(MaxHP, false);
        RPC_RestoreMana(MaxMP, false);
        TeleportToRandomSpawnPoint();
        NetIsGodMode = true;
        NetGodModeEndTime = Now + 3;

        // AI도 로컬 리셋은 해주는게 안전
        LocalReviveReset();
    }
    public void Heal(float _value, bool _canEffect = true)
    {
        if (BlockHeal)
            return;

        if (_canEffect)
        {
            if (!Mathf.Approximately(HP, MaxHP))
                HpRegenPlayTime = 1;
        }
        else
        {
            HpRegenPlayTime = 0;
        }
        float maxHP = HP + _value;
        HP = Mathf.Min(MaxHP, maxHP);
    }
    public void RestoreMP(float _value, bool _canEffect = true)
    {
        if (_canEffect)
        {
            if (!Mathf.Approximately(MP, MaxMP))
                MpRegenPlayTime = 1;
        }
        else
        {
            MpRegenPlayTime = 0;
        }
        float maxMP = MP + _value;
        MP = Mathf.Min(maxMP, maxMP);
    }

    public void AddCoin(float amount)
    {
        Debug.Log(transform.name + "Coin Get!" + amount);
        NetCoin += (int)amount;
    }
    public void LocalReviveReset()
    {
        if (reviveCancellationTokenSource != null)
        {
            if (ingameManager.MyPlayer == this)
                ingameManager.PlayerInfoManager.SetRevive(ReviveTime, false);
            reviveCancellationTokenSource.Cancel();
            reviveCancellationTokenSource.Dispose();
            reviveCancellationTokenSource = null;
        }
        if (!PlayerInfoData.IsAI)
        {
            EnableSpectateCamera(false);
            ingameManager.PlayerInfoManager.ResetRevive();
        }
        _isBaseAttacking = false;
        Stamina = HeroInfo.Stamina;
    }
    private void EnableSpectateCamera(bool enable)
    {
        if (Object.HasInputAuthority)
        {
            if (enable)
            {
                if (SpectateCamera.Instance != null)
                {
                    SpectateCamera.Instance.Activate(transform);
                }
            }
            else
            {
                if (SpectateCamera.Instance != null)
                {
                    SpectateCamera.Instance.Deactivate();
                }
            }
        }
    }
    public void PurchaseItem(Data_Item _item, int _count = 1)
    {
        _ownedItems[_item] = _ownedItems.ContainsKey(_item) ? _ownedItems[_item] + _count : _count;

        if (ingameManager.Guide.NeedCheck && ingameManager.Guide.NeedItemGuide)
            ingameManager.Guide.InvenOpenGuide.Flag = true;
    }
    public void UseItem(int index)
    {
        if (!Object.HasInputAuthority || !IsAlive) return;

        var pim = ingameManager?.PlayerInfoManager;
        if (pim == null || pim.UseItemSlot == null) return;
        if (index < 0 || index >= pim.UseItemSlot.Length) return;

        var slot = pim.UseItemSlot[index];
        if (slot == null) return;

        var item = slot.CurrentItem;
        if (item == null) return;

        UseItem(item);
    }

    // 인벤토리에서 바로 쓸 때 호출할 오버로드
    public void UseItem(Data_Item item)
    {
        if (!Object.HasInputAuthority || !IsAlive) return;
        if (item == null) return;

        var pim = ingameManager?.PlayerInfoManager;
        if (pim == null) return;

        // 쿨타임 체크
        if (pim.ItemCooldowns.IsOnCooldown(item.Index)) return;

        // 가진 아이템 개수 확인
        if (!OwnedItems.TryGetValue(item, out var count) || count <= 0)
        {
            ingameManager?.InventoryManager?.OnItemChanged(item, 0);
            return;
        }

        bool didHP = false;
        bool didMP = false;

        if (item.AbilitiesIndex != null)
        {
            for (int i = 0; i < item.AbilitiesIndex.Length; i++)
            {
                int abil = item.AbilitiesIndex[i];
                if (abil == 0 && !didHP)
                {
                    RPC_Heal(item.value_1);
                    didHP = true;
                }
                if (abil == 1 && !didMP)
                {
                    RPC_RestoreMana(item.value_1);
                    didMP = true;
                }
            }
        }

        // 개수 차감
        count -= 1;
        if (count <= 0)
            OwnedItems.Remove(item);
        else
            OwnedItems[item] = count;

        // 인벤토리 갱신
        ingameManager?.InventoryManager?.OnItemChanged(item, count);

        // 쿨타임 시작
        if (item.CoolTime > 0f)
            pim.ItemCooldowns.Start(item.Index, item.CoolTime);
    }
    #endregion

    #region 파티플레이

    public HashSet<PlayerFollowController> partyPlayers = new HashSet<PlayerFollowController>();
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_UpdateParty(NetworkId[] partyMembers)
    {
        List<Player> newJoiners = new List<Player>();

        foreach (var playerRef in partyMembers)
        {
            if (playerRef == Object.Id)
                continue;

            if (InGameManager.Instance.Players.TryGetValue(playerRef, out var player))
            {
                if (!partyPlayers.Contains(player.FollowController))
                {
                    partyPlayers.Add(player.FollowController);
                    ingameManager.PlayerInfoManager.AddPartyPlayer(player);
                    newJoiners.Add(player);
                    ingameManager.MinimapCam.CreatePlayerIcon(player.transform, isMine: false);
                }
            }
        }

        if (newJoiners.Count > 0)
        {
            ingameManager.PlayerInfoManager.ShowPartyJoinText(newJoiners);
        }
    }
    #endregion

    #region 초기화
    public void Player_HostMiGration()
    {
        CameraPivot.localPosition = PlayerCameraPos;
        ingameManager.MinimapCam.CreatePlayerIcon(transform, isMine: true);
        ingameManager.MinimapCam.SetFollowTarget(transform);

        Stamina = NetStamina;

        if (ingameManager.ShopManager != null && ingameManager.ShopManager.gameObject.activeSelf)
            ingameManager.ShopManager.WeaponSetting();
    }
    public void Player_AllHostMiGration()
    {
        Player_AssignAnimationIDs();
        HeroInfo = DataManager.Instance.HeroData[PlayerInfoData.CharacterIndex];
        _kcc.gravity = -500;
        if (ingameManager == null) ingameManager = InGameManager.Instance;

        if (HasInputAuthority)
            ingameManager.PlayerInfoManager.Init(this);

        if (WeaponIndex != -1)
        {
            WeaponInfo = DataManager.Instance.WeaponData[WeaponIndex];
            GetNextWeapon();
            WalkSpeed = HeroInfo.Speed;
            if (ingameManager.PlayerFollowKeyValue.ContainsKey(Object.Id))
                FollowController = ingameManager.PlayerFollowKeyValue[Object.Id];
            if (PlayerItemComponent != null)
                weaponHitBox = PlayerItemComponent.WeaponPos.GetComponentInChildren<WeaponHitbox>();
            PlayerInfoData.PlayerHP.SetActive(ingameManager.ImRaid);
            PlayerInfoData.PlayerHP.transform.localPosition = Vector3.zero;
            PlayerInfoData.DebuffCanvas.transform.localPosition = Vector3.zero;
            if (ingameManager.ImRaid)
            {
                PlayerInfoData.DebuffCanvas.transform.localPosition = new Vector3(0, PlayerInfoData.PlayerHP.transform.localPosition.y + 0.4f, 0);
            }

            RefreshLayerSpeeds();
        }
    }
    #endregion

    #region AI
    [Networked]
    public bool Net_AIInit { get; set; } = false;
    public void AI_PlayerInit()
    {
        if (!CanAIControl) return;

        if (_aiWeaponUpgradeToken != null)
        {
            _aiWeaponUpgradeToken.Cancel();
            _aiWeaponUpgradeToken.Dispose();
            _aiWeaponUpgradeToken = null;
        }

        if (_aiStatUpgradeToken != null)
        {
            _aiStatUpgradeToken.Cancel();
            _aiStatUpgradeToken.Dispose();
            _aiStatUpgradeToken = null;
        }

        _aiWeaponUpgradeToken = new CancellationTokenSource();
        _aiStatUpgradeToken = new CancellationTokenSource();

        AI_UpgradeNexeWeapon_Coroutine(_aiWeaponUpgradeToken.Token);
        AI_UpgradeRandomStat_Coroutine(_aiStatUpgradeToken.Token);
        if (!Net_AIInit && CanAIControl)
            AI_GetPassiveSkill();
    }

    public void AI_Dispose()
    {
        if (_aiWeaponUpgradeToken != null)
        {
            _aiWeaponUpgradeToken.Cancel();
            _aiWeaponUpgradeToken.Dispose();
            _aiWeaponUpgradeToken = null;
        }

        if (_aiStatUpgradeToken != null)
        {
            _aiStatUpgradeToken.Cancel();
            _aiStatUpgradeToken.Dispose();
            _aiStatUpgradeToken = null;
        }
    }

    #region AI 이동

    // 네비메시 에이전트를 길찾기 전용으로 세팅
    void AI_EnsureAgent()
    {
        if (_aiAgent == null) return;

        // 에이전트 활성화
        _aiAgent.enabled = true;

        // 위치와 회전은 직접 제어
        _aiAgent.updatePosition = false;
        _aiAgent.updateRotation = false;

        // 이동 관련 기본 수치 설정
        _aiAgent.speed = Speed;
        _aiAgent.angularSpeed = 720f;
        _aiAgent.acceleration = 50f;

        // 실제 정지는 직접 제어하므로 최소값
        _aiAgent.stoppingDistance = 0.1f;
        _aiAgent.autoBraking = true;
    }

    // 배회용 랜덤 목적지 선택
    void AI_PickNewTarget()
    {
        // 기존 타겟 초기화
        _aiHasTarget = false;

        // 현재 위치 기준 랜덤 원형 좌표 생성
        Vector3 origin = transform.position;
        Vector2 r = UnityEngine.Random.insideUnitCircle * AI_WANDER_RADIUS;
        Vector3 candidate = origin + new Vector3(r.x, 0f, r.y);

        // 네비메시 위 유효한 위치인지 검사
        if (NavMesh.SamplePosition(candidate, out var hit, AI_WANDER_RADIUS, NavMesh.AllAreas))
        {
            _aiTargetPos = hit.position;
            _aiHasTarget = true;
        }

        // 다음 타겟 선택 시간 예약
        _aiNextPickTime = Time.time + UnityEngine.Random.Range(AI_PICK_INTERVAL_MIN, AI_PICK_INTERVAL_MAX);
    }

    // 현재 타겟 위치로 경로 재계산
    void AI_Repath()
    {
        if (!_aiHasTarget) return;
        if (_aiAgent == null) return;

        // 에이전트 위치 강제 동기화
        _aiAgent.Warp(transform.position);

        // 목적지 설정
        _aiAgent.SetDestination(_aiTargetPos);

        // 다음 리패스 시간 설정
        _aiNextRepathTime = Time.time + AI_REPATH_INTERVAL;
    }

    // 네비메시 경로 결과를 실제 이동으로 변환
    void AI_ApplyMoveFromPath()
    {
        if (_aiAgent == null) return;

        // 네비메시가 계산한 이동 벡터
        Vector3 desired = _aiAgent.desiredVelocity;
        desired.y = 0f;

        if (desired.sqrMagnitude > 0.0001f)
        {
            // 이동 방향 계산
            Vector3 dir = desired.normalized;

            // 이동 방향으로 천천히 회전
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Runner.DeltaTime * 10f);

            // 실제 이동 적용
            float speed = (Speed * 0.6f);
            _kcc.Move(dir * speed);

            // 이동 상태 동기화
            NetIsMoving = true;
            NetSpeed = 0f;
        }
        else
        {
            // 이동할 필요 없으면 정지
            _kcc.Move(Vector3.zero);
            NetIsMoving = false;
            NetSpeed = 0f;
        }
    }

    Vector3 AI_GetNavDesiredDirection()
    {
        if (_aiAgent == null) return Vector3.zero;

        Vector3 desired = _aiAgent.desiredVelocity;
        desired.y = 0f;

        if (desired.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return desired.normalized;
    }

    #endregion

    #region AI 입력

    // AI 입력 생성
    bool AI_BuildInput(out NetworkInputData input)
    {
        input = default;

        if (!CanAIControl) return false;

        Vector3 dirWorld = AI_GetMoveDirectionWorld();

        if (dirWorld.sqrMagnitude < 0.0001f)
        {
            input.moveDir = Vector2.zero;
            input.IsRun = false;
            input.lookDelta = Vector2.zero;

            if (_aiIsChasing && AI_HasChaseTarget())
            {
                Vector3 face = AI_GetChaseTargetPosition() - transform.position;
                face.y = 0f;
                if (face.sqrMagnitude > 0.0001f)
                    input.lookDelta = AI_ComputeLookDelta(face.normalized);
            }

            return true;
        }

        dirWorld.y = 0f;
        dirWorld.Normalize();

        // 기존 플레이어 입력 방식 그대로
        Vector2 md;
        md.x = Vector3.Dot(transform.right, dirWorld);
        md.y = Vector3.Dot(transform.forward, dirWorld);
        md = Vector2.ClampMagnitude(md, 1f);

        input.moveDir = md;
        input.IsRun = _aiIsChasing;

        input.lookDelta = AI_ComputeLookDelta(dirWorld);

        return true;
    }

    // 원하는 방향을 lookDelta 값으로 변환
    Vector2 AI_ComputeLookDelta(Vector3 desiredForward)
    {
        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude < 0.0001f) return Vector2.zero;
        desiredForward.Normalize();

        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
        f.Normalize();

        // 현재 방향과 목표 방향의 각도 계산
        float signedAngle = Vector3.SignedAngle(f, desiredForward, Vector3.up);

        float dt = Runner.DeltaTime;
        float denom = NetRotationSpeed * dt;
        if (denom <= 0.000001f) return Vector2.zero;

        // 회전 입력값으로 변환
        float lookX = signedAngle / denom;

        // 회전 속도 제한
        float maxLook = 10f;
        lookX = Mathf.Clamp(lookX, -maxLook, maxLook);

        return new Vector2(lookX, 0f);
    }

    #endregion

    #region AI 이동 방향

    Vector3 AI_GetMoveDirectionWorld()
    {
        if (ingameManager != null && ingameManager.raidStarted && ingameManager.RaidPlayer != null)
        {
            Player _raidPlayer = ingameManager.RaidPlayer;

            bool _forced = AI_TryForceRaidPlayerChase();

            if (_forced)
            {
                // 원거리 무기인데 타겟이 높으면 이동은 배회로 하고 공격만 노림
                if (AI_IsHighTarget(_raidPlayer) && AI_IsRangedWeapon())
                {
                    return AI_GetWanderMoveDir();
                }

                return AI_GetChaseMoveDir();
            }
        }

        AI_TryAcquirePriorityTarget();
        AI_UpdateChaseTarget();

        if (_aiIsChasing && AI_HasChaseTarget())
            return AI_GetChaseMoveDir();

        return AI_GetWanderMoveDir();
    }


    Vector3 AI_GetChaseMoveDir()
    {
        float _stopDist = AI_GetStopDistanceByWeapon();

        Vector3 _targetWorldPos = AI_GetChaseTargetPosition();
        Vector3 _flat = _targetWorldPos - transform.position;
        _flat.y = 0f;

        if (_flat.sqrMagnitude <= _stopDist * _stopDist)
            return Vector3.zero;

        AI_EnsureAgent();

        // 레이드 타겟이면 NavMesh 투영점으로 이동
        if (_aiChaseTargetRaidBoss != null)
        {
            Vector3 _navPos;
            bool _navOk = AI_TryGetNavPos(_targetWorldPos, AI_RAID_NAV_SAMPLE_RADIUS, out _navPos);

            if (_navOk && AI_CanPathTo(_navPos))
            {
                _aiTargetPos = _navPos;
                _aiLastValidRaidNavPos = _navPos;
                _aiHasLastValidRaidNavPos = true;
                _aiHasTarget = true;
            }
            else
            {
                if (_aiHasLastValidRaidNavPos)
                {
                    _aiTargetPos = _aiLastValidRaidNavPos;
                    _aiHasTarget = true;
                }
                else
                {
                    _aiHasTarget = false;
                    return Vector3.zero;
                }
            }
        }
        else
        {
            // 몬스터는 기존대로
            _aiTargetPos = _targetWorldPos;
            _aiHasTarget = true;
        }

        if (Time.time >= _aiNextRepathTime)
            AI_Repath();

        return AI_GetNavDesiredDirection();
    }

    Vector3 AI_GetWanderMoveDir()
    {
        if (!_aiHasTarget || Time.time >= _aiNextPickTime)
            AI_PickNewTarget();

        Vector3 _to = _aiTargetPos - transform.position;
        _to.y = 0f;

        if (_to.sqrMagnitude <= AI_STOP_DIST * AI_STOP_DIST)
        {
            _aiHasTarget = false;
            return Vector3.zero;
        }

        AI_EnsureAgent();
        if (Time.time >= _aiNextRepathTime)
            AI_Repath();

        Vector3 _navDir = AI_GetNavDesiredDirection();

        if (_navDir.sqrMagnitude > 0.0001f)
            AI_TryAcquireMonsterByForwardRay(_navDir);

        return _navDir;
    }

    bool AI_TryForceRaidPlayerChase()
    {
        if (!CanAIControl) return false;
        if (ingameManager == null) return false;

        Player _raidPlayer = ingameManager.RaidPlayer;
        if (_raidPlayer == null) return false;

        if (_raidPlayer.Object != null && _raidPlayer.Object.Id == Object.Id) return false;
        if (!_raidPlayer.IsAlive || _raidPlayer.NetIsDeath) return false;

        Vector3 _origin = transform.position + Vector3.up * 1.0f;
        Vector3 _targetPos = _raidPlayer.transform.position + Vector3.up * 0.8f;
        float _dist = Vector3.Distance(_origin, _targetPos);

        if (AI_HasBreakableBetween(_origin, _targetPos, _dist))
            return false;

        // 근접인데 공중이면 추격 포기
        if (AI_IsHighTarget(_raidPlayer) && !AI_IsRangedWeapon())
        {
            _aiIsChasing = false;
            _aiChaseTargetRaidBoss = null;
            _aiChaseTargetMonster = null;
            return false;
        }

        _aiChaseTargetRaidBoss = _raidPlayer;
        _aiChaseTargetMonster = null;

        _aiIsChasing = true;
        _aiHasTarget = true;

        // 이동 목적지는 NavMesh 투영점 사용
        Vector3 _navPos;
        bool _navOk = AI_TryGetNavPos(_raidPlayer.transform.position, AI_RAID_NAV_SAMPLE_RADIUS, out _navPos);

        if (_navOk && AI_CanPathTo(_navPos))
        {
            _aiTargetPos = _navPos;
            _aiLastValidRaidNavPos = _navPos;
            _aiHasLastValidRaidNavPos = true;
        }
        else
        {
            if (_aiHasLastValidRaidNavPos)
            {
                _aiTargetPos = _aiLastValidRaidNavPos;
            }
            else
            {
                // 한번도 성공한 투영점이 없으면 배회로 돌림
                _aiIsChasing = false;
                _aiChaseTargetRaidBoss = null;
                _aiHasTarget = false;
                return false;
            }
        }

        return true;
    }

    #endregion

    #region AI 감지

    // 전방으로 몬스터 감지 시 추격 시작
    bool AI_TryAcquireMonsterByForwardRay(Vector3 forwardDir)
    {
        if (ingameManager != null && ingameManager.raidStarted && ingameManager.RaidPlayer != null)
            return false;

        if (Time.time < _aiNextScanTime) return false;
        _aiNextScanTime = Time.time + AI_SCAN_INTERVAL;

        if (_aiChaseTargetRaidBoss != null) return false;

        if (forwardDir.sqrMagnitude < 0.0001f)
            forwardDir = transform.forward;

        forwardDir.y = 0f;
        if (forwardDir.sqrMagnitude < 0.0001f) return false;
        forwardDir.Normalize();

        Vector3 origin = transform.position + Vector3.up * 1.0f;

        float scanRadius = 0.6f;
        float scanMaxDist = AI_PRIORITY_RADIUS;

        if (!Physics.SphereCast(origin, scanRadius, forwardDir, out var hit, scanMaxDist, ~0, QueryTriggerInteraction.Ignore))
            return false;

        var monster = hit.collider != null ? hit.collider.GetComponentInParent<Monster>() : null;
        if (monster == null) return false;
        if (monster.IsDying || monster.IsSummon) return false;

        Vector3 targetPos = monster.transform.position + Vector3.up * 0.8f;
        float distToMonster = Vector3.Distance(origin, targetPos);

        if (AI_HasBreakableBetween(origin, targetPos, distToMonster))
            return false;

        _aiChaseTargetMonster = monster;
        _aiChaseTargetRaidBoss = null;

        _aiIsChasing = true;
        _aiHasTarget = true;
        _aiTargetPos = monster.transform.position;

        return true;
    }

    // 몬스터까지 경로 중 부서지는 오브젝트가 있는지 검사
    bool AI_HasBreakableBetween(Vector3 origin, Vector3 targetPos, float maxDist)
    {
        Vector3 dir = targetPos - origin;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();

        var hits = Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        // 거리순 정렬
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i].collider;
            if (c == null) continue;

            // 먼저 부서지는 오브젝트면 시야 차단
            if (c.CompareTag("Breakable"))
                return true;

            // 몬스터가 먼저면 문제 없음
            var m = c.GetComponentInParent<Monster>();
            if (m != null)
                return false;
        }

        return false;
    }

    bool AI_TryGetNavPos(Vector3 worldPos, float radius, out Vector3 navPos)
    {
        navPos = worldPos;

        if (NavMesh.SamplePosition(worldPos, out var _hit, radius, NavMesh.AllAreas))
        {
            navPos = _hit.position;
            return true;
        }

        return false;
    }

    bool AI_CanPathTo(Vector3 navPos)
    {
        if (_aiAgent == null) return false;

        var _path = new NavMeshPath();
        bool _ok = NavMesh.CalculatePath(transform.position, navPos, NavMesh.AllAreas, _path);
        if (!_ok) return false;

        if (_path.status != NavMeshPathStatus.PathComplete)
            return false;

        if (_path.corners == null || _path.corners.Length == 0)
            return false;

        return true;
    }

    #endregion

    #region AI 추격

    bool AI_IsHighTarget(Player target)
    {
        if (target == null) return false;

        float _dy = target.transform.position.y - transform.position.y;
        return _dy >= AI_HIGH_TARGET_Y;
    }

    bool AI_IsRangedWeapon()
    {
        return _weaponAttackType != 0;
    }

    bool AI_ShouldRangedAttackHighTarget(Player target)
    {
        if (!CanAIControl) return false;
        if (target == null) return false;

        if (NetIsDeath || !IsAlive) return false;
        if (NetIsRoll || NetBlock || NetUseSkill || NetIsChanneling) return false;

        if (!AI_IsRangedWeapon()) return false;
        if (!AI_IsHighTarget(target)) return false;

        Vector3 _myPos = transform.position;
        Vector3 _tPos = target.transform.position;

        Vector3 _flat = _tPos - _myPos;
        _flat.y = 0f;

        float _stopDist = AI_STOP_RANGED;
        float _allow = _stopDist + AI_ATTACK_EPS;

        if (_flat.sqrMagnitude > _allow * _allow) return false;

        Vector3 _origin = _myPos + Vector3.up * 1.0f;
        Vector3 _targetPos = _tPos + Vector3.up * 0.8f;
        float _dist = Vector3.Distance(_origin, _targetPos);

        if (AI_HasBreakableBetween(_origin, _targetPos, _dist)) return false;

        return true;
    }

    // 추격 대상 유지 여부 갱신
    void AI_UpdateChaseTarget()
    {
        if (!_aiIsChasing) return;

        if (!AI_HasChaseTarget() || !AI_IsChaseTargetValid())
        {
            AI_ClearChaseTarget();
            return;
        }

        Vector3 myPos = transform.position;
        Vector3 tPos = AI_GetChaseTargetPosition();

        Vector3 flat = tPos - myPos;
        flat.y = 0f;

        if (flat.sqrMagnitude > AI_CHASE_FORGET_DIST * AI_CHASE_FORGET_DIST)
        {
            AI_ClearChaseTarget();
            return;
        }

        Vector3 origin = myPos + Vector3.up * 1.0f;
        Vector3 targetPos = tPos + Vector3.up * 0.8f;
        float dist = Vector3.Distance(origin, targetPos);

        if (AI_HasBreakableBetween(origin, targetPos, dist))
        {
            AI_ClearChaseTarget();
            return;
        }

        _aiTargetPos = tPos;
        _aiHasTarget = true;

        if (_aiAgent != null)
            _aiAgent.stoppingDistance = AI_GetStopDistanceByWeapon();
    }

    bool AI_HasChaseTarget()
    {
        if (_aiChaseTargetRaidBoss != null) return true;
        if (_aiChaseTargetMonster != null) return true;
        return false;
    }

    Vector3 AI_GetChaseTargetPosition()
    {
        if (_aiChaseTargetRaidBoss != null) return _aiChaseTargetRaidBoss.transform.position;
        if (_aiChaseTargetMonster != null) return _aiChaseTargetMonster.transform.position;
        return transform.position;
    }

    bool AI_IsChaseTargetValid()
    {
        if (_aiChaseTargetRaidBoss != null)
        {
            if (!_aiChaseTargetRaidBoss.IsAlive) return false;
            if (_aiChaseTargetRaidBoss.NetIsDeath) return false;

            if (ingameManager != null && ingameManager.RaidPlayer != null)
            {
                if (_aiChaseTargetRaidBoss == ingameManager.RaidPlayer)
                    return true;
            }

            if (_aiChaseTargetRaidBoss.PlayerInfoData == null) return false;
            if (!_aiChaseTargetRaidBoss.PlayerInfoData.IsRaidBoss) return false;

            return true;
        }

        if (_aiChaseTargetMonster != null)
        {
            if (_aiChaseTargetMonster.IsDying) return false;
            if (_aiChaseTargetMonster.IsSummon) return false;
            return true;
        }

        return false;
    }

    void AI_ClearChaseTarget()
    {
        _aiIsChasing = false;
        _aiChaseTargetMonster = null;
        _aiChaseTargetRaidBoss = null;
        _aiHasTarget = false;
    }

    bool AI_TryAcquirePriorityTarget()
    {
        if (ingameManager != null && ingameManager.raidStarted && ingameManager.RaidPlayer != null)
            return false;

        if (Time.time < _aiNextScanTime) return false;
        _aiNextScanTime = Time.time + AI_SCAN_INTERVAL;

        Vector3 origin = transform.position + Vector3.up * 1.0f;

        Collider[] cols = Physics.OverlapSphere(origin, AI_PRIORITY_RADIUS, ~0, QueryTriggerInteraction.Ignore);
        if (cols == null || cols.Length == 0) return false;

        Player bestRaidBoss = null;
        float bestRaidBossDist = float.MaxValue;

        Monster bestMonster = null;
        float bestMonsterDist = float.MaxValue;

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;

            if (c.CompareTag("Dragon"))
            {
                var p = c.GetComponentInParent<Player>();
                if (p == null) continue;
                if (p.Object.Id == Object.Id) continue;
                if (p.PlayerInfoData == null) continue;
                if (!p.PlayerInfoData.IsRaidBoss) continue;
                if (!p.IsAlive || p.NetIsDeath) continue;

                Vector3 tp = p.transform.position + Vector3.up * 0.8f;
                float dist = Vector3.Distance(origin, tp);
                if (AI_HasBreakableBetween(origin, tp, dist)) continue;

                if (dist < bestRaidBossDist)
                {
                    bestRaidBossDist = dist;
                    bestRaidBoss = p;
                }

                continue;
            }

            if (c.CompareTag("Enemy"))
            {
                var m = c.GetComponentInParent<Monster>();
                if (m == null) continue;
                if (m.IsDying || m.IsSummon) continue;

                Vector3 tp = m.transform.position + Vector3.up * 0.8f;
                float dist = Vector3.Distance(origin, tp);
                if (AI_HasBreakableBetween(origin, tp, dist)) continue;

                if (dist < bestMonsterDist)
                {
                    bestMonsterDist = dist;
                    bestMonster = m;
                }

                continue;
            }
        }

        if (bestRaidBoss != null)
        {
            _aiChaseTargetRaidBoss = bestRaidBoss;
            _aiChaseTargetMonster = null;

            _aiIsChasing = true;
            _aiHasTarget = true;
            _aiTargetPos = bestRaidBoss.transform.position;
            return true;
        }

        if (bestMonster != null)
        {
            _aiChaseTargetMonster = bestMonster;
            _aiChaseTargetRaidBoss = null;

            _aiIsChasing = true;
            _aiHasTarget = true;
            _aiTargetPos = bestMonster.transform.position;
            return true;
        }

        return false;
    }

    #endregion

    #region AI 공격

    // 무기 타입별 정지 거리 반환
    float AI_GetStopDistanceByWeapon()
    {
        // 근접 무기면 가까이 원거리 무기면 멀리
        return (_weaponAttackType == 0) ? AI_STOP_MELEE : AI_STOP_RANGED;
    }

    // 공격 거리 체크
    bool AI_ShouldAttackAtStopDistance()
    {
        if (!CanAIControl) return false;

        if (!_aiIsChasing) return false;
        if (!AI_HasChaseTarget()) return false;
        if (!AI_IsChaseTargetValid()) return false;

        if (NetIsDeath || !IsAlive) return false;
        if (NetIsRoll || NetBlock || NetUseSkill || NetIsChanneling) return false;

        float stopDist = AI_GetStopDistanceByWeapon();

        Vector3 myPos = transform.position;
        Vector3 tPos = AI_GetChaseTargetPosition();

        Vector3 flat = tPos - myPos;
        flat.y = 0f;

        float allow = stopDist + AI_ATTACK_EPS;
        if (flat.sqrMagnitude > allow * allow) return false;

        Vector3 origin = myPos + Vector3.up * 1.0f;
        Vector3 targetPos = tPos + Vector3.up * 0.8f;
        float dist = Vector3.Distance(origin, targetPos);

        if (AI_HasBreakableBetween(origin, targetPos, dist)) return false;

        return true;
    }

    // AI 전투 흐름 정리
    void AI_ServerUpdateCombat(ref NetworkInputData input)
    {
        if (!CanAIControl) return;

        if (NetIsDeath || !IsAlive) return;
        if (NetIsRoll || NetBlock || NetUseSkill || NetIsChanneling) return;

        if (BlockBaseAttack || Stun) return;

        // 레이드 타겟이 높으면 원거리만 공격
        if (ingameManager != null && ingameManager.raidStarted && ingameManager.RaidPlayer != null)
        {
            Player _raidPlayer = ingameManager.RaidPlayer;

            if (AI_ShouldRangedAttackHighTarget(_raidPlayer))
            {
                input.moveDir = Vector2.zero;
                input.IsRun = false;

                Vector3 _face = _raidPlayer.transform.position - transform.position;
                _face.y = 0f;
                if (_face.sqrMagnitude > 0.0001f)
                    input.lookDelta = AI_ComputeLookDelta(_face.normalized);

                if (AI_TryUseRegisteredSkill())
                {
                    _aiWasAttacking = false;
                    return;
                }

                Server_RequestAttackTick_Common();
                _aiWasAttacking = true;
                return;
            }
        }

        bool _canAttack = AI_ShouldAttackAtStopDistance();

        if (_canAttack)
        {
            input.moveDir = Vector2.zero;
            input.IsRun = false;

            if (AI_HasChaseTarget())
            {
                Vector3 _face = AI_GetChaseTargetPosition() - transform.position;
                _face.y = 0f;
                if (_face.sqrMagnitude > 0.0001f)
                    input.lookDelta = AI_ComputeLookDelta(_face.normalized);
            }

            if (AI_TryUseRegisteredSkill())
            {
                _aiWasAttacking = false;
                return;
            }

            Server_RequestAttackTick_Common();
            _aiWasAttacking = true;
            return;
        }

        if (_aiWasAttacking && (!AI_HasChaseTarget() || !AI_IsChaseTargetValid()))
        {
            _aiWasAttacking = false;
            RPC_StopAllAttacks();
        }

        if (!AI_HasChaseTarget() && NetBaseAttacking)
        {
            NetBaseAttacking = false;
        }
    }

    #endregion

    #region AI 스킬
    void AI_GetPassiveSkill()
    {
        Net_AIInit = true;
        return;
        for (int i = 0; i < _aiPassiveSkillIndex.Length; i++)
        {
            int skillIndex = _aiPassiveSkillIndex[i];
            RPC_AIAddPassiveSkill(_aiPassiveSkillIndex[i]);
        }
    }

    bool AI_TryUseRegisteredSkill()
    {
        if (!CanAIControl) return false;

        if (NetIsDeath || !IsAlive) return false;
        if (_isRolling || NetBlock) return false;

        if (_isBaseAttacking) return false;
        if (_isSkillAttacking) return false;
        if (!_isReadyToUseSkill) return false;

        if (Now < _aiNextSkillTryTime) return false;

        float r = UnityEngine.Random.value;
        if (r > AI_SKILL_CHANCE)
            return false;

        int skillIndex = AI_PickRegisteredSkillIndex();
        if (skillIndex < 0)
        {
            AI_ReserveNextSkillTry();
            return false;
        }

        if (!ingameManager.SkillManager.TryUseSkillCost(this, skillIndex))
        {
            AI_ReserveNextSkillTry();
            return false;
        }

        if (NetBaseAttacking)
            RPC_CancelBaseAttackKeepCooldown();

        _isSkillAttacking = true;
        _isReadyToUseSkill = false;

        ingameManager.SkillManager.ActiveSkillFunc(this, skillIndex, transform.forward);

        int animationKey = 0;
        RPC_SetSkill(true, skillIndex, animationKey);

        AI_ReserveNextSkillTry();
        return true;
    }

    int AI_PickRegisteredSkillIndex()
    {
        if (_aiSkillIndex == null || _aiSkillIndex.Length == 0)
            return -1;

        int start = UnityEngine.Random.Range(0, _aiSkillIndex.Length);
        return _aiSkillIndex[start];
    }

    void AI_ReserveNextSkillTry()
    {
        float cd = UnityEngine.Random.Range(AI_SKILL_COOLDOWN_MIN, AI_SKILL_COOLDOWN_MAX);
        _aiNextSkillTryTime = Now + cd;
    }

    #endregion

    #region AI 능력치

    private CancellationTokenSource _aiWeaponUpgradeToken;
    private CancellationTokenSource _aiStatUpgradeToken;

    public void UpgradeNexeWeapon()
    {
        if (nextWeaponData == null) return;

        if (NetCoin >= nextWeaponData.Price)
        {
            NetCoin -= nextWeaponData.Price;
            RPC_OnWeaponChanged(nextWeaponData.Index);
        }
    }

    public async void AI_UpgradeNexeWeapon_Coroutine(CancellationToken token)
    {
        while (true)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(5f), cancellationToken: token);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }

            if (!CanAIControl) break;

            UpgradeNexeWeapon();
        }
    }

    public async void AI_UpgradeRandomStat_Coroutine(CancellationToken token)
    {
        while (true)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(4f), cancellationToken: token);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }

            if (!CanAIControl) break;

            AI_TryUpgradeRandomStatFromData();
        }
    }

    void AI_TryUpgradeRandomStatFromData()
    {
        if (!CanAIControl) return;
        if (NetIsDeath || !IsAlive) return;
        if (NetStatCount <= 0) return;

        Data_Stat statData = AI_PickStatByOrder();
        if (statData == null) return;

        int index = statData.ValueType;
        int affectType = ingameManager.ShowItemManager.AffectTypeFromIndex(index);
        if (affectType == 0) return;

        float plusValue = statData.Value_1;

        if (affectType == 10 && CriticalPercent >= 100f)
            return;

        RPC_GetStatCoin(-1);
        RPC_AddAffectValue(affectType, plusValue);
    }
    Data_Stat AI_PickStatByOrder()
    {
        if (DataManager.Instance == null) return null;
        if (DataManager.Instance.StatData == null) return null;

        int _weaponType = _weaponAttackType;
        if (_weaponType < 0 || _weaponType >= STAT_ORDER_BY_WEAPON.Length)
            return null;

        int[] _order = STAT_ORDER_BY_WEAPON[_weaponType];
        if (_order == null || _order.Length == 0)
            return null;

        int _valueType = _order[_aiStatOrderIndex % _order.Length];
        _aiStatOrderIndex++;

        foreach (var _kv in DataManager.Instance.StatData)
        {
            if (_kv.Value.ValueType == _valueType)
                return _kv.Value;
        }

        return null;
    }
    #endregion

    #endregion

}