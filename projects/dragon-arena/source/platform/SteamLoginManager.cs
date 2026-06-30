using UnityEngine;
using Steamworks;
using System;

public class SteamLoginManager : Singleton<SteamLoginManager>
{
    private const uint APP_ID = 4371940;
    protected override bool IsDontDestroy => true;

    private CSteamID _steamId;
    private string t;

    public bool isInit = false;
    private bool _steamReady = false;

    private Callback<LobbyCreated_t> _lobbyCreated;
    private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;
    private Callback<LobbyEnter_t> _lobbyEnter;
    private CSteamID _currentLobbyId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    protected override void Awake()
    {
        base.Awake();

        if (Instance != this) return;

        // 필요하면 이 부분 사용
        // if (SteamAPI.RestartAppIfNecessary(new AppId_t(APP_ID)))
        // {
        //     Application.Quit();
        //     return;
        // }

        if (!SteamAPI.Init())
        {
            if (GameManager.Instance.IsDev)
            {
                t = UnityEngine.Random.Range(0, 1000).ToString();
                _steamId = new CSteamID(0);
                isInit = true;
                _steamReady = false;
                Debug.Log("개발 모드로 실행 중");
                return;
            }
            Application.Quit();
            Debug.LogWarning("SteamAPI.Init 실패");
            return;
        }

        _steamReady = true;
        isInit = true;
        _steamId = SteamUser.GetSteamID();

        Debug.Log("Steam 초기화 성공");
        Debug.Log("닉네임 " + SteamFriends.GetPersonaName());
        Debug.Log("Steam ID " + _steamId.m_SteamID);
        Debug.Log("현재 AppID " + SteamUtils.GetAppID().m_AppId);

        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
    }

    private void Update()
    {
        if (_steamReady)
        {
            SteamAPI.RunCallbacks();
        }
    }

    public bool IsSteamReady => _steamReady;

    public ulong GetSteamID()
    {
        return _steamId.m_SteamID;
    }

    public string GetSteamNickName()
    {
        if (!string.IsNullOrEmpty(t))
            return t;

        return t;
    }

    private void OnApplicationQuit()
    {
        if (_steamReady && SteamAPI.IsSteamRunning())
        {
            SteamAPI.Shutdown();
        }
    }

    private byte[] authTicketData;

    public string GetSteamToken()
    {
        if (!isInit || !_steamReady)
        {
            return "NOINIT";
        }

        authTicketData = new byte[1024];
        uint ticketSize;

        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        identity.SetSteamID(SteamUser.GetSteamID());

        SteamUser.GetAuthSessionTicket(
            authTicketData,
            authTicketData.Length,
            out ticketSize,
            ref identity
        );

        byte[] trimmedTicket = new byte[ticketSize];
        Array.Copy(authTicketData, trimmedTicket, ticketSize);

        string hexTicket = BitConverter.ToString(trimmedTicket).Replace("-", "").ToLowerInvariant();
        return hexTicket;
    }

    public void OpenInviteDialog()
    {
        ClearLobby();
        if (!_steamReady)
        {
            Debug.LogWarning("스팀이 준비되지 않아서 초대를 사용할 수 없음");
            return;
        }

        var photon = PhotonManager.Instance;
        if (photon == null || !photon.IsJoined)
        {
            Debug.LogWarning("포톤 방에 들어있지 않아서 초대를 보낼 수 없음");
            return;
        }

        if (_currentLobbyId.IsValid())
        {
            SteamFriends.ActivateGameOverlayInviteDialog(_currentLobbyId);
            return;
        }

        int maxPlayers = photon.UserMaxCount > 0 ? photon.UserMaxCount : 6;
        Debug.Log("스팀 로비 생성 요청 최대 인원 " + maxPlayers);

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
    }

    public void ClearLobby()
    {
        if (_currentLobbyId.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyId);
        }
        _currentLobbyId = new CSteamID(0);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("스팀 로비 생성 실패 결과 " + callback.m_eResult);
            return;
        }

        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        string roomName = PhotonManager.Instance.CurrentRoomName;
        if (string.IsNullOrEmpty(roomName))
        {
            roomName = "Room_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        SteamMatchmaking.SetLobbyData(_currentLobbyId, "room", roomName);
        SteamMatchmaking.SetLobbyData(_currentLobbyId, "type", "PhotonFusion");
        SteamMatchmaking.SetLobbyJoinable(_currentLobbyId, true);

        SteamFriends.ActivateGameOverlayInviteDialog(_currentLobbyId);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Debug.Log("스팀 게임 로비 초대 수신 로비ID " + callback.m_steamIDLobby.m_SteamID);

        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEnter(LobbyEnter_t callback)
    {
        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        string roomName = SteamMatchmaking.GetLobbyData(_currentLobbyId, "room");

        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning("스팀 로비에 room 데이터가 없음");
            return;
        }

        if (PhotonManager.Instance != null && PhotonManager.Instance.IsJoined)
        {
            Debug.Log("이미 포톤 방에 참가 중 room " + PhotonManager.Instance.CurrentRoomName);
            return;
        }

        PhotonManager.Instance.TryJoinRoom(roomName);
    }

    public void AddStatInt(string statName, int addValue, string achievementId = null, int targetValue = 0)
    {
        if (!_steamReady || !isInit)
        {
            Debug.LogWarning("Steam 이 초기화되지 않음");
            return;
        }

        int current;
        if (!SteamUserStats.GetStat(statName, out current))
        {
            Debug.LogWarning("스팀 스탯을 찾을 수 없음 이름 " + statName);
            current = 0;
        }

        current += addValue;
        SteamUserStats.SetStat(statName, current);
        SteamUserStats.StoreStats();

        Debug.Log("스팀 스탯 업데이트 이름 " + statName + " 값 " + current);

        if (!string.IsNullOrEmpty(achievementId) && targetValue > 0 && current >= targetValue)
        {
            UnlockAchievement(achievementId);
        }
    }

    public int GetStatInt(string statName)
    {
        if (!_steamReady || !isInit)
        {
            return 0;
        }

        int value;
        if (!SteamUserStats.GetStat(statName, out value))
        {
            return 0;
        }
        return value;
    }

    public void UnlockAchievement(string achievementId)
    {
        if (!_steamReady || !isInit)
        {
            Debug.LogWarning("Steam 이 초기화되지 않음");
            return;
        }

        bool achieved;
        if (!SteamUserStats.GetAchievement(achievementId, out achieved))
        {
            Debug.LogWarning("스팀 업적을 찾을 수 없음 이름 " + achievementId);
            return;
        }

        if (achieved)
        {
            Debug.Log("이미 해금된 업적 이름 " + achievementId);
            return;
        }

        SteamUserStats.SetAchievement(achievementId);
        bool ok = SteamUserStats.StoreStats();

        if (ok)
        {
            Debug.Log("업적 해금 성공 이름 " + achievementId);
        }
        else
        {
            Debug.LogWarning("업적 해금 실패 이름 " + achievementId);
        }
    }
}
