using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct MinimapCamPreset
{
    public Vector3 position;
    public Vector3 eulerAngles;
    public float orthoSize;
    public float nearClip;
    public float farClip;
    public float canvasScale;
}

public class MinimapManager : MonoBehaviour
{
    public bool isRaid
    {
        get
        {
            if (myPlayer == null || !myPlayer.IsNetworkReady)
                return false;
            return myPlayer.PlayerInfoData.IsRaidBoss;
        }
    }
    Player myPlayer;

    [Header("References")]
    [AutoAssign] public Camera minimapCamera;
    [SerializeField] private Transform minimapCanvas;       // World-Space Canvas(아이콘 부모)
    [SerializeField] private Transform minimapTreeCanvas;       // World-Space Canvas(아이콘 부모)
    [SerializeField] private Transform minimapEggCanvas;       // World-Space Canvas(아이콘 부모)
    [SerializeField] private GameObject iconPrefab;         // MinimapObject 포함 프리팹
    [SerializeField] private RenderTexture[] minimapTextures; // 0=큰, 1=작은
    private Transform treeParent;
    private Transform monsterParent;
    private Transform itemParent;
    private Transform otherParent;
    private Transform playerParent;
    public Transform GetTreeParent() => treeParent ?? minimapCanvas;
    public Transform GetMonsterParent() => monsterParent ?? minimapCanvas;
    public Transform GetItemParent() => itemParent ?? minimapCanvas;
    public Transform GetOtherParent() => otherParent ?? minimapCanvas;
    public Transform GetPlayerParent() => playerParent ?? minimapCanvas;

    [Header("Colors")]
    public Color myPlayerColor = Color.red;
    public Color monsterColor = Color.blue;
    public Color itemColor = Color.gray;
    public Color dragonEggColor = Color.black;
    //public Color treeColor = Color.green;
    public Color otherColor = Color.white;

    private readonly List<MinimapObject> _shopIconList = new();
    [SerializeField] private float shopNearestUpdateInterval = 0.2f;
    private float _nextShopNearestTime;

    public Camera MinimapCamera => minimapCamera;
    public Transform MinimapCanvas => minimapCanvas;

    [Header("Follow Camera (SMALL)")]
    [SerializeField] private Transform followTarget;
    public Vector3 followOffset = new Vector3(0, 50, 0);
    public float followSmooth = 10f;
    public bool rotateWithTargetYaw = true;
    public float topDownPitch = 90f;

    [Header("Presets")]
    public MinimapCamPreset smallPreset = new MinimapCamPreset
    {
        position = new Vector3(794.99f, 51.08f, 173f),
        eulerAngles = new Vector3(90f, 0f, -0.391f),
        orthoSize = 35f,
        nearClip = 0.3f,
        farClip = 1000f,
        canvasScale = 0.05f
    };

    public MinimapCamPreset largePreset = new MinimapCamPreset
    {
        position = new Vector3(469f, 50f, 494f),
        eulerAngles = new Vector3(90f, 0f, -0.391f),
        orthoSize = 450f,
        nearClip = 0.3f,
        farClip = 1000f,
        canvasScale = 0.30f
    };

    public MinimapCamPreset largeRaidPreset = new MinimapCamPreset
    {
        position = new Vector3(37.1f, 50f, -17.5f),
        eulerAngles = new Vector3(90f, 0f, 90f),
        orthoSize = 150f,
        nearClip = 0.3f,
        farClip = 1000f,
        canvasScale = 0.30f
    };

    [Header("Startup")]
    public bool startExpanded = false;

    // 상태
    private bool _lockFollow = false;                  // 큰 지도일 때 true
    public bool IsExpanded { get; private set; }       // 외부 확인용

    private readonly Dictionary<Transform, MinimapObject> icons = new();
    private readonly Dictionary<Transform, MinimapObject> shopicons = new();
    public readonly Dictionary<Transform, MinimapObject> treeicons = new();

    // 몬스터 슬롯(아이콘 재사용)
    [Header("Monster Slots")]
    [SerializeField] private int monsterSlotCount = 3;
    private readonly List<MinimapObject> _monsterSlots = new();

    private class EggIconEntry { public MinimapObject mmo; public float expire; }

    //[SerializeField] private int maxEggIndicators = 10;
    //private IndicatorSlot[] _eggSlotsInd;
    private float dragonEggObfuscationRadius = 70;
    public float DragonEggRadius => dragonEggObfuscationRadius;

