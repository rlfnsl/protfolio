using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
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
    private const int GOLD_REWARD_ON_KILL = 300;
    private const float EXP_REWARD_RATE_ON_KILL = 0.5f;
    private const float ASSIST_REWARD_RATE = 0.2f;

    private const float ASSIST_WINDOW_SECONDS = 10f;
    private readonly Dictionary<NetworkId, float> hitTimeByPlayers = new();
    private NetworkId lastHitByPlayerId;
    private float lastHitTime;
    private bool hasLastHitByPlayer = false;
    private static readonly Collider[] _tmpCols = new Collider[32];

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
    private float ParryRadius = 20f;   // 패링 범위 반경
    [SerializeField]
    private float ParryWindow = 0.5f; // 기존에 쓰고 있던 패링 유효 시간이라면 그대로 유지

    private bool _prevBlockPressed;

    private CancellationTokenSource parryStunCts;

    [SerializeField] private float parryPlayerStunDuration = 1.0f;
    [SerializeField] private float parryPlayerKnockbackForce = 1.2f;
    [SerializeField] private float parryPlayerKnockbackTime = 0.12f;

    private HashSet<Player> _hitPlayer = new HashSet<Player>();

    private readonly Dictionary<int, Coroutine> itemDurationCos = new();
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
    public override bool IsDying => NetIsDeath;
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

    protected bool _isReturning = false;
    [AutoAssign("Recall")] public GameObject _recallEffect;

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

        if (_inputManager.returnPressed && !_isBaseAttacking && !_isSkillAttacking && !_isRolling && IsAlive)
        {
            SetReturn(true);
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
        if (Object.HasInputAuthority && (SceneLoader.Instance.CurrentSceneType == SceneType.Ingame || SceneLoader.Instance.CurrentSceneType == SceneType.IngameTest))
            MessageSpawner.Instance.Show(AlterMessageType.Levelup, 2.5f);

        if (HasStateAuthority && ingameManager != null)
        {
            if (Team == TeamType.TeamA)
                ingameManager.TeamB_Dragon?.CheckLevel();
            else
                ingameManager.TeamA_Dragon?.CheckLevel();
        }

        IngameData.Instance.SetLevel(Object.Id, NetLevel);
        ingameManager.SetAverageLevel();
        PlayerInfoData.levelText.text = NetLevel.ToString();
        if (ingameManager.ShowItemManager.gameObject.activeSelf)
        {
            ingameManager.StatManager.ResetStatCount();
        }
    }
    private void OnChanged_NetCoin()
    {
        if (ingameManager != null)
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

        if (ingameManager == null)
            return;

        if (ingameManager.EffectManager == null)
            return;

        if (weaponHitBox == null)
            return;

        if (Object.HasStateAuthority)
            NetParriedAttackCount = 1;

        if (ParriedParticle == null)
        {
            ParriedParticle = ingameManager.EffectManager.PlayPooledEffect(
                "Effect/WeaponParriedLight",
                Vector3.zero,
                Quaternion.identity,
                ParriedTimeDuration,
                parent: weaponHitBox.transform
            );
            return;
        }

        if (!ParriedParticle.gameObject.activeSelf)
        {
            ParriedParticle = ingameManager.EffectManager.PlayPooledEffect(
                "Effect/WeaponParriedLight",
                Vector3.zero,
                Quaternion.identity,
                ParriedTimeDuration,
                parent: weaponHitBox.transform
            );
        }
    }
    private void OnChanged_NetParriedAttackCount()
    {
        if (NetParriedAttackCount > 0)
            return;

        if (ParriedParticle == null)
            return;

        if (ingameManager == null || ingameManager.EffectManager == null)
            return;

        if (ParriedParticle.gameObject.activeSelf)
            ingameManager.EffectManager.StopPooledEffect(ParriedParticle);

        ParriedParticle = null;
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
        ingameManager.MinimapCam.CreatePlayerIcon(this.transform, Object.HasInputAuthority);
        int _myTeamIndex = ingameManager.MyPlayerDataInfo.TeamIndex;

        bool _isEnemy = _myTeamIndex != -1 && _myTeamIndex != PlayerInfoData.TeamIndex;

        PlayerInfoData.UiSetting(_isEnemy);
        _kcc.gravity = -500;

        if (CharAnimator != null && CharAnimator.GetComponent<PlayerAnimationEvent>() == null)
        {
            CharAnimator.gameObject.AddComponent<PlayerAnimationEvent>();
        }

        HeroInfo = DataManager.Instance.HeroData[PlayerInfoData.CharacterIndex];
        HP = HeroInfo.HP;
        MP = HeroInfo.MP;
        DefenseStat = HeroInfo.Defense;
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
                ingameManager.CameraRenderSetting();

                await UniTask.WaitUntil(() => PlayerItemComponent != null && CharAnimator != null);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

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

        if (PlayerInfoData.IsAI)
        {
            int weaponIndex = DataManager.Instance.WeaponData.Values
                    .Where(w => w.WeaponType == HeroInfo.HeroType)
                    .First().Index;
            if (ingameManager.IsHost)
                RPC_OnWeaponChanged(weaponIndex);
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
        if (CharAnimator == null) return;
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

    #region ▶ ReturnBase
    private void SetReturn(bool v)
    {
        if (_isReturning && !v)
        {
            _isReturning = v;
            ingameManager.PlayerInfoManager.SetReturn(-100, false);
            return;
        }

        if (!_isReturning && v)
        {
            _isReturning = v;
            ReturningBase(this.GetCancellationTokenOnDestroy()).Forget();
            ingameManager.PlayerInfoManager.SetReturn(5, true);
            return;
        }
    }

    private async UniTask ReturningBase(CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(5000);

        bool conditionMet = false;

        try
        {
            RPC_SetRecallEffect(true);

            await UniTask.WaitUntil(
                () => !_isReturning || !IsAlive || NetIsMoving || NetIsJump || NetIsRoll || NetBaseAttacking || NetBlock || _isSkillAttacking || _inputManager.basicAttackPressed || _inputManager.useSkillPressed,
                cancellationToken: cts.Token
            );

            conditionMet = true;
        }
        catch (OperationCanceledException)
        {
        }

        if (!conditionMet)
        {
            RPC_TeleportToRandomSpawnPoint();
        }

        SetReturn(false);
        RPC_SetRecallEffect(false);

    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_SetRecallEffect(bool v)
    {
        _recallEffect.SetActive(v);
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
        ClearHitRecords();
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
    public void DamageToHero(float _damage, UnitObject Attacker, bool _isCritical, bool ApplyHitSkill = true, bool CameraShake = true)
    {
        if (IsInvincible() || !IsAlive || Evaid())
            return;

        RPC_CancleReturn();

        #region Attacker 기록

        if (Object.HasStateAuthority)
        {
            float _now = (float)Runner.SimulationTime;

            if (Attacker is Player _attackerPlayer)
            {
                if (_attackerPlayer.PlayerInfoData != null && PlayerInfoData != null &&
                    _attackerPlayer.PlayerInfoData.Team != PlayerInfoData.Team)
                {
                    hitTimeByPlayers[_attackerPlayer.Object.Id] = _now;

                    lastHitByPlayerId = _attackerPlayer.Object.Id;
                    lastHitTime = _now;
                    hasLastHitByPlayer = true;

                    if (CanAIControl)
                    {
                        aiLastAttackerId = _attackerPlayer.Object.Id;
                        aiLastAttackerTime = _now;
                    }
                }
            }
            else if (Attacker is Monster _attackerMonster)
            {
                if (CanAIControl && _attackerMonster.Object != null)
                {
                    aiLastAttackerId = _attackerMonster.Object.Id;
                    aiLastAttackerTime = _now;
                }
            }
        }

        #endregion

        #region Block 계산

        bool _blocked = false;
        bool _parried = false;
        BlockType _blockType = BlockType.NoBlock;

        if (NetBlock && CharAnimator != null)
        {
            var _st = CharAnimator.GetCurrentAnimatorStateInfo(1);
            if (_st.IsName("Block"))
            {
                if (Attacker != null)
                {
                    Vector3 _toAttacker = Attacker.transform.position - transform.position;
                    _toAttacker.y = 0f;

                    if (_toAttacker.sqrMagnitude > 0.0001f)
                    {
                        _toAttacker.Normalize();
                        float _angle = Vector3.Angle(transform.forward, _toAttacker);

                        float _timeSinceBlock = Now - NetLastBlockStartTime;

                        if (_angle <= BlockFrontAngle)
                        {
                            _blocked = true;

                            if (_timeSinceBlock <= ParryWindow)
                                _parried = true;
                        }
                    }
                }
                else
                {
                    _blocked = true;
                }
            }
        }

        #endregion

        #region Damage 계산

        if (ApplyHitSkill)
            _damage = CalculationStatusDamage(Attacker, _damage);

        _damage -= _damage * ReduceDamageRatio;
        _damage = _damage * (100 / (100 + Defense));

        if (_blocked)
        {
            if (_parried)
            {
                _blockType = BlockType.Parried;

                _damage = 0f;

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
                _blockType = BlockType.Block;
                _damage *= BlockDamageMultiplier;
            }
        }

        float _leftDamage = _damage - Shield;
        Shield = Mathf.Max(0f, Shield - _damage);
        Affect.UseShieldAffect(_damage);

        HP -= Mathf.Max(0f, _leftDamage);
        HP = Mathf.Clamp(HP, 0f, MaxHP);

        RPC_ShowDamageText(_damage, _isCritical, Attacker, _blockType, CameraShake);

        if (ApplyHitSkill)
            HitPassiveSkill(Attacker, _damage);

        #endregion

        #region Death 처리

        if (HP <= 0f)
        {
            RPC_StopAllAttacks();

            DiePassiveSkill();

            if (!ingameManager.CheckEndGame())
            {
                if (!NetIsDeath && Attacker is Player)
                    GrantKillRewards();

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

        #endregion
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayGoldGain(PlayerRef targetPlayer, Vector3 worldPos, int goldVisualCount)
    {
        if (Runner.LocalPlayer != targetPlayer)
            return;

        var _ps = InGameManager.Instance.EffectManager.PlayPooledEffect(
            "Effect/GetGold",
            worldPos,
            Quaternion.identity,
            3f
        );
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_ReviveHeal()
    {
        HP = MaxHP;
        MP = MaxMP;
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_CancleReturn()
    {
        if (_isReturning)
            SetReturn(false);
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
    private static readonly Collider[] _parryCols = new Collider[64];

    private void ApplyParryAoE()
    {
        if (!Object.HasStateAuthority) return;

        Vector3 _origin = transform.position + Vector3.up * 1.0f;

        Vector3 _dir = transform.forward;
        _dir.y = 0f;
        if (_dir.sqrMagnitude < 0.0001f)
            _dir = Vector3.forward;
        _dir.Normalize();

        float _radius = ParryRadius;
        float _halfAngle = BlockFrontAngle;

        int _mask = LayerMask.GetMask("Monster", "Enemy", "Hero");

        if (_mask == 0)
        {
            Debug.Log("패링 마스크가 0");
            return;
        }

        int _count = Physics.OverlapSphereNonAlloc(
            _origin,
            _radius,
            _parryCols,
            _mask
        );

        if (_count <= 0)
        {
            Debug.Log("패링성공 아무도없음");
            return;
        }
        HashSet<Monster> _parriedMonsters = new HashSet<Monster>();
        HashSet<Player> _parriedPlayers = new HashSet<Player>();

        for (int _i = 0; _i < _count; _i++)
        {
            var _col = _parryCols[_i];
            if (_col == null) continue;

            var _monster = _col.GetComponentInParent<Monster>();
            if (_monster != null)
            {
                if (_monster.IsDying || _monster.IsSummon) continue;
                if (!_parriedMonsters.Add(_monster)) continue;

                Vector3 _toM = _monster.transform.position - transform.position;
                _toM.y = 0f;
                if (_toM.sqrMagnitude <= 0.0001f) continue;

                float _angle = Vector3.Angle(_dir, _toM.normalized);
                if (_angle > _halfAngle) continue;

                _monster.Parried_Server();
                continue;
            }

            var _player = _col.GetComponentInParent<PlayerFollowController>().TargetPlayer;
            if (_player == null) continue;

            if (_player.Object != null && _player.Object.Id == Object.Id) continue;
            if (!_parriedPlayers.Add(_player)) continue;

            if (_player.PlayerInfoData == null || PlayerInfoData == null) continue;
            if (_player.PlayerInfoData.TeamIndex == PlayerInfoData.TeamIndex) continue;

            Vector3 _toP = _player.transform.position - transform.position;
            _toP.y = 0f;
            if (_toP.sqrMagnitude <= 0.0001f) continue;

            float _pAngle = Vector3.Angle(_dir, _toP.normalized);
            if (_pAngle > _halfAngle) continue;

            _player.ApplyParryStunKnockback_Server(_toP.normalized);
        }
    }
    public void ApplyParryStunKnockback_Server(Vector3 _dir)
    {
        if (!Object.HasStateAuthority) return;

        if (parryStunCts != null)
        {
            parryStunCts.Cancel();
            parryStunCts.Dispose();
            parryStunCts = null;
        }

        parryStunCts = new CancellationTokenSource();
        ApplyParryStunKnockback_Task(parryStunCts.Token, _dir).Forget();
    }

    private async UniTaskVoid ApplyParryStunKnockback_Task(CancellationToken _token, Vector3 _dir)
    {
        _dir.y = 0f;
        if (_dir.sqrMagnitude <= 0.0001f)
            _dir = -transform.forward;

        _dir.Normalize();

        Stun = true;

        KnockBack = true;
        KnockBackDirection = _dir * 3f * parryPlayerKnockbackForce;

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(parryPlayerKnockbackTime), cancellationToken: _token);

            KnockBack = false;
            KnockBackDirection = Vector3.zero;

            float _remain = Mathf.Max(0.01f, parryPlayerStunDuration - parryPlayerKnockbackTime);
            await UniTask.Delay(TimeSpan.FromSeconds(_remain), cancellationToken: _token);

            Stun = false;
        }
        catch (OperationCanceledException)
        {
        }
    }
    #endregion

    #region ▶ Player Info
    private int CalcGoldVisualCount(int gold)
    {
        int _count = Mathf.CeilToInt(gold / 3f);
        if (_count < 1) _count = 1;
        return _count;
    }
    private void ClearHitRecords()
    {
        hitTimeByPlayers.Clear();
        hasLastHitByPlayer = false;
        lastHitByPlayerId = default;
        lastHitTime = 0f;
    }

    private void GrantKillRewards()
    {
        if (!Object.HasStateAuthority)
            return;

        float _now = (float)Runner.SimulationTime;

        if (!hasLastHitByPlayer)
        {
            ClearHitRecords();
            return;
        }

        if (_now - lastHitTime > ASSIST_WINDOW_SECONDS)
        {
            ClearHitRecords();
            return;
        }

        if (!Runner.TryFindObject(lastHitByPlayerId, out var _obj))
        {
            ClearHitRecords();
            return;
        }

        Player _killer = _obj.GetComponent<Player>();
        if (_killer == null)
        {
            ClearHitRecords();
            return;
        }

        if (_killer.PlayerInfoData.Team == PlayerInfoData.Team)
        {
            ClearHitRecords();
            return;
        }

        float _needExp = 0f;
        if (DataManager.Instance != null && DataManager.Instance.HeroEXPData != null)
        {
            if (DataManager.Instance.HeroEXPData.TryGetValue(NetLevel, out var _expData))
                _needExp = _expData.NeedEXP;
        }

        float _killerExp = _needExp * EXP_REWARD_RATE_ON_KILL;
        float _assistExp = _killerExp * ASSIST_REWARD_RATE;

        int _assistGold = Mathf.RoundToInt(GOLD_REWARD_ON_KILL * ASSIST_REWARD_RATE);

        _killer.AddCoin(GOLD_REWARD_ON_KILL);

        int _killerVisual = CalcGoldVisualCount(GOLD_REWARD_ON_KILL);
        RPC_PlayGoldGain(_killer.Object.InputAuthority, transform.position, _killerVisual);
        if (_killerExp > 0f)
            _killer.AddExp(_killerExp);

        foreach (var _pair in hitTimeByPlayers)
        {
            NetworkId _id = _pair.Key;
            float _hitTime = _pair.Value;

            if (_id == _killer.Object.Id)
                continue;

            if (_now - _hitTime > ASSIST_WINDOW_SECONDS)
                continue;

            if (!Runner.TryFindObject(_id, out var _assistObj))
                continue;

            Player _assist = _assistObj.GetComponent<Player>();
            if (_assist == null)
                continue;

            if (_assist.PlayerInfoData.Team == PlayerInfoData.Team)
                continue;

            _assist.AddCoin(_assistGold);

            int _assistVisual = CalcGoldVisualCount(_assistGold);
            RPC_PlayGoldGain(_assist.Object.InputAuthority, transform.position, _assistVisual);
            if (_assistExp > 0f)
                _assist.AddExp(_assistExp);
        }

        ClearHitRecords();
    }
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

        float reviveTime = ReviveTime;
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
        RPC_ReviveHeal();
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
        float reviveTime = ReviveTime;

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
        HP = MaxHP;
        MP = MaxMP;
        TeleportToRandomSpawnPoint();
        NetIsGodMode = true;
        NetGodModeEndTime = Now + 3;

        // AI도 로컬 리셋은 해주는게 안전
        LocalReviveReset();
    }
    public void Heal(float _value, bool _canEffect = true)
    {
        if (BlockHeal || !IsAlive)
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
    }
    public void UseItem(Data_Item item)
    {
        if (!Object.HasInputAuthority || !IsAlive) return;
        if (item == null) return;

        var pim = ingameManager?.PlayerInfoManager;
        if (pim == null) return;

        if (pim.ItemCooldowns.IsOnCooldown(item.Index)) return;

        if (!OwnedItems.TryGetValue(item, out var _count) || _count <= 0)
        {
            ingameManager?.InventoryManager?.OnItemChanged(item, 0);
            return;
        }

        _count -= 1;
        if (_count <= 0) OwnedItems.Remove(item);
        else OwnedItems[item] = _count;

        ingameManager?.InventoryManager?.OnItemChanged(item, _count);

        if (item.CoolTime > 0f)
            pim.ItemCooldowns.Start(item.Index, item.CoolTime);

        if (item.Duration > 0f)
        {
            StartItemDurationEffect(item);
            return;
        }

        ApplyItemInstant(item);
    }

    private void ApplyItemInstant(Data_Item item)
    {
        bool _didHp = false;
        bool _didMp = false;

        if (item.AbilitiesIndex == null) return;

        for (int _i = 0; _i < item.AbilitiesIndex.Length; _i++)
        {
            int _abil = item.AbilitiesIndex[_i];

            if (_abil == 0 && !_didHp)
            {
                RPC_Heal(item.value_1);
                _didHp = true;
            }
            else if (_abil == 1 && !_didMp)
            {
                RPC_RestoreMana(item.value_1);
                _didMp = true;
            }
        }
    }

    private void StartItemDurationEffect(Data_Item item)
    {
        if (item == null) return;

        if (itemDurationCos.TryGetValue(item.Index, out var _co) && _co != null)
        {
            StopCoroutine(_co);
            itemDurationCos.Remove(item.Index);
        }

        var _newCo = StartCoroutine(ItemDurationCo(item));
        itemDurationCos[item.Index] = _newCo;
    }

    private IEnumerator ItemDurationCo(Data_Item item)
    {
        if (item == null) yield break;
        if (item.Duration <= 0f) yield break;

        float _totalHeal = item.value_1;
        float _remainTime = item.Duration;

        if (_totalHeal <= 0f)
        {
            itemDurationCos.Remove(item.Index);
            yield break;
        }

        int _appliedSum = 0;
        int _totalInt = Mathf.RoundToInt(_totalHeal);

        while (_remainTime > 0f)
        {
            if (!IsAlive) break;

            float _dt = _remainTime >= 1 ? 1 : _remainTime;

            float _ratio = _dt / item.Duration;
            int _tickHeal = Mathf.RoundToInt(_totalHeal * _ratio);

            if (_tickHeal < 0) _tickHeal = 0;

            if (_tickHeal > 0)
            {
                ApplyItemTick(item, _tickHeal);
                _appliedSum += _tickHeal;
            }

            _remainTime -= _dt;
            yield return new WaitForSeconds(_dt);
        }

        if (IsAlive)
        {
            int _left = _totalInt - _appliedSum;
            if (_left > 0)
                ApplyItemTick(item, _left);
        }

        itemDurationCos.Remove(item.Index);
    }
    private void ApplyItemTick(Data_Item item, int amount)
    {
        if (item == null) return;
        if (amount <= 0) return;
        if (!IsAlive) return;

        bool _didHp = false;
        bool _didMp = false;

        if (item.AbilitiesIndex == null) return;

        for (int _i = 0; _i < item.AbilitiesIndex.Length; _i++)
        {
            int _abil = item.AbilitiesIndex[_i];

            if (_abil == 0 && !_didHp)
            {
                RPC_Heal(amount);
                _didHp = true;
            }
            else if (_abil == 1 && !_didMp)
            {
                RPC_RestoreMana(amount);
                _didMp = true;
            }
        }
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
                }
            }
        }
    }
    #endregion

    #region 초기화
    public void Player_HostMiGration()
    {
        CameraPivot.localPosition = PlayerCameraPos;

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
        ingameManager.MinimapCam.CreatePlayerIcon(this.transform, Object.HasInputAuthority);
        if (WeaponIndex != -1)
        {
            int _myTeamIndex = ingameManager.MyPlayerDataInfo.TeamIndex;
            bool _isEnemy = _myTeamIndex != -1 && _myTeamIndex != PlayerInfoData.TeamIndex;
            WeaponInfo = DataManager.Instance.WeaponData[WeaponIndex];
            GetNextWeapon();
            WalkSpeed = HeroInfo.Speed;
            if (ingameManager.PlayerFollowKeyValue.ContainsKey(Object.Id))
                FollowController = ingameManager.PlayerFollowKeyValue[Object.Id];
            if (PlayerItemComponent != null)
                weaponHitBox = PlayerItemComponent.WeaponPos.GetComponentInChildren<WeaponHitbox>();
            PlayerInfoData.UiSetting(_isEnemy);

            RefreshLayerSpeeds();
        }
    }
    #endregion
}