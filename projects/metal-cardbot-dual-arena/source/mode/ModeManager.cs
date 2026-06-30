using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Funtion_CWS;
using static LanguageSingleton;
using TMPro;
using Unity.VisualScripting;

public class ModeManager : MonoBehaviour
{
    public static ModeManager Instance { get; private set; }
    DisPosObject disposObject;
    [SerializeField]
    Transform mode;
    List<objectinfo> modeObj = new List<objectinfo>();
    string[] keys = new string[4] { "system_mode_soloplay", "showInfo", "system_mode_challenge", "system_mode_duoplay" };
    string[] modeexplaion = new string[4] { "system_explain_soloplay", "system_exceptionInfo", "system_explain_challenge", "system_explain_duoplay" };
    public string[] scenename = new string[4] { "Battle", "PlayerInfo", "ChallengeBattle", "DuoBattle" };
    GameObject maskObj;
    [SerializeField]
    TextMeshProUGUI timer;
    public LangText exceptiontext;
    float time;
    bool IsDisPosing;
    public bool CanClick = false;
    public bool IsInfo;
    public bool IsCardRiding;
    public bool CanChange = true;
    public int CurIndex;
    public TextMeshProUGUI modeText;
    public Sprite[] sprites;
    public AudioClip BGM;
    public GameObject Dispos, InfoPanel, OneBtn, ThreeBtn;
    public Color EnableColor;
    public button redbtn, bluebtn;
    public CanvasGroup fade;
    //public Image ButtonImage;
    //public TextMeshProUGUI ButtonText;
    private void Awake()
    {
        Instance = this;
        Gamemanager.instance.ErrorCount = 0;
        fade.alpha = 1;
        //if (!Gamemanager.instance.ShowInfo||!RFCard.instance.CardCheck())
        //    RFCard.instance.CardTaging();
        BGM = Gamemanager.instance.LobbySound != null ? Gamemanager.instance.LobbySound : BGM;
        if (!RFCard.instance.CardCheck())
            RFCard.instance.CardTaging();
        SerialPortManager.Instance.SetSideLED(SideLED.Title);
        Gamemanager.instance.IsRestart = false;
        maskObj = Resources.Load<GameObject>("PreFab/first");
        CanChange = true;
        IsDisPosing = true;
        Gamemanager.instance.GameMode = 0;
        time = DataManager.Instance.ProductionData.default_skip_time + 1;
        disposObject = Dispos.AddComponent<DisPosObject>();
        if (sprites.Length > 1)
        {
            CurIndex = sprites.Length;
            for (int i = 0; i < sprites.Length * 2; i++)
            {
                GameObject _g = Instantiate(maskObj, mode);
                RectTransform _rect = _g.GetComponent<RectTransform>();
                _rect.anchoredPosition = new Vector2(i * 600, 0);
                int _index = i % sprites.Length;
                objectinfo _info = _g.AddComponent<objectinfo>();
                _info.Initialize(i, keys[_index], sprites[_index], (sprites.Length * 2 - 1) * 600, modeexplaion[_index]);
                modeObj.Add(_info);
            }
            mode.GetComponent<RectTransform>().anchoredPosition = new Vector2(-(sprites.Length * 600), 68);
        }
        else
        {
            GameObject _g = Instantiate(maskObj, mode);
            RectTransform _rect = _g.GetComponent<RectTransform>();
            _rect.anchoredPosition = Vector2.zero;
            int _index = 0;
            objectinfo _info = _g.AddComponent<objectinfo>();
            _info.Initialize(_index, keys[_index], sprites[_index], 0, modeexplaion[_index]);
            modeObj.Add(_info);
        }
        if (sprites.Length == 1)
        {
            OneBtn.SetActive(true);
        }
        else
        {
            ThreeBtn.SetActive(true);
        }

    }
    IEnumerator Start()
    {
        if (Gamemanager.instance.ShowInfo || sprites.Length == 1)
        {
            InfoPanel.SetActive(false);
        }
        else
        {
            InfoPanel.SetActive(true);
        }
        yield return fade.SetAlphaCanvasGroup_num(0, 0.5f);
        CanClick = true;
        Gamemanager.instance.PlayBGM(BGM);
        if (Gamemanager.instance.ShowInfo || sprites.Length == 1)
        {
            Gamemanager.instance.PlayOneShotTTS(4);
            IsDisPosing = false;
            ModeKey.instance.StageSetting();
        }
        else
        {
            IsDisPosing = true;
            yield return new WaitForSeconds(3);
            OffPaenl();
        }
    }
    public void OffPaenl()
    {
        if (!CanClick) return;
        if (InfoPanel.activeSelf)
        {
            Gamemanager.instance.PlayOneShotTTS(4);
            InfoPanel.SetActive(false);
            IsDisPosing = false;
            ModeKey.instance.StageSetting();
        }
    }
    private void Update()
    {
        if (!IsDisPosing)
        {
            if (time > 0)
            {
                time -= Time.deltaTime;
                timer.text = ((int)time).ToString();
            }
            else
            {
                SelectMode();
            }
        }
    }
    public void SelectMode()
    {
        RFCard.instance.StopCardTaging();
        if (!IsInfo)
        {
            StartCoroutine(CardDisPos());
        }
        else
        {
            Gamemanager.instance.ShowInfo = true;
            Gamemanager.instance.ChangeScene("PlayerInfo", false);
        }
    }
    IEnumerator CardDisPos()
    {
        ModeKey.instance.NullSetting();
        Gamemanager.instance.ChoiceBtn();
        Gamemanager.instance.GameModeText = modeText.text;
        if(Gamemanager.instance.SkipDisPos)
        {
            disposObject.Next();
            yield break;
        }
        Gamemanager.instance.DisPoseCard();
        disposObject.Init();
        IsDisPosing = true;
        yield return StartCoroutine(disposObject.FadeInOut());
    }
    public void ClickBtn(bool _isLeft)
    {
        if (sprites.Length != 1)
        {
            if (!CanChange) return;
            Gamemanager.instance.PushBtn();
            CanChange = false;
            if (_isLeft)
            {
                CurIndex--;
                if (CurIndex < 0)
                    CurIndex = sprites.Length * 2 - 1;
            }
            else CurIndex++;
            Gamemanager.instance.GameMode = CurIndex % sprites.Length;
            for (int i = 0; i < modeObj.Count; i++)
            {
                modeObj[i].ClickBtn(_isLeft);
            }
        }
        else
        {
            SelectMode();
        }
    }
    public void AddDisPos(bool _isleft)
    {
        if ((string.IsNullOrEmpty(Gamemanager.instance.unit1Qr)||Gamemanager.instance.isunit1zero) && _isleft)
            return;
        if ((string.IsNullOrEmpty(Gamemanager.instance.unit2Qr) || Gamemanager.instance.isunit2zero) && !_isleft)
            return;
        Gamemanager.instance.ChoiceBtn();
        StartCoroutine(disposObject.AddDisPos(_isleft));
    }
    public void AddDisPosTimerReset()
    {
        disposObject.TimerReset();
    }
    public void Next()
    {
        Gamemanager.instance.ChoiceBtn();
        disposObject.Next();
    }
    public class DisPosObject : MonoBehaviour
    {
        CanvasGroup cg;
        CanvasGroup blackbg;
        GameObject DisposPanel, AddDisPosPanel;
        TextMeshProUGUI Count, timer;
        int preCount;
        bool isAddDispos;
        float time = 0;
        bool isGray = false;
        int CurCoin
        {
            get
            {
                return Gamemanager.instance.CurCoin;
            }
        }
        int MaxCoin
        {
            get
            {
                //if (Gamemanager.instance.CardDisposCoin == 0)
                //    return 0;
                //return Gamemanager.instance.CardDisposCoin / 500;
                return Gamemanager.instance.MaxCoin;
            }
        }
        int CardPrice
        {
            get
            {
                if (Gamemanager.instance.CardDisposCoin == 0)
                    return 0;
                return Gamemanager.instance.CardDisposCoin;
            }
        }
        private void Awake()
        {
            cg = GetComponent<CanvasGroup>();
            DisposPanel = transform.FindObject("DisposPanel");
            AddDisPosPanel = transform.FindObject("AddDisposPanel");
            blackbg = transform.FindObject<CanvasGroup>("blackcg");
            Count = transform.FindObject<TextMeshProUGUI>("ToTalCardCount");
            timer = transform.FindObject<TextMeshProUGUI>("timer");
            time = DataManager.Instance.ProductionData.default_skip_time + 1;
            preCount = CurCoin;
            DisposPanel.SetActive(false);
            AddDisPosPanel.SetActive(false);
            isGray = false;
            cg.alpha = 0;
            blackbg.alpha = 1;
        }
        public void Init()
        {
            if (MaxCoin != 0)
            {
                Count.text = $"{CurCoin / MaxCoin}({CurCoin % MaxCoin}/{MaxCoin})";
                if (CurCoin < MaxCoin)
                {
                    isGray = true;
                }
            }
            else
            {
                Count.text = "Free";
            }
        }
        public void TimerReset()
        {
            time = DataManager.Instance.ProductionData.default_skip_time + 1;
        }
        private void Update()
        {

            if (isAddDispos)
            {
                if (time > 0)
                {
                    time -= Time.deltaTime;
                    timer.text = ((int)time).ToString();
                }
                else
                {
                    isAddDispos = false;
                    Next();
                }
                if (MaxCoin != 0)
                {
                    if (preCount != CurCoin)
                    {
                        Count.text = $"{CurCoin / MaxCoin}({CurCoin % MaxCoin}/{MaxCoin})";
                        preCount = CurCoin;
                        if (isGray)
                        {
                            if (CurCoin >= MaxCoin)
                            {
                                isGray = false;
                                BtnChange();
                            }
                        }
                        else
                        {
                            if (CurCoin < MaxCoin)
                            {
                                isGray = true;
                                BtnChange();
                            }
                        }
                    }
                }
            }
        }
        void BtnChange()
        {
            if (string.IsNullOrEmpty(Gamemanager.instance.unit1Qr)||Gamemanager.instance.isunit1zero)
            {
                Instance.redbtn.ActiveChange(false);
            }
            else
            {
                Instance.redbtn.ActiveChange(!isGray);
            }
            if (string.IsNullOrEmpty(Gamemanager.instance.unit2Qr) || Gamemanager.instance.isunit2zero)
            {
                Instance.bluebtn.ActiveChange(false);
            }
            else
            {
                Instance.bluebtn.ActiveChange(!isGray);
            }
        }
        public IEnumerator FadeInOut()
        {
            if (Gamemanager.instance.IsAdminID)
            {
                Next();
                yield break;
            }
            yield return new WaitForEndOfFrame();
            SerialPortManager.Instance.SetSideLED(SideLED.CardOutDefault);
            yield return StartCoroutine(cg.SetAlphaCanvasGroup_num(1, 0.5f));
            Gamemanager.instance.PlayOneShotTTS(5);
            DisposPanel.SetActive(true);
            yield return blackbg.SetAlphaCanvasGroup_num(0, 0.5f);
#if UNITY_EDITOR || Com
            yield return new WaitForSeconds(2);
#else

            float time = 0;
            while (time <= 6 && SerialPortManager.Instance.IsCardTaking)
            {
                yield return null;
                time += Time.deltaTime;
            }
#endif
            yield return StartCoroutine(Gamemanager.instance.IsCardZero());
            BtnChange();
            if (Gamemanager.instance.ZeroCard())
            {
                Gamemanager.instance.ErrorCount = 1;
                Next();
                yield break;
            }
            if (string.IsNullOrEmpty(Gamemanager.instance.unit1Qr) && string.IsNullOrEmpty(Gamemanager.instance.unit2Qr))
            {
                Next();
                yield break;
            }
            AddDisPosPanel.GetComponent<AddDisPosCard>().Setting();
            Gamemanager.instance.IsCardDispos = true;
            isAddDispos = true;
            DisposPanel.SetActive(false);
            Gamemanager.instance.PlayOneShotTTS(6);
            AddDisPosPanel.SetActive(true);
            ModeKey.instance.AddDisPos();
            if (MaxCoin == 0)
                yield break;
            Gamemanager.instance.CardReading(true);
            yield return new WaitUntil(() => !Instance.IsCardRiding);
            Gamemanager.instance.CardReading(false);
        }
        public IEnumerator AddDisPos(bool _isleft)
        {
            if (CurCoin < MaxCoin && MaxCoin != 0)
            {
                yield break;
            }
            Gamemanager.instance.PlayOneShotTTS(5);
            Gamemanager.instance.CurCoin -= MaxCoin;
            Gamemanager.instance.SetCoinText();
            ModeKey.instance.NullSetting();
            isAddDispos = false;
            Instance.redbtn.ActiveChange(false);
            Instance.bluebtn.ActiveChange(false);
            isGray = true;
            Gamemanager.instance.DisPoseCard(_isleft, true);
            AddDisPosPanel.SetActive(false);
            DisposPanel.SetActive(true);
#if !UNITY_EDITOR && !Com
                yield return new WaitUntil(() => !SerialPortManager.Instance.IsCardTaking);
                if (SerialPortManager.Instance.IsError)
                {
                    Debug.Log("카드배출에러");
                    Gamemanager.instance.ErrorCount = 1;
                    Next();
                    yield break;
                }
                else
                {
                    yield return StartCoroutine(Gamemanager.instance.IsCardZero());
                }
#else
            yield return new WaitForSeconds(3);
#endif
            if (Gamemanager.instance.ZeroCard())
            {
                Gamemanager.instance.ErrorCount = 1;
                Next();
                yield break;
            }
            Next();
        }
        public void Next()
        {
            isAddDispos = false;
            Gamemanager.instance.SceneChangeAsync(Instance.scenename[Gamemanager.instance.GameMode]);
            Gamemanager.instance.CardReading(true);
        }
    }