    // ====== Offscreen Indicators (작은 미니맵 전용) ======
    [Header("Offscreen Indicators (small only)")]
    [Tooltip("경계에 찍을 인디케이터 프리팹(Image 삼각형 + Text(UI.Text))")]
    [SerializeField] private GameObject indicatorPrefab;
    [Tooltip("사각 경계에서 안쪽으로 살짝 넣는 여유(월드 단위)")]
    [SerializeField] private float edgePadding = 0.6f;
    [Tooltip("표시할 최대 몬스터 인디케이터 수")]
    [SerializeField] private int maxMonsterIndicators = 3;
    [Tooltip("인디케이터 갱신 간격(초)")]
    [SerializeField] private float indicatorUpdateInterval = 0.1f;
    [SerializeField] float indicatorPosSmoothTime = 0.08f; // 위치 부드러움(초)
    [SerializeField] float indicatorYawSmoothTime = 0.06f; // 회전 부드러움(초)

    private float _nextIndicatorUpdateTime;

    // 고정 풀 + 캐시
    private struct IndicatorSlot
    {
        public RectTransform rt;
        public Text text;
        public Image img;
        public bool visible;
        public int lastDistM;
        public float lastYaw;
        public Vector3 lastPos;
        public Vector3 vel;
        public float yawVel;
    }
    private IndicatorSlot[] _monsterSlotsInd;
    private IndicatorSlot _shopInd;
    private float _panelYCached;



    private void Awake()
    {
        if (minimapCamera) minimapCamera.orthographic = true;

        if (minimapCanvas != null)
        {
            foreach (Transform child in minimapCanvas)
            {
                switch (child.name)
                {
                    case "Tree": treeParent = child; break;
                    case "Monster": monsterParent = child; break;
                    case "Item": itemParent = child; break;
                    case "Other": otherParent = child; break;
                    case "Player": playerParent = child; break;
                }
            }
        }

        if (startExpanded) ApplyLargeMapPreset(false);
        else ApplySmallMapPreset();

        EnsureIndicatorPool();
    }
    private void LateUpdate()
    {
        if (minimapCamera == null) return;

        // 작은 미니맵 팔로우/회전
        if (!_lockFollow && followTarget != null)
        {
            Vector3 desiredPos = followTarget.position + followOffset;

            minimapCamera.transform.position = desiredPos;

            float yaw = rotateWithTargetYaw ? followTarget.eulerAngles.y : 0f;
            minimapCamera.transform.rotation = Quaternion.Euler(topDownPitch, yaw, 0f);
        }

        UpdateOffscreenIndicators();

        UpdateNearestShopIcon();
    }
    // 가장 가까운 Shop 아이콘만 표시
    private void UpdateNearestShopIcon()
    {
        if (Time.unscaledTime < _nextShopNearestTime) return;
        _nextShopNearestTime = Time.unscaledTime + shopNearestUpdateInterval;

        if (shopicons.Count == 0) return;

        if (myPlayer == null)
            myPlayer = InGameManager.Instance?.MyPlayer;
        if (myPlayer == null || isRaid)
        {
            foreach (var kv in shopicons)
            {
                if (kv.Value != null)
                    kv.Value.SetVisible(false);
            }
            return;
        }

        // 가장 가까운 상점 탐색
        MinimapObject nearest = null;
        float bestSqr = float.MaxValue;
        Vector3 myPos = myPlayer.transform.position;

        foreach (var kv in shopicons)
        {
            var mmo = kv.Value;
            if (mmo == null || mmo.target == null) continue;

            float sq = (mmo.target.position - myPos).sqrMagnitude;
            if (sq < bestSqr)
            {
                bestSqr = sq;
                nearest = mmo;
            }
        }

        // 결과 적용: 가장 가까운 것만 보이기
        foreach (var kv in shopicons)
        {
            var mmo = kv.Value;
            if (mmo == null) continue;

            bool shouldShow = (mmo == nearest);
            mmo.SetVisible(shouldShow);
        }
    }

    // === 외부 API ===
    public void SetFollowTarget(Transform target) => followTarget = target;
    public void ClearFollowTarget() => followTarget = null;
    public void SetFollowOffset(Vector3 offset) => followOffset = offset;

    public void ApplySmallMapPreset()
    {
        _lockFollow = false;
        IsExpanded = false;
        rotateWithTargetYaw = true;
        if (minimapTextures != null && minimapTextures.Length > 1)
            minimapCamera.targetTexture = minimapTextures[1];
        InGameManager.Instance.PlayerInfoManager.ApplyViewMinimpa(false);
        ApplyPreset(smallPreset);
    }

