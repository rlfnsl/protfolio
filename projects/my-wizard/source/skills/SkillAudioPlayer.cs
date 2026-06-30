using System.Collections.Generic;
using UnityEngine;

public static class SkillAudioPlayer
{
    const string SfxBasePath = "Audio/SFX/";
    const float CastThrottle = 0.06f;
    const float HitThrottle = 0.07f;
    const float SynergyThrottle = 0.16f;

    static readonly Dictionary<string, float> lastPlayTimes = new();

    public static void PlayCast(SkillInterFace skill)
    {
        if (skill == null)
            return;

        if (skill.IsSynergySkill)
        {
            PlaySynergy(skill.SynergyType);
            return;
        }

        string path = skill.GetCastSfxPath();
        if (string.IsNullOrEmpty(path))
            return;

        PlayThrottled(path, skill.IsUpgrade ? 0.84f : 0.76f, CastThrottle);
    }

    public static void PlaySynergy(SpiritSynergy synergy)
    {
        string path = synergy switch
        {
            SpiritSynergy.Steam => SfxBasePath + "synergy_steam",
            SpiritSynergy.Discharge => SfxBasePath + "synergy_discharge",
            SpiritSynergy.Overheat => SfxBasePath + "synergy_overheat",
            SpiritSynergy.Conduction => SfxBasePath + "synergy_conduction",
            SpiritSynergy.Swamp => SfxBasePath + "synergy_swamp",
            SpiritSynergy.Magnetism => SfxBasePath + "synergy_magnetism",
            _ => null
        };

        if (!string.IsNullOrEmpty(path))
            PlayThrottled(path, 0.84f, SynergyThrottle);
    }

    public static void PlayHit(SkillInterFace skill)
    {
        PlayElementHit(skill != null ? skill.ElementalType : ElementType.None);
    }

    public static void PlayElementHit(ElementType element)
    {
        string path = GetHitPath(element);
        if (string.IsNullOrEmpty(path))
            return;

        PlayThrottled(path, 0.36f, HitThrottle);
    }

    public static void PlayLightningTurretSpawn(SkillInterFace skill)
    {
        PlaySkillSfx(skill, SkillInterFace.SfxKeyLightningTurretSpawn, SfxBasePath + "skill_lightning_turret_spawn", 0.78f, 0.05f);
    }

    public static void PlayLightningTurretAttack(SkillInterFace skill, bool upgraded)
    {
        PlaySkillSfx(skill, SkillInterFace.SfxKeyLightningTurretAttack, SfxBasePath + "skill_lightning_turret_attack", upgraded ? 0.9f : 0.78f, 0.04f);
    }

    public static void PlayLightningTurretExplode(SkillInterFace skill)
    {
        PlaySkillSfx(skill, SkillInterFace.SfxKeyLightningTurretExplode, SfxBasePath + "skill_lightning_turret_explode", 0.82f, 0.05f);
    }

    public static void PlayStoneBulletImpact(SkillInterFace skill, bool upgraded)
    {
        PlaySkillSfx(skill, SkillInterFace.SfxKeyStoneBulletImpact, SfxBasePath + "skill_stone_bullet_impact", upgraded ? 0.72f : 0.62f, 0.045f);
    }

    static void PlaySkillSfx(SkillInterFace skill, string key, string fallbackPath, float volume, float minInterval)
    {
        string path = skill != null ? skill.GetSkillSfxPath(key, fallbackPath) : fallbackPath;
        PlayThrottled(path, volume, minInterval);
    }

    static string GetHitPath(ElementType element)
    {
        return element switch
        {
            ElementType.fire => SfxBasePath + "element_hit_fire",
            ElementType.water => SfxBasePath + "element_hit_water",
            ElementType.electric => SfxBasePath + "element_hit_electric",
            ElementType.land => SfxBasePath + "element_hit_land",
            _ => null
        };
    }

    static void PlayThrottled(string resourcePath, float volume, float minInterval)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return;

        float now = Time.unscaledTime;
        if (lastPlayTimes.TryGetValue(resourcePath, out float lastTime) && now - lastTime < minInterval)
            return;

        lastPlayTimes[resourcePath] = now;
        GameAudioManager.Ensure(GameManager.gInstance != null ? GameManager.gInstance.masterMixer : null).PlaySfxResource(resourcePath, volume);
    }

}
