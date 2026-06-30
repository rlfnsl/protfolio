using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Purchasing;
using UnityEngine.UI;

public class IAPManager : MonoBehaviour, IStoreListener
{
    public static IAPManager Instance;
    public Transform Menu, Panel;
    public Sprite[] ClickSprite;
    public GameObject Popup;
    [HideInInspector]
    int[] BuyCash = { 1000, 4700, 9800, 19800, 49900, 99000 };
    public TextMeshProUGUI[] BuyCashText;
    [HideInInspector]
    int[] CashToDia = { 110, 570, 1200, 2500, 6650, 14200 };
    public TextMeshProUGUI[] CashToDiaText;
    [HideInInspector]
    int[] BuyEnergy = { 5, 10, 30, 50, 80, 100 };
    int AdmobEnergy = 3;
    [SerializeField]
    TextMeshProUGUI AdmobEnergyText;
    [SerializeField]
    ParticleMove AdmobEnergyParticle;
    public TextMeshProUGUI[] BuyEnergyText;
    public TextMeshProUGUI[] DiaToEnergyText;
    [HideInInspector]
    int[] BuyGold = { 10000, 50000, 100000, 330000, 600000, 1300000 };
    public TextMeshProUGUI[] BuyGoldText;
    public TextMeshProUGUI[] DiaToGoldText;
    public ParticleMove[] energy;
    public ParticleMove[] cash;
    public ParticleMove[] gold;
    public IAPGachaItem[] IAPgachaItem;
    List<GameObject> PanelList = new List<GameObject>();
    List<Image> MenuList = new List<Image>();
    List<ScrollRect> scrollList = new List<ScrollRect>();
    Coroutine cor;
    float energyValue = 5;
    float goldValue = 100;
    [Header("Product ID")]
    public readonly string productId_id1 = "id1";
    public readonly string productId_id2 = "id2";
    public readonly string productId_id3 = "id3";
    public readonly string productId_id4 = "id4";
    public readonly string productId_id5 = "id5";
    public readonly string productId_id6 = "id6";
    public readonly string[] productIDs = { "id1", "id2", "id3", "id4" , "id5" , "id6" };

    [Header("Cache")]
    private IStoreController storeController; //구매 과정을 제어하는 함수 제공자
    private IExtensionProvider storeExtensionProvider; //여러 플랫폼을 위한 확장 처리 제공자
    [HideInInspector]
    public bool check;

    public GameObject skillitemPrefab;
    public Transform skillitemParent;
    public List<IAPSkillItem> IAPSkillItems = new();

    private void Awake()
    {
        Instance = this;
        for (int i = 0; i < Panel.childCount; i++)
        {
            PanelList.Add(Panel.GetChild(i).gameObject);
            scrollList.Add(Panel.GetChild(i).GetComponentInChildren<ScrollRect>());
        }
        for (int i = 0; i < Menu.childCount; i++)
        {
            int count = i;
            MenuList.Add(Menu.GetChild(i).GetComponent<Image>());
            MenuList[i].GetComponent<Button>().onClick.AddListener(() => ClickButton(count));
        }
        //for (int i = 0; i < BuyCash.Length; i++)
        //{
        //    BuyCashText[i].text = string.Format("{0:#,000}", BuyCash[i]);
        //    CashToDiaText[i].text = string.Format("{0:#,000}", CashToDia[i]);
        //}
        for (int i = 0; i < BuyGold.Length; i++)
        {
            BuyGoldText[i].text = string.Format("{0:#,000}", BuyGold[i]);
            DiaToGoldText[i].text = string.Format("{0:#,000}", BuyGold[i] / goldValue);
        }
        for (int i = 0; i < BuyEnergy.Length; i++)
        {
            BuyEnergyText[i].text = BuyEnergy[i].ToString();
            DiaToEnergyText[i].text = (BuyEnergy[i] / energyValue).ToString();
        }
        AdmobEnergyText.text = AdmobEnergy.ToString();
        for (int i = 0; i < IAPgachaItem.Length; i++)
        {
            int count = i;
            IAPgachaItem[i].transform.GetChild(0).GetComponent<Button>().onClick.AddListener(() => GachaPurchas(IAPgachaItem[count]));
            IAPgachaItem[i].Setting();
        }
    }
    private void Start()
    {
        InitUnityIAP(); //Start 문에서 초기화 필수
        StartCoroutine(DelayedGenerateSkillShopList()); // 상점에 스킬 생성
    }

