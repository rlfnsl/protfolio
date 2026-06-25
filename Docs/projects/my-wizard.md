# 내맘대로마법사

Mobile hack-and-slash project built with Unity URP.

## Role

Developer, planner, and team lead. Estimated contribution: about 70%.

## Main Responsibilities

- Lobby and most outgame features
- Shop, gacha, IAP, rewarded ads, resource purchase sounds and feedback
- Equipment inventory, equip/unequip, upgrade success/failure, trait upgrade
- Server data and Google Sheet data loading flow
- Addressables-based portfolio resource structure
- Firebase Remote Config version/update flow
- Unity editor tools for data import, runtime asset catalog, custom Android build
- Skill manager, skill interface, damage resolver, VFX, shader work
- Mobile profiling and stage optimization

## Runtime Flow

```mermaid
flowchart TD
    Launch["App Launch"] --> Loading["Loading Scene"]
    Loading --> Server["Server User Data"]
    Loading --> Sheet["Sheet Data"]
    Loading --> Resources["Addressables Runtime Assets"]
    Loading --> Audio["Audio Manager"]
    Server --> Lobby["Lobby"]
    Sheet --> Lobby
    Resources --> Lobby
    Lobby --> Outgame["Shop / Gacha / Inventory / Skill"]
    Lobby --> Stage["Stage"]
    Stage --> Spawn["Spawn / Pool"]
    Stage --> Skill["Skill Manager"]
    Skill --> Damage["Shared Damage Resolver"]
    Skill --> Hit["HitCollider / SkillHitRange"]
```

## Representative Code Samples

- `Samples/RuntimeAssets/RuntimeAssetProviderSample.cs`
- `Samples/Loading/LoadingProgressPresenterSample.cs`
- `Samples/Skills/SkillDamageResolverSample.cs`
- `Samples/Skills/LightningTurretSkillSample.cs`
- `Samples/Skills/StoneBulletSkillSample.cs`
- `Samples/Optimization/EnemySeparationNonAllocSample.cs`

