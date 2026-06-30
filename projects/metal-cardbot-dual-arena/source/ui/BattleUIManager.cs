using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Funtion_CWS;
using TMPro;
using DamageNumbersPro;
using DamageNumbersPro.Demo;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager instance;
    public Image ImgDeathBlow;

    public Camera UICamera;
    public DamageNumber damageNumber;
    //게임 첫 시작 연출
    private RectTransform MovingCharacterUI;
    public CanvasGroup CharacterUIalpha;
    private CanvasGroup cgBattleStart;
    public CanvasGroup TimerGroup;
    //플레이어 정보 UI
    private Image PlayerHealthGaze;
    private RectTransform PlayerDeatblow;
    private Image[] DeathBlowGaze;
    private int DeathBlowStartIndex = 0;
    private TextMeshProUGUI PlayerHealthText;
    private UICard[] UICards;
    private RectTransform CardRuler;

    //적 정보 UI
    private Image EnemyHealthGaze;
    private Image GroggyGaze;
    private int TotalGroggy = 50;
    private int CurrentGroggy = 50;
    private TextMeshProUGUI EnemyHealthText;
    private TextMeshProUGUI EnemyGroggyText;
    private TextMeshProUGUI EnemyDefenseInfo1 , EnemyDefenseInfo2;
    private Image SkillSlotsHealthGaze;
    private Image SkillSlotsGroggyGaze;
    private TextMeshProUGUI SkillSlotsEnemyHealthText;
    private TextMeshProUGUI SkillSlotsEnemyGroggyText;
    private TextMeshProUGUI WeaknessText;

    //방어 
    private TextMeshProUGUI TextDefence;
    //플레이어 방어와 체력회복 알람.
    private RectTransform Alram;

    private CanvasGroup DefenseTypeCg;

    private float OriginTextSize;
    private CanvasGroup SkillAlramCG;
    private TextMeshProUGUI SkillAlramText;
    private CanvasGroup AlramCG;
    private Animation AlramAni;
    private TextMeshProUGUI AlramText;
    public Sprite playerTurnSprite, EnemyTurnSprite;
    public CanvasGroup CardSlotGroup;

    //적턴
    private RectTransform rt_EnemyTurn;
    private TextMeshProUGUI EnemyTurnText;

    //게임 종료 
    private Animator EndingAnimator;
    private TextMeshProUGUI EndText;
    private CanvasGroup WhiteFade;
    private TextMeshProUGUI TimerText;

    private RectTransform rt_Timer;

    //왼쪽하단 캐릭터UI
    public Image[] imgPos;
    public RectTransform[] rt_pos;
    public RectTransform[] targetPos;
    private Vector2[] imgVec;
    private Image currentImg;
    private int currentIndex;
    private int[] currentPosition = new int[3];
    private Image EnemyImage;

    public Vector3[] originPos;

    private void Awake()
    {
        instance = this;

        //여기 10은 필살기채워야하는 값.
        PlayerDeatblow = transform.FindObject<RectTransform>("DeathBlowRect");
        GroggyGaze = transform.FindObject<Image>("EnemyGroggyFillAmount");

        EnemyTurnText = transform.FindObject<TextMeshProUGUI>("EnemyAttackSpeed");
        WeaknessText = transform.FindObject<TextMeshProUGUI>("WeaknessText");
        PlayerHealthGaze = transform.FindObject<Image>("PlayerFillAmount");
        EnemyHealthGaze = transform.FindObject<Image>("EnemyFillAmount");
        SkillSlotsHealthGaze = transform.FindObject<Image>("SkillSlotHpBar");
        SkillSlotsGroggyGaze = transform.FindObject<Image>("SkillSlotGroggyBar");
        MovingCharacterUI = transform.FindObject<RectTransform>("MovingUI");
        //rt_EnemyTurn = transform.FindObject<RectTransform>("EnemyTurn");
        rt_Timer = transform.FindObject<RectTransform>("TimerGroup");
        CharacterUIalpha = transform.FindObject<CanvasGroup>("CharacterUIInfo");
        CharacterUIalpha.alpha = 0;
        PlayerHealthText = transform.FindObject<TextMeshProUGUI>("PlayerHealthText");
        EnemyHealthText = transform.FindObject<TextMeshProUGUI>("EnemyHealthText");
        EnemyGroggyText = transform.FindObject<TextMeshProUGUI>("EnemyGroggyText");
        SkillSlotsEnemyHealthText = transform.FindObject<TextMeshProUGUI>("SkillSlotHp");
        SkillSlotsEnemyGroggyText = transform.FindObject<TextMeshProUGUI>("SkillSlotGroggy");
        EnemyDefenseInfo1 = transform.FindObject<TextMeshProUGUI>("Defense1Text");
        EnemyDefenseInfo2 = transform.FindObject<TextMeshProUGUI>("Defense2Text");

        CardSlotGroup = transform.FindObject<CanvasGroup>("PlayerSkillSlot");
        CardSlotGroup.alpha = 0; 

        Alram = transform.FindObject<RectTransform>("Alram");
        SkillAlramText = transform.FindObject<TextMeshProUGUI>("SkillAlramText");
        SkillAlramCG = transform.FindObject<CanvasGroup>("SkillAlramCG");

        AlramText = transform.FindObject<TextMeshProUGUI>("AlramText");
        AlramCG = transform.FindObject<CanvasGroup>("AlramCG");
        AlramAni = transform.FindObject<Animation>("Alram");
        TextDefence = transform.FindObject<TextMeshProUGUI>("TextDefence");

        UICards = new UICard[3];
        imgVec = new Vector2[3];
        currentPosition = new int[3];
        for (int i = 0; i < 3; ++i)
        {
            UICards[i] = transform.FindObject<UICard>("UICard" + i.ToString());
            imgVec[i] = imgPos[i].rectTransform.anchoredPosition;
            currentPosition[i] = i;
        }

        EndingAnimator = transform.FindObject<Animator>("Ending");

        EndText = transform.FindObject<TextMeshProUGUI>("EndText");
        TimerText = transform.FindObject<TextMeshProUGUI>("TimerText");

        cgBattleStart = transform.FindObject<CanvasGroup>("BattleStart");
        WhiteFade = transform.FindObject<CanvasGroup>("WhiteFade");
        TimerGroup = transform.FindObject<CanvasGroup>("TimerGroup");
        DefenseTypeCg = transform.FindObject<CanvasGroup>("DefenseType");

        EnemyImage = transform.FindObject<Image>("EnemyImage");

        CardRuler = transform.FindObject<RectTransform>("CardRuler");

        originPos = new Vector3[3];
        //originPos[0] = rt_EnemyTurn.anchoredPosition;
        originPos[1] = rt_Timer.anchoredPosition;
        originPos[2] = MovingCharacterUI.anchoredPosition;

        StartCoroutine(WaitCommonData());
    }

    public void EnemySetting(float _value1, float _value2 , float _value3 , string _s)
    {
        EnemyDefenseInfo1.text = _value1.ToString() + "%";
        EnemyDefenseInfo2.text = _value2.ToString() + "%";

        if(_value3 > 0)
        {

            WeaknessText.text = _s + " " + LanguageSingleton.instance.FormatString("monster_info_weak", (int)_value3);
        }
        else
        {

            WeaknessText.text = LanguageSingleton.instance.GetString("format_weak_none");
        }

    }

    public void EventStartAlpha()
    {
        StartCoroutine(CharacterUIalpha.SetAlphaCanvasGroup_numTime(0, 0.5f));
        StartCoroutine(CardSlotGroup.SetAlphaCanvasGroup_numTime(0, 0.5f));
        StartCoroutine(TimerGroup.SetAlphaCanvasGroup_numTime(0, 0.5f));
    }

    public void MoveImage(int cardIndex)
    {
        currentImg = imgPos[cardIndex];
        currentIndex = cardIndex;
        if (currentImg.rectTransform.anchoredPosition == imgVec[0])
        {

        }
        else if (currentImg.rectTransform.anchoredPosition == imgVec[1])
        {
            TwoMoveImages();
            RotationRuler(240.0f);
        }
        else
        {

            OneMoveImage();
            RotationRuler(120.0f);
        }

    }

    private void RotationRuler(float _rotateValue)
    {
        StartCoroutine(RotateCoroutine(CardRuler, 0.5f, _rotateValue));
        StartCoroutine(MoveToWorldPosition(rt_pos, targetPos));

        for (int i = 0; i < rt_pos.Length; ++i)
        {
            if (currentIndex == i)
            {
                StartCoroutine(rt_pos[currentIndex].TimeScale(1, 1, 0.5f));
            }
            else
            {
                StartCoroutine(rt_pos[i].TimeScale(0.7f, 0.7f, 0.5f));
            }
        }
    }


    private IEnumerator RotateCoroutine(RectTransform _rt, float time, float rotationAngle)
    {
        float startTime = Time.time;
        Quaternion startRotation = _rt.rotation;

        // 원래 z값에서부터 시작하여 목표 각도까지의 차이 계산
        float angleDifference = rotationAngle;

        // 음수 각도는 양수 각도로 변환
        if (angleDifference < 0)
        {
            angleDifference += 360f;
        }

        // 목표 각도를 계산
        float targetZ = startRotation.eulerAngles.z + angleDifference;

        while (Time.time - startTime < time)
        {
            float t = (Time.time - startTime) / time;
            // 목표 각도로 회전
            _rt.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(startRotation.eulerAngles.z, targetZ, t));
            yield return null;
        }

        _rt.rotation = Quaternion.Euler(0, 0, targetZ);

    }

    public IEnumerator MoveToWorldPosition(RectTransform[] rects, RectTransform[] targetPos, float timeToMove = 1)
    {
        float elapsedTime = 0;
        int rectCount = rects.Length;
        while (elapsedTime < timeToMove)
        {
            for (int i = 0; i < rectCount; i++)
            {
                rects[i].position = targetPos[i].position;
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < rectCount; i++)
        {
            rects[i].position = targetPos[i].position;
        }
    }


    public void SetEnemySprite(Sprite _sprite)
    {
        EnemyImage.sprite = _sprite;
    }

    public void OneMoveImage()
    {
        for (int i = 0; i < imgPos.Length; ++i)
        {
            int newPos = (currentPosition[i] + 1) % imgPos.Length;
            StartCoroutine(MoveToPosition(imgPos[i].rectTransform, imgVec[newPos], 1f));

            currentPosition[i] = newPos;
        }
    }

    public void TwoMoveImages()
    {
        for (int i = 0; i < imgPos.Length; ++i)
        {
            int nextPosition = (currentPosition[i] + 1) % imgPos.Length;
            StartCoroutine(MoveAndThenMoveAgain(imgPos[i].rectTransform, imgVec[nextPosition], 1f, nextPosition));
        }
    }

    public IEnumerator MoveAndThenMoveAgain(RectTransform rect, Vector2 targetPos, float timeToMove, int nextPosition)
    {
        Vector2 startPos = rect.anchoredPosition;
        float elapsedTime = 0;

        while (elapsedTime < timeToMove)
        {
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsedTime / timeToMove);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rect.anchoredPosition = targetPos;

        // 두 번째 이동을 위한 대기
        yield return null;

        int finalNextPosition = (nextPosition + 1) % imgPos.Length;
        StartCoroutine(MoveToPosition(rect, imgVec[finalNextPosition], 1f));
        currentPosition[System.Array.IndexOf(imgPos, rect.GetComponent<Image>())] = finalNextPosition;
    }

    public IEnumerator MoveToPosition(RectTransform rect, Vector2 targetPos, float timeToMove = 1)
    {
        Vector2 startPos = rect.anchoredPosition;
        float elapsedTime = 0;

        while (elapsedTime < timeToMove)
        {
            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsedTime / timeToMove);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rect.anchoredPosition = targetPos;
    }

    //카드데이터 대기.
    public IEnumerator WaitCommonData()
    {
        yield return new WaitUntil(() => DataManager.Instance != null);

        RectTransform horizontal = PlayerDeatblow.transform.GetChild(1).GetComponent<RectTransform>();

        float sizeX = horizontal.sizeDelta.x * 0.1f;
        DeathBlowGaze = new Image[DataManager.Instance.Commons.battle_deathblow_cell];
        for (int i = 0; i < DeathBlowGaze.Length; ++i)
        {
            Image img = Instantiate(ImgDeathBlow, horizontal.transform);
            img.GetComponent<RectTransform>().sizeDelta = new Vector2(sizeX, horizontal.sizeDelta.y);
            DeathBlowGaze[i] = img.transform.GetChild(0).GetComponent<Image>();
            DeathBlowGaze[i].GetComponent<CanvasGroup>().alpha = 0.5f;
        }
    }

    //체력 텍스트 세팅
    public void SetHealth(float _curHealth, float _maxHealth,float _amount ,string type)
    {
        if (type.Equals("Player"))
        {
            PlayerHealthText.text = _curHealth.ToString("N0") + "/" + _maxHealth.ToString("N0");
            PlayerHealthGaze.fillAmount = _amount;
        }
        else
        {
            EnemyHealthText.text =  _curHealth.ToString("N0") + "/" + _maxHealth.ToString("N0");
            SkillSlotsEnemyHealthText.text = _curHealth.ToString("N0") + "/" + _maxHealth.ToString("N0");
            EnemyHealthGaze.fillAmount = _amount;
            SkillSlotsHealthGaze.fillAmount = _amount;
        }

    }

    //플레이어 필살기 세팅
    public void SetDeathBlow(int _index)
    {

        for (int i = DeathBlowStartIndex; i < DeathBlowStartIndex + _index; ++i)
        {
            if (i < DeathBlowGaze.Length)
            {
                DeathBlowGaze[i].GetComponent<CanvasGroup>().alpha = 1;
            }
        }
        DeathBlowStartIndex += _index;
    }

    //적 그로기 세팅
    public void SetGroggy(int _currentGroggy)
    {
        TotalGroggy = _currentGroggy;
        CurrentGroggy = TotalGroggy;
        EnemyGroggyText.text = CurrentGroggy.ToString() + " / " + TotalGroggy.ToString();
        SkillSlotsEnemyGroggyText.text = CurrentGroggy.ToString() + " / " + TotalGroggy.ToString();
    }

    //그로기 값 받아오기.
    public void TakeGroggy(int _groggy)
    {
        CurrentGroggy -= _groggy;

        if(CurrentGroggy <= 0)
        {
            CurrentGroggy = 0;
        }

        GroggyGaze.fillAmount = ((float)CurrentGroggy / (float)TotalGroggy);
        SkillSlotsGroggyGaze.fillAmount = ((float)CurrentGroggy / (float)TotalGroggy);
        EnemyGroggyText.text = CurrentGroggy.ToString() + " / " + TotalGroggy.ToString();
        SkillSlotsEnemyGroggyText.text = CurrentGroggy.ToString() + " / " + TotalGroggy.ToString();
    }

    //그로기 초기화
    public void InitGroggy()
    {
        GroggyGaze.fillAmount = 1.0f;
        SkillSlotsGroggyGaze.fillAmount = 1.0f;
        CurrentGroggy = TotalGroggy;
    }

    //필살기 초기화
    public void InitDeathBlow()
    {
        DeathBlowStartIndex = 0;

        for (int i = 0; i < DeathBlowGaze.Length; ++i)
        {
            DeathBlowGaze[i].GetComponent<CanvasGroup>().alpha = 0.5f;
        }
    }

    //턴
    public void SetTurn(int _turn , bool _b = true)
    {
        if(_b )
        {
            if (_turn == 0)
            {
                EnemyTurnText.text = LanguageSingleton.instance.GetString("first_strike_lose");
            }
            else
            {
                EnemyTurnText.text = LanguageSingleton.instance.GetString("battle_until_attack") + " " + _turn +  LanguageSingleton.instance.GetString("battle_turn");
            }
        }
        else
        {
            if (_turn == 0)
            {
                EnemyTurnText.text = LanguageSingleton.instance.GetString("first_strike_lose");
            }
            else
            {
                EnemyTurnText.text = LanguageSingleton.instance.GetString("battle_groggy") + _turn + LanguageSingleton.instance.GetString("battle_turn");
            }
        }  
    }

    //캐릭터 인포 UI 이동
    public void MoveUI(float p, float _time)
    {
        StartCoroutine(MoveToVector(MovingCharacterUI, p, _time));
    }

    //대미지텍스트 _Vaule = 크기
    public void SetTextDamage(int _damage, Vector3 _pos, Color _color = default , float _size = 1)
    {
        if(_color == default)
        {
            _color = Color.white;
        }

        DamageNumber newDamageNumber = damageNumber.Spawn(_pos, _damage);
        newDamageNumber.SetColor(_color);
        //newDamageNumber.SetScale(_size);
        //newDamageNumber.transform.Find("MeshA")
    }

    //게임 시작시 MetalBotUI 표시.
    public IEnumerator BattleStart(float _alpha , float _time)
    {
        StartCoroutine(TimerGroup.SetAlphaCanvasGroup_numTime(_alpha, _time));
        yield return CharacterUIalpha.SetAlphaCanvasGroup_numTime(_alpha, _time);
    }

    //카드 슬롯 표시
    public IEnumerator IECardSlotGroup(float _alpha , float _time)
    {
       yield return StartCoroutine(CardSlotGroup.SetAlphaCanvasGroup_numTime(_alpha, _time));
       if(_alpha == 1)
        {
            Gamemanager.instance.PlayOneShotTTS(27);
            BattleKey.instance.BattleSetting();
        }
    }

    public IEnumerator PlayerAlarm(string _str , int _index = 0)
    {

        Alram.gameObject.SetActive(true);

        if (_index == 0)
        {        
            AlramCG.GetComponent<Image>().sprite = playerTurnSprite;
        }
        else
        {
            AlramCG.GetComponent<Image>().sprite = EnemyTurnSprite;
        }

        if(_str == "system_enemy_attack")
        {
            Gamemanager.instance.PlayOneShotTTS(26);
        }
        else if(_str == "system_player_attack")
        {
            Gamemanager.instance.PlayOneShotTTS(25);
        }
        else if(_str =="battle_deathblow")
        {
            Gamemanager.instance.PlayOneShotTTS(35);
        }
        else if(_str == "system_counterattack_chance")
        {
            Gamemanager.instance.PlayOneShotTTS(36);
        }
        else if(_str == "system_recovery")
        {
            Gamemanager.instance.PlayOneShotTTS(37);
        }
        else if(_str == "battle_lastattack")
        {
            Gamemanager.instance.PlayOneShotTTS(40);
        }

        Alram.anchoredPosition = Vector2.zero;
        AlramText.text = LanguageSingleton.instance.GetString(_str);

        yield return AlramAni.AnimationEnd("Alram");
        Alram.gameObject.SetActive(false);
    }

    public IEnumerator GroggyAlarm(int _count)
    {
        Alram.gameObject.SetActive(true);
        if (_count == 0)
        {
            AlramText.text = LanguageSingleton.instance.GetString("system_groggy_clear");
        }
        else if(_count == 3)
        {
            AlramText.text = LanguageSingleton.instance.GetString("system_enemy_groggy");
        }
        else
        {
            AlramText.text = LanguageSingleton.instance.GetString("battle_groggy") + _count.ToString() 
                + LanguageSingleton.instance.GetString("battle_turn");
        }

        yield return AlramCG.SetAlphaCanvasGroup_numTime(1, 0.75f);
        yield return new WaitForSeconds(0.5f);
        yield return AlramCG.SetAlphaCanvasGroup_numTime(0, 0.75f);
        Alram.gameObject.SetActive(false);
    }

    //스킬 알람.
    public IEnumerator UseSkillAlarm(string _str)
    {
        Alram.anchoredPosition = Vector2.zero;

        SkillAlramText.text = _str;

        yield return SkillAlramCG.SetAlphaCanvasGroup_numTime(1, 0.75f);
        yield return new WaitForSeconds(0.5f);
        yield return SkillAlramCG.SetAlphaCanvasGroup_numTime(0, 0.75f);
    }

    //디펜스게임 텍스트
    public void SetDefenceText(bool _active, int _value , int _index = 0 , float _size = 0)
    {
        if (_value > 100)
        {
            TextSprite.instance.SpriteSetting(TextDefence, 0);
            TextSprite.instance.DisplayNumber(TextDefence, 0);
        }
        else
        {
            TextSprite.instance.SpriteSetting(TextDefence, _index);
            TextSprite.instance.DisplayNumber(TextDefence, _value);
        }

        

        TextDefence.gameObject.SetActive(_active);
        TextDefence.fontSize = _size;
    }

    public IEnumerator UIDirection()
    {
        StartCoroutine(rt_Timer.MoveToVector(new Vector3(rt_Timer.anchoredPosition.x, 1100), 0.5f));
        //StartCoroutine(rt_EnemyTurn.MoveToVector(new Vector3(rt_EnemyTurn.anchoredPosition.x, 1100), 0.5f));
        yield return StartCoroutine(MovingCharacterUI.MoveToVector(new Vector3(MovingCharacterUI.anchoredPosition.x, -520), 0.5f));
    }

    public void ReturnUIDirection()
    {
        rt_Timer.anchoredPosition = originPos[1];
        //rt_EnemyTurn.anchoredPosition = originPos[0];
        MovingCharacterUI.anchoredPosition = originPos[2];
    }

    #region 기능
    IEnumerator MoveToVector(Transform transform, float p, float timeToMove = 1)
    {
        var currentPos = transform.localPosition;
        var t = 0f;
        Vector3 pos = new Vector3(p, transform.localPosition.y);
        while (t < 1)
        {
            transform.localPosition = Vector3.Lerp(currentPos, pos, t);
            yield return null;
            t += Time.deltaTime / timeToMove;
        }
        transform.localPosition = pos;
    }

    //월드 포지션을 캔버스 포지션으로 수정/
    public Vector2 WorldToCanvasPosition(Vector3 worldPosition)
    {
        Vector2 screenPosition = UICamera.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, screenPosition, Camera.main, out Vector2 canvasPosition);
        return canvasPosition;
    }

    //스킬 카드 캐력터 ui 세팅
    public void UICardSetting(Sprite _sprite , int _index , int _tableGrade , int _typeSprite)
    {
        UICards[_index].Set(_sprite, _tableGrade , _typeSprite);
    }

    //게임 종료
    public IEnumerator Ending(string _str , int _index)
    {
        EndText.text = _str;
        BattleManager.instance.AniEnding.gameObject.SetActive(true);
        string animationName = "";
        if(_index == 0)
        {
            Gamemanager.instance.EffectSound.PlayOneShot(23);
            Gamemanager.instance.PlayOneShotTTS(41);
            EndingAnimator.SetTrigger("Victory");
            animationName = "Victory";
        }
        else if(_index == 1)
        {
            Gamemanager.instance.EffectSound.PlayOneShot(25);
            Gamemanager.instance.PlayOneShotTTS(42);
            EndingAnimator.SetTrigger("Defeat");
            animationName = "Defeat";
        }
        else
        {
            Gamemanager.instance.EffectSound.PlayOneShot(24);
            Gamemanager.instance.PlayOneShotTTS(43);
            EndingAnimator.SetTrigger("TimeOver");
            animationName = "TimeOver";
        }

        yield return EndingAnimator.AnimationEnd(animationName,1);

        if (Gamemanager.instance.ResultSound != null)
        {
            Gamemanager.instance.PlayBGM(Gamemanager.instance.ResultSound);
        }
    }

    public IEnumerator GameEnd_WhiteFade(float _alpha , float _time)
    {
        WhiteFade.gameObject.SetActive(true);
        yield return WhiteFade.SetAlphaCanvasGroup_numTime(_alpha , _time);
    }

    //타이머
    public void SetTimer(string _timer)
    {
        TimerText.text = _timer;
    }

    public IEnumerator EndStartBattle()
    {
        yield return StartCoroutine(cgBattleStart.SetAlphaCanvasGroup_numTime(0, 1.0f));
    }

    public float DamageSize(float _vaule)
    {
        if(_vaule <= 100)
        {
            _vaule = 1.0f;
        }
        else if(_vaule <= 300.0f)
        {
            _vaule = 1.25f;
        }
        else
        {
            _vaule = 1.5f;
        }

        return _vaule;

    }

    #endregion
}
