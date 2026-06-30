using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SkillManager : MonoBehaviour
{
    Dictionary<int, ActiveSkill> ActiveSkills;
    Dictionary<int, ActiveSkill> Hero_ActiveSkills;
    Dictionary<int, ActiveSkill> Dragon_ActiveSkills;
    Dictionary<int, PassiveSkill> Hero_PassiveSkills;
    Dictionary<int, PassiveSkill> Dragon_PassiveSkills;
    ChannelingSkill Channeling = null;

    private bool Set = false;

    private void Update()
    {
        if (!Set)
            return;

        foreach (var active in ActiveSkills.Values)
            active.Tick();

        if (Channeling != null)
        {
            if (Channeling.CanActive() && !Channeling.DoneFlag)
                Channeling.Active();
            else
            {
                Channeling.Done();
                Channeling = null;
            }
        }
    }

    public void Setting()
    {
        Hero_ActiveSkills = new();
        Dragon_ActiveSkills = new();
        Hero_PassiveSkills = new();
        Dragon_PassiveSkills = new();

        foreach (var data in DataManager.Instance.SkillData.Values)
        {
            ActiveSkill skill;

            if (data.SkillType == "")
                continue;
            skill = ActiveSkill.GetActiveSkill(data.SkillType);

            skill.Init(data);
            Hero_ActiveSkills.TryAdd(skill.ID, skill);
        }

        foreach (var data in DataManager.Instance.DragonSkillData.Values)
        {
            ActiveSkill skill;

            if (data.SkillType == "")
                continue;
            skill = ActiveSkill.GetActiveSkill(data.SkillType);

            skill.Init(SkillDataConverter.Convert_Data_Dragon_Skill(data));
            Dragon_ActiveSkills.TryAdd(skill.ID, skill);
        }


        foreach (var data in DataManager.Instance.PassiveSkillData.Values)
        {
            PassiveSkill skill = PassiveSkill.GetPassiveSkill(data);
            if (skill == null)
                continue;

            skill.Init(data);
            Hero_PassiveSkills.TryAdd(skill.ID, skill);
        }

        foreach (var data in DataManager.Instance.DragonPassiveSkillData.Values)
        {
            PassiveSkill skill = PassiveSkill.GetPassiveSkill(SkillDataConverter.Convert_Data_Dragon_Skill_Passive(data));
            if (skill == null)
                continue;

            skill.Init(SkillDataConverter.Convert_Data_Dragon_Skill_Passive(data));
            Dragon_PassiveSkills.TryAdd(skill.ID, skill);
        }

        ActiveSkills = !PhotonManager.Instance.MyPlayerDataInfo.IsRaidBoss ? Hero_ActiveSkills : Dragon_ActiveSkills;

        Set = true;
    }

    public void Migragion(Player player)
    {
        foreach (var skill in ActiveSkills.Values)
        {
            skill.PlayerSetting(player);
        }
    }

    public ActiveSkill ActiveSkillFunc(Player player, int i, Vector3 dir)
    {
        ActiveSkill skill;

        if (player == PhotonManager.Instance.MyPlayer)
        {
            if (!ActiveSkills.TryGetValue(i, out skill))
                return null;

            if (player.HasInputAuthority)
                skill.Setting(player);

            if (skill is ChannelingSkill)
            {
                if (player.HasInputAuthority)
                {
                    Channeling = skill as ChannelingSkill;
                    Channeling.ChannelingSetting(player, dir);
                    player.RPC_SetChannelingSkill(true);
                }
            }
            else if (skill is AimingSkill)
            {
                if (player.HasInputAuthority)
                {
                    Channeling = skill as AimingSkill;
                    Channeling.ChannelingSetting(player, dir);
                    player.RPC_SetChannelingSkill(true);
                }
            }
            else
                skill.Active(player, dir);
        }
        else
        {
            if (player.PlayerInfoData.IsRaidBoss)
            {
                if (!Dragon_ActiveSkills.TryGetValue(i, out skill))
                    return null;
            }
            else
            {
                if (!Hero_ActiveSkills.TryGetValue(i, out skill))
                    return null;
            }

            skill.Setting(player);

            if (skill is ChannelingSkill c)
            {
                var chan = c.Clone(skill.Data) as ChannelingSkill;
                chan?.Setting(player);
                chan?.ChannelingSetting(player, dir);
                player?.RPC_SetChannelingSkill(true);
                return chan;
            }
            else if (skill is AimingSkill)
            {
            }
            else
                skill.Active(player, dir);
        }

        return null;
    }

    public bool TryUseSkillCost(Player player, int i)
    {
        ActiveSkill skill;

        if (player == PhotonManager.Instance.MyPlayer)
        {
            if (!ActiveSkills.TryGetValue(i, out skill))
            {
                return false;
            }

            var cost = skill.Cost;

            if (!skill.CanUse || player.MP < cost)
                return false;

            if (skill.UseSkillCost)
                player.RPC_UseMana(cost);

            if (!(skill is OnOffSkill) && skill.UseCoolTime)
                skill.SetCoolTime();

            return true;
        }
        else
        {
            if (player.PlayerInfoData.IsRaidBoss)
            {
                if (!Dragon_ActiveSkills.TryGetValue(i, out skill))
                    return false;
            }
            else
            {
                if (!Hero_ActiveSkills.TryGetValue(i, out skill))
                    return false;
            }

            var cost = skill.Cost;

            if (player.MP < cost)
                return false;

            if (skill.UseSkillCost)
                player.RPC_UseMana(cost);

            if (!(skill is OnOffSkill))
                skill.SetCoolTime();
            return true;
        }
    }

    public ActiveSkill GetRandomSkill()
    {
        ActiveSkill skill;
        List<int> keys = ActiveSkills.Keys.ToList();
        int RandomIndex;
        do
        {
            RandomIndex = Random.Range(0, keys.Count);

            skill = ActiveSkills[keys[RandomIndex]];

        } while (skill is ChannelingSkill || skill is AimingSkill);

        return skill;
    }

    public PassiveSkill GetPassiveSkill(Player player, int index)
    {
        if (player.PlayerInfoData.IsRaidBoss)
        {
            if (DataManager.Instance.DragonPassiveSkillData.TryGetValue(index, out var _data))
            {
                var data = SkillDataConverter.Convert_Data_Dragon_Skill_Passive(_data);
                PassiveSkill skill = PassiveSkill.GetPassiveSkill(data);
                if (skill == null)
                    return null;

                skill.Init(data);

                if (skill.ActiveSetting(player))
                    return skill;
            }
        }
        else
        {
            if (DataManager.Instance.PassiveSkillData.TryGetValue(index, out var data))
            {
                PassiveSkill skill = PassiveSkill.GetPassiveSkill(data);
                if (skill == null)
                    return null;

                skill.Init(data);

                if (skill.ActiveSetting(player))
                    return skill;
            }
        }
        return null;
    }

    public void TestFunc_GetAllPassive(Player player)
    {
        if (player.PlayerInfoData.IsRaidBoss)
        {
            foreach (var index in Dragon_PassiveSkills)
                player.RPC_AddPassiveSkill(index.Key);
        }
        else
        {
            foreach (var index in Hero_PassiveSkills)
                player.RPC_AddPassiveSkill(index.Key);
        }
    }

    public float SkillCoolTimeRatio(int skillIndex)
    {
        if (ActiveSkills == null)
            return 0;

        if (ActiveSkills.TryGetValue(skillIndex, out var skill))
        {
            return skill.LeftCoolTimeRatio;
        }

        return 0f;
    }

    public bool SkillIsUsing(int skillIndex)
    {
        if (ActiveSkills == null)
            return false;

        if (ActiveSkills.TryGetValue(skillIndex, out var skill))
        {
            return skill.IsUsing;
        }

        return false;
    }
}
