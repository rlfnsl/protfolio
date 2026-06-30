using UnityEngine;
using Steamworks;
using System;
using Fusion;

/// <summary>
/// 스팀 IAP 매니저 (디버그 드라이런 지원):
/// - H 키(기본)로 "구매 시작"을 디버그 로그만 출력
/// - MicroTxn 승인 콜백도 디버그 로그만 출력
/// - 실제 코인 지급/오버레이 열기/결제 호출 없음
/// </summary>
public class SteamIAPManager : Singleton<SteamIAPManager>
{
    protected override bool IsDontDestroy => true;

    // ── 디버그/핫키 옵션 ─────────────────────────────────────────
    [Header("Debug / Hotkey")]
    [Tooltip("true면 실제 동작 대신 로그만 출력 (코인 증가/오버레이/StartPurchase 호출 X)")]
    public bool debugDryRun = true;

    [Tooltip("H 누르면 디버그 구매 시작 로그를 찍습니다.")]
    public bool hotkeyEnabled = true;

    [Tooltip("디버그 트리거 키")]
    public KeyCode hotkey = KeyCode.H;

    [Tooltip("핫키로 구매할(것처럼 보일) 팩 인덱스")]
    public int hotkeyPackIndex = 0;

    // ── 스팀 결제 승인 콜백 ─────────────────────────────────────
    private Callback<MicroTxnAuthorizationResponse_t> _microTxnAuth;

    // ── 코인팩 정의(표시/매핑용) ────────────────────────────────
    [Serializable]
    public class CoinPack
    {
        public string packId = "coin_100";
        public int coins = 100;
        public uint priceCents = 99; // UI 표시용(실결제 가격은 Steamworks 백엔드 기준)
    }

    public CoinPack[] coinPacks = new CoinPack[]
    {
        new CoinPack { packId="coin_100",  coins=100,  priceCents=99  },
        new CoinPack { packId="coin_550",  coins=550,  priceCents=499 },
        new CoinPack { packId="coin_1200", coins=1200, priceCents=999 },
    };

    // 결제 진행 중인 오더(샘플 단일)
    private string _pendingOrderId;
    private CoinPack _pendingPack;

    private void OnEnable()
    {
        // 스팀 초기화된 상태에서만 콜백 등록(디버그 모드에서도 콜백은 만들어 둠)
        if (SteamLoginManager.Instance != null && SteamLoginManager.Instance.isInit)
        {
            _microTxnAuth = Callback<MicroTxnAuthorizationResponse_t>.Create(OnMicroTxnAuthorizationResponse);
        }
        else
        {
            // 초기화 전에도 콜백 객체 생성 시도 (런타임 중 Init될 수 있으니 방어)
            _microTxnAuth = Callback<MicroTxnAuthorizationResponse_t>.Create(OnMicroTxnAuthorizationResponse);
        }
    }

    private void OnDisable()
    {
        _microTxnAuth = null;
    }

    private void Update()
    {
        if (!hotkeyEnabled) return;

        if (Input.GetKeyDown(hotkey))
        {
            // 디버그 드라이런: 로그만 찍고 종료
            string packTitle = $"packIndex={hotkeyPackIndex}";
            if (coinPacks != null && hotkeyPackIndex >= 0 && hotkeyPackIndex < coinPacks.Length)
                packTitle = coinPacks[hotkeyPackIndex].packId;

            Debug.Log($"[IAP:DEBUG] (Hotkey) 구매 시작 요청 → {packTitle}");

            // 내부적으로도 동일한 경로를 태우되, 드라이런이라 실제 동작 없이 로그만
            if (coinPacks != null && hotkeyPackIndex >= 0 && hotkeyPackIndex < coinPacks.Length)
                PurchaseCoins(coinPacks[hotkeyPackIndex]);
            else
                PurchaseCoins(new CoinPack { packId = packTitle, coins = 0, priceCents = 0 });
        }
    }