    public void TestFuncSetting()
    {
        for (int i = 0; i < IAPgachaItem.Length; i++)
        {
            int count = i;
            IAPgachaItem[i].transform.GetChild(0).GetComponent<Button>().onClick.AddListener(() => GachaPurchas(IAPgachaItem[count]));
            IAPgachaItem[i].Setting();
        }
    }
    public void ClickButton(int num)
    {
        for (int i = 0; i < scrollList.Count; i++)
        {
            scrollList[i].content.anchoredPosition = new Vector2(0, scrollList[i].content.anchoredPosition.y);
        }
        for (int i = 0; i < MenuList.Count; i++)
        {
            if (i == num)
            {
                MenuList[i].sprite = ClickSprite[1];
                PanelList[i].SetActive(true);
            }
            else
            {
                MenuList[i].sprite = ClickSprite[0];
                PanelList[i].SetActive(false);
            }
        }
    }
    public void ClickPopup(int num)
    {
        Popup.SetActive(false);
        if (LobbyManager.Instance.inventoryPopup.GuideCanvas_InventoryPopup.activeSelf)
            LobbyManager.Instance.inventoryPopup.CloseSkillInfoPopupUI();

        if (num == 0)
        {
            check = true;
        }
        else
        {
            StopCoroutine(cor);
        }
    }
    /// <summary>
    /// 게임머니 카드상자 구매 (장비가챠)
    /// </summary>
    /// <param name="_index"></param>
    public void GachaPurchas(IAPGachaItem _gachaitem)
    {
        cor = StartCoroutine(GachaPurchasCor(_gachaitem));
    }
    IEnumerator GachaPurchasCor(IAPGachaItem _gachaitem)
    {
        Popup.SetActive(true);
        check = false;
        yield return new WaitUntil(() => check);
        _gachaitem.Click();
    }
    /// <summary>
    /// 골드구매
    /// </summary>
    /// <param name="_index"></param>
    public void GoldUp(int _index)
    {
        cor = StartCoroutine(GoldUpCor(_index));
    }
    IEnumerator GoldUpCor(int _index)
    {
        int plus = BuyGold[_index];
        int minus = (int)(BuyGold[_index] / goldValue);
        ParticleMove _par = gold[_index];
        if (UserDataBase.Instance.userData.cash < minus)
        {
            GameManager.gInstance.PopUpOpen("nocash");
            yield break;
        }
        Popup.SetActive(true);
        check = false;
        yield return new WaitUntil(() => check);
        _par.play((_index + 1) * 5);
        GameAudioManager.Ensure().PlayPurchaseGoldSfx();
        UserDataBase.Instance.userData.gamegold += plus;
        UserDataBase.Instance.userData.cash -= minus;
        APIData.Instance.SetUserData(APIData.PostType.goldPurchase);
    }
    /// <summary>
    /// 에너지구매
    /// </summary>
    /// <param name="_index"></param>
    public void EnergyUp(int _index)
    {
        cor = StartCoroutine(EnergyUpCor(_index, _index < 0));
    }
    IEnumerator EnergyUpCor(int _index, bool _isAdmob = false)
    {
        if (!_isAdmob)
        {
            int plus = BuyEnergy[_index];
            int minus = (int)(BuyEnergy[_index] / energyValue);
            ParticleMove _par = energy[_index];
            if (UserDataBase.Instance.userData.cash < minus)
            {
                GameManager.gInstance.PopUpOpen("nocash");
                yield break;
            }
            if (UserDataBase.Instance.userData.TotalEnergy + plus > UserDataBase.Instance.Max)
            {
                GameManager.gInstance.PopUpOpen("energymax");
                yield break;
            }
            Popup.SetActive(true);
            check = false;
            yield return new WaitUntil(() => check);
            _par.play((_index + 1) * 5);
            GameAudioManager.Ensure().PlayPurchaseEnergySfx();
            UserDataBase.Instance.userData.energy += plus;
            UserDataBase.Instance.userData.cash -= minus;
            APIData.Instance.SetUserData(APIData.PostType.energyPurchase);
        }
        else
        {
            int plus = AdmobEnergy;
            ParticleMove _par = AdmobEnergyParticle;
            if (UserDataBase.Instance.userData.TotalEnergy + plus > UserDataBase.Instance.Max)
            {
                GameManager.gInstance.PopUpOpen("energymax");
                yield break;
            }
            AdmobManager.instance.ShowAd();
            check = false;
            yield return new WaitUntil(() => check);
            _par.play(5);
            GameAudioManager.Ensure().PlayPurchaseEnergySfx();
            UserDataBase.Instance.userData.energy += plus;
            APIData.Instance.SetUserData(APIData.PostType.energyPurchase);
        }
    }
    public void Setting(int _optionvalue)
    {
        for (int i = 0; i < scrollList.Count; i++)
        {
            scrollList[i].content.anchoredPosition = new Vector2(0, scrollList[i].content.anchoredPosition.y);
        }
        ClickButton(_optionvalue);
    }
    /* Unity IAP를 초기화하는 함수 */
    private void InitUnityIAP()
    {
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        /* 구글 플레이 상품들 추가 */
        builder.AddProduct(productId_id1, ProductType.Consumable);
        builder.AddProduct(productId_id2, ProductType.Consumable);
        builder.AddProduct(productId_id3, ProductType.Consumable);
        builder.AddProduct(productId_id4, ProductType.Consumable);
        builder.AddProduct(productId_id5, ProductType.Consumable);
        builder.AddProduct(productId_id6, ProductType.Consumable);

        UnityPurchasing.Initialize(this, builder);

        for (int i = 0; i < BuyCash.Length; i++)
        {
            BuyCashText[i].text = GetPrice(productIDs[i]);
            CashToDiaText[i].text = string.Format("{0:#,000}", CashToDia[i]);
        }
    }

