using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class InGameManager : MonoBehaviour
{
    public static InGameManager Instance { get; private set; }
    #region Managers
    [AutoAssign("SkillButtonManager")] public SkillButtonManager skillButtonManager;
    [AutoAssign("LevelUpManager")] public LevelUpManager LevelUpManager;
    [AutoAssign("AffectManager")] public AffectManager AffectManager;
    [AutoAssign("MonsterSpawnManager")] public MonsterSpawnManager MonsterSpawnManager;
    [AutoAssign("InGameTreeSoundPool")] public InGameTreeSoundPool InGameTreeSoundPool;
    [AutoAssign("ProjectileSpawnManager")] public ProjectileSpawnManager ProjectileSpawnManager;
    [AutoAssign("EffectManager")] public EffectManager EffectManager;
    [AutoAssign("ShowItemPanel")] public ShowItemManager ShowItemManager;
    [AutoAssign("ShopSpawnerManager")] public ShopSpawner ShopSpawner;
    [AutoAssign("PlayerPartyManager")] public PlayerPartyManager PlayerPartyManager;
    public PlayerInfoManager PlayerInfoManager;
    public ShopManager ShopManager;
    public SkillManager SkillManager;
    public ItemInfoManager ItemInfoManager;
    public InventoryManager InventoryManager;
    public IngameMenuManager IngameMenuManager;
    #endregion
    public Dictionary<NetworkId, PlayerFollowController> PlayerFollowKeyValue = new Dictionary<NetworkId, PlayerFollowController>();
    public MinimapManager MinimapCam;
    public Transform DamageCanvas;
    public InGameGuideData Guide = new();
    public GameObject LodingObject;
    public GameObject DragonObject;
    public Canvas PlayerInfoCanvas;
    public MapTransformCreate MapTransformCreate;
    public List<Vector3> SpawnPositions;
    public List<Vector3> IncounterSpawnPositions;
    public Vector3 RaidBossPositions;
    public int UserMaxCount;
    public int RandomSeed;
    public int TestMonsterIndex;
    private bool stopSpawning = false;
    private bool endGame = false;
    public bool EndGame { get => endGame; }
    [Header("Monster Wave / Boosted Spawn Settings")]

    [Tooltip("몇 번째 웨이브마다 부스트(엘리트) 웨이브가 발생할지 설정합니다.\n예: 5 → 5웨이브마다 엘리트 등장")]
    [SerializeField] private int boostedWaveInterval = 5;

    [Tooltip("부스트 웨이브마다 엘리트 스폰 군집을 몇 번 생성할지 설정합니다.\n예: 1 → 엘리트 1군집, 2 → 엘리트 2군집")]
    [SerializeField] private int boostedPerWave = 1;

    [Tooltip("엘리트 몬스터 인덱스를 기본보다 얼마나 상향시킬지 설정합니다.\n예: 2 → 기존 몬스터보다 2레벨 높은 몬스터를 엘리트로 사용")]
    [SerializeField] private int boostedIndexOffset = 2;

    [Header("Boosted Cluster (Elite Group) Settings")]

    [Tooltip("엘리트 주변에 함께 스폰될 일반 몬스터(미니언)의 개수입니다.\n예: 4 → 엘리트 1마리 + 미니언 4마리")]
    [SerializeField] private int boostedClusterMinionCount = 4;

    [Tooltip("엘리트를 중심으로 미니언들이 배치될 원형 반경입니다.\n값이 클수록 군집이 넓게 퍼집니다.")]
    [SerializeField] private float boostedClusterRadius = 6f;

    [Tooltip("미니언 간 최소 거리(충돌 방지용)입니다.\n값이 작을수록 밀집, 클수록 널찍하게 배치됩니다.")]
    [SerializeField] private float boostedClusterSpacing = 2.5f;

    private int _spawnWaveCounter = 0;
    private int _boostedLeftThisWave = 0;

    #region 레이드맵
    public GameObject RaidMap;
    public List<Transform> RaidMapSpawnPos = new();
    public Transform RaidSpawnPos;
    public Transform RaidShopPos;
    public Text RaidTimerText;
    public bool raidStarted = false;
    public float LocalRaidStartTime { get; set; } = -1f;
    private DateTime raidTargetKstTime = DateTime.MinValue;
    float raidStartTime = 13;
    string waitRaidText
    {
        get
        {
            if (waitRaidTextArray == null || waitRaidTextArray.Count == 0)
            {
                return LocalizationManager.Instance.GetText("WaitRaidStart");
            }
            return waitRaidTextArray[LocalizationManager.Instance.CurrentLanguageIndex];
        }
    }
    List<string> waitRaidTextArray = new List<string>();
    #endregion

    private Dictionary<NetworkId, List<int>> spawnCycleMap = new();
    private Dictionary<NetworkId, int> spawnIndexMap = new();
    private Dictionary<NetworkId, int> lastKnownLevelMap = new();
    public List<NetworkObject> DragonEggs = new();
    public List<IncounterBase> Incounters = new();
    public Player RaidPlayer;
    public bool ImRaid;
    #region PhotonManager
    public Player MyPlayer => PhotonManager.Instance.MyPlayer;
    public bool IsHost => PhotonManager.Instance.IsHost;
    public Dictionary<NetworkId, PlayerInfoData> PlayerMap => PhotonManager.Instance.PlayerMap;
    public Dictionary<NetworkId, Player> Players => PhotonManager.Instance.Players;
    public NetworkRunner Runner => PhotonManager.Instance.Runner;
    public PlayerInfoData MyPlayerDataInfo => PhotonManager.Instance.MyPlayerDataInfo;

    [SerializeField] private NetworkObject ProjectileSpawnManagerPrefab;
    [SerializeField] private NetworkObject MonsterSpawnManagerPrefab;
    [SerializeField] private NetworkObject ShopSpawnerPrefab;
    #endregion

    public ParticleSystem[] MeleeParticle;
    public bool _KeyLock { get; set; }
    public bool CanMove { get; set; }
    public bool _MoveLock { get; set; }

    [Header("UI/표시 범위 공통 설정")]
    float visRangeXZ = 20f;
    float uiUpdateInterval = 0.1f;
    float visValue = 0.1f;
    public float VisRangeXZ { get => visRangeXZ; }
    public float UiUpdateInterval
    {
        get => uiUpdateInterval;
    }
    bool isMinimapOpen = false;
    public bool IsMinimapOpen { get => isMinimapOpen; }

    private FogEffect fogEffect;

    private int averageLevel = 1;
    public int AverageLevel => averageLevel;

    public bool CanAIMove = false;

    private bool isTutorialMode = false;
    public bool IsTutorialMode => isTutorialMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        isTutorialMode = PhotonManager.Instance.IsTutorialRoom;
        if (!isTutorialMode)
            LodingObject.SetActive(true);
        PlayerInfoManager?.gameObject.SetActive(false);
        InputManager.Instance.inGameManager = this;
        RaidMap.SetActive(false);
        raidStarted = false;
        PlayerInfoCanvas = PlayerInfoManager.transform.parent.GetComponent<Canvas>();
        IngameData.Instance.ResetData();
        Init().Forget();
    }
    public async UniTask Init()
    {
        await UniTask.WaitUntil(() => GameManager.Instance.GetAllData);
        for (int i = 0; i < LocalizationManager.Instance.GetAllKeys().Count; i++)
        {
            waitRaidTextArray.Add(LocalizationManager.Instance.GetText("WaitRaidStart", i));
        }
    }
    private void Start()
    {
        AudioManager.Instance.StartBGMPlaylist(new[] { DataManager.Instance.IngameBGM.AudioClips[0], DataManager.Instance.IngameBGM.AudioClips[1] });
    }
    public void CameraRenderSetting()
    {
        var cam = Camera.main;
        var dists = new float[32];

        int eff = LayerMask.NameToLayer("Effect"); // 이펙트
        int character = LayerMask.NameToLayer("Characters"); // 캐릭터
        int hero = LayerMask.NameToLayer("Hero"); // 캐릭터
        int dragon = LayerMask.NameToLayer("Dragon"); // 캐릭터
        int enemy = LayerMask.NameToLayer("Enemy"); //적
        int weapon = LayerMask.NameToLayer("Weapon"); //무기
        int worldui = LayerMask.NameToLayer("WorldUI"); //World Canvas UI(데미지 텍스트)
        int eggui = LayerMask.NameToLayer("DragonObject"); //World Canvas UI(데미지 텍스트)
        int deathEffect = LayerMask.NameToLayer("DeathEffect"); //World Canvas UI(데미지 텍스트)

        float defaultDist = 200;
        if (MyPlayer.PlayerInfoData.IsRaidBoss)
        {
            dists[eff] = defaultDist;
            dists[character] = defaultDist;
            dists[hero] = defaultDist;
            dists[dragon] = defaultDist;
            dists[enemy] = defaultDist;
            dists[weapon] = defaultDist;
            dists[worldui] = defaultDist;
            dists[eggui] = defaultDist;
        }
        else
        {
            dists[eff] = defaultDist;
            dists[character] = defaultDist;
            dists[hero] = defaultDist;
            dists[dragon] = defaultDist;
            dists[enemy] = defaultDist;
            dists[weapon] = defaultDist;
            dists[worldui] = defaultDist;
            dists[eggui] = defaultDist;
        }
        cam.layerCullDistances = dists;
        cam.layerCullSpherical = true;
    }
    public void CreateFogEffect()
    {
        if (fogEffect != null)
        {
            return;
        }
        var fog = Instantiate(Resources.Load($"Effect/Fog/Fog_{(MyPlayerDataInfo.IsRaidBoss ? "Boss" : "Player")}"));
        fog.name = "FogEffect";
    }
    public void SetAverageLevel()
    {
        int total = 0;
        int playerCount = 0;
        foreach (var p in Players.Values)
        {
            if (!p.PlayerInfoData.IsRaidBoss)
            {
                total += p.NetLevel;
                playerCount++;
            }
        }
        averageLevel = total / playerCount;
        if (MyPlayer.Object.HasStateAuthority)
        {
            MonsterSpawnManager.CheckMonsterLevel();
        }
    }
    public void MinimapOnOff()
    {
        isMinimapOpen = !isMinimapOpen;

        if (MinimapCam == null || MinimapCam.minimapCamera == null) return;

        if (isMinimapOpen)
        {

            MinimapCam.ApplyLargeMapPreset(raidStarted);
        }
        else
        {
            MinimapCam.ApplySmallMapPreset();
        }
    }
    public async UniTask SpawnNetworkManagers()
    {
        var runner = PhotonManager.Instance.Runner;
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        if (ProjectileSpawnManager == null && ProjectileSpawnManagerPrefab != null)
        {
            var obj = runner.Spawn(ProjectileSpawnManagerPrefab, Vector3.zero, Quaternion.identity);
            await WaitForSpawned(obj);
            ProjectileSpawnManager = obj.GetComponent<ProjectileSpawnManager>();
        }

        if (MonsterSpawnManager == null && MonsterSpawnManagerPrefab != null)
        {
            var obj = runner.Spawn(MonsterSpawnManagerPrefab, Vector3.zero, Quaternion.identity);
            await WaitForSpawned(obj);
            MonsterSpawnManager = obj.GetComponent<MonsterSpawnManager>();
        }

        if (ShopSpawner == null && ShopSpawnerPrefab != null)
        {
            var obj = runner.Spawn(ShopSpawnerPrefab, Vector3.zero, Quaternion.identity);
            await WaitForSpawned(obj);
            ShopSpawner = obj.GetComponent<ShopSpawner>();
        }
    }
    private async UniTask WaitForSpawned(NetworkObject obj)
    {
        await UniTask.WaitUntil(() => obj.HasStateAuthority || obj.IsSpawnable);
    }

    public void Init(Player _player)
    {
        MyPlayer.CameraSetting();

        if (MinimapCam != null)
        {
            MinimapCam.InitMonsterSlots(minimapMonsterSlotCount);
        }

        RefreshNearestMonstersLoop().Forget();
    }
    public async UniTaskVoid CheckCharacterModel()
    {
        await UniTask.Delay(3000);
        var photon = PhotonManager.Instance;

        if (photon == null) return;
        if (Instance == null) return;

        // 삭제 예정 목록
        List<NetworkId> removeList = new List<NetworkId>();

        foreach (var pair in PlayerFollowKeyValue)
        {
            NetworkId key = pair.Key;
            PlayerFollowController follow = pair.Value;

            if (!photon.Players.ContainsKey(key))
            {
                Destroy(follow.gameObject);
                removeList.Add(key);
                continue;
            }
        }

        foreach (var key in removeList)
        {
            PlayerFollowKeyValue.Remove(key);
        }
        PlayerInfoManager.characterAvatars.Clear();
        PlayerInfoManager.partyPlayers.Clear();
        if (MyPlayer.Object.HasStateAuthority)
        {
            if (PlayerPartyManager != null)
            {
                foreach (var value in PlayerFollowKeyValue.Values)
                {
                    if (PlayerPartyManager._partyGroups.TryGetValue(value, out var members))
                    {
                        await UniTask.WaitUntil(() => value.TargetPlayer != null);
                        value.TargetPlayer.RPC_UpdateParty(members.ToArray());
                    }
                }
            }
        }
        if (IsHost)
            MyPlayer.CheckEndGame();
    }
    public void OpenShopPanel(int shopID, System.Action onPurchaseCallback = null)
    {
        ShopManager.Init(shopID, onPurchaseCallback != null);
        ShopManager.SetSpecialShopCallback(onPurchaseCallback);
        ShopManager.gameObject.SetActive(true);
        _KeyLock = true;
    }
    public void OpenInventoryPanel()
    {
        if (MyPlayerDataInfo.IsRaidBoss || ShowItemManager.gameObject.activeSelf)
        {
            return;
        }
        if (InventoryManager.gameObject.activeSelf)
        {
            InventoryManager.Close();
            return;
        }
        InventoryManager.Init();
    }
    public void ToggleItemInfoPanel() => ItemInfoManager.Init();
    public void ShowItemException()
    {
        if (InventoryManager.gameObject.activeSelf)
            return;
        ShowItemManager.Open();
    }

    public void ClickExitButton() => ClickExitButtonAsync();
    public async void ClickExitButtonAsync()
    {
        await PhotonManager.Instance.EndGame();
    }

    public void StartGame()
    {
        Debug.Log("[호스트] 게임을 시작합니다.");

        var keys = new List<NetworkId>(PlayerMap.Keys);

        var preferRaidPlayers = PlayerMap
            .Where(kvp => kvp.Value != null && kvp.Value.IsPreferRaid)
            .Select(kvp => kvp.Key)
            .ToList();

        NetworkId raidRef;

        if (preferRaidPlayers.Count > 0)
        {
            int rand = UnityEngine.Random.Range(0, preferRaidPlayers.Count);
            raidRef = preferRaidPlayers[rand];
            Debug.Log($"[레이드 선택] 선호 플레이어 중 선택됨: {PlayerMap[raidRef].NickName}");
        }
        else
        {
            int rand = UnityEngine.Random.Range(0, keys.Count);
            raidRef = keys[rand];
            Debug.Log($"[레이드 선택] 무작위 선택됨: {PlayerMap[raidRef].NickName}");
        }

        ShopSpawner.CreateRaidShop(RaidShopPos);

#if !UNITY_EDITOR
        if (PlayerMap.Count > 1)
        {
            foreach (var kvp in PlayerMap)
                if (kvp.Key == raidRef)
                    kvp.Value.RPC_IsRaid();
        }
        else if (PlayerMap.Count == 1 && MyPlayerDataInfo.IsPreferRaid)
            PlayerMap[keys[0]].RPC_IsRaid();
#else
        if (PlayerMap.Count > 1)
        {
            foreach (var kvp in PlayerMap)
                if (kvp.Key == raidRef)
                    kvp.Value.RPC_IsRaid();
        }
        else if (PlayerMap.Count == 1 && MyPlayerDataInfo.IsPreferRaid)
            PlayerMap[keys[0]].RPC_IsRaid();
#endif

        WaitAndContinueStartGame().Forget();
    }

    private async UniTaskVoid WaitAndContinueStartGame()
    {
        await UniTask.Delay(20);
        RandomSeed = UnityEngine.Random.Range(0, 10000);
        UnityEngine.Random.InitState(RandomSeed);
        await MapTransformCreate.GetComponent<InGameTreeSpawner>().SpawnIncounter(5);
        MyPlayerDataInfo.RPC_SpawnTree(RandomSeed);

        foreach (var kvp in PlayerMap)
        {
            var player = kvp.Value.GetComponent<Player>();
            player?.TeleportToSpawnPoint();
            player?.RPC_SpawnCharacterModel();
            player?.RPC_ShowLoadingPanel(2);
        }
        if (IsHost)
        {
            CreateAI();
        }

        await UniTask.Delay(200);
        int _count = PlayerMap.Where(a => !a.Value.IsRaidBoss).Count();
        ShopSpawner.SpawnInitialShops(_count + 5);

        Incounters.ForEach((i) => i.Active());
    }
    public async UniTask LoopSpawn()
    {
        await UniTask.Delay(200);
        StartSpawnLoop().Forget();
        MapTransformCreate.GetComponent<InGameTreeSpawner>().StartSpawnDragonObjectLoop().Forget();
    }
    private async UniTaskVoid StartSpawnLoop()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: this.GetCancellationTokenOnDestroy());
        PlayerPartyManager.CheckPartyPlayersAsync().Forget();
        foreach (var player in Players.Values)
        {
            var playerRef = player.Object.Id;
            if (!spawnCycleMap.ContainsKey(playerRef))
            {
                spawnCycleMap[playerRef] = new List<int>() { 0 };
                spawnIndexMap[playerRef] = 0;
                lastKnownLevelMap[playerRef] = 1;
            }
        }

        while (!raidStarted)
        {
            if (stopSpawning) return;

            _spawnWaveCounter++;
            if (boostedWaveInterval > 0 && (_spawnWaveCounter % boostedWaveInterval == 0))
                _boostedLeftThisWave = Mathf.Max(0, boostedPerWave);
            else
                _boostedLeftThisWave = 0;
            if (IsHost)
                MonsterSpawn(10);

            await UniTask.Delay(TimeSpan.FromSeconds(20), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }

    public void DesapwnMonster()
    {

    }
    private async UniTaskVoid StartSpawnDragonObjectLoop()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: this.GetCancellationTokenOnDestroy());

        while (!raidStarted)
        {
            if (stopSpawning) return;
            MonsterSpawn(8);
            await UniTask.Delay(TimeSpan.FromSeconds(300), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }

    public void SpawnDragonObject(Vector3 position)
    {
        if (Runner == null)
        {
            Debug.LogError("[InGameManager] InGameManager/PhotonManager/Runner 가 없음. 알 스폰 오류.");
            return;
        }

        if (DragonObject == null)
        {
            Debug.LogError("[InGameManager] InGameManager/DragonObject가 할당되지 않음. 알 스폰 오류.");
            return;
        }

        Runner.Spawn(DragonObject, position);
    }

    public void OnPlayerLevelUp(Player player)
    {
        var playerRef = player.Object.Id;
        int playerLevel = player.NetLevel;

        var all = DataManager.Instance.MonsterData.Values;
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning("[OnPlayerLevelUp] MonsterData empty");
            return;
        }

        var lowers = all.Where(m => m.LevelGrade < playerLevel).ToList();

        int chosenGrade;
        if (lowers.Count > 0)
        {
            chosenGrade = lowers.Max(m => m.LevelGrade);
        }
        else
        {
            chosenGrade = all.Min(m => m.LevelGrade);
        }

        var candidates = all
            .Where(m => m.LevelGrade == chosenGrade)
            .Select(m => m.Index)
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[OnPlayerLevelUp] No candidates at grade {chosenGrade}");
            return;
        }

        int selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        MonsterSpawnManager.SpawnMonster(player, selected);

        var lowerList = all
            .Where(m => m.LevelGrade < chosenGrade)
            .Select(m => m.LevelGrade)
            .Distinct()
            .OrderByDescending(lv => lv)
            .ToList();

        spawnCycleMap[playerRef] = lowerList;
        spawnIndexMap[playerRef] = 0;
        lastKnownLevelMap[playerRef] = playerLevel;
    }

    public void MonsterSpawn(int _count)
    {
        if (_count <= 0) return;

        var spawnables = Players.Values
            .Where(p => p != null && p.IsAlive && !p.PlayerInfoData.IsRaidBoss)
            .ToList();
        if (spawnables.Count == 0) return;

        int totalTarget = _count * spawnables.Count;

        var monsterDict = DataManager.Instance.MonsterData;
        var all = monsterDict.Values.ToList();
        if (all.Count == 0) return;

        var batchPositions = new List<Vector3>();
        int spawned = 0;
        int ptr = 0;

        while (spawned < totalTarget)
        {
            var player = spawnables[ptr];
            ptr = (ptr + 1) % spawnables.Count;

            int playerLv = player.NetLevel;

            List<Data_Monster> basePool;
            var lower = all.Where(m => m.LevelGrade < playerLv).ToList();
            if (lower.Count > 0)
            {
                int bestLowerGrade = lower.Max(m => m.LevelGrade);
                basePool = lower.Where(m => m.LevelGrade == bestLowerGrade).ToList();
            }
            else
            {
                int minGrade = all.Min(m => m.LevelGrade);
                basePool = all.Where(m => m.LevelGrade == minGrade).ToList();
            }
            if (basePool.Count == 0) continue;

            var basePick = basePool[UnityEngine.Random.Range(0, basePool.Count)];
            int baseIndex = basePick.Index;

            if (_boostedLeftThisWave > 0)
            {
                int left = totalTarget - spawned;
                if (left <= 0) break;

                int minionToSpawn = Mathf.Clamp(boostedClusterMinionCount, 0, Mathf.Max(0, left - 1));

                int eliteIndex = baseIndex + Mathf.Max(1, boostedIndexOffset);
                if (!monsterDict.ContainsKey(eliteIndex))
                {
                    int fallback = monsterDict.Keys.Where(k => k > baseIndex).DefaultIfEmpty(-1).Max();
                    if (fallback > baseIndex) eliteIndex = fallback;
                    else eliteIndex = baseIndex;
                }

                Vector3 centerPos;
                bool gotCenter = MonsterSpawnManager.TryGetValidSpawnPosition(
                    player.transform.position,
                    minSpacing: 4.0f,
                    maxAttempts: 20,
                    out centerPos,
                    batchPositions
                );
                if (!gotCenter) continue;

                MonsterSpawnManager.SpawnMonsterPos(centerPos, eliteIndex);
                batchPositions.Add(centerPos);
                spawned++;

                for (int i = 0; i < minionToSpawn && spawned < totalTarget; i++)
                {
                    int minionIndex = baseIndex;

                    if (MonsterSpawnManager.GetScatterPositionAround(
                        centerPos,
                        boostedClusterRadius,
                        boostedClusterSpacing,
                        12,
                        out var aroundPos,
                        batchPositions
                    ))
                    {
                        MonsterSpawnManager.SpawnMonsterPos(aroundPos, minionIndex);
                        batchPositions.Add(aroundPos);
                        spawned++;
                    }
                }

                _boostedLeftThisWave--;
                continue;
            }

            bool ok = MonsterSpawnManager.SpawnMonsterSafe(
                player,
                baseIndex,
                minSpacing: 4.0f,
                maxAttempts: 20,
                batchPositions: batchPositions
            );

            if (ok) spawned++;
        }
    }

    public async UniTask SetEndGame()
    {
        await UniTask.Delay(1000);
        if (!endGame)
        {
            SceneLoader.Instance.LoadSceneAsync("Result").Forget();
            endGame = true;
        }
    }

    public bool CheckEndGame()
    {
        if (raidStarted)
        {
            foreach (var player in Players.Values)
            {
                if (!player.PlayerInfoData.IsRaidBoss && player.IsAlive)
                    return false;
            }
            return true;
        }

        return false;
    }
    private void Update()
    {
        //if (IsHost && MyPlayer != null && Input.GetKeyDown(KeyCode.G))
        //{
        //    Debug.Log($"[디버그] G키 입력 - 몬스터 인덱스 {TestMonsterIndex} 소환");
        //    MonsterSpawnManager.SpawnMonster(MyPlayer, TestMonsterIndex);
        //}
        //if (IsHost && MyPlayer != null && Input.GetKeyDown(KeyCode.H))
        //{
        //    Debug.Log("몬스터 삭제 + 소환 정지");
        //    MonsterSpawnManager.DespawnAllMonsters();
        //    stopSpawning = true;
        //}
        if (IsTutorialMode) return;

        if (raidStarted || raidTargetKstTime == DateTime.MinValue) return;

        DateTime nowKst = DateTime.UtcNow.AddHours(9);
        TimeSpan remain = raidTargetKstTime - nowKst;

        if (RaidTimerText != null)
        {
            if (remain.TotalSeconds > 0)
            {
                RaidTimerText.gameObject.SetActive(true);
                RaidTimerText.text = $"{waitRaidText} : {remain.Minutes:D2}:{remain.Seconds:D2}";
            }
            else
            {
                RaidTimerText.text = "";
                RaidTimerText.gameObject.SetActive(false);
            }
        }

        if (remain.TotalSeconds <= 0)
        {
            raidStarted = true;
            raidTargetKstTime = DateTime.MinValue;
            RaidMap.SetActive(true);
            CanAIMove = false;
            MyPlayer.RaidStart(10);
            if (IsHost)
                StartRaidPhaseAfterDelay().Forget();
        }
    }

    public float ProgressPlayTimeBeforeRaid()
    {
        DateTime nowKst = DateTime.UtcNow.AddHours(9);
        var remain = raidTargetKstTime - nowKst;
        return raidStartTime * 60 - (float)remain.TotalSeconds;
    }

    private async UniTaskVoid StartRaidPhaseAfterDelay()
    {
        if (IsHost)
        {
            int _spawnIndex = 0;
            foreach (var kvp in PlayerMap)
            {
                var info = kvp.Value;
                var player = info.GetComponent<Player>();

                if (info.IsRaidBoss)
                    player.TeleportToRaidBossSpawn();
                else
                    player.TeleportToRaidSpawn(_spawnIndex++);

                player.RecoverAll();
            }

            await UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
            MonsterSpawnManager.RaidStart();
            MonsterSpawnManager.DespawnAllMonsters();
            DespawnAllDragonEggs();
            DespawnAllIncounters();
        }

        //await UniTask.Delay(5000, cancellationToken: this.GetCancellationTokenOnDestroy());

        //MapTransformCreate.gameObject.SetActive(false);
    }
    public void DespawnAllDragonEggs()
    {
        foreach (var kvp in DragonEggs)
        {
            if (kvp != null && kvp.IsValid)
            {
                PhotonManager.Instance.Runner.Despawn(kvp);
            }
        }

        DragonEggs.Clear();
        Debug.Log("모든 드래곤알 제거 완료.");
    }
    public void DespawnAllIncounters()
    {
        foreach (var incount in Incounters)
        {
            var kvp = incount.GetComponent<NetworkObject>();

            if (kvp != null && kvp.IsValid)
            {
                PhotonManager.Instance.Runner.Despawn(kvp);
            }
        }

        Incounters.Clear();
        Debug.Log("모든 인카운터 제거 완료.");
    }
    public void SetRaidStartTimeKST(DateTime serverKstTime)
    {
        raidStarted = false;
        raidTargetKstTime = serverKstTime.AddMinutes(raidStartTime);
        Debug.Log($"[클라] 레이드 목표 KST 시간 설정됨: {raidTargetKstTime:HH:mm:ss.fff}");
    }
    //private void UpdateNameplatesByMyPlayer()
    //{
    //    if (MyPlayer == null) return;

    //    var myInfo = MyPlayer.PlayerInfoData;
    //    if (myInfo != null && myInfo.CharUICanvas != null && myInfo.CharUICanvas.gameObject.activeSelf)
    //        myInfo.CharUICanvas.gameObject.SetActive(false);

    //    Vector3 myPos = MyPlayer.transform.position;
    //    float rangeSqr = VisRangeXZ * VisRangeXZ;

    //    foreach (var kvp in PlayerMap)
    //    {
    //        var info = kvp.Value;
    //        if (info == null || info.CharUICanvas == null) continue;

    //        if (info.Object != null && info.Object.HasInputAuthority)
    //        {
    //            if (info.CharUICanvas.gameObject.activeSelf)
    //                info.CharUICanvas.gameObject.SetActive(false);
    //            continue;
    //        }

    //        var targetPlayer = info.GetComponent<Player>();
    //        if (targetPlayer == null || !targetPlayer.IsTargetable)
    //        {
    //            if (info.CharUICanvas.gameObject.activeSelf)
    //                info.CharUICanvas.gameObject.SetActive(false);
    //            continue;
    //        }

    //        Vector3 tp = targetPlayer.transform.position;
    //        float dx = tp.x - myPos.x;
    //        float dz = tp.z - myPos.z;
    //        bool shouldShow = (dx * dx + dz * dz) <= rangeSqr;

    //        if (info.CharUICanvas.gameObject.activeSelf != shouldShow)
    //            info.CharUICanvas.gameObject.SetActive(shouldShow);
    //    }
    //}
    #region 근처 오브젝트 찾기
    public struct EggPing
    {
        public Transform tr;
        public float expire;
    }

    [Header("Targeting / Nearest Monsters")]
    [SerializeField] private int nearestMonsterCount = 5;          // 캐시에 유지할 최대 목록 수 (정렬 후 상위 N)
    [SerializeField] private float nearestMonsterRefreshSec = 0.3f; // 몇 초마다 가까운 몬스터 재계산

    [SerializeField] private int minimapMonsterSlotCount = 3;       // 미니맵에 항상 표시할 슬롯 개수(3개)
    [SerializeField] private float minimapAssignIntervalSec = 3f;   // 몇 초마다 슬롯에 재할당할지 (0.3s와 별개로 운용 가능)

    // 내부 상태
    private CancellationTokenSource _minimapAssignCts;

    // 외부에서 조회 가능 (다른 시스템이 참조 가능)
    public readonly List<Monster> NearestMonsters = new List<Monster>();
    private async UniTaskVoid RefreshNearestMonstersLoop()
    {
        var cts = this.GetCancellationTokenOnDestroy();
        while (!cts.IsCancellationRequested)
        {
            RefreshNearestMonsters();

            if (MinimapCam != null)
                MinimapCam.AssignMonsterTargets(NearestMonsters);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(nearestMonsterRefreshSec), cancellationToken: cts);
            }
            catch { break; }
        }
    }

    private void RefreshNearestMonsters()
    {
        NearestMonsters.Clear();

        if (MyPlayer == null || MonsterSpawnManager == null) return;

        Vector3 myPos = MyPlayer.transform.position;

        // 활성 몬스터 수집
        var list = new List<Monster>();
        foreach (var no in MonsterSpawnManager.ActiveMonsters)
        {
            if (no == null) continue;
            var m = no.GetComponent<Monster>();
            if (m == null) continue;

            bool alive = true;
            try { alive = m.HP > 0f && !m.IsSummon; } catch { }
            if (!alive) continue;

            list.Add(m);
        }

        if (list.Count == 0) return;

        list.Sort((a, b) =>
        {
            var da = a.transform.position - myPos; da.y = 0;
            var db = b.transform.position - myPos; db.y = 0;
            return da.sqrMagnitude.CompareTo(db.sqrMagnitude);
        });

        int take = Mathf.Min(nearestMonsterCount, list.Count);
        for (int i = 0; i < take; i++)
            NearestMonsters.Add(list[i]);
    }
    #endregion

    public void ClickShopGuide()
    {
        Guide.ShopUseGuide.Flag = false;
    }

    #region AI
    private readonly Dictionary<NetworkId, PlayerInfoData> _aiInfoList = new Dictionary<NetworkId, PlayerInfoData>();
    private readonly Dictionary<NetworkId, Player> _aiPlayerList = new Dictionary<NetworkId, Player>();
    public void RegisterAI(PlayerInfoData info)
    {
        if (info == null) return;
        if (!info.Object) return;

        NetworkId id = info.Object.Id;
        if (_aiInfoList.ContainsKey(id)) return;

        if (!PhotonManager.Instance.PlayerMap.ContainsKey(id))
        {
            PhotonManager.Instance.PlayerMap.Add(id, info);
        }
        if (!PhotonManager.Instance.Players.ContainsKey(id))
        {
            PhotonManager.Instance.Players.Add(id, info.GetComponent<Player>());
        }
        _aiInfoList.Add(id, info);
        _aiPlayerList.Add(id, info.GetComponent<Player>());
        info.GetComponent<Player>().AI_Init();
    }

    public void UnregisterAI(PlayerInfoData info)
    {
        if (info == null) return;
        if (!info.Object) return;

        NetworkId id = info.Object.Id;
        if (PhotonManager.Instance.PlayerMap.ContainsKey(id))
        {
            PhotonManager.Instance.PlayerMap.Remove(id);
        }
        if (PhotonManager.Instance.Players.ContainsKey(id))
        {
            PhotonManager.Instance.Players.Remove(id);
        }
        _aiInfoList.Remove(id);
        _aiPlayerList.Remove(id);
    }

    public void CreateAI()
    {
        if (!IsHost) return;
        if (Runner == null || !Runner.IsRunning || !Runner.IsServer) return;

        if (!MyPlayerDataInfo.CanAiJoin) return;
        int max = PhotonManager.Instance.UserMaxCount;
        if (max <= 0) return;

        int humanCount = Players.Count;
        int need = max - humanCount;
        if (need <= 0) return;

        var usedSpawn = new HashSet<int>();

        // 현재 플레이어가 사용 중인 스폰 인덱스 수집
        foreach (var p in Players.Values)
        {
            if (p == null) continue;
            if (p.PlayerInfoData == null) continue;
            usedSpawn.Add(p.PlayerInfoData.SpawnIndex);
        }

        // 현재 AI가 사용 중인 스폰 인덱스 수집
        foreach (var aiInfo in _aiInfoList.Values)
        {
            if (aiInfo == null) continue;
            usedSpawn.Add(aiInfo.SpawnIndex);
        }

        // 사용하지 않는 스폰 후보 수집
        var spawnCandidates = new List<int>();
        for (int i = 0; i < SpawnPositions.Count; i++)
        {
            if (!usedSpawn.Contains(i))
                spawnCandidates.Add(i);
        }

        bool hasRaidBoss = HasAnyRaidBoss();

        for (int i = 0; i < need; i++)
        {
            int spawnIndex = spawnCandidates.Count > 0
                ? spawnCandidates[0]
                : UnityEngine.Random.Range(0, SpawnPositions.Count);

            if (spawnCandidates.Count > 0)
                spawnCandidates.RemoveAt(0);

            Vector3 pos = SpawnPositions[Mathf.Clamp(spawnIndex, 0, SpawnPositions.Count - 1)];
            var prefab = PhotonManager.Instance.Player;

            // 레이드보스가 없으면 첫 번째 AI를 레이드보스로 지정
            bool makeRaidBoss = !hasRaidBoss && i == 0;

            var obj = Runner.Spawn(
                prefab,
                pos,
                Quaternion.identity,
                PlayerRef.None,
                onBeforeSpawned: (r, o) =>
                {
                    var info = o.GetComponent<PlayerInfoData>();
                    if (info == null) return;

                    int heroIndex = UnityEngine.Random.Range(0, 3);
                    int heroSkin = 0;
                    int dragonIndex = 0;
                    int dragonSkin = 0;

                    string aiName = "AI_" + (_aiInfoList.Count + 1).ToString();

                    // AI 초기 데이터 세팅
                    info.StateAuthority_InitAI(
                        aiName,
                        spawnIndex,
                        heroIndex,
                        heroSkin,
                        dragonIndex,
                        dragonSkin,
                        PlayerRef.None,
                        false
                    );

                    info.IsIngameScene = true;

                    if (makeRaidBoss)
                        info.IsRaidBoss = true;
                }
            );

            if (obj != null)
            {
                var player = obj.GetComponent<Player>();
                if (player != null)
                {
                    player.TeleportToSpawnPoint();
                    player.RPC_SpawnCharacterModel();
                }

                // 스폰된 AI를 목록에 등록
                var aiInfo = obj.GetComponent<PlayerInfoData>();
                if (aiInfo != null)
                    RegisterAI(aiInfo);
            }

            if (makeRaidBoss)
                hasRaidBoss = true;
        }
    }
    public void DestroyAI()
    {
        if (!IsHost) return;

        var targets = new List<NetworkObject>();

        foreach (var aiInfo in _aiInfoList.Values)
        {
            if (aiInfo == null) continue;
            if (!aiInfo.Object) continue;
            targets.Add(aiInfo.Object);
        }

        foreach (var obj in targets)
        {
            if (obj != null && obj.IsValid)
                Runner.Despawn(obj);
        }
    }
    #endregion

    public bool HasAnyRaidBoss()
    {
        // 실제 플레이어 중 레이드보스가 있는지 확인
        foreach (var p in Players.Values)
        {
            if (p == null) continue;
            if (p.PlayerInfoData == null) continue;
            if (p.PlayerInfoData.IsRaidBoss) return true;
        }

        // AI 중 레이드보스가 있는지 확인
        foreach (var aiInfo in _aiInfoList.Values)
        {
            if (aiInfo == null) continue;
            if (aiInfo.IsRaidBoss) return true;
        }

        return false;
    }
}

