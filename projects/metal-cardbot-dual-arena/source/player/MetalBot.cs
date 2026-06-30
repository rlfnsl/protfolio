using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Funtion_CWS;
public class MetalBot : MonoBehaviour
{
    public Dictionary<string, GameObject> switchbots;
    //스킬 밸류값.
    private float skillValue;
    public Animator botani;
    public string Type;
    public string BotName;

    public Transform AttackPos;
    public Transform[] HitPos;
    public Transform HealthPos;
    public Transform downPos;
    public Transform StunPos;
    public Transform FinalAttackPos;
    public Transform RecoveryShootPos;
    public Transform RecoveryPos;
    public Transform BoundingFollowerPos;
    public Transform PowerUpEffect;
    public Transform EffectSize;
    public Transform WeaponEffect;
    public Transform DefencePos;

    public ParticleSystem BackMovePos;
    public ParticleSystem FrontMovePos;
    public ParticleSystem LastAttackParticle;
    public ParticleSystem[] StageInEffect;
    public ParticleSystem SmogEffect;
    public ParticleSystem[] Smogs;
    public Transform ElectricEffect;

    public ParticleSystem TakeDamageCriticalParticle;
    public ParticleSystem TakeDamageDefaultParticle;
    //가지고있는 투사체.
    public ParticleSystem projectile;
    public ParticleSystem finalAttackBeam;
    public ParticleSystem defenceparticle;

    private float currenthealth = 0;
    private float MaxHealth = 0;

    //근거리 방어력
    private int DefaultDefenceValue;
    //원거리 방어력
    private int CriticalDefenceValue;

    //PlayerInfo
    private List<CardData> cardSKill;
    private int deathblow;

    //플레이어가 그로기값을 가지고 적에게 피해를줌.
    //플레이어는 카드 값의 개수를 받아오지만
    //몬스터는 플레이어의 카드값을 받아오며 누적함.
    private int groggy;
    public float SumAttackDamage;

    public Vector3 shieldSize;

    //몬스터의 스턴 턴 갯수.
    private int totalGroggy = 50;
    private int currentGroggy;
    private int groggyCount = 2;
    private int currentDeathBlow;

    //MonsterInfo
    private Monster Enemy;
    public bool groggyState = false;

    public float currentState = 0;

    //봇 사운드
    public MetalBotSound botSound;
    public int Totalgroggy
    {
        get
        {
            return totalGroggy;
        }

    }

    public void Awake()
    {
        if (Type == "Enemy")
        {
            AttackPos = transform.GetChild(0).transform;
            HitPos = new Transform[5];           
            botSound = GetComponent<MetalBotSound>();
        }
        else
        {
            HitPos = new Transform[5];
        }
        cardSKill = new List<CardData>();
        switchbots = new Dictionary<string, GameObject>();
    }

