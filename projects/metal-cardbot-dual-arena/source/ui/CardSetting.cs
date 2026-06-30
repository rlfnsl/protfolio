using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Funtion_CWS;
using System;
using UnityEngine.UI;
using System.Linq;

public class CardSetting : MonoBehaviour
{
    public static CardSetting instance;


    [System.Serializable]
    public struct RuletText
    {
        public List<TextMeshProUGUI> text;
        public Vector2 OriginPos;
    }

    public List<CardData> CardDatas
    {
        get
        {
            return cardDatas;
        }
    }
    public float TotalHP
    {
        get
        {
            float _value = 0;
            for (int i = 0; i < CardDatas.Count; i++)
            {
                _value += CardDatas[i].LastHp;
            }
            return _value;
        }
    }
    //public SupportData Support
    //{
    //    get
    //    {
    //        return support;
    //    }
    //}
    public Sprite CheckFillCard;
    public Animation BattleStartAni;
    public AudioClip RuletClip;
    public Sprite[] TypeSprite;
    //SupportData support;
    GameObject tagImage, battleStartTitle, Falsh, Infomation;
    Transform parant;
    CardData card, curCard;
    CanvasGroup cg;
    TextMeshProUGUI timertext, filltext, totalcardtext, WeakText;
    Coroutine cor;
    LangText ButtonText, tagText;
    bool isStart = true;
    bool isResult = false; // 결정안됏을떄
    bool isCardResult = false;
    bool isConfirm = false;
    int curindex = 0;
    float cardsize = 0.25f;
    CardData DumiCardData;
    public event Action KeySetting = () => { };
    Image FillObject;
    List<Transform> cardPos = new List<Transform>();
    List<CardData> cardDatas = new List<CardData>();
    float hp;
    private void Awake()
    {
        instance = this;
        cg = GetComponent<CanvasGroup>();
        card = Resources.Load<CardData>("PreFab/MyCard");
        timertext = transform.FindObject<TextMeshProUGUI>("timer");
        filltext = transform.FindObject<TextMeshProUGUI>("FillText");
        totalcardtext = transform.FindObject<TextMeshProUGUI>("ToTalCardCount");
        WeakText = transform.FindObject<TextMeshProUGUI>("WeakText");
        ButtonText = transform.FindObject<LangText>("ButtonText");
        tagText = transform.FindObject<LangText>("tagText");
        parant = transform.FindObject<Transform>("Object");
        DumiCardData = transform.FindObject<CardData>("DumiCardData");
        tagImage = transform.FindObject("tagImage");
        battleStartTitle = BattleStartAni.transform.FindObject("battleStartTitle");
        battleStartTitle.AddComponent<CanvasGroup>();
        FillObject = transform.FindObject<Image>("Fill");
        cardPos = parant.GetComponentsInChildren<Transform>().Where(a => a.name.Contains("cardpos")).ToList();
        Infomation = transform.FindObject("Infomation");
        Falsh = transform.FindObject("Falsh");
        Falsh.SetActive(false);
        for (int i = 0; i < cardPos.Count; i++)
        {
            cardPos[i].Find("arrow").gameObject.SetActive(false);
        }
        Init();
    }
    void Init()
    {
        curindex = 0;
        isStart = true;
        tagImage.SetActive(false);
        WeakText.transform.parent.gameObject.SetActive(!tagImage.activeSelf);
        cg.alpha = 0;
        filltext.text = "";
        hp = 0;
        FillObject.fillAmount = 0;
        RuletClip = Gamemanager.instance.EffectSound.GetClip(8) != null ? Gamemanager.instance.EffectSound.GetClip(8) : RuletClip;
    }
    public void KeySet()
    {
        KeySetting();
    }
    public void TagImageEnanble(bool _isActive)
    {
        Infomation.SetActive(_isActive);
    }
    /// <summary>
    /// 카드 고를때
    /// </summary>
    public void CardSet()
    {
        SerialPortManager.Instance.SetSideLED(SideLED.EnemyInfo);
        Infomation.SetActive(true);
        tagImage.SetActive(false);
        WeakText.transform.parent.gameObject.SetActive(!tagImage.activeSelf);
        isResult = false;
        isCardResult = false;
        Gamemanager.instance.CanQrScan = false;
        if (isStart)
        {
            isStart = false;
            cg.alpha = 1;

            //처음엔 세가지카드를만듬
            for (int i = 0; i < Gamemanager.instance.CardQRId.Length; i++)
            {
                GameObject _g = Instantiate(card.gameObject, parant);
                _g.SetActive(false);
                cardDatas.Add(_g.GetComponent<CardData>());
                _g.transform.SetParent(cardPos[i].Find("cardrealpos"));
                cardPos[i].Find("Check").GetComponent<Image>().enabled = false;
                _g.transform.GetComponent<RectTransform>().localPosition = new Vector3(0, 1214);
                _g.transform.GetComponent<RectTransform>().localScale = Vector3.one * cardsize;
            }
            //만약 QR를 리딩해서 왓을경우
            if (Gamemanager.instance.CardQRId[0] != -1)
            {
                DumiCardData.gameObject.SetActive(true);
                cardDatas[0].gameObject.SetActive(true);
                cardDatas[0].Setting(Gamemanager.instance.CardQRId[0]);
                DumiCardData.Setting(Gamemanager.instance.CardQRId[0], false);
                curCard = cardDatas[0];
                cardDatas[0].ResetLayout();
                StartCoroutine("SelectCor");
            }
            //건너뛰기를했을경우
            else
            {
                Gamemanager.instance.SettingCard(false);
                StartCoroutine(SkipCor());
            }

            if (EnemyInfo.Instance.WeakCount == 0)
            {
                WeakText.text = LanguageSingleton.instance.GetString("weakpoint_none");
            }
            else
            {
                WeakText.text = LanguageSingleton.instance.GetString("weakpoint_check");
            }
        }
        else
        {
            //현재 몇번째 카드인지
            int _curindex = 0;
            for (int i = 0; i < cardDatas.Count - 1; i++)
            {
                if (curCard == cardDatas[i])
                {
                    _curindex = i + 1;
                    break;
                }
            }
            cardDatas[_curindex].Setting(Gamemanager.instance.CardQRId[_curindex]);
            DumiCardData.Setting(Gamemanager.instance.CardQRId[_curindex], false);
            DumiCardData.gameObject.SetActive(true);
            cardDatas[_curindex].ResetLayout();
            Select();
            StartCoroutine("SelectCor");
            cardDatas[_curindex].gameObject.SetActive(true);
            curCard = cardDatas[_curindex];
        }
    }
    IEnumerator CardCharge(int index)
    {
        //yield return StartCoroutine(cardDatas[index].transform.MovetoVector1(Vector3.zero, 3000));
        cardDatas[index].ParticlePlay();
        yield return StartCoroutine(cardDatas[index].GetComponent<Animation>().AnimationEnd());
        FillObject.fillAmount += 0.34f;
        hp += cardDatas[index].LastHp;
        filltext.text = $"{LanguageSingleton.instance.GetString("system_hp")} : {string.Format("{0:#,###}", hp)}";
        Gamemanager.instance.PlayOneShotEffect(10);
        cardDatas[index].ParticleStop();
        cardPos[index].Find("Check").GetComponent<Image>().enabled = true;
        cardPos[index].Find("arrow").gameObject.SetActive(false);
    }
    /// <summary>
    /// 다음 연출관련코드
    /// </summary>
    public void NextProduction()
    {
        BattleManager.instance.StartBattle();
        SerialPortManager.Instance.SetSideLED(SideLED.EnemySpawn);
    }
    public void Result()
    {
        isCardResult = true;
    }
    public void Confirm()
    {
        isConfirm = true;
    }
    public void SetTagImage(string _key)
    {
        tagImage.SetActive(true);
        WeakText.transform.parent.gameObject.SetActive(!tagImage.activeSelf);
        tagText.LocalizeChanged(_key);
    }
    /// <summary>
    /// 결정
    /// </summary>
    public void Select()
    {
        //건너뛰기클릭X
        if (!isResult)
        {
            if (cor != null)
            {
                StopCoroutine(cor);
                cor = null;
            }
            isResult = true;
            BattleKey.instance.NullSetting();
        }
        //건너뛰기클릭
        else
        {
            BattleKey.instance.NullSetting();
            KeySetting = BattleKey.instance.NullSetting;
            StopCoroutine("SupportCardSetCor");
            StopCoroutine("SelectCor");
            Gamemanager.instance.SettingCard(false);
            StartCoroutine(SkipCor());
        }
    }
    /// <summary>
    /// 건너뛰기 눌렀을때
    /// </summary>
    /// <returns></returns>
    IEnumerator SkipCor()
    {
        BattleKey.instance.NullSetting();
        KeySetting = BattleKey.instance.NullSetting;
        if (cor != null)
            StopCoroutine(cor);
        tagImage.SetActive(false);
        WeakText.transform.parent.gameObject.SetActive(!tagImage.activeSelf);

        for (int i = 0; i < cardDatas.Count; i++)
        {
            if (cardDatas[i].gameObject.activeSelf)
            {
                continue;
            }
            cardDatas[i].gameObject.SetActive(true);
            cardDatas[i].Setting(Gamemanager.instance.CardQRId[i]);
            //cardDatas[i].transform.position = cardPos[i].Find("cardrealpos").position;
            //cardDatas[i].transform.localScale = Vector3.one * cardsize;
            cardDatas[i].ResetLayout();
        }

        if (cor != null)
        {
            StopCoroutine(cor);
            cor = null;
        }
        DumiCardData.gameObject.SetActive(false);
        yield return new WaitUntil(() => EnemyInfo.Instance.Cg.alpha == 0);
        for (int i = curindex; i < 3; i++)
        {
            yield return CardCharge(i);
        }
        yield return new WaitForSeconds(1);
        StartCoroutine(CardSettingEnding());
        yield break;
    }
    /// <summary>
    /// 타이머 코루틴
    /// </summary>
    /// <returns></returns>
    IEnumerator CardSetCor(bool _isReulst = false)
    {
        float time = DataManager.Instance.ProductionData.default_skip_time + 0.9f;
        timertext.text = ((int)time).ToString();
        while (time > 0)
        {
            timertext.text = ((int)time).ToString();
            yield return null;
            time -= Time.deltaTime;
        }
        //결정아직안했을때
        if (!_isReulst)
        {
            Select();
        }
        else
        {
            Result();
        }
        if (cor != null)
            cor = null;
    }
    /// <summary>
    /// 진행 눌럿을때
    /// </summary>
    /// <returns></returns>
    IEnumerator SelectCor()
    {
        bool _islast = false;
        int _curindex = 0;
        ButtonText.LocalizeChanged("confirm_stat_button");
        Gamemanager.instance.PlayOneShotTTS(16);
        BattleKey.instance.CardSelectSetting();
        KeySetting = BattleKey.instance.CardSelectSetting;
        cor = StartCoroutine(CardSetCor(true));
        yield return new WaitUntil(() => isCardResult);
        if (cor != null)
        {
            StopCoroutine(cor);
            cor = null;
        }
        BattleKey.instance.NullSetting();
        KeySetting = BattleKey.instance.NullSetting;
        for (int i = 0; i < cardDatas.Count; i++)
        {
            if (cardDatas[i] == curCard)
            {
                _curindex = i;
                if (i == cardDatas.Count - 1)
                {
                    _islast = true;
                }
                break;
            }
        }
        cardDatas[_curindex].ResetLayout();
        if (curindex + 1 < cardDatas.Count)
        {
            cardPos[curindex + 1].Find("arrow").gameObject.SetActive(true);
        }
        if (curindex != cardDatas.Count - 1)
            Falsh.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        DumiCardData.gameObject.SetActive(false);
        Falsh.SetActive(false);
        yield return CardCharge(curindex++);
        //카드 작아진후
        if (_islast)
        {
            StartCoroutine(CardSettingEnding());
        }
        else
        {
            totalcardtext.text = $"{_curindex + 1}/3";
            yield return new WaitForSeconds(1);
            cor = StartCoroutine(CardSetCor());
            Gamemanager.instance.PlayOneShotTTSCor(11, 12);
            SetTagImage("skill_card_tag");
            Gamemanager.instance.CanQrScan = true;
            ButtonText.LocalizeChanged("no_card_button");
            isResult = true;
            BattleKey.instance.RuletSetting();
            KeySetting = BattleKey.instance.RuletSetting;
            yield return new WaitUntil(() => Gamemanager.instance.CardQRId[_curindex + 1] != -1);
            Gamemanager.instance.CanQrScan = false;
            if (cor != null)
            {
                StopCoroutine(cor);
                cor = null;
            }
            CardSet();
        }
    }
    IEnumerator CardSettingEnding()
    {
        ButtonText.LocalizeChanged("confirm_card_button");
        BattleKey.instance.NullSetting();
        KeySetting = BattleKey.instance.NullSetting;
        if (EnemyInfo.Instance.WeakCount == 1)
        {
            if (Gamemanager.instance.CardType.Contains(EnemyInfo.Instance.WeakCount))
                WeakText.text = LanguageSingleton.instance.GetString("weakpoint_melee_success");
            else
                WeakText.text = LanguageSingleton.instance.GetString("weakpoint_melee_fail");
        }
        else if (EnemyInfo.Instance.WeakCount == 2)
        {
            if (Gamemanager.instance.CardType.Contains(EnemyInfo.Instance.WeakCount))
                WeakText.text = LanguageSingleton.instance.GetString("weakpoint_range_success");
            else
                WeakText.text = LanguageSingleton.instance.GetString("weakpoint_range_fail");
        }
        yield return StartCoroutine(EndConfirm());
        yield return StartCoroutine(BattleStartAni.AnimationEnd());
        Gamemanager.instance.FadeBGM(0, DataManager.Instance.ProductionData.battle_warning_count + DataManager.Instance.ProductionData.warning_hold_time + BattleStartAni.AnimatonLength(), BattleStage.Instance.CurMapBGM);
        yield return StartCoroutine(StartCor(battleStartTitle, DataManager.Instance.ProductionData.battle_warning_count, false));
        yield return StartCoroutine(StartCor(battleStartTitle, DataManager.Instance.ProductionData.warning_hold_time, true, false));
        //Gamemanager.instance.PlayBGM(BattleStage.Instance.CurMapBGM);
        NextProduction();
        gameObject.SetActive(false);
        BattleManager.instance.firstStrike.StartAlpha();
    }
    /// <summary>
    /// 배틀시작연출
    /// </summary>
    /// <param name="_g"></param>
    /// <param name="_max"></param>
    /// <param name="_continue"></param>
    /// <param name="_isActive"></param>
    /// <returns></returns>
    IEnumerator StartCor(GameObject _g, float _max, bool _continue, bool _isActive = true)
    {
        if (_continue)
        {
            _g.SetActive(true);
            yield return new WaitForSeconds(_max);
            if (_isActive)
                _g.SetActive(false);
            else
                yield return StartCoroutine(_g.GetComponent<CanvasGroup>().SetAlphaCanvasGroup_num(0, 0.5f));
        }
        else
        {
            int count = 0;
            while (count < _max)
            {
                _g.SetActive(true);
                yield return new WaitForSeconds(0.2f);
                _g.SetActive(false);
                yield return new WaitForSeconds(0.2f);
                count++;
            }
        }
    }
    /// <summary>
    /// 라스트 확인
    /// </summary>
    /// <returns></returns>
    IEnumerator EndConfirm()
    {
        Gamemanager.instance.PlayOneShotTTS(20);
        Gamemanager.instance.PlayOneShotEffect(12);
        Gamemanager.instance.CanQrScan = false;
        ButtonText.LocalizeChanged("confirm_card_button");
        float _timer = DataManager.Instance.ProductionData.default_skip_time + 0.9f;
        BattleKey.instance.CardConfirm();
        while (!isConfirm && _timer > 0)
        {
            _timer -= Time.deltaTime;
            timertext.text = ((int)_timer).ToString();
            yield return null;
        }
        Gamemanager.instance.PlayOneShotTTS(21);
        BattleKey.instance.NullSetting();
    }
}
