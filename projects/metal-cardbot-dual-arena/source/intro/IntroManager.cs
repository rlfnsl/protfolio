using System.Collections;
using UnityEngine;
using Funtion_CWS;
using TMPro;
using Unity.VisualScripting;
using System.Runtime.Remoting.Messaging;
using UnityEngine.Video;

public class IntroManager : MonoBehaviour
{
    public static IntroManager instance;
    public Transform VideoPlayer;
    public GameObject IntroObject;
    public GameObject OpeningObject, admincall;
    public TextMeshProUGUI CoinText, IntroText;
    public AudioClip Bgm;
    public GameObject[] TextCoinObject;
    public GameObject[] RankingObject;
    public GameObject[] IntroProduction;
    public CanvasGroup fade;
    IntroVideo IntroVideoManager;
    public float Delay;
    bool canPlay;
    bool isPlaying;
    float delayTime = 0;
    AudioSource aud;
    Gamemanager manager;
    IEnumerator cor;

    private void Awake()
    {
        instance = this;
        isPlaying = false;
        aud = GetComponent<AudioSource>();
        manager = Gamemanager.instance;
        manager.ShowInfo = false;
        manager.IsCardDispos = false;
        manager.FirstScore = 0;
        IntroVideoManager = manager.GetComponent<IntroVideo>();
        IntroVideoManager.Setting(VideoPlayer.GetComponent<VideoPlayer>(), VideoPlayer.GetComponent<AudioSource>());
        //if (PlayerPrefs.HasKey("Coin"))
        //{
        //    manager.CurCoin = PlayerPrefs.GetInt("Coin");
        //}
        //else
        //{
        //    manager.CurCoin = 0;
        //    PlayerPrefs.SetInt("Coin", manager.CurCoin);
        //}
        //manager.SetCoinText();
        if (manager.MaxCoin != 0)
        {
            CoinText.text = $"{manager.CurCoin / manager.MaxCoin}({manager.CurCoin % manager.MaxCoin}/{manager.MaxCoin})";
            IntroText.text = $"{manager.CurCoin / manager.MaxCoin}({manager.CurCoin % manager.MaxCoin}/{manager.MaxCoin})";
        }
        else
        {
            CoinText.text = $"Free";
            IntroText.text = $"Free";
        }
        IntroObject.SetActive(false);
        OpeningObject.SetActive(true);
        TextCoinObject[0].SetActive(true);
        TextCoinObject[1].SetActive(false);
        RankingObject[0].SetActive(true);
        RankingObject[1].SetActive(false);
        IntroProduction[0].SetActive(true);
        //delayTime = DataManager.Instance.ProductionData.main_idle_time;
        delayTime = 9999999;
        Bgm = manager.MainSound != null ? manager.MainSound : Bgm;
        aud.clip = Bgm;
        aud.Play();
        //IntroObject.GetComponent<AudioSource>().clip = manager.IdelSound != null ? manager.IdelSound : IntroObject.GetComponent<AudioSource>().clip;
        IntroVideoManager.PlayLoop(ShowRanking);
        Gamemanager.instance.IsAdminID = false;
    }
    IEnumerator Start()
    {
        canPlay = false;
        //aud.Play();
        manager.PlayOneShotTTSCor(1, 2);
        yield return StartCoroutine(CardSettingCor());
        yield return new WaitForSeconds(5);
        yield return new WaitUntil(() => delayTime != 0);
        StartCoroutine("TTSSoundPlay");

        //manager.PlayOneShotTTS(2);
        while (true)
        {
            yield return new WaitUntil(() => Delay > delayTime);
            StopCoroutine("TTSSoundPlay");
            manager.PlayOneShotTTS(51);
            if (manager.MaxCoin != 0)
            {
                //if (manager.CurCoin / manager.MaxCoin == 0)
                //{
                OpeningObject.SetActive(false);
                //aud.Stop();
                IntroVideoManager.VideoStop();
                IntroObject.SetActive(true);
                StartCoroutine("IntroStart");

                //}
            }
            else
            {
                OpeningObject.SetActive(false);
                //aud.Stop();
                IntroVideoManager.VideoStop();
                IntroObject.SetActive(true);
                StartCoroutine("IntroStart");
            }
            yield return new WaitUntil(() => Delay < 1);
            manager.FadeBGM(0, 3, Bgm, 1, aud);
            StartCoroutine("TTSSoundPlay");
            StopCoroutine("IntroStart");
            StopCoroutine("RankingCor");
            //aud.Play();
            OpeningObject.SetActive(true);
            IntroVideoManager.PlayLoop(ShowRanking);
            IntroObject.SetActive(false);
        }
    }
    private void Update()
    {
        //if (Input.anyKeyDown)
        //{
        //    Delay = 0;
        //}
        if (Delay < delayTime && !TextCoinObject[1].activeSelf)
        {
            Delay += Time.deltaTime;
        }
    }
    IEnumerator TTSSoundPlay()
    {
        while (true)
        {
            yield return new WaitForSeconds(30);
            manager.PlayOneShotTTS(52);
        }
    }
    void ShowRanking()
    {
        manager.FadeBGM(0, 3, manager.IdelSound, 1, aud);
        Delay = delayTime + 1;
    }
    public void Click(bool _insertCoin = false)
    {
        if (manager.ZeroCard() || !canPlay || isPlaying)
            return;
        Delay = 0;
        if (_insertCoin)
        {
            if (!IntroObject.activeSelf || !IntroProduction[0].activeSelf)
            {
                Delay = 0;
            }
        }
        //else
        //{
        //    if (!IntroObject.activeSelf || !IntroProduction[0].activeSelf)
        //    {
        //        Delay = 0;
        //    }
        //}
        //프리모드
        if (manager.MaxCoin == 0)
        {
            isPlaying = true;
            IntroVideoManager.VideoStop();
            manager.ChoiceBtn();
            manager.CurGameCount++;
            if (!manager.IsAdminID)
            {
                PlayerPrefs.SetInt("CurGameCount", manager.CurGameCount);
            }
            StartCoroutine(ChangeScene());
            return;
        }
        //게임시작할 돈이 될때
        if (manager.CurCoin >= manager.MaxCoin)
        {
            isPlaying = true;
            IntroVideoManager.VideoStop();
            Delay = 0;
            manager.CardReading(true);
            //instance = null;
            manager.CurCoin -= manager.MaxCoin;
            manager.SetCoinText();
            manager.ChoiceBtn();
            if (!manager.IsAdminID)
            {
                manager.CurGameCount++;
                PlayerPrefs.SetInt("CurGameCount", manager.CurGameCount);
            }
            StartCoroutine(ChangeScene());
            return;
        }
        //갯수가적을때
        else
        {
            ////돈을넣은게아닌 버튼을 눌럿을때
            //if (IntroObject.activeSelf && !_insertCoin)
            //{
            //    manager.PlayOneShotTTS(2);
            //    TextCoinObject[0].SetActive(true);
            //    TextCoinObject[1].SetActive(false);
            //    return;
            //}
            //if (TextCoinObject[0].activeSelf)
            //{
            //    StartCoroutine("GameStartCor");
            //    TextCoinObject[0].SetActive(false);
            //    manager.PlayOneShotTTS(3);
            //    TextCoinObject[1].SetActive(true);
            //}

            //동전넣었을때
            if (_insertCoin)
            {
                //랭킹,대기화면일때
                if (IntroObject.activeSelf)
                {
                    //랭킹화면일때
                    if (IntroProduction[0].activeSelf)
                    {
                        manager.PlayOneShotTTS(3);
                        TextCoinObject[1].SetActive(true);
                        TextCoinObject[0].SetActive(false);
                        return;
                    }
                    //대기화면일때
                    else
                    {
                        manager.PlayOneShotTTS(2);
                        TextCoinObject[0].SetActive(true);
                        TextCoinObject[1].SetActive(false);
                    }
                }
                //메인화면일때
                else
                {
                    //코인을 넣어달라는 화면이 아닐때
                    if (!TextCoinObject[1].activeSelf)
                    {
                        manager.PlayOneShotTTS(3);
                        TextCoinObject[0].SetActive(false);
                        TextCoinObject[1].SetActive(true);
                    }
                    return;
                }
            }
            //클릭했을때
            else
            {
                if (!TextCoinObject[1].activeSelf)
                {
                    StartCoroutine("GameStartCor");
                    TextCoinObject[0].SetActive(false);
                    TextCoinObject[1].SetActive(true);
                    manager.PlayOneShotTTS(3);
                }
            }
        }
    }
    IEnumerator ChangeScene()
    {
        manager.FadeBGM(0, 1);
        yield return fade.SetAlphaCanvasGroup_num(1, 0.5f);
        manager.ChangeScene("Mode", false);
    }
    IEnumerator GameStartCor()
    {
        manager.CardReading(false);
        float _time = DataManager.Instance.ProductionData.default_skip_time;
        int curcoin = manager.CurCoin;
        while (_time > 0 && manager.CurCoin < manager.MaxCoin)
        {
            yield return null;
            _time -= Time.deltaTime;
            if (curcoin < manager.CurCoin)
            {
                _time = DataManager.Instance.ProductionData.default_skip_time;
                curcoin = manager.CurCoin;
            }
        }
        manager.CardReading(true);
        if (_time > 0)
        {
            Click();
        }
        else
        {
            manager.PlayOneShotTTS(2);
            TextCoinObject[0].SetActive(true);
            TextCoinObject[1].SetActive(false);
            Delay = 0;
        }

    }
    IEnumerator CardSettingCor()
    {
        if ((manager.isunit1zero && manager.isunit2zero) && !manager.CardError.activeSelf)
        {
            IntroKey.instance.ZeroClick();
            admincall.SetActive(true);
            //if (!manager.CardError.activeSelf) manager.CardError.SetActive(true);
            yield return new WaitUntil(() => !manager.ZeroCard());
            admincall.SetActive(false);
            canPlay = true;
            manager.CardError.SetActive(false);
            IntroKey.instance.DefaultSet();
        }
        else
        {
            canPlay = true;
        }
    }
    IEnumerator IntroStart()
    {
        var _data = DataManager.Instance.ProductionData;
        while (true)
        {
            IntroProduction[0].SetActive(true);
            //IntroProduction[0].GetComponent<AudioSource>().Play();
            RankingObject[0].SetActive(true);
            RankingObject[1].SetActive(false);
            StartCoroutine("RankingCor");
            yield return new WaitForSeconds(_data.ranking_veiw_time);
            manager.PlayOneShotTTS(2);
            Delay = 0;

            //StopCoroutine("RankingCor");
            //IntroProduction[0].SetActive(false);
            //IntroProduction[1].SetActive(true);
            ////IntroProduction[1].GetComponent<AudioSource>().Play();
            //yield return new WaitForSeconds(_data.direction_screen_time);

        }
    }
    IEnumerator RankingCor()
    {
        var _data = DataManager.Instance.ProductionData;
        while (true)
        {
            yield return new WaitForSeconds(_data.ranking_veiw_time / 2);
            RankingObject[0].SetActive(!RankingObject[0].activeSelf);
            RankingObject[1].SetActive(!RankingObject[1].activeSelf);
        }
    }
}
