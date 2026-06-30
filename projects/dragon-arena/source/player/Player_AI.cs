using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;

public partial class Player
{
    #region AI Variables
    private int myNexusIndex = -1;
    public int MyNexusIndex
    {
        get
        {
            if (myNexusIndex == -1 && ingameManager != null)
            {
                if (Team == TeamType.TeamA)
                    myNexusIndex = ingameManager.TeamA_Dragon.SpawnIndex;
                else
                    myNexusIndex = ingameManager.TeamB_Dragon.SpawnIndex;
            }
            return myNexusIndex;
        }
    }

    private Monster aiChaseTargetMonster;
    private Player aiChaseTargetEnemyPlayer;
    private bool aiIsChasing;

    private float aiNextScanTime;
    private const float AI_SCAN_INTERVAL = 0.2f;
    private const float AI_PRIORITY_RADIUS = 15f; // 탐지 사거리 약간 확장

    private const float AI_ATTACK_EPS = 0.2f;
    private const float AI_STOP_MELEE = 2.0f;
    private const float AI_STOP_RANGED = 6.0f;

    private bool aiForceRun;
    [SerializeField]
    private Vector3 aiMainGoalPos;
    private float aiNextGoalUpdateTime;
    private const float AI_GOAL_UPDATE_INTERVAL = 0.5f;

    private int aiStatOrderIndex = 0;
    private readonly int[][] STAT_ORDER_BY_WEAPON =
    {
        new int[] { 2, 1, 1, 4, 1 },
        new int[] { 2, 1, 1, 4, 1 },
        new int[] { 2, 9, 9, 4, 3 },
    };

    [Header("AI Layer & Avoidance")]
    [SerializeField] private LayerMask aiObstacleMask;
    [SerializeField] private LayerMask aiTreeMask;

    private Vector3 aiCurrentAvoidDir;
    private float aiAvoidEndTime;
    private const float AI_AVOID_DURATION = 0.4f;

    private Vector3 aiLastStableMoveDir;
    private float aiLastStableMoveTime;

    private NetworkId aiLastAttackerId;
    private float aiLastAttackerTime;
    #endregion

