using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using static LanguageSingleton;

[System.Serializable]
public class Stage
{
    public int difficulty, percent, id, grade, monsterid;
}
[System.Serializable]
public class Monster
{
    public int id, grade, robottype, hp, atk, def_melee, def_range, groggygauge, atk_speed;
}
[System.Serializable]
public class Skill
{
    public enum Rarity
    {
        None = 0,
        common,
        rare,
        epic,
        legendary,
        mythic
    }
    public int card_id, robot_type, type, grade;
    public int ability_lv, hp_lv, deathblow_lv, groggy_lv;
    public int ability_value
    {
        get
        {
            return type == 3 ? DataManager.Instance.GetLevelValue(ability_lv).stat_counter_value : type == 4 ? DataManager.Instance.GetLevelValue(ability_lv).stat_recovery_value : DataManager.Instance.GetLevelValue(ability_lv).stat_ability_value;
        }
    }
    public int hp_value { get { return DataManager.Instance.GetLevelValue(hp_lv).stat_hp_value; } }
    public int deathblow_value { get { return DataManager.Instance.GetLevelValue(deathblow_lv).stat_deathblow_value; } }
    public int groggy_value { get { return DataManager.Instance.GetLevelValue(groggy_lv).stat_groggy_value; } }
    public int card_rarity;
    public string name;
    public string SkillName
    {
        get
        {
            return instance.GetString(name);
        }
    }
    public string RobotName
    {
        get
        {
            return instance.GetString("name_robot_type" + robot_type);
        }
    }
}
[System.Serializable]
public class QRData
{
    public string QR, Name;
    public int Type, CardId;
}
//[System.Serializable]
//public class SupportData
//{
//    public string cardName;
//    public string Name
//    {
//        get
//        {
//            return instance.GetString(cardName);
//        }
//    }
//    public int card_id, card_type, robot_type,value_type,card_value,same_value;
//}
[System.Serializable]
public class SoundData
{
    public string botName;

    public string StageIn, DefaultAttack, CriticalAttack, DefaultBeaten, CriticalBeaten, MoveFoward, MoveBack, DeathBlowAttack
        , Defence, FinalAttack1, FinalAttack2, FinalAttack3, RecoveryShoot, Recovery, DefeatDown, FinalAttackBeaten, BaseJump;

}

[System.Serializable]
public class SkillCardLevelValue
{
    public int level, stat_ability_value, stat_hp_value, stat_deathblow_value, stat_groggy_value, stat_counter_value, stat_recovery_value;
}


public class DataManager : MonoBehaviour
{
    [System.Serializable]
    public class SkillCard
    {
        public int Percent, cardid;
    }
    [System.Serializable]
    public class Result
    {
        public int difficult, combo_s, combo_a, combo_b, damage_s, damage_a, damage_b, time_s, time_a, time_b, hp_s, hp_a, hp_b, combo_unit,
            combo_score, damage_unit, damage_score, time_unit, time_score, hp_unit, hp_score, score_s, score_a, score_b, extrabattle;
    }
    [System.Serializable]
    public class Common
    {
        public int battle_time, battle_deathblow_cell, battle_combo_time, battle_combo1, battle_combo2, battle_combo3, battle_combo4, first_strike_time,
            battle_manual_time, battle_manual_meleehit, battle_manual_rangehit, battle_deathblow_time, battle_finalattack_time, battle_manual_finalhit,
            battle_manual_defensehit, manual_time_delay, battle_groggy_turn, stage_difficulty_value, game_update_season;
        public float attack_damage_minimum, attack_damage_maximum, battle_deathblow_atk, battle_deathblow_height, first_strike_enemy, first_strike_player, defense_skill_max, hidden_boss_score;
    }
    [System.Serializable]
    public class Production
    {
        public float enemy_appearance_time, height_robot1, height_robot2, height_robot3, height_robot4, height_robot5, height_robot6, height_robot7,
            height_robot8, height_robot9, height_robot10, height_robot11, height_robot12,
            stage_panning_time, main_idle_time, warning_hold_time, ranking_veiw_time, direction_screen_time, text_size1, text_size2, text_size3, text_size4,
            first_height_robot1, first_height_robot2, first_height_robot3, first_height_robot4, first_height_robot5, first_height_robot6,
            first_height_robot7, first_height_robot8, first_height_robot9, first_height_robot10, first_height_robot11, first_height_robot12;
        public int battle_warning_count, pay_skip_time, name_skip_time, default_skip_time, coin_insert_skip, battle_exclamationmark_time, slot_skip_time;
    }

