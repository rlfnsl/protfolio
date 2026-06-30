using Funtion_CWS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static Record;

public class PlayerInfoManager : MonoBehaviour
{
    InfoKey key;
    public TextMeshProUGUI Name, SoloPoint, InfoTimerText, SetNameTimerText;
    public GameObject NamePrefab;
    public CanvasGroup Info, NoCard, SetName;
    public Transform NameParant;
    public Transform CurNamePanel;
    List<NameObject> nameObjects = new List<NameObject>();
    List<TextMeshProUGUI> CurNameList = new List<TextMeshProUGUI>();
    string[] names = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };
    float Timer;
    bool isInfo;
    bool isSetName;
    string curName;
    int index;
    public GameObject Del, Decide;
    private void Awake()
    {
        key = GetComponent<InfoKey>();
        List<Transform> _list = new List<Transform>();
        _list = NameParant.GetComponentsInChildren<Transform>().Where(a => a.name == "Name").ToList();
        for (int i = 0; i < _list.Count; i++)
        {
            NameObject _N = _list[i].gameObject.AddComponent<NameObject>();
            _N.Init(names[i]);
            nameObjects.Add(_N);
        }
        NameObject _Del = Del.AddComponent<NameObject>();
        _Del.Init();
        nameObjects.Add(_Del);
        NameObject _Decide = Decide.AddComponent<NameObject>();
        _Decide.Init(null, true);
        nameObjects.Add(_Decide);
        CurNameList = CurNamePanel.GetComponentsInChildren<TextMeshProUGUI>().Where(a => a.name.Contains("CurNameText")).ToList();
        for (int i = 0; i < CurNameList.Count; i++)
            CurNameList[i].text = "";
        if (!RFCard.instance.CardCheck())
        {
            key.NullSetting();
            Info.alpha = 0;
            NoCard.alpha = 1;
            StartCoroutine(NoCardCor());
        }
        else
        {
            ShowInfo();
        }
        SetName.alpha = 0;
    }
    IEnumerator NoCardCor()
    {
        yield return new WaitForSeconds(2);
        Gamemanager.instance.ChangeScene("Mode", false);
    }
    public void ShowInfo()
    {
        StartCoroutine(KeySet());
    }
    IEnumerator KeySet()
    {
        yield return new WaitForEndOfFrame();
        key.DefaultSet();
        isSetName = false;
        isInfo = true;
        NoCard.alpha = 0;
        SetName.alpha = 0;
        Info.alpha = 1;
        string name = RFCard.instance.GetName();
        if (string.IsNullOrEmpty(name))
        {
            Name.text = LanguageSingleton.instance.GetString("noname");
        }
        else
        {
            Name.text = name;
        }
        int point = RFCard.instance.GetSoloPoint();
        if (point == 0)
        {
            SoloPoint.text = "000,000,000";
        }
        else
        {
            SoloPoint.text = string.Format("{0:#,###}", point);
        }
        Timer = DataManager.Instance.ProductionData.default_skip_time + 0.9f;
    }
    public void ShowSetName()
    {
        key.NameSetting();
        Timer = DataManager.Instance.ProductionData.default_skip_time + 0.9f;
        isSetName = true;
        isInfo = false;
        curName = "";
        index = 0;
        for (int i = 0; i < CurNameList.Count; i++)
        {
            CurNameList[i].text = "";
        }
            for (int i = 0; i < nameObjects.Count; i++)
        {
            nameObjects[i].ReSetInfo(i == 0);
        }
        SetName.alpha = 1;
        Info.alpha = 0;
    }
    public void Click(bool _IsLeft)
    {
        nameObjects[index].Click(false);
        if (_IsLeft)
        {
            index--;
            if (index < 0)
            {
                index = nameObjects.Count - 1;
            }
        }
        else
        {
            index = (index + 1) % nameObjects.Count;
        }
        nameObjects[index].Click(true);
    }
    public void Select()
    {
        NameObject _N = nameObjects[index];
        if (string.IsNullOrEmpty(_N.Name))
        {
            if (_N.Decide)
            {
                Result();
            }
            else
            {
                if (curName.Length > 0)
                {
                    curName = curName.Substring(0, curName.Length - 1);
                }
            }
        }
        else
        {
            if (curName.Length < CurNameList.Count)
            {
                curName += nameObjects[index].Name;
                if(curName.Length== CurNameList.Count)
                {
                    nameObjects[index].Click(false);
                    index = nameObjects.Count - 1;
                    nameObjects[index].Click(true);
                }
            }
        }
        for (int i = 0; i < CurNameList.Count; i++)
        {
            if (i < curName.Length)
                CurNameList[i].text = curName[i].ToString();
            else
                CurNameList[i].text = "";
        }
    }
    void Result()
    {
        Debug.Log(curName);
        RFCard.instance.SetName(curName);
        ShowInfo();
    }
    // Update is called once per frame
    void Update()
    {
        if (isInfo)
        {
            if (Timer > 0)
            {
                Timer -= Time.deltaTime;
                InfoTimerText.text = ((int)Timer).ToString();
            }
            else
            {
                Gamemanager.instance.ChangeScene("Mode");
            }
        }
        if (isSetName)
        {
            if (Timer > 0)
            {
                Timer -= Time.deltaTime;
                SetNameTimerText.text = ((int)Timer).ToString();
            }
            else
            {
                Result();
            }
        }
    }
}