    #region AI Gizmos (시각화)
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !CanAIControl) return;

        // 최종 목적지 (파란색)
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position + Vector3.up, aiMainGoalPos + Vector3.up);
        Gizmos.DrawWireSphere(aiMainGoalPos, 0.7f);

        // 추격 대상 (빨간색)
        if (aiIsChasing && AI_HasChaseTarget())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(AI_GetChaseTargetPosition(), 1.2f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, AI_GetChaseTargetPosition() + Vector3.up * 1.5f);
        }
    }
    #endregion

    #region AI Init & Dispose
    [Networked] public bool Net_AIInit { get; set; } = false;
    private CancellationTokenSource aiWeaponUpgradeToken;
    private CancellationTokenSource aiStatUpgradeToken;

    public void AI_PlayerInit()
    {
        if (!CanAIControl) return;
        AI_Dispose();

        aiWeaponUpgradeToken = new CancellationTokenSource();
        aiStatUpgradeToken = new CancellationTokenSource();

        AI_UpgradeNexeWeapon_Coroutine(aiWeaponUpgradeToken.Token).Forget();
        AI_UpgradeRandomStat_Coroutine(aiStatUpgradeToken.Token).Forget();

        if (!Net_AIInit && CanAIControl) Net_AIInit = true;

        aiIsChasing = false;
        aiForceRun = false;

        if(ingameManager == null) ingameManager = InGameManager.Instance;

        int _idx = UnityEngine.Random.Range(0, ingameManager.NexusSpawnPositions.Count);
        aiMainGoalPos = ingameManager.NexusSpawnPositions[_idx].position;
    }

    public void AI_Dispose()
    {
        aiWeaponUpgradeToken?.Cancel();
        aiWeaponUpgradeToken?.Dispose();
        aiWeaponUpgradeToken = null;
        aiStatUpgradeToken?.Cancel();
        aiStatUpgradeToken?.Dispose();
        aiStatUpgradeToken = null;
    }
    #endregion

    #region AI 핵심 로직 (타겟팅 우선순위 강화)
    private bool AI_BuildInput(out NetworkInputData input)
    {
        input = default;
        if (!CanAIControl || NetIsDeath || !IsAlive) return false;

        // 1. 매 프레임 타겟을 먼저 찾음 (뒤에 있는 몬스터 감지)
        AI_TryAcquirePriorityTarget();
        AI_UpdateChaseTarget();

        // 2. 이동 방향 계산
        Vector3 _dirWorld = AI_GetMoveDirectionWorld();

        if (_dirWorld.sqrMagnitude < 0.0001f)
        {
            input.moveDir = Vector2.zero;
            input.IsRun = false;
            // 멈춰있을 때 타겟 조준
            if (aiIsChasing && AI_HasChaseTarget())
            {
                Vector3 _face = AI_GetChaseTargetPosition() - transform.position;
                _face.y = 0f;
                input.lookDelta = AI_ComputeLookDelta(_face.normalized);
            }
            AI_ServerUpdateCombat(ref input);
            return true;
        }

        _dirWorld.y = 0f;
        _dirWorld.Normalize();

        Vector2 _md;
        _md.x = Vector3.Dot(transform.right, _dirWorld);
        _md.y = Vector3.Dot(transform.forward, _dirWorld);
        input.moveDir = Vector2.ClampMagnitude(_md, 1f);
        input.IsRun = true; // 대상을 쫓거나 이동할 땐 항상 뛰게 설정
        input.lookDelta = AI_ComputeLookDelta(_dirWorld);

        AI_ServerUpdateCombat(ref input);
        return true;
    }

    private Vector3 AI_GetMoveDirectionWorld()
    {
        Vector3 _targetPos;
        float _stopDist = 1.0f;

        // [수정] 추격 대상이 있다면 넥서스 무시하고 대상에게 직행
        if (aiIsChasing && AI_HasChaseTarget())
        {
            _targetPos = AI_GetChaseTargetPosition();
            _stopDist = Mathf.Max(0.5f, AI_GetStopDistanceByWeapon() - 0.3f);
        }
        else
        {
            AI_UpdateMainGoalAndRunState();
            _targetPos = AI_SanitizeGoalPos(aiMainGoalPos);
        }

        return AI_GetDirectMoveDirection(_targetPos, _stopDist);
    }

    private Vector3 AI_GetDirectMoveDirection(Vector3 goal, float stopDist)
    {
        Vector3 _to = goal - transform.position;
        _to.y = 0f;

        if (_to.sqrMagnitude <= stopDist * stopDist) return Vector3.zero;

        Vector3 _dir = _to.normalized;
        _dir = AI_AvoidObstacleAdvanced(_dir);

        return AI_StabilizeMoveDir(_dir);
    }
    #endregion

    #region 맵 이탈 방지 및 장애물 회피 (NavMesh 준수)
    private Vector3 AI_SanitizeGoalPos(Vector3 goalPos)
    {
        // 맵 밖으로 나가는 현상 방지: NavMesh의 가장 가까운 가장자리로 강제 고정
        if (NavMesh.SamplePosition(goalPos, out var _hit, 30.0f, NavMesh.AllAreas))
        {
            return _hit.position;
        }
        return transform.position;
    }

    private Vector3 AI_AvoidObstacleAdvanced(Vector3 moveDir)
    {
        if (Time.time < aiAvoidEndTime) return aiCurrentAvoidDir;

        int _mask = aiObstacleMask.value | aiTreeMask.value;
        Vector3 _origin = transform.position + Vector3.up * 1.0f;
        float _dist = 2.0f;
        float _radius = 0.6f;

        if (Physics.SphereCast(_origin, _radius, moveDir, out var _hit, _dist, _mask))
        {
            Vector3 _hitNormal = _hit.normal;
            _hitNormal.y = 0;
            Vector3 _sideStepDir = Vector3.Cross(Vector3.up, _hitNormal);
            if (Vector3.Dot(_sideStepDir, moveDir) < 0) _sideStepDir = -_sideStepDir;

            aiCurrentAvoidDir = _sideStepDir.normalized;
            aiAvoidEndTime = Time.time + AI_AVOID_DURATION;
            return aiCurrentAvoidDir;
        }

        // [추가] 가려는 방향이 NavMesh 밖이라면 회전시킴
        Vector3 _nextPos = transform.position + moveDir * 1.5f;
        if (!NavMesh.SamplePosition(_nextPos, out var _, 1.0f, NavMesh.AllAreas))
        {
            // NavMesh 끝에 도달하면 반대 방향으로 꺾음
            return Vector3.Reflect(moveDir, transform.right).normalized;
        }

        return moveDir;
    }
    #endregion

    #region 몬스터/플레이어 탐지 (뒤쪽 포함 전체 스캔)
    private bool AI_TryAcquirePriorityTarget()
    {
        if (Time.time < aiNextScanTime) return aiIsChasing;
        aiNextScanTime = Time.time + AI_SCAN_INTERVAL;

        // OverlapSphere는 뒤에 있는 물체도 모두 감지함
        Collider[] _cols = Physics.OverlapSphere(transform.position, AI_PRIORITY_RADIUS, ~0, QueryTriggerInteraction.Ignore);

        Player _nearestEnemy = null;
        Monster _nearestMonster = null;
        float _minDistP = float.MaxValue;
        float _minDistM = float.MaxValue;

        foreach (var col in _cols)
        {
            Player p = col.GetComponentInParent<Player>();
            if (p != null && p.IsAlive && p.Team != Team)
            {
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < _minDistP) { _minDistP = d; _nearestEnemy = p; }
            }

            Monster m = col.GetComponentInParent<Monster>();
            if (m != null && !m.IsDying)
            {
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d < _minDistM) { _minDistM = d; _nearestMonster = m; }
            }
        }

        if (_nearestEnemy != null)
        {
            aiChaseTargetEnemyPlayer = _nearestEnemy;
            aiChaseTargetMonster = null;
            aiIsChasing = true;
        }
        else if (_nearestMonster != null)
        {
            aiChaseTargetMonster = _nearestMonster;
            aiChaseTargetEnemyPlayer = null;
            aiIsChasing = true;
        }
        else
        {
            aiIsChasing = false;
        }
        return aiIsChasing;
    }
    #endregion

    #region 나머지 유틸리티 (목적지 갱신 및 스탯 업그레이드)
    private void AI_UpdateMainGoalAndRunState()
    {
        if (!CanAIControl || ingameManager == null || PlayerInfoData == null) return;
        if (Time.time < aiNextGoalUpdateTime) return;
        aiNextGoalUpdateTime = Time.time + AI_GOAL_UPDATE_INTERVAL;

        TeamType _myTeam = PlayerInfoData.Team;
        if (ingameManager.TryGetAllyDragonHitPos(_myTeam, out Vector3 _allyHitPos))
        {
            aiMainGoalPos = _allyHitPos;
            return;
        }

        if (ingameManager.TryGetEnemyDragonSpottedPos(_myTeam, out Vector3 _enemyDragonPos))
        {
            aiMainGoalPos = _enemyDragonPos;
            return;
        }

        if (ingameManager.NexusSpawnPositions != null && ingameManager.NexusSpawnPositions.Count > 0)
        {
            if (Vector3.Distance(transform.position, aiMainGoalPos) <= 4f || aiMainGoalPos == transform.position)
            {
                int _idx = UnityEngine.Random.Range(0, ingameManager.NexusSpawnPositions.Count);
                aiMainGoalPos = ingameManager.NexusSpawnPositions[_idx].position;
            }
        }
    }

    private void AI_ServerUpdateCombat(ref NetworkInputData input)
    {
        if (!aiIsChasing || !AI_HasChaseTarget()) return;
        Vector3 _targetPos = AI_GetChaseTargetPosition();
        float dist = Vector3.Distance(transform.position, _targetPos);

        if (dist <= AI_GetStopDistanceByWeapon() + AI_ATTACK_EPS)
        {
            input.moveDir = Vector2.zero;
            Vector3 _face = _targetPos - transform.position;
            _face.y = 0f;
            input.lookDelta = AI_ComputeLookDelta(_face.normalized);
            Server_RequestAttackTick_Common();
        }
    }

    private void AI_UpdateChaseTarget() { if (aiIsChasing && (!AI_HasChaseTarget() || !AI_IsChaseTargetValid())) aiIsChasing = false; }
    private bool AI_HasChaseTarget() => aiChaseTargetEnemyPlayer != null || aiChaseTargetMonster != null;
    private Vector3 AI_GetChaseTargetPosition() => aiChaseTargetEnemyPlayer != null ? aiChaseTargetEnemyPlayer.transform.position : aiChaseTargetMonster.transform.position;
    private bool AI_IsChaseTargetValid() => (aiChaseTargetEnemyPlayer != null && aiChaseTargetEnemyPlayer.IsAlive) || (aiChaseTargetMonster != null && !aiChaseTargetMonster.IsDying);
    private float AI_GetStopDistanceByWeapon() => (_weaponAttackType == 0) ? AI_STOP_MELEE : AI_STOP_RANGED;
    private Vector2 AI_ComputeLookDelta(Vector3 desiredForward) { float _angle = Vector3.SignedAngle(transform.forward, desiredForward, Vector3.up); return new Vector2(Mathf.Clamp(_angle / (NetRotationSpeed * Runner.DeltaTime + 0.01f), -15f, 15f), 0); }

    private Vector3 AI_StabilizeMoveDir(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude > 0.0001f) { aiLastStableMoveDir = moveDir; aiLastStableMoveTime = Time.time; return moveDir; }
        if (Time.time - aiLastStableMoveTime <= 0.25f) return aiLastStableMoveDir;
        return Vector3.zero;
    }

    public async UniTaskVoid AI_UpgradeNexeWeapon_Coroutine(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(5f), cancellationToken: token).SuppressCancellationThrow();
            if (nextWeaponData != null && NetCoin >= nextWeaponData.Price) { NetCoin -= nextWeaponData.Price; RPC_OnWeaponChanged(nextWeaponData.Index); }
        }
    }

    public async UniTaskVoid AI_UpgradeRandomStat_Coroutine(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(4f), cancellationToken: token).SuppressCancellationThrow();
            if (NetStatCount > 0) AI_TryUpgradeRandomStatFromData();
        }
    }

    private void AI_TryUpgradeRandomStatFromData()
    {
        if (NetStatCount <= 0 || DataManager.Instance?.StatData == null) return;
        int _vType = STAT_ORDER_BY_WEAPON[_weaponAttackType % 3][aiStatOrderIndex++ % 5];
        foreach (var kv in DataManager.Instance.StatData) { if (kv.Value.ValueType == _vType) { int aff = ingameManager.StatManager.AffectTypeFromIndex(_vType); if (aff != 0) { RPC_GetStatCoin(-1); RPC_AddAffectValue(aff, kv.Value.Value_1); } break; } }
    }
    #endregion
}