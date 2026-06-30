using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkillManager_WS : MonoBehaviour
{
    #region Public
    public static SkillManager_WS instance;
    public Player player;
    public Dictionary<int, SkillInterFace> SkillKeyValue = new();
    public Dictionary<SpiritSynergy, SkillInterFace> SynergySkillKeyValue = new();
    public Dictionary<int, ObjectPool_WS<HitCollider>> upgradeSkillObjectPools = new();
    public Dictionary<int, ObjectPool_WS<HitCollider>> defaultSkillObjectPools = new();
    public Dictionary<string, ObjectPool_WS<SkillSubmit>> skillSubMitObjectPools = new();
    public ObjectPool_WS<HitCollider> SynergyObjectPool;
    public SkillButtonManager SkillButton;
    public SkillInterFace SynergySkill;
    #endregion

    #region Private
    private int[] equipskill;
    private List<SkillInterFace> allSkills = new();
    private Dictionary<int, int> SkillBtnKeyValue = new();
    #endregion

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }

        equipskill = GameManager.gInstance != null ? GameManager.gInstance.equipskill : new int[] { 1, 2, 3, 4, 5, 6 };
        LoadSkills();
    }

    private void LoadSkills()
    {
        Type skillType = typeof(SkillInterFace);
        List<Type> skillTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => skillType.IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        foreach (Type type in skillTypes)
        {
            SkillInterFace skill = (SkillInterFace)Activator.CreateInstance(type);
            if (skill.IsSynergySkill)
            {
                if (!SynergySkillKeyValue.ContainsKey(skill.SynergyType))
                    SynergySkillKeyValue.Add(skill.SynergyType, skill);
            }
            else
                allSkills.Add(skill);
        }
        Setting();
    }

    public void Setting()
    {
        var _tempskill = allSkills.Where(skill => equipskill.Contains(skill.id)).ToList();
        for (int i = 0; i < _tempskill.Count; i++)
        {
            if (!SkillKeyValue.ContainsKey(_tempskill[i].id))
                SkillKeyValue.Add(_tempskill[i].id, _tempskill[i]);
        }

        for (int i = 0; i < equipskill.Length; i++)
        {
            SkillButton.CreateSkillButton(SkillKeyValue[equipskill[i]], i);
            SkillBtnKeyValue.Add(equipskill[i], i);
            CreateSkill(equipskill[i], SkillKeyValue[equipskill[i]].Name, Mathf.Max(1, (int)SkillKeyValue[equipskill[i]].CurProjectileCount));
        }
        SetUseSkill(equipskill[0]);
    }
    public List<SkillInterFace> GetRandomUpgradeableSkillsByUnlocked(HashSet<int> unlockedSkillIds, int count)
    {
        if (unlockedSkillIds == null || unlockedSkillIds.Count == 0)
            return new List<SkillInterFace>();

        List<SkillInterFace> _candidates = SkillKeyValue
            .Select(_pair => _pair.Value)
            .Where(_skill =>
                unlockedSkillIds.Contains(_skill.id) &&
                (
                    _skill.Level < _skill.LevelupType.Count ||
                    (_skill.Level >= _skill.LevelupType.Count && _skill.CanUpgradeSkill() && !_skill.IsUpgrade)
                )
            )
            .ToList();

        if (_candidates.Count == 0)
            return new List<SkillInterFace>();

        List<SkillInterFace> _filtered = _candidates
            .Where(_skill => !lastSkillIds.Contains(_skill.id))
            .ToList();

        List<SkillInterFace> _finalList = _filtered.Count >= count ? _filtered : _candidates;

        for (int _i = 0; _i < _finalList.Count; _i++)
        {
            int _r = UnityEngine.Random.Range(_i, _finalList.Count);
            (_finalList[_i], _finalList[_r]) = (_finalList[_r], _finalList[_i]);
        }

        List<SkillInterFace> _result = _finalList.Take(count).ToList();
        lastSkillIds = _result.Select(_s => _s.id).ToList();

        return _result;
    }
    void CreateSkill(int _num, string _skillname, int _count)
    {
        if (SkillKeyValue[_num].SkillBehavior is IStandaloneSkillBehavior standaloneSkill &&
            !standaloneSkill.RequiresSkillObjectPool)
        {
            return;
        }

        Action<HitCollider> initializeHitCollider = (HitCollider hitCollider) =>
        {
            hitCollider.SkillSetting(SkillKeyValue[_num]);
        };

        RuntimeAssetProvider.LoadAssetAsync<GameObject>(_skillname + "_Upgrade", result =>
        {
            if (result.Succeeded)
            {
                GameObject prefab = result.Result;
                AddressableHandler.Instance.ReplaceShadersAll(prefab);

                if (!upgradeSkillObjectPools.ContainsKey(_num))
                {
                    upgradeSkillObjectPools[_num] = new ObjectPool_WS<HitCollider>(
                        prefab.GetComponent<HitCollider>(), _count, this.transform, initializeHitCollider
                    );
                }
            }
            else
            {
                Debug.LogError($"Failed to load {_skillname}_Upgrade prefab.");
            }
        });

        RuntimeAssetProvider.LoadAssetAsync<GameObject>(_skillname + "_Default", result =>
        {
            if (result.Succeeded)
            {
                GameObject prefab = result.Result;
                AddressableHandler.Instance.ReplaceShadersAll(prefab);

                if (!defaultSkillObjectPools.ContainsKey(_num))
                {
                    defaultSkillObjectPools[_num] = new ObjectPool_WS<HitCollider>(
                        prefab.GetComponent<HitCollider>(), _count, this.transform, initializeHitCollider
                    );
                }
            }
            else
            {
                Debug.LogError($"Failed to load {_skillname}_Default prefab.");
            }
        });

        foreach (var subEffectKey in SkillKeyValue[_num].GetSubEffectKeys())
        {
            RuntimeAssetProvider.LoadAssetAsync<GameObject>(subEffectKey, result =>
            {
                if (result.Succeeded)
                {
                    GameObject prefab = result.Result;
                    AddressableHandler.Instance.ReplaceShadersAll(prefab);

                    if (!skillSubMitObjectPools.ContainsKey(subEffectKey))
                    {
                        Action<SkillSubmit> initializeSubmit = (SkillSubmit submit) =>
                        {
                            submit.SetSkill(SkillKeyValue[_num]);
                        };

                        skillSubMitObjectPools[subEffectKey] = new ObjectPool_WS<SkillSubmit>(
                            prefab.GetComponent<SkillSubmit>(), _count, this.transform, initializeSubmit
                        );
                    }
                }
                else
                {
                    Debug.LogError($"Failed to load {subEffectKey} prefab.");
                }
            });
        }
    }

    public void CreateSynergySkill()
    {
        Action<HitCollider> initializeHitCollider = (HitCollider hitCollider) =>
        {
            hitCollider.SkillSetting(SynergySkill);
        };

        string name = SynergySkill.SynergyType.ToString();
        RuntimeAssetProvider.LoadAssetAsync<GameObject>(name, result =>
        {
            if (result.Succeeded)
            {
                GameObject prefab = result.Result;
                AddressableHandler.Instance.ReplaceShadersAll(prefab);

                SynergyObjectPool = new ObjectPool_WS<HitCollider>(
                        prefab.GetComponent<HitCollider>(), 1, this.transform, initializeHitCollider
                        );
            }
            else
            {
                Debug.LogError($"Failed to load {name}_Upgrade prefab.");
            }
        });
    }

    private List<int> lastSkillIds = new List<int>();
    public ObjectPool_WS<SkillSubmit> GetSkillSubmit(string _key)
    {
        if(skillSubMitObjectPools.ContainsKey(_key))
        {
            return skillSubMitObjectPools[_key];
        }
        return null;
    }
    public List<Sprite> GetUpgradePassiveIcon(string passiveName)
    {
        var result = SkillKeyValue
            .Select(pair => pair.Value)
            .Where(skill =>skill.UpgradePassive != null && skill.UpgradePassive.Contains(passiveName))
            .Select(skill => skill.SkillIcon)
            .Where(icon => icon != null)
            .ToList();
        return result;
    }

    public void LevelUp(int _index, List<PlusValueType> _type)
    {
        int _btnIndex = SkillBtnKeyValue[_index];
        SkillKeyValue[_index].ApplyLevelUp(_type);
        SkillButton.LevelUp(_btnIndex);
    }

    public void UpgradeSkill(int _index)
    {
        int _btnIndex = SkillBtnKeyValue[_index];
        SkillButton.UpgradeSkill(_btnIndex);
        SkillKeyValue[_index].ApplyUpgrade();
    }

    public void UseSynergySkill(ElementType _elemental)
    {
        if (!IsMatchingSynergy(_elemental)) return;
        SynergySkill.UseSynergySkill();
    }

    public bool IsMatchingSynergy(ElementType _elemental)
    {
        if (SynergySkill == null) return false;

        return SynergySkill.SynergyType switch
        {
            SpiritSynergy.Steam => _elemental == ElementType.fire || _elemental == ElementType.water,
            SpiritSynergy.Discharge => _elemental == ElementType.fire || _elemental == ElementType.electric,
            SpiritSynergy.Overheat => _elemental == ElementType.fire || _elemental == ElementType.land,
            SpiritSynergy.Conduction => _elemental == ElementType.water || _elemental == ElementType.electric,
            SpiritSynergy.Swamp => _elemental == ElementType.water || _elemental == ElementType.land,
            SpiritSynergy.Magnetism => _elemental == ElementType.electric || _elemental == ElementType.land,
            _ => false
        };
    }

    // -----------------------------
    // ✅ 새로 추가된 부분
    // -----------------------------
    public void ApplyStatusEffect(string effectName, float power, float duration)
    {
        Debug.Log($"[ApplyStatusEffect] {effectName} 적용됨 (power={power}, duration={duration})");
        // TODO: 실제 상태이상 시스템 연결시 여기에 구현
    }

    public void UpgradeStatusEffect(string effectName, int level)
    {
        Debug.Log($"[UpgradeStatusEffect] {effectName} 레벨업 (Lv {level})");
        // TODO: 상태이상 효과 강화 로직 추가 가능
    }
    public void ResetSkillCool()
    {
        SkillButton.ResetAllSkillCooldown();
    }
    public void Update()
    {
        if ((Input.GetKeyDown(KeyCode.L)))
        {
            ResetSkillCool();
        }
    }
    public void SetUseSkill(int skillId)
    {
        SkillButton.SetUseSkillButton(skillId);
    }
}
