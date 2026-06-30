# Dragon Arena

Steam PC 5v5 MOBA 프로젝트입니다. Photon Fusion 2 기반 매칭/대기방/Host Migration, 네트워크 오브젝트 풀, 플레이어/AI, 스킬, Steam 연동 코드를 모았습니다.

## Source Scripts

### Networking

- [`source/networking/PhotonManager.cs`](source/networking/PhotonManager.cs): 매칭, 방 생성/참가, StartGame, Host Migration, 재접속 흐름을 포함한 Photon Fusion 매니저
- [`source/networking/PooledNetworkObjectProvider.cs`](source/networking/PooledNetworkObjectProvider.cs): Photon Fusion 네트워크 오브젝트 풀링

### Player / UI

- [`source/player/Player_Hero.cs`](source/player/Player_Hero.cs): 플레이어 조작, 공격, 패링, 방어, 이동, 스킬 연동
- [`source/player/Player_AI.cs`](source/player/Player_AI.cs): AI 플레이어 입력과 업그레이드 루틴
- [`source/ui/MinimapFogOfWar.cs`](source/ui/MinimapFogOfWar.cs): 캐릭터 주변만 밝히는 미니맵 Fog of War

### Skill System

- [`source/skills/SkillManager.cs`](source/skills/SkillManager.cs): 스킬 생성/관리와 스킬 실행 흐름
- [`source/skills/SkillBase.cs`](source/skills/SkillBase.cs): 스킬 공통 베이스

### Platform / Steam

- [`source/platform/SteamLoginManager.cs`](source/platform/SteamLoginManager.cs): Steam API 초기화와 로그인 확인
- [`source/platform/SteamIAPManager.cs`](source/platform/SteamIAPManager.cs): Steam MicroTxn 구매 승인 흐름

### Editor / Build Tools

- [`source/editor/BuildTool.cs`](source/editor/BuildTool.cs): 개발/라이브 빌드 설정 툴
- [`source/editor/SteamBuild.cs`](source/editor/SteamBuild.cs): Steam 빌드와 app id 파일 갱신
- [`source/editor/StructFromCSVGenerator.cs`](source/editor/StructFromCSVGenerator.cs): Google Sheet TSV 기반 구조체/바이너리 생성 툴
- [`source/editor/AutoAssignDrawer.cs`](source/editor/AutoAssignDrawer.cs): SerializeField 자동 할당 Editor Drawer

## 링크

- [Steam](https://store.steampowered.com/app/4371940/Dragon_Arena/)
- [Gameplay Video](https://youtu.be/okkGrxD1XE0)