    string GetPrice(string productId)
    {
        Product product = storeController.products.WithID(productId);
        if (product != null)
        {
            string localizedPrice = product.metadata.localizedPriceString;
            Debug.Log("Product Price: " + localizedPrice);
            return localizedPrice;
        }
        else
        {
            Debug.Log("Product not found");
            return "Loding...";
        }
    }

    /* 구매하는 함수 */
    public void Purchase(string productId)
    {
        Product product = storeController.products.WithID(productId); //상품 정의

        if (product != null && product.availableToPurchase) //상품이 존재하면서 구매 가능하면
        {
            storeController.InitiatePurchase(product); //구매가 가능하면 진행
        }
        else //상품이 존재하지 않거나 구매 불가능하면
        {
            Debug.Log("상품이 없거나 현재 구매가 불가능합니다");
        }
    }

    IEnumerator DelayedGenerateSkillShopList()
    {
        // SkillPageManager가 준비될 때까지 대기
        yield return new WaitUntil(() => SkillPageManager.instance != null);

        // 추가로 myskills가 초기화될 때까지 대기
        yield return new WaitUntil(() => SkillPageManager.instance.myskills != null);

        // 한 프레임 더 대기 (안전장치)
        yield return null;

        CreateSkillList();
    }

    void CreateSkillList()
    {
        // 보유 스킬 중 piece가 15개 미만인 것만 필터링
        var availableSkills = SkillPageManager.instance.myskills
            .Where(x => x.Value.myskill.skillpiece < 15) // 조각 15개 미만
            .Select(x => x.Key); // skillId만 추출

        foreach (int skillId in availableSkills)
        {
            // 스킬 상점 아이템 생성
            IAPSkillItem newItem = Instantiate(skillitemPrefab, skillitemParent).GetComponent<IAPSkillItem>();

            // 버튼 이벤트 연결
            newItem.transform.GetChild(0).GetComponent<Button>()
                .onClick.AddListener(() => SkillPurchase(newItem));

            newItem.Id = skillId;
            newItem.price = CalculateSkillPrice(skillId);

            newItem.Setting();

            IAPSkillItems.Add(newItem);
        }

        if (skillitemPrefab.scene.rootCount != 0)
            Destroy(skillitemPrefab);

        GameAudioManager.Ensure().RefreshButtonSfxBindings();
    }

    // 스킬 가격 계산
    int CalculateSkillPrice(int skillId)
    {
        var skillData = APIData.Instance.SkillKeyValue[skillId];
        // 등급에 따른 가격 설정 예시
        switch (skillData.rank)
        {
            case 1: return 200;
            case 2: return 300;
            case 3: return 500;
            case 4: return 800;
            case 5: return 1000;
            default: return 200;
        }
    }

    // 스킬 구매 (새로 추가)
    public void SkillPurchase(IAPSkillItem _skillitem)
    {
        cor = StartCoroutine(SkillPurchaseCor(_skillitem));
    }

    IEnumerator SkillPurchaseCor(IAPSkillItem _skillitem)
    {
        LobbyManager.Instance.inventoryPopup.OpenSkillInfoPopUpUI_withShop(_skillitem.SKILLDATA);
        check = false;
        yield return new WaitUntil(() => check);
        _skillitem.Click();
    }

    public void buy_done(IAPSkillItem _skillitem)
    {
        IAPSkillItems.Remove(_skillitem);
        Destroy(_skillitem.gameObject);
    }

    #region Interface
    /* 초기화 성공 시 실행되는 함수 */
    public void OnInitialized(IStoreController controller, IExtensionProvider extension)
    {
        Debug.Log("초기화에 성공했습니다");

        storeController = controller;
        storeExtensionProvider = extension;
    }