[Serializable]
public class InGameGuideData
{
    [Serializable]
    public class InGameGuideValue
    {
        private bool _value = false;
        public List<GameObject> Object;

        public bool Flag
        {
            get => _value;
            set
            {
                _value = value;
                foreach (var o in Object)
                    o?.SetActive(_value);

                InGameManager.Instance.Guide.Check();
            }
        }

        public bool Active
        {
            get
            {
                foreach (var o in Object)
                {
                    if (o == null)
                        continue;

                    if (o.activeSelf)
                        return true;
                }

                return false;
            }
        }
    }

    public InGameGuideValue OpenStatGuide;
    public InGameGuideValue StatGuide;
    public InGameGuideValue CloseStatGuide;
    public InGameGuideValue SkillGuide;
    public InGameGuideValue CloseTabGuide;
    public InGameGuideValue EquipActiveSkillGuide;
    public InGameGuideValue SetActiveSkillGuide;
    public InGameGuideValue UseActiveSkillGuide;
    public InGameGuideValue ShopOpenGuide;
    public InGameGuideValue DragonObjectGuide;
    public InGameGuideValue ShopUseGuide;
    public InGameGuideValue InvenOpenGuide;
    public InGameGuideValue InvenUseGuide;

    [HideInInspector]
    public bool NeedCheck = false;