    //메탈봇 세팅
    public void SetMetalbots(string _str)
    {
        botani = switchbots[_str].GetComponent<Animator>();
        botSound = switchbots[_str].GetComponent<MetalBotSound>();
        BotName = switchbots[_str].name;
        AttackPos = switchbots[_str].transform.Find("Pos");
        HealthPos = switchbots[_str].transform.Find("Health");
        HitPos[0] = switchbots[_str].transform.GetChild(2).transform;
        HitPos[1] = switchbots[_str].transform.GetChild(3).transform;
        HitPos[2] = switchbots[_str].transform.GetChild(4).transform;
        HitPos[3] = switchbots[_str].transform.GetChild(5).transform;
        HitPos[4] = switchbots[_str].transform.GetChild(6).transform;
        downPos = switchbots[_str].transform.Find("downPos");
        StunPos = switchbots[_str].transform.Find("stunPos");
        FinalAttackPos = switchbots[_str].transform.Find("FinalAttackPos");
        RecoveryShootPos = switchbots[_str].transform.Find("RecoveryShootPos");
        RecoveryPos = switchbots[_str].transform.Find("RecoveryPos");
        BoundingFollowerPos = switchbots[_str].transform.Find("BoundingBoxFollower");
        WeaponEffect = BoundingFollowerPos.transform.Find("WeaponEffect");
        WeaponEffect.gameObject.SetActive(false);
        shieldSize = switchbots[_str].transform.Find("ShieldSize").transform.localScale;
        SmogEffect = switchbots[_str].transform.Find("Smog").GetComponent<ParticleSystem>();
        DefencePos = switchbots[_str].transform.Find("DefencePos").transform;
        SmogEffect.gameObject.SetActive(false);
        SmogEffect.Stop();
        ElectricEffect = SmogEffect.transform.Find("BoneFollower");

        Smogs = new ParticleSystem[2];
       
            Smogs[0] = SmogEffect.transform.Find("BackSmoke").GetComponent<ParticleSystem>();
            Smogs[1] = SmogEffect.transform.Find("FrontSmoke").GetComponent<ParticleSystem>();
        

        if (BoundingFollowerPos.transform.Find("PowerUpEffect") != null)
        {
            PowerUpEffect = BoundingFollowerPos.transform.Find("PowerUpEffect");
            PowerUpEffect.gameObject.SetActive(false);

            EffectSize = PowerUpEffect.transform.Find("EffectSizer");

            FinalAttackPos = BoundingFollowerPos.transform.Find("FinalAttackPos");
        }
        else
        {
            PowerUpEffect = null;
        }

        StageInEffect = switchbots[_str].GetComponentsInChildren<ParticleSystem>()
           .Where(particleSystem => particleSystem.gameObject.name.Contains("StageInEffect_"))
           .ToArray();
    }

    public void Simulate(bool _isStop)
    {
        for(int i = 0; i < Smogs.Length; ++i)
        {
            if(_isStop)
            {
                Smogs[i].Stop();
            }
            else
            {
                Smogs[i].Play();
            }
           
        }
    }

    public void OnEffect()
    {
        FrontMovePos.gameObject.SetActive(true);
        BackMovePos.gameObject.SetActive(true);
    }

    //디펜스 연출
    public void SetDefencePos()
    {
        defenceparticle.Play();
        defenceparticle.GetComponent<Transform>().position = HitPos[0].position;
    }

    //가한 대미지.
    public float TakeDamage(int _damage)
    {
        //BattleManager에서 받아오기 대미지 계산한 값.
        currenthealth -= _damage;
    
        if (currenthealth <= 0)
            currenthealth = 0;

        Setting();

        return currenthealth;
    }

    IEnumerator WaitIdle()
    {
       yield return botani.AnimationEnd("Idle");

    }

    public void Setting()
    {
        if (currenthealth <= MaxHealth * 0.25f)
        {
            SmogEffect.gameObject.SetActive(true);
            SmogEffect.Play();

            if(groggyState)
            {
                //그로기
                currentState = 1.0f;
            }
            else
            {
                //타이어드
                currentState = 0.5f;        
            }

            StartCoroutine(BlendTree(botani, currentState, 0.25f));
        }
        else
        {
            SmogEffect.gameObject.SetActive(false);

            if (!groggyState)
            { 
                //아이들
                currentState = 0;

                StartCoroutine(BlendTree(botani, currentState, 0.25f));
            }
        }
    }

    public IEnumerator BlendTree(Animator ani, float targetValue, float timeToMove)
    {
        float startValue = ani.GetFloat("Blend");
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / timeToMove;
            float currentValue = Mathf.Lerp(startValue, targetValue, t);
            ani.SetFloat("Blend", currentValue);
            yield return null;
        }