    public void ApplyLargeMapPreset(bool raidStarted)
    {
        _lockFollow = true;
        IsExpanded = true;
        rotateWithTargetYaw = false;
        if (minimapTextures != null && minimapTextures.Length > 0)
            minimapCamera.targetTexture = minimapTextures[0];
        InGameManager.Instance.PlayerInfoManager.ApplyViewMinimpa(true);
        ApplyPreset(raidStarted ? largeRaidPreset : largePreset);
    }

    public void ToggleView(bool expanded, bool raidStarted)
    {
        if (expanded) ApplyLargeMapPreset(raidStarted);
        else ApplySmallMapPreset();
    }

    private void ApplyPreset(MinimapCamPreset p)
    {
        if (minimapCamera == null) return;

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = p.orthoSize;
        minimapCamera.nearClipPlane = p.nearClip;
        minimapCamera.farClipPlane = p.farClip;

        minimapCamera.transform.position = p.position;
        minimapCamera.transform.rotation = Quaternion.Euler(p.eulerAngles);

        if (minimapCanvas != null)
            minimapCanvas.localScale = Vector3.one * p.canvasScale;

        _panelYCached = minimapCanvas ? minimapCanvas.position.y : _panelYCached;
    }

    // === 아이콘 생성/관리 ===
    public void CreatePlayerIcon(Transform playerTr, bool isMine)
    {
        if (playerTr == null || minimapCanvas == null || iconPrefab == null) return;
        if (icons.ContainsKey(playerTr)) { UpdatePlayerIconColor(playerTr, isMine); return; }

        var go = Instantiate(iconPrefab, minimapCanvas);
        var mmo = go.GetComponent<MinimapObject>();
        mmo.Setup(this, playerTr, MinimapKind.Player, isMine);
        icons[playerTr] = mmo;
    }

    public void CreateItemIcon(Transform itemTr)
    {
        if (itemTr == null || minimapCanvas == null || iconPrefab == null) return;
        if (icons.ContainsKey(itemTr)) return;

        var go = Instantiate(iconPrefab, minimapCanvas);
        var mmo = go.GetComponent<MinimapObject>();
        mmo.Setup(this, itemTr, MinimapKind.Item);
        icons[itemTr] = mmo;
        icons[itemTr].name = icons.Count.ToString();

        if (itemTr.GetComponent<ShopObject>() != null)
            shopicons[itemTr] = mmo;
    }

    public void CreateDragonEggIcon(Transform itemTr)
    {
        if (itemTr == null || minimapCanvas == null || iconPrefab == null) return;

        // 이미 있으면 그냥 활성화
        if (icons.TryGetValue(itemTr, out var exist) && exist != null)
        {
            if (!exist.gameObject.activeSelf) exist.gameObject.SetActive(true);
            exist.SetVisible(true);
            return;
        }

        var go = Instantiate(iconPrefab, minimapCanvas);
        var mmo = go.GetComponent<MinimapObject>();
        go.transform.SetParent(minimapEggCanvas, false);
        mmo.Setup(this, itemTr, MinimapKind.DragonEgg);
        mmo.SetVisible(true);

        if (!go.activeSelf) go.SetActive(true);

        icons[itemTr] = mmo;
    }

    //public void CreateTreeIcon(Transform itemTr)
    //{
    //    if (itemTr == null || minimapCanvas == null || iconPrefab == null || !isRaid) return;

    //    var go = Instantiate(iconPrefab, minimapTreeCanvas);
    //    var mmo = go.GetComponent<MinimapObject>();
    //    mmo.Setup(this, itemTr, MinimapKind.Tree);
    //    treeicons[itemTr] = mmo;
    //}

    public void RemoveIcon(Transform tr)
    {
        if (tr == null) return;
        if (icons.TryGetValue(tr, out var mmo))
        {
            if (mmo != null && _shopIconList.Count > 0)
                _shopIconList.Remove(mmo);

            if (mmo != null && mmo.gameObject != null) Destroy(mmo.gameObject);
            icons.Remove(tr);
        }
        if (followTarget == tr) followTarget = null;
    }

    private void UpdatePlayerIconColor(Transform tr, bool isMine)
    {
        if (!icons.TryGetValue(tr, out var mmo) || mmo == null) return;
        mmo.Setup(this, tr, MinimapKind.Player, isMine);
    }

