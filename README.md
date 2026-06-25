# Wooseok Choi - Game Client Portfolio

Unity/C# game client portfolio repository.

This repository is a public-safe architecture and sample-code hub. Commercial Unity projects cannot be published as full source, so the code here is rewritten to show the same design intent without exposing private assets, server URLs, paid SDK keys, or production content.

## Links

- GitHub Profile: https://github.com/rlfnsl
- Notion Portfolio: https://app.notion.com/p/0421d1336f4f4636b66fabd898b1761c?source=copy_link
- PDF/PPT Portfolio: provided separately

## Focus Areas

- Mobile live-service client architecture
- Runtime asset loading with Addressables abstraction
- Loading progress and startup pipeline design
- Skill manager, shared damage resolving, hit processing
- Mobile optimization with NonAlloc physics patterns
- Photon Fusion 2 multiplayer session and migration concepts
- Steam multiplayer lobby/room flow
- Arcade hardware integration with serial, RF card, and QR flow
- Unity editor tooling for data import and custom builds

## Main Projects

| Project | Platform | Role | Public Documentation |
| --- | --- | --- | --- |
| 내맘대로마법사 | Mobile / Unity URP / Hack & Slash | Developer, Planner, Team Lead / about 70% | [Docs/projects/my-wizard.md](Docs/projects/my-wizard.md) |
| Dragon Arena | Steam PC / 5v5 MOBA / Photon Fusion 2 | Main Developer / about 60% | [Docs/projects/dragon-arena.md](Docs/projects/dragon-arena.md) |
| RaidOne | Steam PC / 1v5 Boss Raid / Photon Fusion 2 | Main Developer / about 60% | [Docs/projects/raidone.md](Docs/projects/raidone.md) |
| Demon Squad Global | Mobile Idle RPG / Global LiveOps | Solo Maintenance and Global Response | [Docs/projects/demon-squad-global.md](Docs/projects/demon-squad-global.md) |
| Metal Cardbot Dual Arena | Arcade Hardware Game | Main Developer / about 60% | [Docs/projects/metal-cardbot-dual-arena.md](Docs/projects/metal-cardbot-dual-arena.md) |

## Repository Structure

```text
Docs/
  architecture-overview.md
  projects/
Samples/
  RuntimeAssets/
  Loading/
  Skills/
  Optimization/
  EditorTools/
  Networking/
  Hardware/
  LiveOps/
```

## Sample Code

The samples are intentionally small and focused. They are not copy-pasted production files.

- `RuntimeAssets`: Addressables/Resources abstraction idea
- `Loading`: smooth loading progress presenter
- `Skills`: skill damage resolver, hit utility, stone bullet, lightning turret
- `Optimization`: NonAlloc enemy separation pattern
- `EditorTools`: Android custom build pipeline sample
- `Networking`: Photon-style session recovery flow sample
- `Hardware`: arcade card/QR/serial state flow sample
- `LiveOps`: daily rewarded-ad reward grant flow sample

## Notes

This repository is designed to be read together with the Notion and PPT/PDF portfolio. The PPT/PDF gives a fast overview, Notion contains project stories and video slots, and this repository provides code-level credibility.

