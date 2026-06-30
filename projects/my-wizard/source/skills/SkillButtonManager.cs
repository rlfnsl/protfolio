using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillButtonManager : MonoBehaviour
{
    public Transform buttonContainer;
    public List<SkillButton> buttons = new List<SkillButton>();
    public RectTransform mouseRange;
    public int TestUseSkill;
    public float gameStartSkillWaitTime = 2f;

    public void CreateSkillButton(SkillInterFace _skill, int index)
    {
        SkillButton skillButton = buttonContainer.GetChild(index).GetComponent<SkillButton>();
        skillButton.SetSkill(_skill, _skill.SkillIcon, index, this);
        buttons.Add(skillButton);
        buttons[buttons.Count - 1].key = (KeyCode)256 + buttons.Count;
    }

    public void SetButton(SkillInterFace _skill, Sprite _sprite, int index)
    {
        buttons[index].SetSkill(_skill, _sprite, index, this);
    }
    public void UpgradeSkill(int index)
    {
        buttons[index].UpgradeSkill();
    }
    public void LevelUp(int index)
    {
        buttons[index].SetLv();
    }
    public void SetUseSkillButton(int skillId)
    {
        foreach (var btn in buttons)
        {
            if (btn.skill.id == skillId)
            {
                btn.SetUseSkill();
                break;
            }
        }
    }
    public void ResetAllSkillCooldown()
    {
        foreach (var btn in buttons)
        {
            btn.ResetSkillCooldown();
        }
    }
}