    public void InitMonsterSlots(int count = 3)
    {
        if (isRaid) return;
        monsterSlotCount = Mathf.Max(1, count);
        EnsureMonsterSlots(monsterSlotCount);
    }
    public void AssignMonsterTargets(IList<Monster> nearest)
    {
        if (_monsterSlots.Count == 0) return;

        for (int i = 0; i < _monsterSlots.Count; i++)
        {
            var slot = _monsterSlots[i];
            if (slot == null) continue;

            if (nearest != null && i < nearest.Count && nearest[i] != null)
            {
                slot.target = nearest[i].transform;
                slot.SetVisible(true);
            }
            else
            {
                slot.target = null;
                slot.SetVisible(false);
            }
        }
    }

    private void EnsureMonsterSlots(int count)
    {
        while (_monsterSlots.Count < count)
        {
            var slot = CreateMonsterSlot();
            if (slot != null) _monsterSlots.Add(slot);
            else break;
        }

        while (_monsterSlots.Count > count)
        {
            var last = _monsterSlots[Mathf.Max(0, _monsterSlots.Count - 1)];
            if (last != null) Destroy(last.gameObject);
            _monsterSlots.RemoveAt(Mathf.Max(0, _monsterSlots.Count - 1));
        }
    }

    private MinimapObject CreateMonsterSlot()
    {
        if (minimapCanvas == null || iconPrefab == null) return null;
        var go = Instantiate(iconPrefab, minimapCanvas);
        var mmo = go.GetComponent<MinimapObject>();
        mmo.Setup(this, null, MinimapKind.Monster);

        mmo.SetVisible(false);

        return mmo;
    }
    // ====== 인디케이터 풀 생성(1회) ======
    private void EnsureIndicatorPool()
    {
        if (indicatorPrefab == null || minimapCanvas == null) return;

        // 몬스터
        if (_monsterSlotsInd == null || _monsterSlotsInd.Length != maxMonsterIndicators)
        {
            if (_monsterSlotsInd != null)
                for (int i = 0; i < _monsterSlotsInd.Length; i++)
                    if (_monsterSlotsInd[i].rt) _monsterSlotsInd[i].rt.gameObject.SetActive(false);

            _monsterSlotsInd = new IndicatorSlot[Mathf.Max(0, maxMonsterIndicators)];
            for (int i = 0; i < _monsterSlotsInd.Length; i++)
            {
                var go = Instantiate(indicatorPrefab, minimapCanvas);
                _monsterSlotsInd[i] = new IndicatorSlot
                {
                    rt = go.GetComponent<RectTransform>(),
                    text = go.GetComponentInChildren<Text>(true),
                    img = go.GetComponent<Image>(),
                    visible = false,
                    lastDistM = -1,
                    lastYaw = 99999f,
                    lastPos = new Vector3(99999f, 99999f, 99999f)
                };
                go.SetActive(false);
            }
        }

        // 상점
        if (_shopInd.rt == null)
        {
            var go = Instantiate(indicatorPrefab, minimapCanvas);
            _shopInd = new IndicatorSlot
            {
                rt = go.GetComponent<RectTransform>(),
                text = go.GetComponentInChildren<Text>(true),
                img = go.GetComponent<Image>(),
                visible = false,
                lastDistM = -1,
                lastYaw = 99999f,
                lastPos = new Vector3(99999f, 99999f, 99999f)
            };
            go.SetActive(false);
        }

        ////드래곤Egg
        //if (_eggSlotsInd == null || _eggSlotsInd.Length != maxEggIndicators)
        //{
        //    // 기존 것들 비활성
        //    if (_eggSlotsInd != null)
        //        for (int i = 0; i < _eggSlotsInd.Length; i++)
        //            if (_eggSlotsInd[i].rt) _eggSlotsInd[i].rt.gameObject.SetActive(false);

        //    _eggSlotsInd = new IndicatorSlot[Mathf.Max(0, maxEggIndicators)];
        //    for (int i = 0; i < _eggSlotsInd.Length; i++)
        //    {
        //        var go = Instantiate(indicatorPrefab, minimapCanvas);
        //        _eggSlotsInd[i] = new IndicatorSlot
        //        {
        //            rt = go.GetComponent<RectTransform>(),
        //            text = go.GetComponentInChildren<Text>(true),
        //            img = go.GetComponent<Image>(),
        //            visible = false,
        //            lastDistM = -1,
        //            lastYaw = 99999f,
        //            lastPos = new Vector3(99999f, 99999f, 99999f)
        //        };
        //        go.SetActive(false);
        //    }
        //}
    }

