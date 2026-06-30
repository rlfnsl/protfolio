using System.Collections;
using UnityEngine;
using TMPro;
using static LanguageSingleton;
using System;

public class LangText : MonoBehaviour
{
    public bool ChangeFont = true;
    public string textKey;
    public bool CanChangePos = false;
    public Vector3 ChangePos;
    public TMP_FontAsset[] Font;
    string[] dropdownKey;
    RectTransform rect;
    private void Awake()
    {
        if (instance == null) return;
        LocalizeChanged();
        instance.LocalizeChanged += LocalizeChanged;
        if (CanChangePos)
        {
            rect = GetComponent<RectTransform>();
            if (instance.curLangIndex == 1)
                rect.anchoredPosition = ChangePos;
        }
        if (ChangeFont)
        {
            if (Gamemanager.instance != null)
                GetComponent<TextMeshProUGUI>().font = Gamemanager.instance.Font[instance.curLangIndex];
            if (instance.curLangIndex == 0)
            {
                GetComponent<TextMeshProUGUI>().lineSpacing = 20;
            }
        }
        else
        {
            if (Font.Length == 0)
            {
                if (Gamemanager.instance != null)
                    GetComponent<TextMeshProUGUI>().font = Gamemanager.instance.NOChangeFont[instance.curLangIndex];
                if (instance.curLangIndex == 0)
                {
                    GetComponent<TextMeshProUGUI>().lineSpacing = 20;
                }
            }
            else
            {
                GetComponent<TextMeshProUGUI>().font = Font[instance.curLangIndex];
            }
        }
    }
    private void OnEnable()
    {
        if (!instance) return;
        if (instance.check)
            LocalizeChanged();
    }

    private void OnDestroy()
    {
        if (instance != null)
            instance.LocalizeChanged -= LocalizeChanged;
    }
    string Localize(string key)
    {
        if (instance == null)
            return "";
        return instance.langkeyvalue[instance.curLangIndex][key].Replace("\\n", "\n");
        //int keyIndex = instance.Langs[0].value.FindIndex(x => x.ToLower() == key.ToLower());
        //return instance.Langs[instance.curLangIndex + 1].value[keyIndex].Replace("\\n", "\n");
    }

    public void LocalizeChanged()
    {
        if (string.IsNullOrEmpty(textKey))
            return;
        if (GetComponent<TextMeshProUGUI>() != null)
        {
            GetComponent<TextMeshProUGUI>().text = Localize(textKey);
        }
        else if (GetComponent<TMP_Dropdown>() != null)
        {
            TMP_Dropdown dropdown = GetComponent<TMP_Dropdown>();
            dropdown.captionText.text = Localize(dropdownKey[dropdown.value]);

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                dropdown.options[i].text = Localize(dropdownKey[i]);
            }
        }
    }
    public void LocalizeChanged(string _key)
    {
        if (string.IsNullOrEmpty(_key))
        {
            GetComponent<TextMeshProUGUI>().text = _key;
            return;
        }
        if (GetComponent<TextMeshProUGUI>() != null)
        {
            GetComponent<TextMeshProUGUI>().text = Localize(_key);
        }
        else if (GetComponent<TMP_Dropdown>() != null)
        {
            TMP_Dropdown dropdown = GetComponent<TMP_Dropdown>();
            dropdown.captionText.text = Localize(dropdownKey[dropdown.value]);

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                dropdown.options[i].text = Localize(dropdownKey[i]);
            }
        }
    }
}