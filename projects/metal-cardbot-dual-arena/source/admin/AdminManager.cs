using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Funtion_CWS;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using UnityEngine.Events;
using System;

public class AdminManager : KeyInput
{
    public static AdminManager instance;
    [System.Serializable]
    struct AdminBtn
    {
        public List<Image> btns;
    }
    enum AdminType
    {
        None,
        CalculateSetting,
        test,
        Setting,
        PassWord,
        Info
    }
    AdminBtn[] AdminObject;
    AdminType adminType;
    List<Transform> AdminParant = new List<Transform>();
    List<Image> CurImage = new List<Image>();
    int index;
    [Header("����")]
    int preindex;
    bool Isclick;
    [SerializeField]
    TextMeshProUGUI InfoText;
    [SerializeField]
    GameObject InfoPanel;
    [SerializeField]
    Image[] InfoBtn;
    [Header("����")]
    [SerializeField]
    TextMeshProUGUI totalgamecount;
    [SerializeField]
    TextMeshProUGUI totalexpense;
    [SerializeField]
    TextMeshProUGUI totaldispos;
    [SerializeField]
    TextMeshProUGUI curgamecount;
    [SerializeField]
    TextMeshProUGUI curexpense;
    [SerializeField]
    TextMeshProUGUI curdispos;
    [Header("PassWord")]
    [SerializeField]
    TextMeshProUGUI passwordText;
    [SerializeField]
    TextMeshProUGUI timerText;
    int timer = 31;
    int limitLength = 8;
    int[] password = { 0000 };
    int reportpassword = 0000;
    string realpassword = "";
    [Header("�ϵ�����׽�Ʈ")]
    [SerializeField]
    Color[] btnColor;
    [SerializeField]
    TextMeshProUGUI CreditText, CoinBillText;
    Transform CurHardPage;
    public int coin, bill, cardindex;
    [SerializeField]
    Image CardImage;
    [SerializeField]
    TextMeshProUGUI QRText, NameText, TypeText, GradeText, ValueText, GradeNameText;
    public bool QRTest;
    public Skill skill;
    //public SupportData supportData;
    [Header("�׽�Ʈ������")]
    [SerializeField]
    new AudioSource[] audio;
    [SerializeField]
    TextMeshProUGUI DemoVolume, BGMVolume, EffectVolume, LangText, coinText, CardCountText, CardCoinText, TTSText;
    [Header("Default")]
    [SerializeField]
    Sprite[] ClickImage = new Sprite[2];
    public GameObject BG;
    public Animation spalshAni;
    private void Awake()
    {
        instance = this;
        BG.SetActive(true);
    }
    new IEnumerator Start()
    {
        SoundSetting(true);
        RFCard.instance.MyData.Clear();
        if (!Gamemanager.instance.IsAdMin)
        {
            BG.SetActive(true);
            transform.GetChild(0).gameObject.SetActive(false);
            StartCoroutine(LEDBlink());
            List<Coroutine> cors = new List<Coroutine>
            {
                StartCoroutine(spalshAni.AnimationEnd()),
                StartCoroutine(Gamemanager.instance.IsCardZero())
            };
            foreach (var c in cors)
                yield return c;
            yield return new WaitUntil(() => DataManager.Instance.Check);
            Gamemanager.instance.ChangeScene("Intro");
        }
        else
        {
            BG.SetActive(false);
            yield return new WaitUntil(() => LanguageSingleton.instance.check);
            Init();
            transform.GetChild(0).gameObject.SetActive(true);
            Gamemanager.instance.IsSolo = false;
            PassWordSetting();
            StartCoroutine("TimerCor");
#if UNITY_EDITOR
            StopCoroutine("TimerCor");
            AdminParant[1].SetAsLastSibling();
            CurImage[CurImage.Count - 1].sprite = ClickImage[0];
            adminType = AdminType.None;
            CurImage = AdminObject[1].btns;
            CurImage[0].sprite = ClickImage[1];
            Setting(InputLeftQ, InputLeftW, InputLeftE);
            index = 0;
#endif
        }
        yield break;
    }
    IEnumerator LEDBlink()
    {
        SerialPortManager.Instance.SetSideLED(SideLED.Title);
        yield return new WaitForSeconds(1);
        SerialPortManager.Instance.ButtonLED(BtnLED_Type.Blink, BtnLED_Player.Player1, BtnLED_Button.All, BtnLED_Blink.Normal);
        yield return new WaitForSeconds(0.1f);
        SerialPortManager.Instance.ButtonLED(BtnLED_Type.Blink, BtnLED_Player.Player2, BtnLED_Button.All, BtnLED_Blink.Normal);
    }
    #region �⺻
    public void Init()
    {
        InfoPanel.SetActive(false);
        QRTest = false;
        PageSetting();
        adminType = AdminType.PassWord;
        FindObject(AdminParant, this.transform);
        AdminObject = new AdminBtn[AdminParant.Count];
        for (int i = 0; i < AdminParant.Count; i++)
        {
            AdminObject[i].btns = new List<Image>();
            FindObject(AdminObject[i].btns, AdminParant[i]);
        }
        DataSet();
        AdminParant[0].SetAsLastSibling();
        CurImage = AdminObject[0].btns;
    }
    void FindObject<T>(List<T> _image, Transform _parant) where T : Component
    {
        int childCount = _parant.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = _parant.GetChild(i);
            T imageComponent = child.GetComponent<T>();

            if (imageComponent != null)
            {
                // Image ������Ʈ�� ���� ���� ������Ʈ�� List�� �߰��մϴ�.
                _image.Add(imageComponent);
            }
        }
    }
    #endregion
    void MoveBtn(bool _isLeft)
    {
        int preindex = index;
        index += _isLeft ? -1 : 1;
        if (index < 0)
        {
            index = CurImage.Count - 1;
        }
        else
        {
            index %= CurImage.Count;
        }
        CurImage[preindex].sprite = ClickImage[0];
        CurImage[index].sprite = ClickImage[1];
    }
    void ClickBtn()
    {
        switch (adminType)
        {
            case AdminType.None:
                {
                    if (index == CurImage.Count - 1)
                    {
                        AdminEnd();
                    }
                    else if (index == 0 || index == 1 || index == 2)
                    {
                        for (int i = 0; i < CurImage.Count; i++)
                            CurImage[i].sprite = ClickImage[0];
                        if (index == 0)
                            DataSet();
                        CurImage = AdminObject[index + 2].btns;
                        AdminParant[index + 2].SetAsLastSibling();
                        CurImage[0].sprite = ClickImage[1];
                        adminType = (AdminType)(index + 1);
                        index = 0;
                    }
                    else if (index == 3)
                    {
                        InfoInit(CurImage[index].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text, Gamemanager.instance.AllReset);
                    }
                    else if (index == 4)
                    {
                        InfoInit(CurImage[index].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text, Gamemanager.instance.CoinReset);
                    }
                    else if (index == 5)
                    {
                        InfoInit(CurImage[index].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text, Gamemanager.instance.GameCountReset);
                    }
                    break;
                }
            case AdminType.Info:
                {
                    Isclick = true;
                    break;
                }
            case AdminType.Setting:
                {
                    switch (index)
                    {
                        case 0:
                            {
                                Gamemanager.instance.DemoVolume -= 10;
                                break;
                            }
                        case 1:
                            {
                                Gamemanager.instance.DemoVolume += 10;
                                break;
                            }
                        case 2:
                            {
                                for (int i = 0; i < audio.Length; i++)
                                    audio[i].Stop();
                                audio[0].Play();
                                break;
                            }
                        case 3:
                            {
                                Gamemanager.instance.BGMVolume -= 10;
                                break;
                            }
                        case 4:
                            {
                                Gamemanager.instance.BGMVolume += 10;
                                break;
                            }
                        case 5:
                            {
                                for (int i = 0; i < audio.Length; i++)
                                    audio[i].Stop();
                                audio[1].Play();
                                break;
                            }
                        case 6:
                            {
                                Gamemanager.instance.EffectVolume -= 10;
                                break;
                            }
                        case 7:
                            {
                                Gamemanager.instance.EffectVolume += 10;
                                break;
                            }
                        case 8:
                            {
                                for (int i = 0; i < audio.Length; i++)
                                    audio[i].Stop();
                                audio[2].Play();
                                break;
                            }
                        case 9:
                            {
                                Gamemanager.instance.TTSVolume -= 10;
                                break;
                            }
                        case 10:
                            {
                                Gamemanager.instance.TTSVolume += 10;
                                break;
                            }
                        case 11:
                            {
                                for (int i = 0; i < audio.Length; i++)
                                    audio[i].Stop();
                                audio[3].Play();
                                break;
                            }
                        //case 12:
                        //    {
                        //        LanguageSingleton.instance.curLangIndex++;
                        //        LanguageSingleton.instance.curLangIndex = LanguageSingleton.instance.curLangIndex % 2;
                        //        PlayerPrefs.SetInt("LangIndex", LanguageSingleton.instance.curLangIndex);
                        //        break;
                        //    }
                        case 12:
                            {
                                int count = Gamemanager.instance.MaxCoin;
                                Gamemanager.instance.MaxCoin = Mathf.Clamp(count - 1, 0, 10);
                                break;
                            }
                        case 13:
                            {
                                int count = Gamemanager.instance.MaxCoin;
                                Gamemanager.instance.MaxCoin = Mathf.Clamp(count + 1, 0, 10);
                                break;
                            }
                        case 15:
                            {
                                int count = Gamemanager.instance.CardDisposCoin;
                                Gamemanager.instance.CardDisposCoin = Mathf.Clamp(count - 500, 0, 10000);
                                break;
                            }
                        case 16:
                            {
                                int count = Gamemanager.instance.CardDisposCoin;
                                Gamemanager.instance.CardDisposCoin = Mathf.Clamp(count + 500, 0, 10000);
                                break;
                            }
                        case 17:
                            {
                                int count = Gamemanager.instance.CardDisposCount;
                                Gamemanager.instance.CardDisposCount = Mathf.Clamp(count - 1, 1, 10);
                                break;
                            }
                        case 18:
                            {
                                int count = Gamemanager.instance.CardDisposCount;
                                Gamemanager.instance.CardDisposCount = Mathf.Clamp(count + 1, 1, 10);
                                break;
                            }
                        case 14:
                            {
                                for (int i = 0; i < audio.Length; i++)
                                    audio[i].Stop();
                                AdminParant[1].SetAsLastSibling();
                                CurImage[CurImage.Count - 1].sprite = ClickImage[0];
                                adminType = AdminType.None;
                                CurImage = AdminObject[1].btns;
                                CurImage[0].sprite = ClickImage[1];
                                index = 0;
                                break;
                            }
                    }
                    PageSetting();
                    break;
                }
            case AdminType.test:
                {
                    switch (index)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                            {
                                int _count = AdminObject[3].btns.Count;
                                for (int i = 0; i < _count - 1; i++)
                                {
                                    if (i == index)
                                    {
                                        AdminParant[3].GetChild(AdminObject[3].btns.Count + (i + 1)).gameObject.SetActive(true);
                                        CurHardPage = AdminParant[3].GetChild(AdminObject[3].btns.Count + (i + 1));
                                    }
                                    else
                                    {
                                        AdminParant[3].GetChild(AdminObject[3].btns.Count + (i + 1)).gameObject.SetActive(false);
                                    }
                                }
                                KeySetting();
                                break;
                            }
                        case 4:
                            {
                                AdminParant[1].SetAsLastSibling();
                                CurImage[CurImage.Count - 1].sprite = ClickImage[0];
                                adminType = AdminType.None;
                                CurImage = AdminObject[1].btns;
                                CurImage[0].sprite = ClickImage[1];
                                index = 0;
                                break;
                            }
                    }
                    break;
                }
            case AdminType.PassWord:
                {
                    int i = 0;
                    string _s = CurImage[index].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text;
                    if (int.TryParse(_s, out i))
                    {
                        if ((realpassword.Length + 1) <= limitLength)
                            realpassword += i.ToString();
                    }
                    else
                    {
                        if (_s == "Ȯ��")
                        {
                            if (password.Contains(int.Parse(realpassword)))
                            {
                                StopCoroutine("TimerCor");
                                AdminParant[1].SetAsLastSibling();
                                CurImage[CurImage.Count - 1].sprite = ClickImage[0];
                                adminType = AdminType.None;
                                CurImage = AdminObject[1].btns;
                                CurImage[0].sprite = ClickImage[1];
                                Setting(InputLeftQ, InputLeftW, InputLeftE);
                                index = 0;
                            }
                            if (realpassword == reportpassword.ToString())
                            {
                                Gamemanager.instance.ReportingObject.SetActive(true);
                                Gamemanager.instance.CanReporting = !Gamemanager.instance.CanReporting;
                            }

                        }
                        else if (_s == "������")
                        {
                            AdminEnd();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(realpassword))
                                realpassword = realpassword.Substring(0, realpassword.Length - 1);
                        }
                    }
                    string _count = "";
                    for (int y = 0; y < realpassword.Length; y++)
                    {
                        _count += "*";
                    }
                    passwordText.text = _count;
                    break;
                }
            case AdminType.CalculateSetting:
                {
                    switch (index)
                    {
                        case 0:
                            {
                                Gamemanager.instance.Calculate();
                                break;
                            }
                        case 1:
                            {
                                AdminParant[1].SetAsLastSibling();
                                CurImage[CurImage.Count - 1].sprite = ClickImage[0];
                                adminType = AdminType.None;
                                CurImage = AdminObject[1].btns;
                                CurImage[0].sprite = ClickImage[1];
                                index = 0;
                                break;
                            }
                    }
                    break;
                }
        }
    }
    public void DataSet()
    {
        totaldispos.text = string.Format("{0:#,##0}��", Gamemanager.instance.TotalDisposCount);
        totalgamecount.text = string.Format("{0:#,##0}ȸ", Gamemanager.instance.TotalGameCount);
        totalexpense.text = string.Format("{0:#,##0}��", Gamemanager.instance.TotalExpense);
        curdispos.text = string.Format("{0:#,##0}��", Gamemanager.instance.CurDisposCount);
        curgamecount.text = string.Format("{0:#,##0}ȸ", Gamemanager.instance.CurGameCount);
        curexpense.text = string.Format("{0:#,##0}��", Gamemanager.instance.CurExpense);
    }
    void InfoInit(string text, UnityAction _callback)
    {
        adminType = AdminType.Info;
        Isclick = false;
        preindex = index;
        index = 0;
        InfoText.text = $"{text}��\n�����Ͻðڽ��ϱ�?";
        InfoBtn[0].sprite = ClickImage[1];
        InfoBtn[1].sprite = ClickImage[0];
        InfoPanel.SetActive(true);
        CurImage = InfoBtn.ToList();
        StartCoroutine(ClickCheck(_callback));
    }
    IEnumerator ClickCheck(UnityAction _callback)
    {
        yield return new WaitUntil(() => Isclick);
        if (index == 0)
        {
            _callback();
        }
        adminType = AdminType.None;
        CurImage = AdminObject[1].btns;
        index = preindex;
        InfoPanel.SetActive(false);
    }
    void KeySetting()
    {
        switch (index)
        {
            case 0:
                {
                    BtnSetting();
                    break;
                }
            case 1:
                {
                    CoinSetting();
                    break;
                }
            case 2:
                {
                    CardSetting();
                    break;
                }
            case 3:
                {
                    QRTestSetting();
                    break;
                }
        }
    }
    /// <summary>
    /// �׽�Ʈ������ ����
    /// </summary>
    public void PageSetting()
    {
        int value = Mathf.Clamp(Gamemanager.instance.DemoVolume, 0, 100);
        DemoVolume.text = value.ToString();
        PlayerPrefs.SetInt("DemoSound", value);

        value = Mathf.Clamp(Gamemanager.instance.BGMVolume, 0, 100);
        BGMVolume.text = value.ToString();
        PlayerPrefs.SetInt("BGMSound", value);

        value = Mathf.Clamp(Gamemanager.instance.EffectVolume, 0, 100);
        EffectVolume.text = value.ToString();
        PlayerPrefs.SetInt("EffectSound", value);

        value = Mathf.Clamp(Gamemanager.instance.TTSVolume, 0, 100);
        TTSText.text = value.ToString();
        PlayerPrefs.SetInt("TTSSound", value);

        Gamemanager.instance.VolumeSetting();

        value = Mathf.Clamp(Gamemanager.instance.MaxCoin, 0, 10);
        coinText.text = value.ToString();
        PlayerPrefs.SetInt("MaxCoin", value);

        //value = Mathf.Clamp(Gamemanager.instance.CardDisposCount, 1, 99);
        //CardCountText.text = value.ToString();
        //PlayerPrefs.SetInt("CardDisposCount", value);

        //value = Mathf.Clamp(Gamemanager.instance.CardDisposCoin, 0, 10000);
        //CardCoinText.text = value.ToString();
        //PlayerPrefs.SetInt("CardDisposCoin", value);

        //LangText.text = LanguageSingleton.instance.curLangIndex == 0 ? "�ѱ���" : "English";
    }
    /// <summary>
    /// ����
    /// </summary>
    void AdminEnd()
    {
        Gamemanager.instance.ChangeScene("Intro");
        Gamemanager.instance.IsAdMin = false;
    }
    void TestEnd()
    {
        DefaultSetting();
        CurHardPage.gameObject.SetActive(false);
        QRTest = false;
    }
    #region �н�����
    void PassWordSetting()
    {
        Setting(InputPassWordQ, InputPassWordW, InputPassWordE);
    }
    void InputPassWordQ()
    {
        MoveBtn(true);
    }
    void InputPassWordW()
    {
        ClickBtn();
    }
    void InputPassWordE()
    {
        MoveBtn(false);
    }
    IEnumerator TimerCor()
    {
        float _time = timer;
        while (_time > 0)
        {
            timerText.text = ((int)_time).ToString();
            yield return null;
            _time -= Time.deltaTime;
        }
        AdminEnd();
    }
    #endregion
    #region QR�׽�Ʈ
    void QRTestSetting()
    {
        QRTest = true;
        CardImage.sprite = null;
        skill = null;
        //supportData = null;
        GradeNameText.text = "Grade";
        QRText.text = "QR";
        NameText.text = "Name";
        TypeText.text = "Type";
        GradeText.text = "Grade";
        ValueText.text = "Value";
        Setting(TestEnd, TestEnd, TestEnd);
    }
    public void QrCardSetting(string _qr)
    {
        QRText.text = _qr;
        if (skill != null)
        {
            CardImage.sprite = Resources.Load<Sprite>("robot/front_robot_" + skill.robot_type);
            NameText.text = LanguageSingleton.instance.GetString(skill.name);
            if (skill.type == 1)
            {
                TypeText.text = "�ٰŸ� ����";
            }
            else if (skill.type == 2)
            {
                TypeText.text = "���Ÿ� ����";
            }
            else if (skill.type == 3)
            {
                TypeText.text = "���(����)";
            }
            else if (skill.type == 4)
            {
                TypeText.text = "���(ȸ��)";
            }
            else if (skill.type == 5)
            {
                TypeText.text = "�Ϲ� ����";
            }
            GradeNameText.text = "Grade";
            GradeText.text = skill.grade.ToString();
            ValueText.text = skill.ability_value.ToString();
        }
        //if (supportData != null)
        //{

        //    NameText.text = supportData.Name;

        //    if (supportData.card_type == 1)
        //    {
        //        TypeText.text = "��� ī��";
        //        CardImage.sprite = Resources.Load<Sprite>("Support/weapon_robot_" + supportData.robot_type);
        //    }
        //    else if (supportData.card_type == 2)
        //    {
        //        TypeText.text = "��Ŭ ī��";
        //        CardImage.sprite = Resources.Load<Sprite>("Support/vehicle_robot_" + supportData.robot_type);
        //    }
        //    GradeNameText.text = "SameValue";
        //    GradeText.text = supportData.same_value.ToString();
        //    ValueText.text = supportData.card_value.ToString();
        //}
    }
    #endregion
    #region ī������׽�Ʈ
    void CardSetting()
    {
        for (int i = 0; i < 3; i++)
        {
            if (i == 0)
            {
                CurHardPage.GetChild(i + 1).GetComponent<Image>().sprite = ClickImage[1];
            }
            else
            {
                CurHardPage.GetChild(i + 1).GetComponent<Image>().sprite = ClickImage[0];
            }
        }
        cardindex = 0;
        Setting(CardLeftMoveSet, CardClick, CardRightMoveSet);
    }
    void CardLeftMoveSet()
    {
        CurHardPage.GetChild(cardindex + 1).GetComponent<Image>().sprite = ClickImage[0];
        cardindex = (cardindex - 1) % 3 < 0 ? 2 : (cardindex - 1) % 3;
        CurHardPage.GetChild(cardindex + 1).GetComponent<Image>().sprite = ClickImage[1];
    }
    void CardRightMoveSet()
    {
        CurHardPage.GetChild(cardindex + 1).GetComponent<Image>().sprite = ClickImage[0];
        cardindex = (cardindex + 1) % 3 < 0 ? 2 : (cardindex + 1) % 3;
        CurHardPage.GetChild(cardindex + 1).GetComponent<Image>().sprite = ClickImage[1];
    }
    void CardClick()
    {
        if (cardindex == 0)
        {
            Gamemanager.instance.DisPoseCard();
        }
        else if (cardindex == 1)
        {
            Gamemanager.instance.DisPoseCard(false);
        }
        else
        {
            TestEnd();
        }
    }
    #endregion
    #region �����׽�Ʈ
    public void CoinBillTextSetting()
    {
        CoinBillText.text = $"Coin {coin}/ BILL {bill}";
        CreditText.text = $"CREDITS {coin / Gamemanager.instance.MaxCoin + (bill * 2) / Gamemanager.instance.MaxCoin}";
    }
    void CoinSetting()
    {
        Setting(CoinClick, CoinClick, CoinClick);
    }
    void CoinClick()
    {
        TestEnd();
        coin = 0;
        bill = 0;
        CoinBillText.text = "Coin 0/ BILL 0";
        CreditText.text = "CREDITS 0";
    }
    #endregion
    #region ��ư�׽�Ʈ
    void BtnSetting()
    {
        Setting(btnclick1, btnclick2, btnclick3, btnclick4, btnclick5, btnclick6);
    }
    void btnclick1()
    {
        if (CurHardPage.Find("2pred").GetComponent<Image>().color == btnColor[0])
        {
            TestEnd();
            btn();
            return;
        }
        btn(0);
    }
    void btnclick2()
    {
        btn(1);
    }
    void btnclick3()
    {
        btn(2);
    }
    void btnclick4()
    {
        if (CurHardPage.Find("1pred").GetComponent<Image>().color == btnColor[0])
        {
            TestEnd();
            btn();
            return;
        }
        btn(3);
    }
    void btnclick5()
    {
        btn(4);
    }
    void btnclick6()
    {
        btn(5);
    }
    void btn(int _index = -1)
    {
        if (_index == -1)
        {
            for (int i = 0; i < 6; i++)
            {
                CurHardPage.GetChild(1 + i).GetComponent<Image>().color = Color.white;
            }
        }
        else
        {
            for (int i = 0; i < 6; i++)
            {
                if (i == _index)
                {
                    CurHardPage.GetChild(1 + i).GetComponent<Image>().color = btnColor[i % 3];
                }
                else
                {
                    CurHardPage.GetChild(1 + i).GetComponent<Image>().color = Color.white;
                }
            }
        }
    }
    #endregion
    #region defalut
    void DefaultSetting()
    {
        Gamemanager.instance.IsSolo = false;
        Setting(InputLeftQ, InputLeftW, InputLeftE);
    }
    public override void InputLeftE()
    {
        MoveBtn(false);
    }

    public override void InputLeftQ()
    {
        MoveBtn(true);
    }

    public override void InputLeftW()
    {
        ClickBtn();
    }

    public override void InputRightE()
    {
        throw new System.NotImplementedException();
    }

    public override void InputRightQ()
    {
        throw new System.NotImplementedException();
    }

    public override void InputRightW()
    {
        throw new System.NotImplementedException();
    }
    #endregion
}
