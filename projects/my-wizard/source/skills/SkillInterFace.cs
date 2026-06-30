using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public enum PlusValueType
{
    Damage = 0, //데미지
    AttackSize, //범위증가
    ProjectileCout, //투사체
    CoolTime, //쿨타임
    Time, //지속시간
    IntervelCount, //데미지 입히는 텀
}
public abstract class SkillInterFace
{
    public const string SfxKeyCast = "cast";
    public const string SfxKeyCastUpgrade = "cast_upgrade";
    public const string SfxKeyLightningTurretSpawn = "lightning_turret_spawn";
    public const string SfxKeyLightningTurretAttack = "lightning_turret_attack";
    public const string SfxKeyLightningTurretExplode = "lightning_turret_explode";
    public const string SfxKeyStoneBulletImpact = "stone_bullet_impact";
    public const string CommonUpgradeCastSfxPath = "Audio/SFX/skill_upgrade_common_cast";

    public Player player;
    public int id;
    public string Name;
    public string SkillName; //스킬이름
    public string SkillKey;
    public HitCollider OriginSkill;
    public HitCollider SkillUpgrade;
    public Sprite SkillIcon; //스킬기본이미지
    public Sprite UpgradeSkillIcon; //스킬업그레이드이미지
    public SkillData SkillData;
    public SkillManager_WS Manager;
    public ElementType ElementalType;
    public ElementType ElementalType2;
    public SpiritSynergy SynergyType;
    public bool IsSynergySkill = false;
    public bool IsUseSkill = false;
    public bool IsPersistentSkill = false;  //지속스킬
    public BuffManager BuffManager => player.BuffManager;
    public float SkillDelay = 0;
    public bool isSingleHit = false;
    public Dictionary<int, List<int>> LevelupType = new Dictionary<int, List<int>>();
    public HashSet<int> LevelupTypeIndex = new HashSet<int>();
    public List<string> UpgradePassive = new List<string>();
    public ISkillBehavior SkillBehavior { get; set; }
    public Action<Transform> HitAction;
    public bool CanUseSkill = false;
    readonly Dictionary<string, string> skillSfxPaths = new();
    public ObjectPool_WS<HitCollider> CurSkill
    {
        get
        {
            if (Manager == null)
                Manager = SkillManager_WS.instance;
            if (IsSynergySkill)
            {
                return Manager.SynergyObjectPool;
            }
            else
            {
                if (IsUpgrade)
                {
                    return Manager.upgradeSkillObjectPools[id];
                }
                else
                {
                    return Manager.defaultSkillObjectPools[id];
                }
            }
        }
    }
    public ObjectPool_WS<HitCollider> ReturnPool(string _name)
    {
        if (Manager == null)
            Manager = SkillManager_WS.instance;
        if (IsSynergySkill)
        {
            return Manager.SynergyObjectPool;
        }
        else
        {
            if (_name.Contains("_Upgrade"))
            {
                return Manager.upgradeSkillObjectPools[id];
            }
            else
            {
                return Manager.defaultSkillObjectPools[id];
            }
        }
    }
    #region 스킬관련 능력치
    public int Level = 1;

    public Dictionary<PlusValueType, float> valueSetters = new();
    public Dictionary<PlusValueType, Func<float>> valueIncrements = new();
    public Dictionary<PlusValueType, int> SetLevel = new(); //스킬레벨

    protected abstract void ConfigureValueMappings();

    protected void SetSkillSound(string castSfxPath, string upgradeCastSfxPath = null)
    {
        SetSkillSfx(SfxKeyCast, castSfxPath);
        SetSkillSfx(SfxKeyCastUpgrade, upgradeCastSfxPath);
    }

    protected void SetSkillSfx(string key, string resourcePath)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (string.IsNullOrEmpty(resourcePath))
        {
            skillSfxPaths.Remove(key);
            return;
        }

