using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;
using System.IO;

public class LegendManager : MonoBehaviour
{
    public enum ranktype
    {
        solo,
        duo,
        challenge,
        none
    }
    [System.Serializable]
    public class RankingData
    {
        public List<RankingEntry> Rankings;
    }
    [System.Serializable]
    public class RankingEntry
    {
        public string Name;
        public int Ranking;
    }
    public ranktype type;
    string[] rankingtype = { "ranking_soloplay", "ranking_duoplay", "ranking_challenge" };
    string[] highrankingtype = { "system_ranking_soloplay", "system_ranking_duoplay", "system_ranking_challenge" };
    List<TextMeshProUGUI> ranktext = new List<TextMeshProUGUI>();
    List<TextMeshProUGUI> nameText = new List<TextMeshProUGUI>();
    List<TextMeshProUGUI> pointText = new List<TextMeshProUGUI>();
    LangText modetext;
    //int LastPoint;
    private const string folderName = "Json";
    private const string rankingFileName = "Ranking.json";
    private const int initialRankingCount = 10;
    private void Awake()
    {
        modetext = GetComponentsInChildren<LangText>(true).Where(a => a.transform.name.Contains("ModeText")).FirstOrDefault();
        ranktext = GetComponentsInChildren<TextMeshProUGUI>(true).Where(a => a.transform.name.Contains("ranking")).ToList();
        nameText = GetComponentsInChildren<TextMeshProUGUI>(true).Where(a => a.transform.name.Contains("Name")).ToList();
        pointText = GetComponentsInChildren<TextMeshProUGUI>(true).Where(a => a.transform.name.Contains("Point")).ToList();
        LoadRankingData();
        if (LanguageSingleton.instance == null) return;
        for (int i = 0; i < ranktext.Count; i++)
        {
            ranktext[i].text = LanguageSingleton.instance.GetString("system_ranking_" + (i + 1));
        }
        SetRanking();
    }
    private void LoadRankingData()
    {
        string folderPath = Path.Combine(Application.streamingAssetsPath, folderName);
        string filePath = Path.Combine(folderPath, rankingFileName);

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            RankingData rankingData = JsonUtility.FromJson<RankingData>(json);
            ApplyRankingData(rankingData);
        }
        else
        {
            CreateDefaultRankingData(folderPath);
        }
    }
    public void SetRanking(ranktype _type = ranktype.none)
    {
        if (modetext == null) return;
        string _key = "";
        if (_type == ranktype.none)
        {
            if (ranktext.Count > 1)
            {
                _key = rankingtype[(int)type];
            }
            else
            {
                _key = highrankingtype[(int)type];
            }
        }
        else
        {
            if (ranktext.Count > 1)
            {
                _key = rankingtype[(int)_type];
            }
            else
            {
                _key = highrankingtype[(int)_type];
            }
        }

        modetext.LocalizeChanged(_key);
    }
    public void EndingGame(int playerScore)
    {
        //다시하기일때
        if (Gamemanager.instance.IsRestart)
        {
            //둘중 점수높은걸 내 점수로 변환
            int myscore = Mathf.Max(playerScore, Gamemanager.instance.FirstScore);
            //점수가 더 낮을때
            if (myscore < Gamemanager.instance.LastPoint)
            {
                BattleManager.instance.GoToIntro();
                return;
            }
            else
            {
                BattleManager.instance.RankingPanel.gameObject.SetActive(true);
                BattleKey.instance.AddBattleSetting();
                BattleManager.instance.RankingPanel.Score = myscore;
                BattleManager.instance.RankingPanel.NewRanking();
            }
        }
        //아닐때
        else
        {
            if (playerScore <= 0)
            {
                BattleManager.instance.GoToIntro();
                return;
            }
            //엑스트라점수보다낮을때
            if (playerScore < Gamemanager.instance.curextrapoint)
            {
                //랭킹점수보다낮을때
                if (playerScore < Gamemanager.instance.LastPoint)
                {
                    BattleManager.instance.GoToIntro();
                    return;
                }
                //랭킹보다높을때
                else
                {
                    BattleManager.instance.RankingPanel.gameObject.SetActive(true);
                    BattleManager.instance.RankingPanel.Score = playerScore;
                    if (Gamemanager.instance.FirstScore == 0)
                        Gamemanager.instance.FirstScore = playerScore;
                    BattleManager.instance.RankingPanel.NewRanking();
                }
            }
            //엑스트라점수보다높을때
            else
            {
                //카드가있을때
                if (!Gamemanager.instance.ZeroCard())
                {
                    BattleManager.instance.RankingPanel.gameObject.SetActive(true);
                    BattleManager.instance.RankingPanel.Score = playerScore;
                    if (Gamemanager.instance.FirstScore == 0)
                        Gamemanager.instance.FirstScore = playerScore;
                    BattleManager.instance.RankingPanel.NextEnemy();
                }
                //카드가없을때
                else
                {
                    //랭킹점수보다낮을때
                    if (playerScore < Gamemanager.instance.LastPoint)
                    {
                        BattleManager.instance.GoToIntro();
                        return;
                    }
                    //랭킹보다높을때
                    else
                    {
                        BattleManager.instance.RankingPanel.gameObject.SetActive(true);
                        BattleManager.instance.RankingPanel.Score = playerScore;
                        if (Gamemanager.instance.FirstScore == 0)
                            Gamemanager.instance.FirstScore = playerScore;
                        BattleManager.instance.RankingPanel.NewRanking();
                    }
                }
            }

        }
    }
    public void UpdatePlayerRanking(string playerName, int playerScore)
    {

        string folderPath = Path.Combine(Application.streamingAssetsPath, folderName);
        string filePath = Path.Combine(folderPath, rankingFileName);

        string json = File.ReadAllText(filePath);
        RankingData rankingData = JsonUtility.FromJson<RankingData>(json);

        UpdateRankingData(rankingData, playerName, playerScore);


        string updatedJson = JsonUtility.ToJson(rankingData);
        File.WriteAllText(filePath, updatedJson);
        ApplyRankingData(rankingData);
    }
    private void CreateDefaultRankingData(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        List<RankingEntry> defaultRankings = new List<RankingEntry>();

        for (int i = 0; i < initialRankingCount; i++)
        {
            //switch (i)
            //{
            //    case 0:
            //        RankingEntry entry = new RankingEntry
            //        {
            //            Name = "ChoiWooseok",
            //            Ranking = 10000
            //        };

            //        defaultRankings.Add(entry);
            //        break;
            //    case 1:
            //        entry = new RankingEntry
            //        {
            //            Name = "ParkSanghee",
            //            Ranking = 9000
            //        };

            //        defaultRankings.Add(entry);
            //        break;
            //    case 2:
            //        entry = new RankingEntry
            //        {
            //            Name = "LeeMinwoo",
            //            Ranking = 8000
            //        };

            //        defaultRankings.Add(entry);
            //        break;
            //    case 3:
            //        entry = new RankingEntry
            //        {
            //            Name = "LeeMinjong",
            //            Ranking = 7000
            //        };

            //        defaultRankings.Add(entry);
            //        break;
            //    case 4:
            //        entry = new RankingEntry
            //        {
            //            Name = "KimSoobin",
            //            Ranking = 6000
            //        };

            //        defaultRankings.Add(entry);
            //        break;
            //    default:
            //        entry = new RankingEntry
            //        {
            //            Name = "Player",
            //            Ranking = 0
            //        };

            //        defaultRankings.Add(entry);
            //        break;
            //}
            RankingEntry entry = new RankingEntry
            {
                Name = "Player",
                Ranking = 2000 - (i * 100)
            };

            defaultRankings.Add(entry);
        }

        RankingData rankingData = new RankingData
        {
            Rankings = defaultRankings
        };

        rankingData.Rankings.Sort((a, b) => b.Ranking.CompareTo(a.Ranking));
        string jsonData = JsonUtility.ToJson(rankingData);
        string filePath = Path.Combine(folderPath, rankingFileName);

        File.WriteAllText(filePath, jsonData);


        ApplyRankingData(rankingData);
    }
    private void ApplyRankingData(RankingData rankingData)
    {
        //LastPoint = rankingData.Rankings[initialRankingCount - 1].Ranking;
        Gamemanager.instance.SetLastPoint(rankingData.Rankings[initialRankingCount - 1].Ranking);
        for (int i = 0; i < nameText.Count; i++)
        {
            if (i < rankingData.Rankings.Count)
            {
                nameText[i].text = rankingData.Rankings[i].Name;
                if (rankingData.Rankings[i].Ranking != 0)
                {
                    pointText[i].text = string.Format("{0:#,0}", rankingData.Rankings[i].Ranking);
                }
                else
                {
                    pointText[i].text = "000,000,000";
                }

            }
            else
            {
                nameText[i].text = "";
                pointText[i].text = "";
            }
        }
    }
    private void UpdateRankingData(RankingData rankingData, string playerName, int playerScore)
    {

        bool playerFound = false;
        int sameNum = 0;

        for (int i = 0; i < rankingData.Rankings.Count; i++)
        {
            if (rankingData.Rankings[i].Ranking == playerScore)
            {
                playerFound = true;
                sameNum = i;
                break;
            }
        }

        RankingEntry newEntry = new RankingEntry
        {
            Name = playerName,
            Ranking = playerScore
        };
        rankingData.Rankings.Add(newEntry);

        rankingData.Rankings.Sort((a, b) => b.Ranking.CompareTo(a.Ranking));
        if (playerFound)
        {
            RankingEntry temp = rankingData.Rankings[sameNum];
            int index = rankingData.Rankings.IndexOf(newEntry);
            rankingData.Rankings[sameNum] = newEntry;
            rankingData.Rankings[index] = temp;
            if (Record.Instance != null)
            {
                Record.Instance.MyRankingNum = sameNum;
            }
        }
        else
        {
            for (int i = 0; i < rankingData.Rankings.Count; i++)
            {
                if (rankingData.Rankings[i].Name == playerName && rankingData.Rankings[i].Ranking == playerScore)
                {
                    if (Record.Instance != null)
                    {
                        Record.Instance.MyRankingNum = i;
                    }
                    break;
                }
            }
        }

        if (rankingData.Rankings.Count > initialRankingCount)
        {
            rankingData.Rankings.RemoveRange(initialRankingCount, rankingData.Rankings.Count - initialRankingCount);
        }
    }
}
