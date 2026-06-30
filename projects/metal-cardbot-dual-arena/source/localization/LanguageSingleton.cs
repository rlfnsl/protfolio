using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class Lang
{
    public string lang, langLocalize;
    public List<string> value = new List<string>();
}

public class LanguageSingleton : MonoBehaviour
{
    public bool check = false;
    public static LanguageSingleton instance;
    public List<Dictionary<string,string>> langkeyvalue = new List<Dictionary<string, string>>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this);
        }

        else Destroy(this);
        check = false;
#if !UNITY_EDITOR
        //if (Application.internetReachability == NetworkReachability.NotReachable)
        //{
            InitLang();
            check = true;
            return;
        //}
#endif
        StartCoroutine(GetLangs());

    }
    IEnumerator GetLangs()
    {
        GetLang();
        yield return new WaitUntil(() => check);
        InitLang();
    }

    const string langURL = "<REDACTED_GOOGLE_SHEET_URL>";
    public event System.Action LocalizeChanged = () => { };
    public event System.Action LocalizeSettingChanged = () => { };


    public int curLangIndex;
    public List<Lang> Langs;


    void InitLang()
    {
        int langIndex = PlayerPrefs.GetInt("LangIndex", -1);
        int systemIndex = Langs.FindIndex(x => x.lang.ToLower() == Application.systemLanguage.ToString().ToLower());
        if (systemIndex == -1) systemIndex = 0;
        int index = langIndex == -1 ? 0 : langIndex;
        SetLangIndex(index);
        ToDictionary();
    }
    void ToDictionary()
    {
        for (int i = 0; i < Langs.Count - 1; i++)
        {
            Dictionary<string, string> _key = new Dictionary<string, string>();
            for (int j = 0; j < Langs[0].value.Count; j++)
            {
                _key[Langs[0].value[j]] = Langs[i + 1].value[j];
            }
            langkeyvalue.Add(_key);
        }
    }
    public void SetLangIndex(int index)
    {
        curLangIndex = index;
        PlayerPrefs.SetInt("LangIndex", curLangIndex);
        LocalizeChanged();
        LocalizeSettingChanged();
    }


    [ContextMenu("언어 가져오기")]
    void GetLang()
    {
        StartCoroutine(GetLangCo());
    }

    IEnumerator GetLangCo()
    {
        UnityWebRequest www = UnityWebRequest.Get(langURL);
        yield return www.SendWebRequest();
        SetLangList(www.downloadHandler.text);
    }
    void SetLangList(string tsv)
    {

        string[] row = tsv.Split('\n');
        int rowSize = row.Length;
        int columnSize = row[0].Split('\t').Length;
        string[,] Sentence = new string[rowSize, columnSize];



        for (int i = 0; i < rowSize; i++)
        {
            string[] column = row[i].Split('\t');
            for (int j = 0; j < columnSize; j++)
                Sentence[i, j] = column[j].Replace("\r", "");
        }

        Langs = new List<Lang>();

        for (int i = 0; i < columnSize; i++)
        {
            Lang lang = new Lang();
            lang.lang = Sentence[0, i];
            lang.langLocalize = Sentence[1, i];

            for (int j = 2; j < rowSize; j++) lang.value.Add(Sentence[j, i]);
            Langs.Add(lang);
        }
        //for (int i = 0; i < columnSize-1; i++)
        //{
        //    Dictionary<string,string> _key = new Dictionary<string,string>();
        //    for (int j = 0; j < Langs[0].value.Count; j++)
        //    {
        //        _key[Langs[0].value[j]] = Langs[i + 1].value[j];
        //    }
        //    langkeyvalue.Add(_key);
        //}

        check = true;
    }
    public string GetString(string _key)
    {
        return langkeyvalue[curLangIndex][_key].Replace("\\n", "\n");
        //int index = Langs[0].value.FindIndex(x => x.ToLower() == _key.ToLower());
        //return Langs[curLangIndex + 1].value[index].Replace("\\n", "\n");
    }
    //public string FormatString(string _key, int _value, int _value2 = 0)
    //{
    //    if (_value2 != 0)
    //    {
    //        return string.Format(GetString(_key), _value, _value2);
    //    }
    //    else
    //    {
    //        return string.Format(GetString(_key), _value);
    //    }
    //}
    //public string FormatString(string _key, string _value, string _value2 = "")
    //{
    //    if (!string.IsNullOrEmpty(_value2))
    //    {
    //        return string.Format(GetString(_key), _value, _value2);
    //    }
    //    else
    //    {
    //        return string.Format(GetString(_key), _value);
    //    }
    //}
    public string FormatString(string _key, params object[] _value)
    {
        return string.Format(GetString(_key), _value);
    }
}