    /* 초기화 실패 시 실행되는 함수 */
    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.Log("초기화에 실패했습니다");
    }

    /* 구매에 실패했을 때 실행되는 함수 */
    public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
    {
        Debug.Log("구매에 실패했습니다");
    }

    /* 구매를 처리하는 함수 */
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        PaymentData paymentData = new PaymentData();
        paymentData.price = -1;
#if !UNITY_EDITOR
#if UNITY_ANDROID
        googlePayloadJson GoogleReceipt = getReceiptPayload(args.purchasedProduct.receipt);
        paymentData.productId = args.purchasedProduct.definition.id;
        paymentData.orderId = GoogleReceipt.orderId;
#endif
#else
        paymentData.productId = args.purchasedProduct.definition.id;
        paymentData.orderId = "GPA.Test." + System.DateTime.Now;
#endif

        Debug.Log("구매에 성공했습니다");
        ParticleMove _par = null;
        int _count = 0;
        //천원
        if (args.purchasedProduct.definition.id == productId_id1)
        {
            UserDataBase.Instance.userData.cash += CashToDia[0];
            _par = cash[0];
            _count = 5;
        }
        //오천원
        else if (args.purchasedProduct.definition.id == productId_id2)
        {
            UserDataBase.Instance.userData.cash += CashToDia[1];
            _par = cash[1];
            _count = 10;
        }
        //만원(10%)
        else if (args.purchasedProduct.definition.id == productId_id3)
        {
            UserDataBase.Instance.userData.cash += CashToDia[2];
            _par = cash[2];
            _count = 15;
        }
        //3만원(30%)
        else if (args.purchasedProduct.definition.id == productId_id4)
        {
            UserDataBase.Instance.userData.cash += CashToDia[3];
            _par = cash[3];
            _count = 20;
        }
        //5만원(50%)
        else if (args.purchasedProduct.definition.id == productId_id5)
        {
            UserDataBase.Instance.userData.cash += CashToDia[4];
            _par = cash[4];
            _count = 25;
        }
        //10만원(100%)
        else if (args.purchasedProduct.definition.id == productId_id6)
        {
            UserDataBase.Instance.userData.cash += CashToDia[5];
            _par = cash[5];
            _count = 30;
        }
        _par.play(_count);
        GameAudioManager.Ensure().PlayPurchaseDiamondSfx();
        APIData.Instance.SetUserData(APIData.PostType.cash);
        APIData.Instance.SendReceiptData(paymentData);
        Debug.Log("paymentData.productId : " + paymentData.productId + ", paymentData.orderId : " + paymentData.orderId + "paymentData.price : " + paymentData.price);
        paymentData = null;
        return PurchaseProcessingResult.Pending;
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        throw new System.NotImplementedException();
    }

#region ctrl+c, ctrl+v
    class googlePayloadJson
    {
        public string orderId;
        public string packageName;
        public string productId;
        public uint purchaseTime;
        public uint purchaseState;
        public string purchaseToken;
        public googlePayloadJson()
        {
            this.orderId = null;
            this.packageName = null;
            this.productId = null;
            this.purchaseTime = 0;
            this.purchaseState = 0;
            this.purchaseToken = null;
        }
    }

    [System.Serializable]
    class unityReceipt
    {
        public string Store;
        public string TransactionID;
        public string Payload;
        public unityReceipt()
        {
            this.Store = null;
            this.TransactionID = null;
            this.Payload = null;
        }
    }
    [System.Serializable]
    class googlePlayPayload
    {
        public string json;
        public string signature;
        public googlePlayPayload()
        {
            this.json = null;
            this.signature = null;
        }
    }
    private googlePayloadJson getReceiptPayload(string strReceipt)
    {
#if UNITY_EDITOR
        return null;
#endif
        unityReceipt u5r = JsonUtility.FromJson<unityReceipt>(strReceipt);
        if (u5r != null)
        {
            googlePayloadJson receipt = new googlePayloadJson();
            googlePlayPayload gpp = JsonUtility.FromJson<googlePlayPayload>(u5r.Payload);
            if (gpp != null)
            {
                string go = gpp.json;
                if (!string.IsNullOrEmpty(go))
                {
                    googlePayloadJson googleJson = JsonUtility.FromJson<googlePayloadJson>(go);
                    if (googleJson != null)
                    {
                        receipt = googleJson;
                    }
                    else
                    {
                        receipt = null;
                    }
                }
            }

            return receipt;// OK
        }
        return null;
    }
#endregion
#endregion
}