        skillSfxPaths[key] = resourcePath;
    }

    public virtual string GetCastSfxPath()
    {
        if (IsSynergySkill)
            return null;

        string castPath = GetSkillSfxPath(SfxKeyCast);
        if (!IsUpgrade)
            return castPath;

        string upgradePath = GetSkillSfxPath(SfxKeyCastUpgrade);
        if (!string.IsNullOrEmpty(upgradePath))
            return upgradePath;

        return string.IsNullOrEmpty(castPath) ? null : CommonUpgradeCastSfxPath;
    }

    public virtual string GetSkillSfxPath(string key, string fallbackPath = null)
    {
        if (string.IsNullOrEmpty(key))
            return fallbackPath;

        return skillSfxPaths.TryGetValue(key, out string path) && !string.IsNullOrEmpty(path)
            ? path
            : fallbackPath;
    }

    public void ApplyLevelUp(List<PlusValueType> type, int level = 1)
    {
        if (IsSynergySkill) return;
        bool _canLevelUp = false;
        for (int i = 0; i < type.Count; i++)
        {
            if (valueSetters.ContainsKey(type[i]) && valueIncrements.ContainsKey(type[i]))
            {
                _canLevelUp = true;
                float originValue = valueSetters[type[i]];
                SetLevel[type[i]] = SetLevel[type[i]] + level;
                float increment = valueIncrements[type[i]]();
                if (IsPlusValueType(type[i]))
                {
                    valueSetters[type[i]] = (originValue + increment);
                }
                else
                {
                    valueSetters[type[i]] = (originValue * (1 + increment));
                }
            }
        }
        if (_canLevelUp)
        {
            Level += level;
        }
    }
    public void ApplyUpgrade()
    {
        if (IsSynergySkill || IsUpgrade) return;
        SpecialLevelEffect();
        List<PlusValueType> type = LevelupType[LevelupType.Count].Select(x => (PlusValueType)x).ToList();
        for (int i = 0; i < type.Count; i++)
        {
            if (valueSetters.ContainsKey(type[i]) && valueIncrements.ContainsKey(type[i]))
            {
                float originValue = valueSetters[type[i]];
                float increment = valueIncrements[type[i]]();
                if (IsPlusValueType(type[i]))
                {
                    valueSetters[type[i]] = (originValue + increment);
                }
                else
                {
                    valueSetters[type[i]] = (originValue * (1 + increment));
                }
            }
        }
        IsUpgrade = true;
    }
    private void InitializeDefaultMappings()
    {
        if (IsSynergySkill)
        {
            return;
        }

        if (GameManager.gInstance == null)
        {
            valueSetters[PlusValueType.Damage] = 100;
            valueIncrements[PlusValueType.Damage] = () => 30 * .01f;
            SetLevel[PlusValueType.Damage] = 1;

            valueSetters[PlusValueType.AttackSize] = 1;
            valueIncrements[PlusValueType.AttackSize] = () => 20 * .01f;
            SetLevel[PlusValueType.AttackSize] = 1;

            valueSetters[PlusValueType.ProjectileCout] = 1;
            valueIncrements[PlusValueType.ProjectileCout] = () => 1;
            SetLevel[PlusValueType.ProjectileCout] = 1;

            valueSetters[PlusValueType.CoolTime] = 5;
            valueIncrements[PlusValueType.CoolTime] = () => -20 * .01f;
            SetLevel[PlusValueType.CoolTime] = 1;

            valueSetters[PlusValueType.Time] = 3;
            valueIncrements[PlusValueType.Time] = () => -20 * .01f;
            SetLevel[PlusValueType.Time] = 1;
        }
        else
        {
            valueSetters[PlusValueType.Damage] = SkillData.value1;
            valueIncrements[PlusValueType.Damage] = () => SkillData.value1Add * .01f;
            SetLevel[PlusValueType.Damage] = 1;

            valueSetters[PlusValueType.AttackSize] = SkillData.size;
            valueIncrements[PlusValueType.AttackSize] = () => SkillData.sizeAdd * .01f;
            SetLevel[PlusValueType.AttackSize] = 1;

            valueSetters[PlusValueType.ProjectileCout] = SkillData.projectile;
            valueIncrements[PlusValueType.ProjectileCout] = () => SkillData.projectileAdd;
            SetLevel[PlusValueType.ProjectileCout] = 1;

            valueSetters[PlusValueType.CoolTime] = SkillData.cool;
            valueIncrements[PlusValueType.CoolTime] = () => SkillData.coolDec * .01f;
            SetLevel[PlusValueType.CoolTime] = 1;

            valueSetters[PlusValueType.Time] = SkillData.time;
            valueIncrements[PlusValueType.Time] = () => SkillData.timeAdd * .01f;
            SetLevel[PlusValueType.Time] = 1;
        }
    }
    public float speed;

    public bool IsUpgrade;
    #region 최종 능력치

    public virtual float CurDamage
    {
        get
        {
            return valueSetters[PlusValueType.Damage];
        }
    }

    public virtual float CurAttackSize
    {
        get
        {
            PlusValueType _type = PlusValueType.AttackSize;
            if (IsSynergySkill) return valueSetters[_type];

            if (IsContainLevelUpType(_type))
            {
                if (player.playerStat.TotalSize == 0) return valueSetters[_type];
                return valueSetters[_type] * player.playerStat.TotalSize;
            }
            else
            {
                return valueSetters[_type];
            }
        }
    }

    public virtual float CurProjectileCount
    {
        get
        {
            PlusValueType _type = PlusValueType.ProjectileCout;
            if (IsSynergySkill) return Mathf.Max(1, valueSetters[_type]);

            if (IsContainLevelUpType(_type))
            {
                return Mathf.Max(1, valueSetters[_type] + player.GetProjecttile(ElementalType));
            }
            else
            {
                return Mathf.Max(1, valueSetters[_type]);
            }
        }
    }

    public virtual float CurCoolTime
    {
        get
        {
            if (IsSynergySkill) return valueSetters[PlusValueType.CoolTime];

            if (GameManager.gInstance == null)
                return valueSetters[PlusValueType.CoolTime];

            float _value = 1;
            switch (ElementalType)
            {
                case ElementType.fire:
                    _value = player.playerStat.TotalFireCool;
                    break;
                case ElementType.electric:
                    _value = player.playerStat.TotalElectricCool;
                    break;
                case ElementType.land:
                    _value = player.playerStat.TotalLandCool;
                    break;
                case ElementType.water:
                    _value = player.playerStat.TotalWaterCool;
                    break;
                case ElementType.None:
                    _value = player.playerStat.TotalCool;
                    break;
            }

            if (PassiveManager.instance != null)
                _value *= PassiveManager.instance.PassiveCool;

            if (_value == 0)
                _value = 1;

            return valueSetters[PlusValueType.CoolTime] * _value;
        }
    }

    public virtual float CurTime
    {
        get
        {
            PlusValueType _type = PlusValueType.Time;
            if (IsSynergySkill) return valueSetters[_type];

            if (IsContainLevelUpType(_type))
            {
                if (player.playerStat.TotalTime == 0) return valueSetters[_type];
                return valueSetters[_type] * player.playerStat.TotalTime;
            }
            else
            {
                return valueSetters[_type];
            }
        }
    }

    public virtual float CurIntervelCount
    {
        get
        {

            if (valueSetters.TryGetValue(PlusValueType.IntervelCount, out float _value))
            {
                return _value;
            }
            return -1;
        }
    }

    #endregion

    #endregion
    /// <summary>
    /// 버프,디버프용
    /// </summary>
    public virtual void SkillEffect()
    {

    }
    /// <summary>
    /// 스킬업그레이드할때 호출
    /// </summary>
    public virtual void SpecialLevelEffect()
    {

    }
    public List<int> GetLevelUpType()
    {
        return LevelupType[Level];
    }
    public List<int> GetUpgradeType()
    {
        return LevelupType[LevelupType.Count];
    }
    public bool IsDebuff()
    {
        if (SkillData.statsType >= (SkillValueType)23) return true;
        return false;
    }
