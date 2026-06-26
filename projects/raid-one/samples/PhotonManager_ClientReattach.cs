// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\KillTheDragon\Assets\Scripts\Photon\PhotonManager.cs
// Lines: 689-750

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