        // Ensure that the final value is set precisely
        ani.SetFloat("Blend", targetValue);
    }

    //가한 그로기
    public void TakeGroggy(int _groggy)
    {
        currentGroggy -= _groggy;

        if(currentGroggy <= 0)
        {
            groggyState = true;
            currentState = 1.0f;
            StartCoroutine(BlendTree(botani, currentState, 0.25f));
        }

    }

    //처음에 카드 세팅
    public void SetCardData(List<CardData> _card)
    {
        cardSKill = _card;
        SetData("Player");
    }

    //몬스터 카드 세팅.
    public void EnemyData(Monster _card)
    {
        Enemy = _card;
        groggyCount = DataManager.Instance.Commons.battle_groggy_turn;
        SetData("Enemy");
    }

    //카드 밸류 세팅
    public void SetData(string _str)
    {
        if (_str.Equals("Player"))
        {
            for (int i = 0; i < cardSKill.Count; ++i)
            {
                currenthealth += cardSKill[i].LastHp;
                SumAttackDamage += cardSKill[i].LastValue;
            }
        }
        else
        {
            currenthealth = Enemy.hp;
            skillValue = Enemy.atk;
            totalGroggy = Enemy.groggygauge;
            DefaultDefenceValue = Enemy.def_melee;
            CriticalDefenceValue = Enemy.def_range;
            currentGroggy = totalGroggy;
        }

        MaxHealth = currenthealth;
    }

    //플레이어 밸류 세팅
    public void SetPlayerData(int _index)
    {
        skillValue = cardSKill[_index].LastValue;
        groggy = cardSKill[_index].LastGroggy;
        deathblow = cardSKill[_index].LastDatthBlow;
        currentDeathBlow += deathblow;
    }

    //임시 무적 코드
    public void SetEnemyGroggy(int _groggy)
    {
        totalGroggy = _groggy;
        currentGroggy = _groggy;
        currenthealth = 999999;
    }

    public void SetPlayer(int _health)
    {
        MaxHealth = _health;
        currenthealth = _health;
    }

    //메탈봇 스위칭.
    public void SetSwitchCharacter(GameObject _ani)
    {
        switchbots.Add(_ani.name, _ani);
        switchbots[_ani.name].transform.localPosition = new Vector3(-7, 0, 0);
    }

    //UI Fillamount
    public float HealthAmount()
    {
        float amount = ((float)currenthealth / (float)MaxHealth);
        return amount;
    }

    //적 그로기 카운팅
    public void SetGroggyCount(int count)
    {
        groggyCount -= count;
    
        if(groggyCount == -1)
        {
            currentGroggy = totalGroggy;
            groggyState = false;
        }
        else
        {
            StartCoroutine(BlendTree(botani, currentState, 0.25f));
        }
    }

    //체력 회복 스킬.
    public float RecoverySkill(float _amount)
    {
        float recovery = skillValue * _amount;

        if (MaxHealth <= currenthealth + recovery)
        {
            currenthealth = MaxHealth;
        }
        else
        {
            currenthealth += (int)recovery;
        }

        if(currenthealth >= MaxHealth * 0.25f)
        {
            SmogEffect.gameObject.SetActive(false);
            if (!groggyState)
                botani.SetFloat("Blend", 0);
        }

        BattleUIManager.instance.SetTextDamage((int)recovery, HealthPos.position , Color.green);

        return currenthealth;
    }

    //처음 한번만 
    public float GetMaxHealth()
    {
        return MaxHealth;
    }
    //처음 한번만.
    public float GetHealth()
    {
        return currenthealth;
    }

    public int GetDeathBlow()
    {
        return deathblow;
    }

    public int GetCurrentDeathBlow()
    {
        return currentDeathBlow;
    }

    public int GetCurrentGroggyCount()
    {
        return groggyCount;
    }

    public float GetSkillValue()
    {
        return skillValue;
    }

    public int GetGroggy()
    {
        return groggy;
    }

    public int GetShortDefence()
    {
        return DefaultDefenceValue;
    }

    public int GetLongDefence()
    {
        return CriticalDefenceValue;
    }

    public float GetSumAtkDamage()
    {
        return SumAttackDamage;
    }

    public void InitGroggy()
    {
        groggy = 0;
        groggyCount = DataManager.Instance.Commons.battle_groggy_turn;
        groggyState = false;
        currentGroggy = totalGroggy;
        BattleUIManager.instance.InitGroggy();
    }

    public void InItDeathBlow()
    {
        currentDeathBlow = 0;
        BattleUIManager.instance.InitDeathBlow();
    }

    public List<CardData> GetCardData()
    {
        return cardSKill;
    }

    public void InitParameter()
    {
        AnimatorControllerParameter[] parameters = botani.parameters;

        // 각 파라미터를 루프를 돌며 끕니다.
        foreach (AnimatorControllerParameter parameter in parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
                botani.SetBool(parameter.name, false);
        }
    }

}