/// <summary>
/// 스킬레벨업내용
/// </summary>
/// <param name="_enum"></param>
/// <returns></returns>
public string LevelUpText(PlusValueType type)
    {
        if (IsSynergySkill) return string.Empty;

        string text = null;

        switch (type)
        {
            case PlusValueType.Damage:
                {
                    float inc = valueIncrements[type]();
                    float percent;

                    if (SkillData.statsType == SkillValueType.damage)
                    {
                        percent = MathF.Abs(MathF.Round(inc * 100));
                        text = LanguageSingleton.LangInstance.FormatString($"LevelUp_{type}", percent);
                    }
                    else
                    {
                        percent = MathF.Abs(MathF.Round((1 - inc) * 100));
                        text = LanguageSingleton.LangInstance.FormatString($"LevelUp_{SkillData.statsType}", percent);
                    }
                }
                break;

            case PlusValueType.AttackSize:
            case PlusValueType.CoolTime:
            case PlusValueType.Time:
                {
                    float inc = valueIncrements[type]();
                    float percent = MathF.Abs(MathF.Round(inc * 100));
                    text = LanguageSingleton.LangInstance.FormatString($"LevelUp_{type}", percent);
                }
                break;

            case PlusValueType.IntervelCount:
                {
                    float inc = valueIncrements[type]();
                    if (SkillBehavior is HomingSkillBehavior)
                    {
                        float value = MathF.Abs(MathF.Round(inc));
                        text = LanguageSingleton.LangInstance.FormatString($"LevelUp_{type}_Hit", value);
                    }
                    else
                    {
                        float percent = MathF.Abs(MathF.Round(inc * 100));
                        text = LanguageSingleton.LangInstance.FormatString($"LevelUp_{type}", percent);
                    }
                }
                break;

            case PlusValueType.ProjectileCout:
                text = LanguageSingleton.LangInstance.GetData($"LevelUp_{type}");
                break;
        }

        return text;
    }

    /// <summary>
    /// 스킬강화텍스트
    /// </summary>
    /// <param name="_enum"></param>
    /// <returns></returns>
    public string UpgradeText(PlusValueType _enum)
    {
        if (IsSynergySkill) return string.Empty;

        return LanguageSingleton.LangInstance.GetData("UpgradeText_" + SkillKey);
    }
    public void SetSkill(string _name, string _skillkey)
    {
        Manager = SkillManager_WS.instance;
        player = Manager.player;

        InitializeDefaultMappings();
        ConfigureValueMappings();

        Name = _name;
        SkillKey = _skillkey;

        var _manager = GameDataManager.Instance.GetComponent<SkillUpgradeData>();
        var _data = _manager.skillUpgradeIndex[SkillData.dynamicId];
        for (int i = 0; i < _data.Count; i++)
        {
            LevelupType[i + 1] = _data[i];
            for (int y = 0; y < _data[i].Count; y++)
            {
                if (!LevelupTypeIndex.Contains(_data[i][y]))
                {
                    LevelupTypeIndex.Add(_data[i][y]);
                }
            }
        }

        if (_manager.skillUpgradePassive.TryGetValue(SkillData.dynamicId, out var passive))
            UpgradePassive = passive;
        else
            UpgradePassive = new List<string>();

        if (!IsSynergySkill)
        {
            if (GameManager.gInstance != null)
            {
                SkillName = LanguageSingleton.LangInstance.GetData(_skillkey);
                SkillIcon = APIData.Instance.AddressableSprite[_name];
                UpgradeSkillIcon = APIData.Instance.AddressableSprite[_name + "_ev"];
            }
        }

        speed = 10;
    }
    /// <summary>
    /// 시너지 스킬 정보입력
    /// </summary>
    /// <param name="damage"></param>
    /// <param name="size"></param>
    /// <param name="coolTime"></param>
    /// <param name="duration"></param>
    public void SetSynergyValues(float damage, float duration, float coolTime)
    {
        Manager = SkillManager_WS.instance;
        player = Manager.player;
        IsSynergySkill = true;
        valueSetters[PlusValueType.Damage] = damage;
        valueSetters[PlusValueType.CoolTime] = coolTime;
        valueSetters[PlusValueType.Time] = duration;

        valueSetters[PlusValueType.AttackSize] = 1;
        valueSetters[PlusValueType.ProjectileCout] = 1;
        valueSetters[PlusValueType.IntervelCount] = 1;
    }
    public void UseSkill()
    {
        SkillAudioPlayer.PlayCast(this);
        Manager.UseSynergySkill(ElementalType);
        SkillEffect();
        if (SkillBehavior != null)
        {
            Manager.StartCoroutine(FireWithDelay());
        }
    }
    public void UseSynergySkill()
    {
        if (IsUseSkill) return;

        // 현재 풀에서 이미 사용 중이라면 다시 사용하지 않음
        if (CurSkill.HasActiveObject()) return;

        SkillAudioPlayer.PlaySynergy(SynergyType);
        SkillEffect();
        Manager.StartCoroutine(FireWithDelay());
        Manager.StartCoroutine(SkillyCool());
    }
    private IEnumerator SkillyCool()
    {
        IsUseSkill = true;
        float timer = 0f;
        float duration = CurCoolTime;

        while (timer < duration)
        {
            if (!PauseManager.freeze)
            {
                timer += Time.deltaTime;
            }
            yield return null;
        }

        IsUseSkill = false;
    }
    private IEnumerator FireWithDelay()
    {
        if (SkillBehavior is IStandaloneSkillBehavior)
        {
            Transform target = player.enemyTracker != null ? player.enemyTracker.FindClosestEnemy() : null;
            SkillBehavior.ExecuteSkill(this, player.transform, target, speed);
            yield break;
        }

        if (SkillBehavior is OrbitingSkillBehavior)
        {
            SkillBehavior.ExecuteSkill(this, player.transform, null, speed);
            yield break;
        }

        int projectileCount = (int)CurProjectileCount;
        for (int i = 0; i < projectileCount; i++)
        {
            var enemy = player.enemyTracker.FindClosestEnemy();
            Transform target = enemy != null ? enemy : null;

            SkillBehavior.ExecuteSkill(this, player.transform, target, speed);

            if (SkillBehavior is HomingSkillBehavior)
            {
                if (SkillDelay > 0.1f)
                    yield return new WaitForSeconds(SkillDelay);
                //최솟값
                else
                    yield return new WaitForSeconds(0.1f);
            }
            else
            {
                if (SkillDelay > 0)
                    yield return new WaitForSeconds(SkillDelay);
                else
                    yield return new WaitForEndOfFrame();
            }
        }
    }
    bool IsPlusValueType(PlusValueType _type)
    {
        if (SkillBehavior is HomingSkillBehavior)
        {
            if (_type == PlusValueType.IntervelCount)
                return true;
        }
        if (_type == PlusValueType.ProjectileCout)
            return true;
        return false;
    }
    public bool IsContainLevelUpType(PlusValueType _type)
    {
        return LevelupTypeIndex.Contains((int)_type);
    }
    public bool CanUpgradeSkill()
    {
        if (IsSynergySkill)
            return false;

        if (Level != LevelupType.Count)
            return false;

        return PassiveManager.instance.HasAllPassives(UpgradePassive);
    }
    public virtual IEnumerable<string> GetSubEffectKeys()
    {
        yield break;
    }

    public virtual void OnHitSpawnSubEffects(HitCollider hit, Collider2D other, Vector3 hitPos)
    {
    }
}