    /// <summary>
    /// 버튼에서 호출할 간단한 구매 API: coinPacks[index] 구매 시도
    /// (debugDryRun=true면 모든 실제 동작 생략하고 로그만 남김)
    public void PurchaseCoinsByIndex(int index)
    {
        if (coinPacks == null || index < 0 || index >= coinPacks.Length)
        {
            Debug.LogWarning("[IAP] 잘못된 코인팩 인덱스");
            return;
        }
        PurchaseCoins(coinPacks[index]);
    }

    /// <summary>
    /// 코어 구매 로직
    /// debugDryRun = true 일 때는:
    ///  - 오버레이 열지 않음
    ///  - 코인 지급하지 않음
    ///  - 승인 콜백도 '모의' 로그만 출력
    /// </summary>
    public void PurchaseCoins(CoinPack pack)
    {
        // 유니크 오더ID (실서비스: 서버 생성 권장)
        _pendingOrderId = $"{DateTime.UtcNow.Ticks}_{UnityEngine.Random.Range(1000, 9999)}";
        _pendingPack = pack;

        // 디버그 드라이런: 실제 결제 호출/오버레이/코인 지급 없이 로그만
        if (debugDryRun)
        {
            Debug.Log($"[IAP:DEBUG] StartPurchase(드라이런) → SKU: {_pendingPack.packId}, Coins:{_pendingPack.coins}, OrderID:{_pendingOrderId}");

            // 실제라면 Overlay 오픈/서버 결제 페이지 이동. 드라이런은 로그만:
            Debug.Log("[IAP:DEBUG] (드라이런) Steam Overlay/결제 페이지 생략");

            // 승인 콜백도 모의 로그만
            Debug.Log("[IAP:DEBUG] (드라이런) MicroTxnAuthorizationResponse_t → Authorized=true (모의)");
            Debug.Log("[IAP:DEBUG] (드라이런) 승인 처리 완료. 코인 지급은 수행하지 않습니다.");

            ClearPending();
            return;
        }

        // 이하 실제 플로우(원하면 유지):
        if (SteamLoginManager.Instance == null || !SteamLoginManager.Instance.isInit)
        {
            Debug.LogWarning("[IAP] Steam 미초기화 상태");
            ClearPending();
            return;
        }

        string overlayUrl = BuildYourBackendPaymentUrl(_pendingOrderId, _pendingPack);
        Debug.Log($"[IAP] 결제 페이지 오픈: {overlayUrl}");
        SteamFriends.ActivateGameOverlayToWebPage(overlayUrl);
    }

    private string BuildYourBackendPaymentUrl(string orderId, CoinPack pack)
    {
        return $"<REDACTED_STEAM_IAP_URL>?order={orderId}&sku={pack.packId}&qty=1";
    }

    // 스팀 결제 승인 콜백
    private void OnMicroTxnAuthorizationResponse(MicroTxnAuthorizationResponse_t param)
    {
        bool authorized = param.m_bAuthorized == 1;

        if (debugDryRun)
        {
            // 드라이런: 콜백이 오든 말든, 여기서도 로그만
            Debug.Log($"[IAP:DEBUG] (드라이런) MicroTxn 콜백 수신 → AppID={param.m_unAppID}, OrderID={param.m_ulOrderID}, Authorized={authorized}");
            Debug.Log("[IAP:DEBUG] (드라이런) 승인이어도 코인 지급/상태 변경 없음");
            ClearPending();
            return;
        }

        Debug.Log($"[IAP] MicroTxn 콜백: AppID={param.m_unAppID}, OrderID={param.m_ulOrderID}, Authorized={authorized}");

        if (!authorized)
        {
            Debug.LogWarning("[IAP] 결제 거절/취소");
            ClearPending();
            return;
        }

        if (_pendingPack != null)
        {
            Debug.Log("구매완료");
        }
        ClearPending();
    }

    private void ClearPending()
    {
        _pendingOrderId = null;
        _pendingPack = null;
    }
}
