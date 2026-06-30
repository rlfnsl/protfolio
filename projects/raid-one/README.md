# RaidOne

Steam PC 1 vs 5 온라인 보스 레이드 프로젝트입니다. Photon Fusion 2 세션 유지, Host Migration, 재접속 대기, 오류 복원, 히어로 전투 흐름 코드를 모았습니다.

## Source Scripts

### Networking

- [`source/networking/PhotonManager.cs`](source/networking/PhotonManager.cs): 세션 생성/참가, Cloud connection lost 대응, 클라이언트 재접속 대기, Host Migration, 오류 복원

### Player / Gameplay

- [`source/player/Player_Hero.cs`](source/player/Player_Hero.cs): 히어로 이동, 공격, 피격, 부활, 자동 업그레이드, 스킬 연동
- [`source/gameplay/InGameManager.cs`](source/gameplay/InGameManager.cs): 인게임 전체 진행, 상태 전환, 승패 흐름
- [`source/ui/MinimapManager.cs`](source/ui/MinimapManager.cs): 미니맵 표시와 플레이어/보스 위치 갱신

### Skill System

- [`source/skills/SkillManager.cs`](source/skills/SkillManager.cs): 스킬 생성/관리와 실행 흐름
- [`source/skills/SkillBase.cs`](source/skills/SkillBase.cs): 스킬 공통 베이스

### Platform / Editor

- [`source/platform/SteamLoginManager.cs`](source/platform/SteamLoginManager.cs): Steam API 초기화와 로그인 확인
- [`source/platform/SteamIAPManager.cs`](source/platform/SteamIAPManager.cs): Steam MicroTxn 구매 승인 흐름
- [`source/editor/BuildTool.cs`](source/editor/BuildTool.cs): 개발/라이브 빌드 설정 툴
- [`source/editor/SteamBuild.cs`](source/editor/SteamBuild.cs): Steam 라이브/데모 빌드 툴
- [`source/editor/AutoAssignDrawer.cs`](source/editor/AutoAssignDrawer.cs): SerializeField 자동 할당 Editor Drawer

## 링크

- [Steam](https://store.steampowered.com/app/3896480/Raid_One__1_vs_5_Online_Boss_Battle/)