    [HideInInspector]
    public bool CloseStatGuideFlag;
    [HideInInspector]
    public bool SetActiveSkillGuideFlag;
    [HideInInspector]
    public bool UseActiveSkillGuideFlag;

    [HideInInspector]
    public bool NeedItemGuide = false;

    public void SetGuide()
    {
        if (PlayerPrefs.GetInt("Guide", 0) != 1)
        {
            NeedCheck = true;

            var raid = InGameManager.Instance.MyPlayer.PlayerInfoData.IsRaidBoss;

            OpenStatGuide.Flag = !raid;
            StatGuide.Flag = !raid;
            CloseStatGuide.Flag = false;

            SkillGuide.Flag = true;
            EquipActiveSkillGuide.Flag = true;
            SetActiveSkillGuide.Flag = false;
            UseActiveSkillGuide.Flag = false;
            CloseTabGuide.Flag = false;
            DragonObjectGuide.Flag = raid;
            ShopOpenGuide.Flag = !raid;

            ShopUseGuide.Flag = !raid;

            InvenOpenGuide.Flag = false;
            InvenUseGuide.Flag = false;

            CloseStatGuideFlag = true;
            SetActiveSkillGuideFlag = true;
            UseActiveSkillGuideFlag = true;
        }
        else
        {
            NeedCheck = false;

            OpenStatGuide.Flag = false;
            StatGuide.Flag = false;
            CloseStatGuide.Flag = false;

            SkillGuide.Flag = false;
            EquipActiveSkillGuide.Flag = false;
            SetActiveSkillGuide.Flag = false;
            UseActiveSkillGuide.Flag = false;
            CloseTabGuide.Flag = false;
            DragonObjectGuide.Flag = false;
            ShopOpenGuide.Flag = false;

            ShopUseGuide.Flag = false;

            InvenOpenGuide.Flag = false;
            InvenUseGuide.Flag = false;

            CloseStatGuideFlag = false;
            SetActiveSkillGuideFlag = false;
            UseActiveSkillGuideFlag = false;
        }
    }

    public void Check()
    {
        if (!NeedCheck)
            return;

        if (OpenStatGuide.Flag || StatGuide.Flag || CloseStatGuide.Flag)
            return;

        if (SkillGuide.Flag)
            return;

        if (EquipActiveSkillGuide.Flag || SetActiveSkillGuide.Flag || UseActiveSkillGuide.Flag)
            return;

        PlayerPrefs.SetInt("Guide", 1);
    }
}