    private void HideAllIndicators()
    {
        if (_monsterSlotsInd != null)
            for (int i = 0; i < _monsterSlotsInd.Length; i++)
                if (_monsterSlotsInd[i].rt) { _monsterSlotsInd[i].rt.gameObject.SetActive(false); _monsterSlotsInd[i].visible = false; }
        if (_shopInd.rt) { _shopInd.rt.gameObject.SetActive(false); _shopInd.visible = false; }
        //if (_eggSlotsInd != null)
        //    for (int i = 0; i < _eggSlotsInd.Length; i++)
        //        if (_eggSlotsInd[i].rt)
        //        {
        //            _eggSlotsInd[i].rt.gameObject.SetActive(false);
        //            _eggSlotsInd[i].visible = false;
        //        }
    }

    // ====== 오프스크린 인디케이터 갱신(쓰로틀) ======
    private void UpdateOffscreenIndicators()
    {
        // 쓰로틀(버벅이면 0으로)
        if (Time.unscaledTime < _nextIndicatorUpdateTime) return;
        _nextIndicatorUpdateTime = Time.unscaledTime + indicatorUpdateInterval;

        if (IsExpanded || indicatorPrefab == null || minimapCamera == null || minimapCanvas == null)
        { HideAllIndicators(); return; }

        if (myPlayer == null)
        {
            myPlayer = InGameManager.Instance?.MyPlayer;
        }
        if (myPlayer == null) { HideAllIndicators(); return; }

        EnsureIndicatorPool();

        // 미니맵 아이콘과 동일 Y 평면
        //_panelYCached = (icons.Count > 0)
        //    ? icons.Values.Where(a => a.kind == MinimapKind.Player || a.kind == MinimapKind.Monster || a.kind == MinimapKind.Item).First().transform.position.y
        //    : minimapCanvas.position.y;
        _panelYCached = minimapCanvas.position.y;

        // 오쏘 사각경계
        float halfH = minimapCamera.orthographicSize;
        float aspect = minimapCamera.targetTexture
            ? (float)minimapCamera.targetTexture.width / minimapCamera.targetTexture.height
            : minimapCamera.aspect;
        float halfW = halfH * aspect;

        // 카메라 로컬 평면 축(회전 반영) — forward 대신 up 사용!
        var camTr = minimapCamera.transform;
        Vector3 camPos = camTr.position;
        Vector3 camRight = new Vector3(camTr.right.x, 0f, camTr.right.z).normalized;
        Vector3 camFwd = new Vector3(camTr.up.x, 0f, camTr.up.z).normalized;
        if (camRight.sqrMagnitude < 1e-6f) camRight = Vector3.right;
        if (camFwd.sqrMagnitude < 1e-6f) camFwd = Vector3.forward;

        // ---- 몬스터 최대 N개 ----
        if (!isRaid)
        {
            var nearest = InGameManager.Instance?.NearestMonsters;
            int take = (nearest != null) ? Mathf.Min(maxMonsterIndicators, nearest.Count) : 0;

            for (int i = 0; i < _monsterSlotsInd.Length; i++)
            {
                var slot = _monsterSlotsInd[i];

                if (nearest == null || i >= take || nearest[i] == null)
                {
                    if (slot.visible) { slot.rt.gameObject.SetActive(false); slot.visible = false; slot.vel = Vector3.zero; slot.yawVel = 0f; }
                    _monsterSlotsInd[i] = slot;
                    continue;
                }

                var tr = nearest[i].transform;
                if (ProjectToRectEdge(camPos, camRight, camFwd, halfW, halfH, edgePadding, tr.position,
                                      out Vector3 edgeWorld, out float yawDeg))
                {
                    int distM = Mathf.RoundToInt(Vector3.Distance(myPlayer.transform.position, tr.position));
                    SmoothApply(ref slot, edgeWorld, yawDeg, distM, monsterColor);
                }
                else
                {
                    if (slot.visible) { slot.rt.gameObject.SetActive(false); slot.visible = false; slot.vel = Vector3.zero; slot.yawVel = 0f; }
                }

                _monsterSlotsInd[i] = slot;
            }
            // ---- 상점 1개(가장 가까운 것) ----
            var shops = InGameManager.Instance?.ShopSpawner?.ActiveShops;
            if (_shopInd.rt != null && shops != null && shops.Count > 0)
            {
                ShopObject nearestShop = null;
                float bestSqr = float.MaxValue;
                Vector3 myPos = myPlayer.transform.position;
                for (int i = 0; i < shops.Count; i++)
                {
                    var s = shops[i]; if (s == null) continue;
                    float sq = (s.transform.position - myPos).sqrMagnitude;
                    if (sq < bestSqr) { bestSqr = sq; nearestShop = s; }
                }

                if (nearestShop != null &&
                    ProjectToRectEdge(camPos, camRight, camFwd, halfW, halfH, edgePadding,
                                      nearestShop.transform.position, out Vector3 edgeW, out float yaw))
                {
                    int distM = Mathf.RoundToInt(Vector3.Distance(myPlayer.transform.position, nearestShop.transform.position));
                    SmoothApply(ref _shopInd, edgeW, yaw, distM, itemColor);
                }
                else
                {
                    if (_shopInd.visible) { _shopInd.rt.gameObject.SetActive(false); _shopInd.visible = false; _shopInd.vel = Vector3.zero; _shopInd.yawVel = 0f; }
                }
            }
            else
            {
                if (_shopInd.visible) { _shopInd.rt.gameObject.SetActive(false); _shopInd.visible = false; _shopInd.vel = Vector3.zero; _shopInd.yawVel = 0f; }
            }
        }
    }


