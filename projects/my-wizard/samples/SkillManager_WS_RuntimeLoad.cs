// Portfolio sample extracted from a commercial Unity project.
// Sensitive URLs/keys/tokens were redacted before publishing.
// Source: C:\Users\qlwns\ghost\Assets\Scripts\InGame\SKILL_WS\SkillManager_WS.cs
// Lines: 118-198, 200-220

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

// ...

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
