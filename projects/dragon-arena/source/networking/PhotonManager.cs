using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonManager : Singleton<PhotonManager>, INetworkRunnerCallbacks
{
    protected override bool IsDontDestroy => true;

    [SerializeField] private NetworkRunner _runner;
    private readonly List<SessionInfo> sessionList = new();
    public List<SessionInfo> SessionList => sessionList;

    public NetworkObject Player;
    [HideInInspector] public WaitingRoomPanel waitingRoomPanel;

    public string CurrentRoomName { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsJoined = false;

    public Dictionary<NetworkId, PlayerInfoData> PlayerMap = new();
    public Dictionary<NetworkId, Player> Players = new();

    private Dictionary<PlayerRef, NetworkId> _playerRefToNetId = new();

    public PlayerInfoData MyPlayerDataInfo;
    public bool IsHost = false;
    public Player MyPlayer;
    public NetworkRunner Runner => _runner;
    public int UserMaxCount;

    // 매칭 관련
    public int MyScore => UserData.Instance?.Score ?? 1500;
    private const int MATCH_SIZE = 10;
    private const int MATCH_ELO_RANGE = 300;
    private bool _isMatchmakingRoom = false;

    // 비밀방 여부
    public bool IsSecretRoom { get; private set; } = false;

    public List<int> randIndex = new();

    private byte[] connectionToken;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();
    private InputManager _input;

    private bool _isFastJoining = false;
    public bool IsFastJoining => _isFastJoining;

    private const int SNAPSHOT_INTERVAL_MS = 5000; // 5초마다 전송

    private bool _emergencyExitInProgress = false; // 중복 방지

    private bool _isPhotonInit = false;
    public bool _IsPhotonInit => _isPhotonInit;

    // === Runner Watchdog ===
    private CancellationTokenSource _runnerWatchCts;
    private bool _runnerRecreateInProgress = false;
    private float _lastRunnerCreateAttemptTime = -999f;

    // 간격/디바운스(필요시 조정 가능)
    private const float RUNNER_WATCH_INTERVAL = 1f;
    private const float RUNNER_CREATE_DEBOUNCE = 5f;    // 5초 내 중복 생성 시도 방지
    private const float RUNNER_ABSENCE_GRACE = 1f;      // 사라진 직후 약간의 유예

    private bool _cloudReconnecting = false;   // CloudConnectionLost(reconnecting=true) 동안 true
    private bool _clientReconnected = false;   // 끊겼던 클라가 새 호스트로 다시 붙으면 true

    private bool isTutorialRoom = false;
    public bool IsTutorialRoom => isTutorialRoom;
    private const int teamMaxCount = 5;
    public int TeamMaxCount => teamMaxCount;

    protected override void Awake()
    {
        base.Awake();

        Application.quitting += OnAppQuitting;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

        StartRunnerWatchdog();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        StopRunnerWatchdog();

        Application.quitting -= OnAppQuitting;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
    }


    private void Start()
    {
        if (_runner == null)
        {
            Init().Forget();
        }
        else
        {
            Debug.Log("[PhotonManager] 이미 Runner가 존재하므로 로비 입장 생략");
        }
    }
    public void StartTutorial()
    {
        SceneLoader.Instance.LoadSceneAsync("Tutorial").Forget();
    }
    public void SetRunner(NetworkRunner r) => _runner = r;

    private async UniTaskVoid PushNewSnapshot()
    {
        if (_runner == null || !_runner.IsServer) return;
        await _runner.PushHostMigrationSnapshot();
    }

    // === 초기화 ===
    public async UniTask Init()
    {
        Debug.Log("[PhotonManager] 초기화 시작");

        if (_runner != null && !_runner.IsRunning)
        {
            Debug.Log("[PhotonManager] 기존 Runner IsRunning=false → 정리");
            SafeDestroyRunner();
        }

        if (_runner != null)
        {
            await _runner.Shutdown(shutdownReason: ShutdownReason.Ok);
            _runner.RemoveCallbacks(this);
            NetworkRunner.CloudConnectionLost -= OnCloudConnectionLost;
            Destroy(_runner.gameObject);
            _runner = null;
        }

        _spawned.Clear();
        PlayerMap.Clear();
        Players.Clear();
        _playerRefToNetId.Clear();
        randIndex = FunctionCWS.Function_WS.GetRandom<int>(20);
        MyPlayer = null;
        MyPlayerDataInfo = null;
        IsJoined = false;
        _isFastJoining = false;
        _isMatchmakingRoom = false;
        IsSecretRoom = false;
        IsVisible = true;
        isTutorialRoom = false;

        if (SceneLoader.Instance.CurrentSceneType == SceneType.Lobby)
        {
            Debug.Log("[PhotonManager] 로비씬, StartLobbyAndGetSessionList 대기");
            await StartLobbyAndGetSessionList();
            Debug.Log("[PhotonManager] 로비 입장 완료");
        }
        else
        {
            Debug.Log("[PhotonManager] 로비씬 아님, Init 종료");
        }
    }

    // isSecret 이 true 면 처음부터 비밀방
    public void CreateRoom(string roomName, bool isSecret)
    {
        if (IsJoined) return;

        bool isVisible = !isSecret;

        // 내부 상태 플래그 미리 세팅
        IsSecretRoom = isSecret;
        IsVisible = isVisible;

        StartGameWithSceneChange(roomName, isVisible, isSecret).Forget();
    }

    // === 수동 검색 참가: WaitingRoom 사용 ===
    public void TryJoinRoom(string keyword)
    {
        if (IsJoined) return;
        JoinRoom(keyword).Forget();
    }

    private async UniTask JoinRoom(string keyword)
    {
        IsJoined = true;
        await StartGame(GameMode.Client, keyword, /*isVisible:*/ false, /*forceWaitingRoom:*/ true, /*isMatchmaking:*/ false);
    }

    public void FastJoin()
    {
        if (IsJoined || _isFastJoining) return;
        _isFastJoining = true;
        FastJoinMatchmaking().Forget();
    }
    public async void CancelFastJoin()
    {
        if (!_isFastJoining)
        {
            Debug.Log("[Matchmaking] FastJoin 취소 요청 무시 (진행중 아님)");
            return;
        }

        ClearPlayerCaches();
        DestroyAllLocalPlayers();

        _isFastJoining = false;
        Debug.Log("[Matchmaking] FastJoin 취소 시작");

        if (_runner == null)
        {
            Debug.Log("[Matchmaking] Runner 없음 → 바로 로비 복귀");
            await SceneLoader.Instance.LoadSceneAsync("Lobby");
            // FIX: SceneLoader가 Init()을 호출하므로 여기서 Init() 금지
            return;
        }

        // --- Player Despawn 처리 ---
        try
        {
            if (MyPlayer != null && _runner.IsRunning)
            {
                Debug.Log("[Matchmaking] 로컬 플레이어 직접 Despawn 시도");
                _runner.Despawn(MyPlayer.Object);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Matchmaking] Player Despawn 중 예외: {ex.Message}");
        }

        // 캐시 초기화
        MyPlayer = null;
        MyPlayerDataInfo = null;
        PlayerMap.Clear();
        Players.Clear();
        _playerRefToNetId.Clear();

        // --- 호스트인 경우 ---
        if (_runner.IsServer)
        {
            Debug.Log("[Matchmaking] Host → FastJoin 취소로 방 나가기 및 HostMigration 시도");

            var nextHost = _runner.ActivePlayers
                .Where(p => p != _runner.LocalPlayer)
                .FirstOrDefault();

            if (nextHost != PlayerRef.None)
            {
                Debug.Log($"[Matchmaking] HostMigration: 다음 호스트 {nextHost}");

                await _runner.PushHostMigrationSnapshot();
                await _runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
            }
            else
            {
                Debug.Log("[Matchmaking] 남은 유저 없음 → 방 완전 종료");
                await EndGame(); // 이 안에서 로비 로드
            }
        }
        // --- 클라이언트인 경우 ---
        else
        {
            Debug.Log("[Matchmaking] Client → FastJoin 취소로 방 퇴장 및 로비 복귀");
            await _runner.Shutdown(shutdownReason: ShutdownReason.Ok);
            await SceneLoader.Instance.LoadSceneAsync("Lobby");
        }

        // 상태 정리
        IsJoined = false;
        MyPlayer = null;
        MyPlayerDataInfo = null;
        GameManager.Instance.OpenLodingPanel(99);
        CheckGame().Forget();
        // FIX: 로비 씬에서는 SceneLoader가 PhotonManager.Init()을 호출하므로 재호출 금지
        Debug.Log("[Matchmaking] FastJoin 취소 완료 및 로비 복귀 완료");
    }
    public async UniTask CheckGame()
    {
        await UniTask.WaitUntil(() => _IsPhotonInit);
        GameManager.Instance.CloseLodingPanel();
    }
    private async UniTask FastJoinMatchmaking()
    {
        IsJoined = true;

        // 1) 매칭 가능한 세션 탐색(±300, MM모드, IsOpen)
        SessionInfo? target = FindBestMatchmakingSession(MyScore);

        if (target == null)
        {
            string sessionName = $"MM_{MyScore}_{Guid.NewGuid().ToString()[..6]}";
            Debug.Log($"[Matchmaking] No room found. Creating host room: {sessionName}");

            await StartGame(
                GameMode.Host,
                sessionName,
                isVisible: true,
                forceWaitingRoom: false,
                isMatchmaking: true
            );

        }
        else
        {
            Debug.Log($"[Matchmaking] Found room: {target.Name}");
            await StartGame(
                GameMode.Client,
                target.Name,
                isVisible: true,
                forceWaitingRoom: false,
                isMatchmaking: true
            );
        }
    }

    private SessionInfo? FindBestMatchmakingSession(int myScore)
    {
        int min = myScore - MATCH_ELO_RANGE;
        int max = myScore + MATCH_ELO_RANGE;

        int myBundle = ServerDataManager.Instance.BundleVersion;

        var candidates = sessionList.Where(s =>
        {
            if (!s.IsOpen) return false;
            if (!s.IsVisible) return false;

            if (!s.Properties.TryGetValue("Mode", out var mode)) return false;
            if (!"MM".Equals((string)mode)) return false;

            if (!s.Properties.TryGetValue("ScoreMin", out var pMin)) return false;
            if (!s.Properties.TryGetValue("ScoreMax", out var pMax)) return false;

            if (!s.Properties.TryGetValue("BundleVersion", out var bVer)) return false;
            int roomBundle = (int)bVer;

            if (roomBundle != myBundle) return false;

            int roomMin = (int)pMin;
            int roomMax = (int)pMax;

            bool allowed = (myScore >= roomMin && myScore <= roomMax);

            int curPlayers = s.PlayerCount;
            bool hasSlot = curPlayers < MATCH_SIZE;

            return allowed && hasSlot;
        }).ToList();

        if (candidates.Count == 0) return null;

        return candidates.OrderByDescending(c => c.PlayerCount).First();
    }

    public void RefreshRooms()
    {
        if (IsJoined) return;
        EndGame().Forget();
        StartLobbyAndGetSessionList().Forget();
    }
    private static void DestroyAllLocalPlayers()
    {
        var allPlayers = PhotonManager.Instance.Players.Values;

        foreach (var p in allPlayers)
        {
            if (!p) continue;
            Destroy(p.gameObject);
        }
    }
    private void ClearPlayerCaches()
    {
        try
        {
            if (Runner != null && Runner.IsServer)
            {
                // 서버면 스폰했던 것들을 최대한 정석적으로 내리기
                foreach (var kv in _spawned)
                    if (kv.Value && kv.Value.IsSpawnable) Runner.Despawn(kv.Value);
            }
        }
        catch { }

        _spawned.Clear();
        Players.Clear();
        PlayerMap.Clear();
        _playerRefToNetId.Clear();
        MyPlayer = null;
        MyPlayerDataInfo = null;
        IsJoined = false;
        _isMatchmakingRoom = false;
        _isPhotonInit = false;
        isTutorialRoom = false;

        IsSecretRoom = false;
        IsVisible = true;
    }

    private async UniTaskVoid StartGameWithSceneChange(string sessionName, bool isVisible = false, bool isSecretRoom = false)
    {
        IsJoined = true;
        await StartGame(
            GameMode.Host,
            sessionName,
            isVisible,
            /*forceWaitingRoom:*/ true,
            /*isMatchmaking:*/ false,
            isSecretRoom
        );
    }

    // === StartGame 공통 진입 ===
    private async UniTask StartGame(
    GameMode mode,
    string sessionName,
    bool isVisible,
    bool forceWaitingRoom,
    bool isMatchmaking,
    bool isSecretRoom = false)
    {
        GameManager.Instance.OpenLodingPanel();
        if (_runner == null || !_runner.LobbyInfo.IsValid)
        {
            Debug.LogWarning("[StartGame] Runner가 준비되지 않음. 로비 재초기화 시도");
            IsJoined = false;
            await StartLobbyAndGetSessionList();
            if (_runner == null || !_runner.LobbyInfo.IsValid)
            {
                GameManager.Instance.OpenInfoPanel("ConnectionRefused");
                Debug.LogError("[StartGame] Runner 준비 실패");
                return;
            }
        }

        CurrentRoomName = sessionName;
        IsHost = mode == GameMode.Host;
        _isMatchmakingRoom = isMatchmaking;

        if (isMatchmaking)
        {
            IsSecretRoom = false;
            IsVisible = true;
        }
        else
        {
            IsSecretRoom = isSecretRoom;
            IsVisible = isVisible;
        }

        var connToken = BuildConnectionTokenWithScoreAndVersion(MyScore);

        string modeStr;
        int hiddenInUi;

        if (isMatchmaking)
        {
            modeStr = "MM";
            hiddenInUi = 1;
        }
        else
        {
            if (isSecretRoom)
            {
                modeStr = "ManualSecret";
                hiddenInUi = 0;
                isVisible = false;
                IsVisible = false;
            }
            else
            {
                modeStr = "Manual";
                hiddenInUi = 0;
            }
        }

        var props = new Dictionary<string, SessionProperty>
        {
            { "CreatorID", SteamLoginManager.Instance.GetSteamID().ToString() },
            { "MaxPlayers", MATCH_SIZE },
            { "RoomName", CurrentRoomName },
            { "Mode", modeStr },
            { "HiddenInUI", hiddenInUi },
            { "BundleVersion", ServerDataManager.Instance.BundleVersion }
        };

        if (isMatchmaking)
        {
            props["ScoreMin"] = MyScore - MATCH_ELO_RANGE;
            props["ScoreMax"] = MyScore + MATCH_ELO_RANGE;
        }

        // FIX: Fusion SceneRef는 "지금 활성 씬"을 주고, 실제 WaitingRoom 전환은 이후 SceneLoader로 처리
        int activeIndex = isMatchmaking ? SceneManager.GetActiveScene().buildIndex : SceneManager.GetActiveScene().buildIndex + 1;
        var sceneRef = SceneRef.FromIndex(activeIndex);

        var args = new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            Scene = sceneRef, // FIX
            SceneManager = _runner.GetComponent<NetworkSceneManagerDefault>(),
            PlayerCount = MATCH_SIZE,
            IsOpen = true,
            IsVisible = isVisible,
            SessionProperties = props,
            ConnectionToken = connToken,
            HostMigrationResume = HostMigrationResume,
            HostMigrationToken = null
        };

        IsVisible = isVisible;
        UserMaxCount = MATCH_SIZE;

        var result = await _runner.StartGame(args);

        if (!result.Ok)
        {
            IsJoined = false;
            Debug.LogError($"StartGame 실패: {result.ShutdownReason}");
            Init().Forget();
            if (result.ShutdownReason == ShutdownReason.ConnectionRefused)
            {
                GameManager.Instance.CloseLodingPanel();
                GameManager.Instance.OpenInfoPanel("ConnectionRefused");
            }
            return;
        }
        GameManager.Instance.CloseLodingPanel();
        Debug.Log("[PhotonManager] StartGame 성공");

        if (forceWaitingRoom && !_isMatchmakingRoom)
        {
            await UniTask.WaitUntil(() => WaitingRoomManager.Instance && WaitingRoomManager.Instance.waitingRoomPanel);
            SceneLoader.Instance.LoadSceneAsync("WaitingRoom").Forget();
            Debug.Log("[PhotonManager] WaitingRoom 씬 로드 요청");

            if (_runner.IsServer)
            {
                Debug.Log("[PhotonManager] Starting periodic snapshot (host only)");
                InvokeRepeating(nameof(PushNewSnapshot),
                                SNAPSHOT_INTERVAL_MS / 1000f,
                                SNAPSHOT_INTERVAL_MS / 1000f);
            }
            waitingRoomPanel = WaitingRoomManager.Instance.waitingRoomPanel;
            waitingRoomPanel.SetRoomInfo(IsVisible);
        }
        else
        {
            if (_runner.IsServer)
            {
                Debug.Log("[Matchmaking] Host active. Waiting for players to reach 6...");
                InvokeRepeating(nameof(PushNewSnapshot),
                                SNAPSHOT_INTERVAL_MS / 1000f,
                                SNAPSHOT_INTERVAL_MS / 1000f);
            }
            else
            {
                Debug.Log("[Matchmaking] Client joined. Waiting for host to start...");
            }
        }
    }


    private byte[] BuildConnectionTokenWithScoreAndVersion(int score)
    {
        var baseToken = Fusion.HostMigration.ConnectionTokenUtils.NewToken();

        byte[] scoreBytes = BitConverter.GetBytes(score);
        byte[] bundleBytes = BitConverter.GetBytes(ServerDataManager.Instance.BundleVersion);

        byte[] final = new byte[baseToken.Length + scoreBytes.Length + bundleBytes.Length];

        Buffer.BlockCopy(baseToken, 0, final, 0, baseToken.Length);
        Buffer.BlockCopy(scoreBytes, 0, final, baseToken.Length, scoreBytes.Length);
        Buffer.BlockCopy(bundleBytes, 0, final, baseToken.Length + scoreBytes.Length, bundleBytes.Length);

        connectionToken = final;

        return final;
    }
    public async UniTask MoveToIngame()
    {
        if (SceneLoader.Instance.CurrentSceneType == SceneType.Ingame)
        {
            Debug.Log("[MoveToIngame] 이미 Ingame 상태");
            return;
        }

        var runner = _runner;
        if (runner == null)
        {
            Debug.LogError("Runner 없음");
            return;
        }

        var sceneManager = runner.SceneManager;
        if (sceneManager == null)
        {
            Debug.LogError("SceneManager 없음");
            return;
        }
        InputManager.Instance.LockEscKey = true;
        //if (Runner.IsSceneAuthority)
        //{
        //    await Runner.UnloadScene(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));
        //    await Runner.LoadScene(SceneRef.FromIndex(2), LoadSceneMode.Additive);
        //}
        await UniTask.WaitUntil(() => MyPlayer != null);
        await SceneLoader.Instance.LoadSceneAsync("Ingame");
        await UniTask.WaitUntil(() => InGameManager.Instance);
        //SceneLoader.Instance.LoadSceneInfo("Ingame");
        await InGameManager.Instance.SpawnNetworkManagers();

        InGameManager.Instance.Init(MyPlayer);

        MyPlayerDataInfo.RPC_IngameCheck();
        InputManager.Instance.LockEscKey = false;
        if (runner.IsServer)
        {
            await UniTask.WaitUntil(() =>
                PlayerMap.Values.All(p => p != null && p.IsIngameScene)
            );

            InGameManager.Instance.StartGame();
        }
    }
    public void SetFastJoin(bool _value)
    {
        _isFastJoining = _value;
    }
    private async UniTask StartLobbyAndGetSessionList()
    {
        // FIX: 존재하지만 죽어 있는 Runner 정리
        if (_runner != null && !_runner.IsRunning)
        {
            Debug.Log("[StartLobby] 기존 Runner는 존재하지만 IsRunning=false → 정리");
            SafeDestroyRunner();
        }

        if (_runner != null)
        {
            Debug.Log("[StartLobby] Runner 이미 존재, 스킵");
            return;
        }

        connectionToken = Fusion.HostMigration.ConnectionTokenUtils.NewToken();

        var go = new GameObject("NetworkRunner") { transform = { parent = transform } };
        _runner = go.AddComponent<NetworkRunner>();
        DontDestroyOnLoad(go);

        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);
        _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        NetworkRunner.CloudConnectionLost -= OnCloudConnectionLost;
        NetworkRunner.CloudConnectionLost += OnCloudConnectionLost;

        _input = InputManager.Instance;

        await _runner.JoinSessionLobby(SessionLobby.ClientServer);

        // 로비 진입 대기는 LobbyInfo만 확인 (SessionInfo는 로비에서 false일 수 있음)
        await UniTask.WaitUntil(() =>
            _runner != null &&
            _runner.LobbyInfo.IsValid
        );

        Debug.Log("[StartLobby] LobbyInfo Valid");
    }



    private void OnCloudConnectionLost(NetworkRunner runner, ShutdownReason reason, bool reconnecting)
    {
        Debug.Log($"[CloudConnectionLost] reason={reason}, reconnecting={reconnecting}");
        _cloudReconnecting = reconnecting;   // 재접속 대기 on/off
        if (!reconnecting)
            WaitForHostMigrationOrReturnToLobby().Forget();
    }
    private async UniTaskVoid WaitForClientReattachOrTimeout(float maxWaitSeconds)
    {
        Debug.Log("[ClientReattach] 재접속 대기 시작");
        // 로딩 패널: 네가 준 API 사용 (시간은 표시용, 콜백은 닫기용)
        GameManager.Instance.OpenLodingPanel(maxWaitSeconds);

        float waited = 0f;
        const float step = 0.5f;

        while (waited < maxWaitSeconds)
        {
            // 1) OnConnectedToServer에서 신호가 오면 성공
            if (_clientReconnected)
            {
                Debug.Log("[ClientReattach] 재접속 성공");
                GameManager.Instance.CloseLodingPanel();
                return;
            }

            // 2) Runner가 살아 있고(=IsRunning), 세션/플레이어가 다시 유효해지면 성공으로 간주
            if (_runner != null && _runner.IsRunning)
            {
                // ActivePlayers가 최소 1 이상이 되거나(본인 포함) SessionInfo 유효해지면 복구된 것으로 판단
                // (Fusion 버전에 따라 세부 조건은 달라질 수 있으니, 가장 보수적 조건 2개를 함께 체크)
                bool playersBack = _runner.ActivePlayers != null && _runner.ActivePlayers.Count() > 0;
                bool sessionValid = _runner.SessionInfo != null && _runner.SessionInfo.IsValid;

                if (playersBack || sessionValid)
                {
                    Debug.Log("[ClientReattach] 러너/세션 복구 감지");
                    GameManager.Instance.CloseLodingPanel();
                    return;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(step), cancellationToken: this.GetCancellationTokenOnDestroy());
            waited += step;
        }

        // 타임아웃: 이제 정리하고 로비 복귀
        Debug.Log("[ClientReattach] 타임아웃 - 로비 복귀");
        GameManager.Instance.CloseLodingPanel();

        try
        {
            if (_runner != null)
                await _runner.Shutdown(shutdownReason: ShutdownReason.Ok);
        }
        catch { }

        ClearPlayerCaches();
        DestroyAllLocalPlayers();

        await SceneLoader.Instance.LoadSceneAsync("Lobby");
    }

    public void UpdateSessionList(List<SessionInfo> list)
    {
        sessionList.Clear();
        sessionList.AddRange(list);
        _isPhotonInit = true;

        // === 디버그 출력 추가 ===
        if (sessionList.Count == 0)
        {
            Debug.Log("[SessionList] 현재 열린 방이 없습니다.");
        }
        else
        {
            Debug.Log($"[SessionList] 총 {sessionList.Count}개 방 발견:");
            foreach (var s in sessionList)
            {
                string roomName = s.Name;
                int playerCount = s.PlayerCount;
                int maxPlayers = s.MaxPlayers;
                bool isVisible = s.IsVisible;
                bool isOpen = s.IsOpen;

                string mode = s.Properties.TryGetValue("Mode", out var m) ? (string)m : "Unknown";
                string creator = s.Properties.TryGetValue("CreatorID", out var c) ? c.ToString() : "???";

                Debug.Log($" └─ [Room] {roomName} | {playerCount}/{maxPlayers} | {mode} | {(isOpen ? "Open" : "Closed")} | {(isVisible ? "Visible" : "Hidden")} | Creator: {creator}");
            }
        }

        // === UI에는 매칭방(HiddenInUI=true) 숨김 ===
        var uiList = sessionList.FindAll(s =>
        {
            if (!(s.IsOpen && s.IsVisible)) return false;
            if (s.Properties.TryGetValue("HiddenInUI", out var hidden) && (int)hidden == 1)
                return false;
            return true;
        });

        LobbyManager.Instance?.sessionPanel?.SetSessionList(uiList);
    }

    public async UniTask EndGame(Action _SuccesccCallback = null)
    {
        await _runner.PushHostMigrationSnapshot();
        ClearPlayerCaches();
        DestroyAllLocalPlayers();

        if (_runner != null && _runner.IsServer)
        {
            Debug.Log("[EndGame] Host 종료 경로");
            await _runner.Shutdown(shutdownReason: ShutdownReason.Ok);

            SafeDestroyRunner();

            await SceneLoader.Instance.LoadSceneAsync("Lobby", _SuccesccCallback);
            return;
        }

        Debug.Log("[EndGame] 클라이언트 종료 경로");
        if (_runner != null)
            await _runner.Shutdown(shutdownReason: ShutdownReason.Ok);

        SafeDestroyRunner();

        await SceneLoader.Instance.LoadSceneAsync("Lobby", _SuccesccCallback);
    }



    public void CloseRoom()
    {
        if (_runner != null && _runner.IsServer && _runner.SessionInfo != null)
            _runner.SessionInfo.IsOpen = false;
    }
    public void OpenRoom()
    {
        if (_runner != null && _runner.IsServer && _runner.SessionInfo != null)
            _runner.SessionInfo.IsOpen = true;
    }
    public void SetRoomVisibility(bool v)
    {
        IsVisible = v;

        if (_runner != null && _runner.IsServer && _runner.SessionInfo != null)
            _runner.SessionInfo.IsVisible = v;
    }
    public void SetSecretRoom(bool isSecret)
    {
        IsSecretRoom = isSecret;
        SetRoomVisibility(!isSecret);
    }
    private void SafeDestroyRunner()
    {
        if (_runner == null) return;

        try { _runner.RemoveCallbacks(this); } catch { }
        NetworkRunner.CloudConnectionLost -= OnCloudConnectionLost;

        var go = _runner.gameObject;
        _runner = null;

        if (go) Destroy(go);
    }

    private void StartRunnerWatchdog()
    {
        StopRunnerWatchdog();
        _runnerWatchCts = new CancellationTokenSource();
        RunnerWatchLoop(_runnerWatchCts.Token).Forget();
    }

    private void StopRunnerWatchdog()
    {
        try { _runnerWatchCts?.Cancel(); } catch { }
        try { _runnerWatchCts?.Dispose(); } catch { }
        _runnerWatchCts = null;
    }

    /// <summary>
    /// 주기적으로 Runner 상태를 검사하고, 필요 조건을 만족하면 새 Runner를 만든다.
    /// 조건:
    ///  - 현재 씬이 Lobby
    ///  - 게임에 조인 중이 아님(IsJoined=false)
    ///  - Runner가 null이거나 IsRunning=false
    ///  - HostMigration/Shutdown 등으로 잠깐 비는 시간은 GRACE 만큼 유예
    /// </summary>
    private async UniTaskVoid RunnerWatchLoop(CancellationToken ct)
    {
        // 약간의 초기 유예
        await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 씬 타입 확인 (SceneLoader 싱글톤 없을 수 있는 초기 프레임 대비)
                var loader = SceneLoader.Instance;
                var sceneType = loader ? loader.CurrentSceneType : SceneType.Lobby;

                bool inLobby = (sceneType == SceneType.Lobby);
                bool hasRunner = (_runner != null);
                bool runnerHealthy = hasRunner;
                bool canAttempt =
                    inLobby &&
                    !_runnerRecreateInProgress &&
                    !_cloudReconnecting &&
                    (Time.unscaledTime - _lastRunnerCreateAttemptTime) >= RUNNER_CREATE_DEBOUNCE;

                if (!runnerHealthy && canAttempt)
                {
                    // 사라진 직후의 짧은 유예
                    await UniTask.Delay(TimeSpan.FromSeconds(RUNNER_ABSENCE_GRACE), cancellationToken: ct);
                    if (ct.IsCancellationRequested) break;

                    // 다시 한 번 조건 확인(유예 중 회복했을 수도 있음)
                    inLobby = (SceneLoader.Instance ? SceneLoader.Instance.CurrentSceneType : SceneType.Lobby) == SceneType.Lobby;
                    hasRunner = (_runner != null);
                    runnerHealthy = hasRunner;
                    canAttempt =
                        inLobby &&
                        !_runnerRecreateInProgress &&
                        (Time.unscaledTime - _lastRunnerCreateAttemptTime) >= RUNNER_CREATE_DEBOUNCE;

                    if (!runnerHealthy && canAttempt)
                    {
                        _runnerRecreateInProgress = true;
                        _lastRunnerCreateAttemptTime = Time.unscaledTime;

                        // 혹시 죽어있는 Runner 잔존 시 정리
                        if (hasRunner && !_runner.IsRunning)
                            SafeDestroyRunner();

                        Debug.Log("[RunnerWatchdog] Runner 부재 감지 → 로비에서 새 Runner 생성 시도");
                        await StartLobbyAndGetSessionList();  // 새 Runner 생성 + Lobby Join

                        _runnerRecreateInProgress = false;
                    }
                }
            }
            catch (OperationCanceledException) { /* 종료 */ }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunnerWatchdog] 예외: {ex.Message}");
                _runnerRecreateInProgress = false;
            }

            // 다음 점검까지 대기
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(RUNNER_WATCH_INTERVAL), cancellationToken: ct);
            }
            catch (OperationCanceledException) { /* 종료 */ }
        }
    }

    /// <summary>
    /// 외부에서 즉시 재시도 유도하고 싶을 때 호출(선택)
    /// </summary>
    public void NudgeRunnerWatchdog()
    {
        _lastRunnerCreateAttemptTime = -999f;
    }
    private void RegisterPlayerObject(PlayerRef playerRef, NetworkObject obj)
    {
        if (obj == null || !obj.IsValid) return;

        var p = obj.GetComponent<Player>();
        var info = obj.GetComponent<PlayerInfoData>();

        _spawned[playerRef] = obj;
        _playerRefToNetId[playerRef] = obj.Id;

        if (p != null) Players[obj.Id] = p;
        if (info != null) PlayerMap[obj.Id] = info;
    }
    #region Callbacks

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[OnPlayerJoined] {player}");

        if (_playerRefToNetId.ContainsKey(player))
        {
            Debug.Log($"[OnPlayerJoined] {player} 는 HostMigration 으로 이미 복구됨. Spawn 생략");
            return;
        }

        if (!runner.IsServer) return;

        var prefab = Player;
        Vector3 spawnPos = new Vector3(player.RawEncoded % runner.Config.Simulation.PlayerCount, -1.17f, 0);

        var obj = runner.Spawn(prefab, spawnPos, Quaternion.identity, player);
        if (obj == null) return;

        RegisterPlayerObject(player, obj);

        if (_isMatchmakingRoom && SceneLoader.Instance.CurrentSceneType == SceneType.Lobby)
        {
            int cur = runner.ActivePlayers.Count();
            Debug.Log($"[Matchmaking] Current Players: {cur}/{MATCH_SIZE}");

            var netId = obj.Id;
            if (PlayerMap.TryGetValue(netId, out var pInfo))
            {
                int spawnIndex = randIndex[(cur - 1) % randIndex.Count];
                pInfo.RPC_AssignSpawnIndex(spawnIndex);
                Debug.Log($"[FastJoin] {pInfo.NickName} -> SpawnIndex {spawnIndex}");
            }

            if (cur >= MATCH_SIZE)
                StartMatchFromMatchmaking().Forget();
        }
    }


    // 매칭 시작: 방 닫고 전원 Ingame으로 보냄
    public async UniTaskVoid StartMatchFromMatchmaking()
    {
        if (!_runner.IsServer) return;

        Debug.Log("[Matchmaking] Player count reached. Starting match...");

        // 더 이상 입장 못 하게 닫기
        try { _runner.SessionInfo.IsOpen = false; } catch { }

        // 클라이언트들의 PlayerInfoData를 확보할 때까지 잠깐 대기(아직 dict 동기화 중일 수 있음)
        await UniTask.Delay(300);

        // 요구사항: 이걸 호출하면 시작
        foreach (var player in PlayerMap.Values)
        {
            if (player != null)
                player.RPC_MoveToIngame();
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef playerRef)
    {
        Debug.Log($"[OnPlayerLeft] {playerRef}");

        NetworkId netId = default;
        bool hasNetId = _playerRefToNetId != null && _playerRefToNetId.TryGetValue(playerRef, out netId);

        if (_spawned != null && _spawned.TryGetValue(playerRef, out var spawnedObj))
        {
            if (spawnedObj != null && spawnedObj.IsValid)
                runner.Despawn(spawnedObj);

            _spawned.Remove(playerRef);
        }
        else if (hasNetId)
        {
            if (runner != null && runner.IsRunning)
            {
                var no = runner.FindObject(netId);
                if (no != null && no.IsValid)
                    runner.Despawn(no);
            }
        }

        if (hasNetId)
        {
            if (Players != null) Players.Remove(netId);
            if (PlayerMap != null) PlayerMap.Remove(netId);
        }

        if (_playerRefToNetId != null)
            _playerRefToNetId.Remove(playerRef);

        if (WaitingRoomManager.Instance != null && WaitingRoomManager.Instance.waitingRoomPanel != null)
        {
            WaitingRoomManager.Instance.waitingRoomPanel.RemovePlayer(netId);
        }

        if (_isMatchmakingRoom && runner.IsServer)
        {
            int cur = runner.ActivePlayers.Count();
            if (cur < MATCH_SIZE)
                _runner.SessionInfo.IsOpen = true;
        }

        if (runner.IsServer)
        {
            PushNewSnapshot().Forget();
        }

        if (SceneLoader.Instance.CurrentSceneType == SceneType.Ingame)
        {
            InGameManager.Instance.CheckCharacterModel().Forget();

            if (IsHost && MyPlayer != null)
            {
                MyPlayer.CheckEndGame();
            }
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (_input == null) return;

        var data = new NetworkInputData
        {
            moveDir = _input.moveDir,
            lookDelta = _input.lookDelta,
            skillSlot = _input.skillSlotPressed,
            IsRun = _input.runPressed
        };

        input.Set(data);
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log("[OnSessionListUpdated]");
        UpdateSessionList(sessionList);
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest req, byte[] token)
    {
        Debug.Log("[OnConnectRequest]");
        // 인게임 중에는 호스트 마이그레이션 재접속 허용
        if (SceneLoader.Instance.CurrentSceneType == SceneType.Ingame)
        {
            req.Accept();
            return;
        }

        // 토큰에서 점수와 클라 번들버전 꺼내기
        int clientBundleVersion;
        int clientScore = ExtractScoreAndBundleFromToken(token, out clientBundleVersion);

        int hostBundleVersion = ServerDataManager.Instance.BundleVersion;

        Debug.Log($"HostBundle {hostBundleVersion}, ClientBundle {clientBundleVersion}");

        // 번들버전 다르면 바로 거부
        if (clientBundleVersion != hostBundleVersion)
        {
            Debug.Log($"[OnConnectRequest] 번들버전 불일치 접속 거부 host {hostBundleVersion}, client {clientBundleVersion}");
            req.Refuse();
            return;
        }

        // 밑에는 기존 매칭 룰 그대로
        if (_isMatchmakingRoom)
        {
            int roomMin = MyScore - MATCH_ELO_RANGE;
            int roomMax = MyScore + MATCH_ELO_RANGE;

            if (runner.SessionInfo != null &&
                runner.SessionInfo.Properties.TryGetValue("ScoreMin", out var pMin) &&
                runner.SessionInfo.Properties.TryGetValue("ScoreMax", out var pMax))
            {
                roomMin = (int)pMin;
                roomMax = (int)pMax;
            }

            if (clientScore < roomMin || clientScore > roomMax)
            {
                Debug.Log($"[OnConnectRequest] Reject score {clientScore} not in [{roomMin},{roomMax}]");
                req.Refuse();
                return;
            }

            int cur = runner.ActivePlayers.Count();
            if (cur >= MATCH_SIZE)
            {
                Debug.Log("[OnConnectRequest] Reject room full");
                req.Refuse();
                return;
            }
        }

        req.Accept();
    }



    private int ExtractScoreAndBundleFromToken(byte[] token, out int clientBundleVersion)
    {
        clientBundleVersion = 0;

        if (token == null || token.Length < 8)
        {
            // 최소 8바이트는 있어야 점수 4바이트 + 번들 4바이트 읽을 수 있음
            return MyScore;
        }

        int offset = token.Length - 8;

        int score = BitConverter.ToInt32(token, offset);
        clientBundleVersion = BitConverter.ToInt32(token, offset + 4);

        return score;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[OnConnectedToServer]");
        if (_cloudReconnecting)
        {
            _clientReconnected = true;
            _cloudReconnecting = false;
        }
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[OnDisconnectedFromServer] reason: {reason}");

        switch (reason)
        {
            case NetDisconnectReason.Timeout:
                // 기존 처리 유지 (패널 띄우고 로비 복귀)
                GameManager.Instance.OpenInfoPanel("ConnectionTimeout", ErrorType.Error, () =>
                {
                    runner.Shutdown(shutdownReason: ShutdownReason.Ok);
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case NetDisconnectReason.ByRemote:
                if (!runner.IsServer)
                {
                    _clientReconnected = false;
                    WaitForClientReattachOrTimeout(12f).Forget(); // 최대 12초 대기 후 판단
                }
                else
                {
                    // 서버인데 ByRemote? 이 케이스는 드묾. 안전망으로 기존 로직
                    WaitForHostMigrationOrReturnToLobby().Forget();
                }
                break;

            default:
                // 기타 사유: 즉시 정리 후 로비
                ClearPlayerCaches();
                DestroyAllLocalPlayers();
                runner.Shutdown(shutdownReason: ShutdownReason.Ok);
                LeftHost();
                break;
        }
    }


    private async UniTaskVoid WaitForHostMigrationOrReturnToLobby()
    {
        if (_runner != null && !_runner.IsServer)
        {
            Debug.Log("[WaitForHostMigration] 나는 서버 권한이 아님 → 클라 대기 루틴으로 전환");
            WaitForClientReattachOrTimeout(12f).Forget();
            return;
        }
        Debug.Log("[WaitForHostMigration] 호스트 마이그레이션 대기 시작");
        float waitTime = 0f;
        const float maxWaitTime = 10f;

        while (waitTime < maxWaitTime)
        {
            if (_runner != null && _runner.IsServer)
            {
                Debug.Log("[WaitForHostMigration] 호스트 마이그레이션 성공");
                return;
            }

            await UniTask.Delay(500);
            waitTime += 0.5f;
            Debug.Log($"[WaitForHostMigration] 대기 중... ({waitTime}/{maxWaitTime}s)");
        }

        Debug.Log("[WaitForHostMigration] 타임아웃 - Runner 종료 및 로비 복귀");
        ClearPlayerCaches();
        DestroyAllLocalPlayers();
        await _runner.Shutdown(shutdownReason: ShutdownReason.Ok);
        GameManager.Instance.OpenInfoPanel("HostMigrationTimeout");
        await SceneLoader.Instance.LoadSceneAsync("Lobby");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        Debug.Log($"[OnShutdown] reason: {reason}");
        CancelInvoke(nameof(PushNewSnapshot));

        if (reason == ShutdownReason.HostMigration)
            return; // 승계 대기: 여기선 로컬 청소/씬 이동/Runner 파괴 안 함

        ClearPlayerCaches();
        DestroyAllLocalPlayers();

        // FIX: 일반 종료 → 죽은 Runner를 반드시 제거해 재초기화가 막히지 않게
        SafeDestroyRunner();

        Debug.Log("[Shutdown] 일반 종료 → 로비 복귀");
        HandleShutdownReason(reason);
    }

    public async void LeftHost()
    {
        GameManager.Instance.OpenInfoPanel("LeftHost");
        await SceneLoader.Instance.LoadSceneAsync("Lobby");
    }

    public void HandleShutdownReason(ShutdownReason reason)
    {
        switch (reason)
        {
            case ShutdownReason.Ok:
                break;

            case ShutdownReason.Error:
                GameManager.Instance.OpenInfoPanel("ErrorText", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.IncompatibleConfiguration:
                GameManager.Instance.OpenInfoPanel("IncompatibleConfiguration", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.ServerInRoom:
                GameManager.Instance.OpenInfoPanel("ServerInRoom");
                break;

            case ShutdownReason.DisconnectedByPluginLogic:
                GameManager.Instance.OpenInfoPanel("DisconnectedByPluginLogic", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.GameClosed:
                GameManager.Instance.OpenInfoPanel("GameClosed", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.GameNotFound:
                GameManager.Instance.OpenInfoPanel("GameNotFound");
                break;

            case ShutdownReason.MaxCcuReached:
                GameManager.Instance.OpenInfoPanel("MaxCcuReached");
                break;

            case ShutdownReason.InvalidRegion:
                GameManager.Instance.OpenInfoPanel("InvalidRegion", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.GameIdAlreadyExists:
                GameManager.Instance.OpenInfoPanel("GameIdAlreadyExists");
                break;

            case ShutdownReason.GameIsFull:
                GameManager.Instance.OpenInfoPanel("GameIsFull");
                break;

            case ShutdownReason.InvalidAuthentication:
                GameManager.Instance.OpenInfoPanel("InvalidAuthentication", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.CustomAuthenticationFailed:
                GameManager.Instance.OpenInfoPanel("CustomAuthenticationFailed", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.AuthenticationTicketExpired:
                GameManager.Instance.OpenInfoPanel("AuthenticationTicketExpired", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.PhotonCloudTimeout:
                GameManager.Instance.OpenInfoPanel("PhotonCloudTimeout", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.AlreadyRunning:
                GameManager.Instance.OpenInfoPanel("AlreadyRunning");
                break;

            case ShutdownReason.InvalidArguments:
                GameManager.Instance.OpenInfoPanel("InvalidArguments", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.HostMigration:
                break;

            case ShutdownReason.ConnectionTimeout:
                GameManager.Instance.OpenInfoPanel("ConnectionTimeout", ErrorType.Error, () =>
                {
                    SceneLoader.Instance.LoadSceneAsync("Lobby").Forget();
                });
                break;

            case ShutdownReason.ConnectionRefused:
                GameManager.Instance.OpenInfoPanel("ConnectionRefused", ErrorType.Error, () => { SceneLoader.Instance.LoadSceneAsync("Lobby").Forget(); });
                break;

            case ShutdownReason.OperationTimeout:
                GameManager.Instance.OpenInfoPanel("OperationTimeout", ErrorType.Error, () => { SceneLoader.Instance.LoadSceneAsync("Lobby").Forget(); });
                break;

            case ShutdownReason.OperationCanceled:
                GameManager.Instance.OpenInfoPanel("OperationCanceled", ErrorType.Error, () => { SceneLoader.Instance.LoadSceneAsync("Lobby").Forget(); });
                break;

            default:
                GameManager.Instance.OpenInfoPanel("Unknown", ErrorType.Error, () => { SceneLoader.Instance.LoadSceneAsync("Lobby").Forget(); });
                break;
        }
    }

    public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        Debug.Log("[OnHostMigration] 호출됨");

        // 기존 주기 스냅샷 중단
        CancelInvoke(nameof(PushNewSnapshot));

        await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
        await UniTask.Yield();
        Debug.Log("[HostMigration] 기존 Runner Shutdown 완료");

        GameObject runnerObj = new("NetworkRunner") { transform = { parent = transform } };
        var newRunner = runnerObj.AddComponent<NetworkRunner>();
        newRunner.ProvideInput = true;
        newRunner.AddCallbacks(this);
        newRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        // FIX: 새 러너를 PhotonManager에 등록 + DDOL + 이벤트 재구독
        DontDestroyOnLoad(runnerObj);
        SetRunner(newRunner);
        NetworkRunner.CloudConnectionLost -= OnCloudConnectionLost;
        NetworkRunner.CloudConnectionLost += OnCloudConnectionLost;

        IsHost = hostMigrationToken.GameMode == GameMode.Host;

        // WaitingRoom 케이스
        if (SceneLoader.Instance.CurrentSceneType == SceneType.WaitingRoom)
        {
            Debug.Log("[HostMigration] WaitingRoom → 씬 재시작 및 StartGame 처리");

            UserMaxCount = MATCH_SIZE;

            var props = new Dictionary<string, SessionProperty>
        {
            { "CreatorID", SteamLoginManager.Instance.GetSteamID().ToString() },
            { "MaxPlayers", MATCH_SIZE }
        };
            var result = await newRunner.StartGame(new StartGameArgs
            {
                GameMode = hostMigrationToken.GameMode,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex), // FIX
                SceneManager = newRunner.GetComponent<NetworkSceneManagerDefault>(),
                PlayerCount = MATCH_SIZE,
                IsVisible = IsVisible, // FIX: 기존 값 유지
                IsOpen = true,
                SessionProperties = props,
                HostMigrationToken = hostMigrationToken,
                HostMigrationResume = HostMigrationResume,
                ConnectionToken = connectionToken
            });

            if (!result.Ok)
            {
                Debug.LogError($"[HostMigration] WaitingRoom StartGame 실패: {result.ShutdownReason}");
                GameManager.Instance.OpenInfoPanel("HostMigrationFailed");
            }
            else
            {
                // WaitingRoom 씬 유지/보장
                if (SceneLoader.Instance.GetActiveSceneName() != "WaitingRoom")
                    SceneLoader.Instance.LoadSceneAsync("WaitingRoom").Forget();

                if (newRunner.IsServer)
                {
                    Debug.Log("[HostMigration] Registering periodic snapshot on new host");
                    InvokeRepeating(nameof(PushNewSnapshot),
                                    SNAPSHOT_INTERVAL_MS / 1000f,
                                    SNAPSHOT_INTERVAL_MS / 1000f);
                    await newRunner.PushHostMigrationSnapshot();
                }

                await UniTask.WaitUntil(() => WaitingRoomManager.Instance && WaitingRoomManager.Instance.waitingRoomPanel);
                waitingRoomPanel = WaitingRoomManager.Instance.waitingRoomPanel;
                waitingRoomPanel.SetRoomInfo(IsVisible);
            }

            return;
        }

        // Ingame 케이스
        Debug.Log("[HostMigration] Ingame → 씬 유지, StartGame 및 Snapshot 복구");
        var Sceneref = SceneLoader.Instance.CurrentSceneType == SceneType.Lobby ? SceneRef.FromIndex(1) : SceneLoader.Instance.CurrentSceneType == SceneType.Result ? SceneRef.FromIndex(4) : SceneRef.FromIndex(3);
        var ingameResult = await newRunner.StartGame(new StartGameArgs
        {
            GameMode = hostMigrationToken.GameMode,
            IsOpen = false,
            IsVisible = IsVisible,
            Scene = Sceneref,
            SceneManager = newRunner.GetComponent<NetworkSceneManagerDefault>(),
            HostMigrationToken = hostMigrationToken,
            HostMigrationResume = HostMigrationResume,
            ConnectionToken = connectionToken
        });

        if (!ingameResult.Ok)
        {
            Debug.LogError($"[HostMigration] Ingame StartGame 실패: {ingameResult.ShutdownReason}");
            GameManager.Instance.OpenInfoPanel("HostMigrationFailed");
        }
        else
        {
            if (newRunner.IsServer)
            {
                Debug.Log("[HostMigration] Registering periodic snapshot on new host");
                InvokeRepeating(nameof(PushNewSnapshot),
                                SNAPSHOT_INTERVAL_MS / 1000f,
                                SNAPSHOT_INTERVAL_MS / 1000f);
                await newRunner.PushHostMigrationSnapshot();
            }
        }
    }


    private void HostMigrationResume(NetworkRunner runner)
    {
        Debug.Log("[HostMigrationResume] 시작");
        DestroyAllLocalPlayers();
        Players.Clear();
        PlayerMap.Clear();
        _spawned.Clear();
        _playerRefToNetId.Clear();
        MyPlayer = null;
        MyPlayerDataInfo = null;

        foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects())
        {
            if (!resumeNO.NetworkTypeId.IsPrefab)
            {
                Debug.LogWarning($"[HostMigrationResume] {resumeNO.name} 은 Prefab 이 아님 복구 건너뜀");
                continue;
            }

            Vector3 pos;
            Quaternion rot;

            // 1 Player 인 경우에는 Player 가 들고 있던 NetPosition 사용
            if (resumeNO.TryGetBehaviour<Player>(out var resumePlayer))
            {
                pos = resumePlayer.NetPosition;
                rot = resumePlayer.NetRotation;

                // 혹시 기본값이면 트랜스폼 값으로 보정
                if (pos == default)
                    pos = resumeNO.transform.position;
                if (rot == default)
                    rot = resumeNO.transform.rotation;
            }
            else if (resumeNO.TryGetBehaviour<NetworkTransform>(out var netTransform))
            {
                pos = netTransform.Data.Position;
                rot = netTransform.Data.Rotation;
            }
            else
            {
                pos = resumeNO.transform.position;
                rot = resumeNO.transform.rotation;
            }

            var newNO = runner.Spawn(
                resumeNO,
                pos,
                rot,
                resumeNO.InputAuthority,
                onBeforeSpawned: (r, obj) =>
                {
                    obj.CopyStateFrom(resumeNO);
                });

            if (newNO.TryGetBehaviour<PlayerInfoData>(out var info))
            {
                //나에게 권한이없는것중
                if (!newNO.HasInputAuthority)
                {
                    if (!info.IsAI && info.HostRef != PlayerRef.None)
                    {
                        UniTask.Void(async () =>
                        {
                            await UniTask.NextFrame();
                            if (newNO != null && newNO.IsValid)
                                runner.Despawn(newNO);
                        });
                        continue;
                    }
                }

                var key = newNO.Id;

                if (!info.IsAI)
                {
                    PlayerMap[key] = info;

                    if (newNO.TryGetBehaviour<Player>(out var player))
                    {
                        Players[key] = player;
                    }

                    if (newNO.HasInputAuthority)
                    {
                        MyPlayerDataInfo = info;
                        MyPlayer = player;
                    }

                    var auth = newNO.InputAuthority;
                    if (auth != PlayerRef.None)
                    {
                        runner.SetPlayerObject(auth, newNO);

                        if (_playerRefToNetId != null)
                            _playerRefToNetId[auth] = key;

                        if (_spawned != null)
                            _spawned[auth] = newNO;
                    }
                }


                Debug.Log($"[Resume] 플레이어 복원 완료 {newNO.name} / {key}");
                continue;
            }

            if (newNO.TryGetBehaviour<Monster>(out var monster))
            {
                continue;
            }

            Debug.Log($"[HostMigrationResume] 일반 오브젝트 복원 {newNO.name}");
        }

        foreach (var sceneObj in runner.GetResumeSnapshotNetworkSceneObjects())
        {
            sceneObj.Item1.CopyStateFrom(sceneObj.Item2);
            Debug.Log($"[Resume] SceneObject 복구 {sceneObj.Item1.name}");
        }
        Debug.Log("[HostMigrationResume] 완료");
    }
    public void OnSceneLoadDone(NetworkRunner runner) => Debug.Log("[OnSceneLoadDone]");
    public void OnSceneLoadStart(NetworkRunner runner) => Debug.Log("[OnSceneLoadStart]");
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.Log("[OnConnectFailed]연결 실패");
        //runner.Shutdown();
    }
    #endregion
    #region 게임 튕겼을때
    // PC/모바일 공통: 앱 종료 직전
    private void OnAppQuitting()
    {
        TriggerEmergencyHostMigrationAsync("Application.quitting").Forget();
    }

    // 일부 플랫폼에서 추가로 호출
    private void OnApplicationQuit()
    {
        TriggerEmergencyHostMigrationAsync("OnApplicationQuit").Forget();
    }

    // 모바일: 백그라운드 진입
    private void OnApplicationPause(bool pause)
    {
        if (pause)
            TriggerEmergencyHostMigrationAsync("OnApplicationPause(true)").Forget();
    }

    // 포커스 잃음(Alt+Tab/전화 등) - 스냅샷만 한 번 더 밀어두기
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            TryEmergencySnapshotAsync("[FocusLost]").Forget();
    }

#if UNITY_EDITOR
    // 에디터 ▶ Stop 직전
    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            TriggerEmergencyHostMigrationAsync("Editor.ExitingPlayMode").Forget();
    }
#endif

    // .NET 도메인 레벨 예외
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        TryEmergencySnapshotAsync("[UnhandledException]").Forget();
    }

    // 관찰되지 않은 Task 예외
    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        TryEmergencySnapshotAsync("[UnobservedTaskException]").Forget();
    }
    /// <summary>
    /// 호스트가 갑자기 종료될 때: 스냅샷 즉시 push + HostMigration 종료 발사
    /// </summary>
    private async UniTask TriggerEmergencyHostMigrationAsync(string where)
    {
        if (_emergencyExitInProgress) return;
        _emergencyExitInProgress = true;

        if (_runner == null || !_runner.IsRunning || !_runner.IsServer)
            return; // 호스트가 아니면 의미 없음

        Debug.Log($"[EmergencyHM] Triggered at {where}");

        try
        {
            await TryEmergencySnapshotAsync($"[{where}]");

            // HostMigration 이유로 종료 발사 (클라들은 마이그레이션 루틴으로 진입)
            Debug.Log("[EmergencyHM] Shutting down with HostMigration");
            await _runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EmergencyHM] Trigger failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 호스트일 경우 스냅샷 1회 밀어두는 안전망
    /// </summary>
    private async UniTask TryEmergencySnapshotAsync(string tag)
    {
        try
        {
            if (_runner != null && _runner.IsRunning && _runner.IsServer)
            {
                Debug.Log($"[EmergencyHM] PushHostMigrationSnapshot {tag}");
                await _runner.PushHostMigrationSnapshot();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EmergencyHM] Snapshot push failed: {ex.Message}");
        }
    }
    #endregion
}
