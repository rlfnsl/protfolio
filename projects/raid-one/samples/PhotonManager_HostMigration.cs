// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\KillTheDragon\Assets\Scripts\Photon\PhotonManager.cs
// Lines: 1426-1536, 1539-1660

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

// ...

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
