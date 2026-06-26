// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\Dragon-Arena\Assets\Scripts\Photon\PhotonManager.cs
// Lines: 303-374, 433-585

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

// ...

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