    public bool Check;
    public static DataManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }

        else Destroy(this);
        Check = false;
#if !UNITY_EDITOR
        //if (Application.internetReachability == NetworkReachability.NotReachable)
        //{
            //Check = true;
            //return;
        //}
        Check = true;
        return;
#endif
        StartCoroutine(GetDataCo());
    }
    const string stageURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string monsterURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string skillURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string skillcardURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string resultURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string commonURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string QRURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string ProductionURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string SoundURL = "<REDACTED_GOOGLE_SHEET_URL>";
    const string LevelURL = "<REDACTED_GOOGLE_SHEET_URL>";

    public List<Stage> Stages;
    public List<Monster> Monsters;
    public List<Skill> Skills;
    public List<SkillCard> SkillCards;
    public List<QRData> QrDatas;
    //public List<SupportData> SupportDatas;
    public List<SoundData> SoundDatas;
    public List<Result> Results;
    public List<SkillCardLevelValue> SkillLevelValueDatas;
    public Common Commons;
    public Production ProductionData;

    [ContextMenu("�����Ͱ������� ��������")]
    void GetData()
    {
        StartCoroutine(GetDataCo());
    }

    IEnumerator GetDataCo()
    {
        List<Coroutine> corlist = new List<Coroutine>();
        for (int i = 0; i < 10; i++)
        {
            corlist.Add(StartCoroutine(Send(i)));
        }
        //yield return new WaitUntil(() => check);
        foreach (Coroutine cor in corlist)
        {
            yield return cor;
        }
        Check = true;
    }
    IEnumerator Send(int num)
    {
        UnityWebRequest www;
        switch (num)
        {
            case 0:
                www = UnityWebRequest.Get(stageURL);
                yield return www.SendWebRequest();
                SetStageList(www.downloadHandler.text);
                break;
            case 1:
                www = UnityWebRequest.Get(monsterURL);
                yield return www.SendWebRequest();
                SetMonsterList(www.downloadHandler.text);
                break;
            case 2:
                www = UnityWebRequest.Get(skillURL);
                yield return www.SendWebRequest();
                SetSkillList(www.downloadHandler.text);
                break;
            case 3:
                www = UnityWebRequest.Get(skillcardURL);
                yield return www.SendWebRequest();
                SetSkillCardList(www.downloadHandler.text);
                break;
            case 4:
                www = UnityWebRequest.Get(resultURL);
                yield return www.SendWebRequest();
                SetResultList(www.downloadHandler.text);
                break;
            case 5:
                www = UnityWebRequest.Get(commonURL);
                yield return www.SendWebRequest();
                SetCommonList(www.downloadHandler.text);
                break;
            case 6:
                www = UnityWebRequest.Get(QRURL);
                yield return www.SendWebRequest();
                SetQRList(www.downloadHandler.text);
                break;
            case 7:
                www = UnityWebRequest.Get(ProductionURL);
                yield return www.SendWebRequest();
                SetProductionList(www.downloadHandler.text);
                break;
            case 8:
                www = UnityWebRequest.Get(SoundURL);
                yield return www.SendWebRequest();
                SetSoundData(www.downloadHandler.text);
                break;
            case 9:
                www = UnityWebRequest.Get(LevelURL);
                yield return www.SendWebRequest();
                SetLevelData(www.downloadHandler.text);
                break;
        }
    }
    void SetStageList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        Stages = new List<Stage>();

        for (int i = 1; i < rowSize; i++)
        {
            Stage data = new Stage();
            data.difficulty = int.Parse(Sentence[i, 0]);
            data.percent = int.Parse(Sentence[i, 1]);
            data.id = int.Parse(Sentence[i, 2]);
            data.grade = int.Parse(Sentence[i, 3]);
            data.monsterid = int.Parse(Sentence[i, 4]);
            Stages.Add(data);
        }
    }
    void SetMonsterList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        Monsters = new List<Monster>();

        for (int i = 1; i < rowSize; i++)
        {
            Monster data = new Monster();
            data.id = int.Parse(Sentence[i, 0]);
            data.grade = int.Parse(Sentence[i, 1]);
            data.robottype = int.Parse(Sentence[i, 2]);
            data.hp = int.Parse(Sentence[i, 3]);
            data.atk = int.Parse(Sentence[i, 4]);
            data.def_melee = int.Parse(Sentence[i, 5]);
            data.def_range = int.Parse(Sentence[i, 6]);
            data.groggygauge = int.Parse(Sentence[i, 7]);
            data.atk_speed = int.Parse(Sentence[i, 8]);
            Monsters.Add(data);
        }
    }
    void SetSkillList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        Skills = new List<Skill>();

        for (int i = 1; i < rowSize; i++)
        {
            int a = 0;
            Skill data = new Skill();
            data.card_id = int.Parse(Sentence[i, a++]);
            data.robot_type = int.Parse(Sentence[i, a++]);
            data.type = int.Parse(Sentence[i, a++]);
            data.card_rarity = int.Parse(Sentence[i, a++]);
            data.grade = int.Parse(Sentence[i, a++]);
            data.name = Sentence[i, a++].Replace(" ", "");
            data.ability_lv = int.Parse(Sentence[i, a++]);
            data.hp_lv = int.Parse(Sentence[i, a++]);
            data.deathblow_lv = int.Parse(Sentence[i, a++]);
            data.groggy_lv = int.Parse(Sentence[i, a++]);
            Skills.Add(data);
        }
    }
    void SetSkillCardList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        SkillCards = new List<SkillCard>();

        for (int i = 1; i < rowSize; i++)
        {
            SkillCard data = new SkillCard();
            data.Percent = int.Parse(Sentence[i, 0]);
            data.cardid = int.Parse(Sentence[i, 1]);
            SkillCards.Add(data);
        }
    }
    void SetQRList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        QrDatas = new List<QRData>();

        for (int i = 1; i < rowSize; i++)
        {
            QRData data = new QRData();
            data.QR = Sentence[i, 0];
            data.Name = Sentence[i, 1];
            data.Type = int.Parse(Sentence[i, 2]);
            data.CardId = int.Parse(Sentence[i, 3]);
            //data.SupportCardType = int.Parse(Sentence[i, 4]);
            QrDatas.Add(data);
        }
    }
    void SetResultList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        Results = new List<Result>();
        for (int i = 1; i < rowSize; i++)
        {
            Result data = new Result();
            data.difficult = int.Parse(Sentence[i, 0]);
            data.combo_s = int.Parse(Sentence[i, 1]);
            data.combo_a = int.Parse(Sentence[i, 2]);
            data.combo_b = int.Parse(Sentence[i, 3]);
            data.damage_s = int.Parse(Sentence[i, 4]);
            data.damage_a = int.Parse(Sentence[i, 5]);
            data.damage_b = int.Parse(Sentence[i, 6]);
            data.time_s = int.Parse(Sentence[i, 7]);
            data.time_a = int.Parse(Sentence[i, 8]);
            data.time_b = int.Parse(Sentence[i, 9]);
            data.hp_s = int.Parse(Sentence[i, 10]);
            data.hp_a = int.Parse(Sentence[i, 11]);
            data.hp_b = int.Parse(Sentence[i, 12]);
            data.combo_unit = int.Parse(Sentence[i, 13]);
            data.combo_score = int.Parse(Sentence[i, 14]);
            data.damage_unit = int.Parse(Sentence[i, 15]);
            data.damage_score = int.Parse(Sentence[i, 16]);
            data.time_unit = int.Parse(Sentence[i, 17]);
            data.time_score = int.Parse(Sentence[i, 18]);
            data.hp_unit = int.Parse(Sentence[i, 19]);
            data.hp_score = int.Parse(Sentence[i, 20]);
            data.score_s = int.Parse(Sentence[i, 21]);
            data.score_a = int.Parse(Sentence[i, 22]);
            data.score_b = int.Parse(Sentence[i, 23]);
            data.extrabattle = int.Parse(Sentence[i, 24]);
            Results.Add(data);
        }
    }
    void SetCommonList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        for (int i = 1; i < rowSize; i++)
        {
            Common data = new Common();
            data.attack_damage_minimum = float.Parse(Sentence[i, 0]);
            data.attack_damage_maximum = float.Parse(Sentence[i, 1]);
            data.battle_time = int.Parse(Sentence[i, 2]);
            data.battle_deathblow_cell = int.Parse(Sentence[i, 3]);
            data.battle_combo_time = int.Parse(Sentence[i, 4]);
            data.battle_combo1 = int.Parse(Sentence[i, 5]);
            data.battle_combo2 = int.Parse(Sentence[i, 6]);
            data.battle_combo3 = int.Parse(Sentence[i, 7]);
            data.battle_combo4 = int.Parse(Sentence[i, 8]);
            data.first_strike_time = int.Parse(Sentence[i, 9]);
            data.battle_manual_time = int.Parse(Sentence[i, 10]);
            data.battle_manual_meleehit = int.Parse(Sentence[i, 11]);
            data.battle_manual_rangehit = int.Parse(Sentence[i, 12]);
            data.battle_deathblow_time = int.Parse(Sentence[i, 13]);
            data.battle_deathblow_atk = float.Parse(Sentence[i, 14]);
            data.battle_finalattack_time = int.Parse(Sentence[i, 15]);
            data.battle_manual_finalhit = int.Parse(Sentence[i, 16]);
            data.battle_manual_defensehit = int.Parse(Sentence[i, 17]);
            data.manual_time_delay = int.Parse(Sentence[i, 18]);
            data.battle_deathblow_height = float.Parse(Sentence[i, 19]);
            data.first_strike_enemy = float.Parse(Sentence[i, 20]);
            data.first_strike_player = float.Parse(Sentence[i, 21]);
            data.battle_groggy_turn = int.Parse(Sentence[i, 22]);
            data.stage_difficulty_value = int.Parse(Sentence[i, 23]);
            data.defense_skill_max = float.Parse(Sentence[i, 24]);
            data.game_update_season = int.Parse(Sentence[i, 25]);
            data.hidden_boss_score = float.Parse(Sentence[i, 26]);
            Commons = data;
        }
    }
    void SetProductionList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];


        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        for (int i = 1; i < rowSize; i++)
        {
            Production data = new Production();
            int a = 0;
            data.enemy_appearance_time = float.Parse(Sentence[i, a++]);
            data.height_robot1 = float.Parse(Sentence[i, a++]);
            data.height_robot2 = float.Parse(Sentence[i, a++]);
            data.height_robot3 = float.Parse(Sentence[i, a++]);
            data.height_robot4 = float.Parse(Sentence[i, a++]);
            data.height_robot5 = float.Parse(Sentence[i, a++]);
            data.height_robot6 = float.Parse(Sentence[i, a++]);
            data.height_robot7 = float.Parse(Sentence[i, a++]);
            data.height_robot8 = float.Parse(Sentence[i, a++]);
            data.height_robot9 = float.Parse(Sentence[i, a++]);
            data.height_robot10 = float.Parse(Sentence[i, a++]);
            data.height_robot11 = float.Parse(Sentence[i, a++]);
            data.height_robot12 = float.Parse(Sentence[i, a++]);
            data.stage_panning_time = float.Parse(Sentence[i, a++]);
            data.battle_warning_count = int.Parse(Sentence[i, a++]);
            data.main_idle_time = float.Parse(Sentence[i, a++]);
            data.warning_hold_time = float.Parse(Sentence[i, a++]);
            data.coin_insert_skip = int.Parse(Sentence[i, a++]);
            data.default_skip_time = int.Parse(Sentence[i, a++]);
            data.name_skip_time = int.Parse(Sentence[i, a++]);
            data.pay_skip_time = int.Parse(Sentence[i, a++]);
            data.slot_skip_time = int.Parse(Sentence[i, a++]);
            data.ranking_veiw_time = float.Parse(Sentence[i, a++]);
            data.direction_screen_time = float.Parse(Sentence[i, a++]);
            data.battle_exclamationmark_time = int.Parse(Sentence[i, a++]);
            data.text_size1 = float.Parse(Sentence[i, a++]);
            data.text_size2 = float.Parse(Sentence[i, a++]);
            data.text_size3 = float.Parse(Sentence[i, a++]);
            data.text_size4 = float.Parse(Sentence[i, a++]);
            data.first_height_robot1 = float.Parse(Sentence[i, a++]);
            data.first_height_robot2 = float.Parse(Sentence[i, a++]);
            data.first_height_robot3 = float.Parse(Sentence[i, a++]);
            data.first_height_robot4 = float.Parse(Sentence[i, a++]);
            data.first_height_robot5 = float.Parse(Sentence[i, a++]);
            data.first_height_robot6 = float.Parse(Sentence[i, a++]);
            data.first_height_robot7 = float.Parse(Sentence[i, a++]);
            data.first_height_robot8 = float.Parse(Sentence[i, a++]);
            data.first_height_robot9 = float.Parse(Sentence[i, a++]);
            data.first_height_robot10 = float.Parse(Sentence[i, a++]);
            data.first_height_robot11 = float.Parse(Sentence[i, a++]);
            data.first_height_robot12 = float.Parse(Sentence[i, a++]);
            ProductionData = data;
        }
    }
    //void SetSupportList(string tsv)
    //{

    //    string[] row = tsv.Split('\n');
    //    int rowSize = row.Length;
    //    int columnSize = row[0].Split('\t').Length;
    //    string[,] Sentence = new string[rowSize, columnSize];


    //    for (int i = 0; i < rowSize; i++)
    //    {
    //        if (i == 0) continue;
    //        string[] column = row[i].Split('\t');
    //        for (int j = 0; j < columnSize; j++)
    //        {
    //            Sentence[i, j] = column[j];
    //        }
    //    }
    //    SupportDatas = new List<SupportData>();
    //    for (int i = 1; i < rowSize; i++)
    //    {
    //        SupportData data = new SupportData();
    //        data.card_id = int.Parse(Sentence[i, 0]);
    //        data.card_type = int.Parse(Sentence[i, 1]);
    //        data.robot_type = int.Parse(Sentence[i, 2]);
    //        data.cardName = Sentence[i, 3];
    //        data.value_type = int.Parse(Sentence[i, 4]);
    //        data.card_value = int.Parse(Sentence[i, 5]);
    //        data.same_value = int.Parse(Sentence[i, 6]);
    //        SupportDatas.Add(data);
    //    }
    //}

    void SetSoundData(string tsv)
    {
        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];

        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        SoundDatas = new List<SoundData>();
        for (int i = 1; i < rowSize; i++)
        {
            SoundData data = new SoundData();
            data.botName = Sentence[i, 0];
            data.StageIn = Sentence[i, 1];
            data.DefaultAttack = Sentence[i, 2];
            data.CriticalAttack = Sentence[i, 3];
            data.DefaultBeaten = Sentence[i, 4];
            data.CriticalBeaten = Sentence[i, 5];
            data.MoveFoward = Sentence[i, 6];
            data.MoveBack = Sentence[i, 7];
            data.DeathBlowAttack = Sentence[i, 8];
            data.Defence = Sentence[i, 9];
            data.FinalAttack1 = Sentence[i, 10];
            data.FinalAttack2 = Sentence[i, 11];
            data.FinalAttack3 = Sentence[i, 12];
            data.RecoveryShoot = Sentence[i, 13];
            data.Recovery = Sentence[i, 14];
            data.DefeatDown = Sentence[i, 15];
            data.FinalAttackBeaten = Sentence[i, 16];
            data.BaseJump = Sentence[i, 17];
            SoundDatas.Add(data);
        }
    }
    void SetLevelData(string tsv)
    {
        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];

        for (int i = 0; i < rowSize; i++)
        {
            if (i == 0) continue;
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
            {
                Sentence[i, j] = column[j];
            }
        }

        SkillLevelValueDatas = new List<SkillCardLevelValue>();
        for (int i = 1; i < rowSize; i++)
        {
            SkillCardLevelValue data = new SkillCardLevelValue();
            int a = 0;
            data.level = int.Parse(Sentence[i, a++]);
            data.stat_ability_value = int.Parse(Sentence[i, a++]);
            data.stat_hp_value = int.Parse(Sentence[i, a++]);
            data.stat_deathblow_value = int.Parse(Sentence[i, a++]);
            data.stat_groggy_value = int.Parse(Sentence[i, a++]);
            data.stat_counter_value = int.Parse(Sentence[i, a++]);
            data.stat_recovery_value = int.Parse(Sentence[i, a++]);
            SkillLevelValueDatas.Add(data);
        }
    }
    public SkillCardLevelValue GetLevelValue(int _id)
    {
        return SkillLevelValueDatas.Where(a => a.level == _id).FirstOrDefault();
    }
    public Stage GetStage(int _id)
    {
        return Stages.FirstOrDefault(a => a.id == _id);
    }
    public int GetStageId(int _difficulty, bool _isRestart)
    {
        List<Stage> _stage = Stages.Where(a => a.difficulty == _difficulty && (_isRestart ? a.id >= 1000 : a.id < 1000)).ToList();
        List<int> probabilities = _stage.Select(stage => stage.percent).ToList();
        int rand = Random.Range(_isRestart ? 0 : 1, _stage.Sum(stage => stage.percent) + 1);
        int cumulativeProbability = 0;
        for (int i = 0; i < probabilities.Count; i++)
        {
            cumulativeProbability += probabilities[i];
            if (rand <= cumulativeProbability)
            {
                return _stage[i].id;
            }
        }
        return 0;
    }
    public Monster GetMonster(int _id)
    {
        return Monsters.FirstOrDefault(a => a.id == _id);
    }
    public Skill GetSkill(int _id)
    {
        return Skills.FirstOrDefault(a => a.card_id == _id);
    }
    public SkillCard GetSkillCard()
    {
        List<int> probabilities = SkillCards.Select(card => card.Percent).ToList();
        int rand = Random.Range(1, SkillCards.Sum(card => card.Percent) + 1);
        int cumulativeProbability = 0;
        for (int i = 0; i < probabilities.Count; i++)
        {
            cumulativeProbability += probabilities[i];
            if (rand <= cumulativeProbability)
            {
                return SkillCards[i];
            }
        }
        return null;
    }
    public QRData GetQrData(string _qr)
    {
        return QrDatas.FirstOrDefault(a => a.QR == _qr);
    }
    public Skill GetQRSkillData(string _qr)
    {
        var skill = GetSkill(GetQrData(_qr).CardId);
        if (skill == null)
            return null;
        return skill;
    }
    //public SupportData GetQRSupportData(string _qr)
    //{
    //    return GetSupportType(GetQrData(_qr).SupportCardType);
    //}
}