    public class objectinfo : MonoBehaviour
    {
        public int index;
        public string key;
        public string value
        {
            get
            {
                return instance.GetString(key);
            }
        }
        float max;
        Image image;
        Animator ani;
        RectTransform rect;
        CanvasGroup cg;
        bool IsInfo;
        string modeexplain;
        private void Awake()
        {
            ani = GetComponent<Animator>();
            image = transform.FindObject<Image>("ModeImage");
            rect = GetComponent<RectTransform>();
            cg = GetComponent<CanvasGroup>();
        }
        public void Initialize(int index, string key, Sprite _sprite, float _max, string modepxplain)
        {
            this.modeexplain = modepxplain;
            this.index = index;
            this.key = key;
            image.sprite = _sprite;
            image.SetNativeSize();
            if (key == "showInfo")
                IsInfo = true;
            max = _max;
            if (Instance.sprites.Length == 1)
            {
                rect.localScale = Vector3.one;
                Instance.modeText.text = value;
                ani.enabled = true;
            }
            else
            {
                if (index == Instance.sprites.Length)
                {
                    cg.alpha = 1;
                    rect.localScale = Vector3.one;
                    Instance.modeText.text = value;
                    ani.enabled = true;
                }
                else
                {
                    cg.alpha = 0;
                    rect.localScale = Vector3.one * .5f;
                    ani.enabled = false;
                    image.GetComponent<RectTransform>().localScale = Vector3.one;
                }
            }
        }
        public void ClickBtn(bool _isleft)
        {
            StartCoroutine(ClickBtnCor(_isleft));
        }
        IEnumerator ClickBtnCor(bool _isleft)
        {
            float _nextValue = 0;
            if (_isleft)
            {
                _nextValue = rect.anchoredPosition.x + 600;
            }
            else
            {
                _nextValue = rect.anchoredPosition.x - 600;
            }
            if (Instance.CurIndex % (Instance.sprites.Length * 2) == index)
            {
                ani.enabled = true;
                StartCoroutine(rect.TimeScale(1, 1, 0.8f));
                StartCoroutine(cg.SetAlphaCanvasGroup_num(1, .8f));
                Instance.modeText.text = value;
                if (IsInfo)
                {
                    Instance.exceptiontext.LocalizeChanged("system_exceptionInfo");
                    Instance.IsInfo = true;
                }
                else
                {
                    Instance.exceptiontext.LocalizeChanged(modeexplain);
                    Instance.IsInfo = false;
                }
            }
            else
            {
                ani.enabled = false;
                image.GetComponent<RectTransform>().localScale = Vector3.one;
                StartCoroutine(rect.TimeScale(.5f, .5f, 0.8f));
                StartCoroutine(cg.SetAlphaCanvasGroup_num(0, .8f));
            }
            yield return rect.MoveToVector(new Vector3(_nextValue, rect.anchoredPosition.y, 0), .8f);
            if (rect.anchoredPosition.x < -0.1f)
            {
                rect.anchoredPosition = new Vector2(max, rect.anchoredPosition.y);
            }
            if (rect.anchoredPosition.x > max + 1)
            {
                rect.anchoredPosition = Vector2.zero;
            }
            if (!Instance.CanChange)
                Instance.CanChange = true;
        }
    }
}