    /// <summary>
    /// target을 카메라 로컬 평면(X=Right, Y=Up(=지도 위쪽))으로 투영하여
    /// 패딩 적용 사각형(halfW, halfH) 경계에 정확히 올려놓는다.
    /// 화면 내부면 false, 밖이면 true와 worldPos/yaw를 반환.
    /// </summary>
    private bool ProjectToRectEdge(
        Vector3 camPos, Vector3 camRight, Vector3 camUpAsForward,
        float halfW, float halfH, float padding,
        Vector3 targetWorldPos,
        out Vector3 worldPosOnEdge, out float worldYawDeg)
    {
        Vector3 delta = targetWorldPos - camPos;
        float dx = Vector3.Dot(delta, camRight);        // +x
        float dz = Vector3.Dot(delta, camUpAsForward);  // +z(지도 위쪽)

        float padW = Mathf.Max(0.01f, halfW - padding);
        float padH = Mathf.Max(0.01f, halfH - padding);

        if (Mathf.Abs(dx) <= padW && Mathf.Abs(dz) <= padH)
        {
            worldPosOnEdge = default;
            worldYawDeg = 0f;
            return false; // 화면 안
        }

        float s = Mathf.Max(Mathf.Abs(dx) / padW, Mathf.Abs(dz) / padH);
        if (s < 1e-4f) s = 1f;

        float ex = dx / s;
        float ez = dz / s;

        Vector3 edgeOffset = camRight * ex + camUpAsForward * ez;
        worldPosOnEdge = new Vector3(camPos.x + edgeOffset.x, _panelYCached, camPos.z + edgeOffset.z);

        Vector3 flatDir = targetWorldPos - worldPosOnEdge; flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 1e-6f) flatDir = delta;
        worldYawDeg = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
        return true;
    }
    void SmoothApply(ref IndicatorSlot slot, Vector3 targetPos, float targetYawDeg, int distM, Color color)
    {
        if (!slot.visible) { slot.rt.gameObject.SetActive(true); slot.visible = true; }

        var newPos = Vector3.SmoothDamp(
            slot.rt.position, targetPos, ref slot.vel,
            Mathf.Max(0.0001f, indicatorPosSmoothTime),
            Mathf.Infinity, Time.unscaledDeltaTime);

        var newYaw = Mathf.SmoothDampAngle(
            slot.lastYaw, targetYawDeg, ref slot.yawVel,
            Mathf.Max(0.0001f, indicatorYawSmoothTime),
            Mathf.Infinity, Time.unscaledDeltaTime);

        if ((newPos - slot.rt.position).sqrMagnitude > 1e-6f)
        {
            slot.rt.position = newPos; slot.lastPos = newPos;
        }
        if (Mathf.Abs(Mathf.DeltaAngle(slot.lastYaw, newYaw)) > 0.05f)
        {
            slot.rt.rotation = Quaternion.Euler(90f, newYaw, 0f); slot.lastYaw = newYaw;
        }

        // 거리 텍스트는 값 변할 때만
        if (slot.text && slot.lastDistM != distM)
        {
            slot.text.text = distM.ToString() + "m";
            slot.lastDistM = distM;
        }

        // 색상은 한 번만
        if (slot.img && slot.img.color != color) slot.img.color = color;
        if (slot.text && slot.text.color != color) slot.text.color = color;
    }
}
