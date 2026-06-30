using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillBase
{
    protected int _id;
    public int ID { get => _id; }

    public float Range;
    public List<float> Value = new List<float>();
    protected ModuleBase _module;
    public string Affect = "";
    public string EffectName = "";
    public int EffectPoint = -1;
    public string[] Coefs;

    public virtual SkillEffect CreateEffect(Player player, string name, Vector3 pos, Vector3 forward, System.Action<NetworkBehaviour> action)
    {
        var skillEffect = InGameManager.Instance.EffectManager.CreateSkillEffect(player.Object.Id, name, pos, forward);

        if (skillEffect == null)
            return null;

        skillEffect.AddSkillActiveAction(action);

        return skillEffect;
    }
}
