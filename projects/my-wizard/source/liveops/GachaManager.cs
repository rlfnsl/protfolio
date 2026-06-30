using Coffee.UIExtensions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GachaManager : MonoBehaviour
{
    [System.Serializable]
    public struct ParticleList
    {
        public List<ParticleSystem> _list;
    }
    public GameObject GachaBox;
    public Transform GachaResult;
    public GameObject SkipBtn;
    public GameObject ClosingBtn;
    public GameObject[] GachaEffects;
    public List<ParticleList> GachaParticle = new List<ParticleList>();
    public Color[] GradeColor;
    public Color HighColor;
    [SerializeField, Range(0.05f, 1f)] private float resultRevealDelay = 0.2f;
    LobbyManager lobbymanager;
    public void Setting(int gachatype, int count, List<int> index)
    {
        GameAudioManager audio = GameAudioManager.Ensure();
        audio.StopGachaResultLoopSfx();
        audio.PlayGachaOpenSfx();

        if (GachaParticle.Count == 0)
        {
            for (int i = 0; i < GachaEffects.Length; i++)
            {
                var _list = GachaEffects[i].GetComponentsInChildren<ParticleSystem>(true).ToList();
                ParticleList particleList = new ParticleList
                {
                    _list = _list
                };
                GachaParticle.Add(particleList);
            }
        }
        if (lobbymanager == null)
            lobbymanager = LobbyManager.Instance;
        ClosingBtn.SetActive(false);
        SkipBtn.SetActive(true);
        if (GachaBox.activeSelf)
            GachaBox.SetActive(false);
        if (GachaResult.childCount != 0)
        {
            for (int i = GachaResult.childCount - 1; i >= 0; i--)
            {
                Destroy(GachaResult.GetChild(i).gameObject);
            }
        }
        for (int i = 0; i < GachaEffects.Length; i++)
        {
            GachaEffects[i].SetActive(i < count);
        }
        GachaResult.gameObject.SetActive(false);
        int _ItemMaxRank = 0;
        Debug.Log(index.Count);
        if (gachatype == 1)
        {
            for (int i = 0; i < count; i++)
            {
                var Item = APIData.Instance.GetItem<StaticItemData>(index[i]);
                for (int y = 0; y < GachaParticle[i]._list.Count; y++)
                {
                    var mainModule = GachaParticle[i]._list[y].main;
                    mainModule.startColor = GradeColor[(int)Item.rankType - 1];
                    if (_ItemMaxRank < (int)Item.rankType - 1)
                    {
                        _ItemMaxRank = (int)Item.rankType - 1;
                    }
                }
                lobbymanager.AddItem(index[i]);
                lobbymanager.CreateLoon(GachaResult, Item);
            }
        }
        else if (gachatype == 2)
        {
            lobbymanager.SetPiece(index);
            for (int i = 0; i < count; i++)
            {
                var Item = APIData.Instance.GetItem<SkillData>(index[i]);
                for (int y = 0; y < GachaParticle[i]._list.Count; y++)
                {
                    var mainModule = GachaParticle[i]._list[y].main;
                    mainModule.startColor = GradeColor[Item.rank - 1];
                    if (_ItemMaxRank < Item.rank - 1)
                    {
                        _ItemMaxRank = Item.rank - 1;
                    }
                }
                lobbymanager.CreateSkill(GachaResult, Item);
            }
        }
        HighColor = GradeColor[_ItemMaxRank];
        GachaBox.SetActive(true);
    }
    public void SkipBtnClick()
    {
        GameAudioManager audio = GameAudioManager.Ensure();
        audio.PlayGachaLightningStrikeSfx();
        audio.PlayGachaResultSfx();

        GachaBox.SetActive(false);
        GachaResult.gameObject.SetActive(true);
        ClosingBtn.SetActive(true);
        for (int i = 0; i < GachaResult.childCount; i++)
        {
            StartCoroutine(GachaEffectCountCor(GachaEffects[i].GetComponent<UIParticle>(), GachaResult.GetChild(i).GetComponent<CanvasGroup>(), false));
        }
        audio.StartGachaResultLoopSfx();
    }
    public void GachaEffectStart()
    {
        StartCoroutine(GachaEffectCor());
    }
    IEnumerator GachaEffectCor()
    {
        for (int i = 0; i < GachaResult.childCount; i++)
        {
            GachaResult.GetChild(i).GetComponent<CanvasGroup>().alpha = 0;
        }
        ClosingBtn.gameObject.SetActive(true);
        GachaResult.gameObject.SetActive(true);
        for (int i = 0; i < GachaEffects.Length; i++)
        {
            if (GachaEffects[i].activeSelf)
            {
                //StartCoroutine(GachaEffectCountCor(GachaEffects[i].GetComponent<UIParticle>(), GachaResult.GetChild(i).GetComponent<CanvasGroup>()));
                //yield return new WaitForSeconds(.2f);
                yield return StartCoroutine(GachaEffectCountCor(
                    GachaEffects[i].GetComponent<UIParticle>(),
                    GachaResult.GetChild(i).GetComponent<CanvasGroup>()));
            }
        }
        GameAudioManager.Ensure().StartGachaResultLoopSfx();
    }
    IEnumerator GachaEffectCountCor(UIParticle _particle, CanvasGroup _cg, bool playResultSound = true)
    {
        _particle.Play();
        if (playResultSound)
            GameAudioManager.Ensure().PlayGachaLightningStrikeSfx();
        yield return new WaitForSeconds(resultRevealDelay);
        if (playResultSound)
            GameAudioManager.Ensure().PlayGachaResultSfx();
        _cg.alpha = 1;
    }
    public void OnDisable()
    {
        GameAudioManager.Ensure().StopGachaResultLoopSfx();

        if (GachaResult.childCount != 0)
        {
            for (int i = GachaResult.childCount - 1; i >= 0; i--)
            {
                Destroy(GachaResult.GetChild(i).gameObject);
            }
        }
    }
}
