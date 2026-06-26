# Dragon Arena

Steam PC용 5v5 MOBA 프로젝트입니다. RaidOne 코드베이스를 확장해 Photon Fusion 2 기반 매칭/대기방/전투 흐름과 미니맵, AI, 네트워크 오브젝트 처리 일부를 담당했습니다.

## 샘플 코드

- `PhotonManager_MatchmakingAndStart.cs`: 점수/번들 버전 기반 세션 필터링과 StartGame 설정
- `PooledNetworkObjectProvider.cs`: Photon Fusion 네트워크 오브젝트 풀
- `MinimapFogOfWar.cs`: 캐릭터 주변만 밝혀지는 미니맵 Fog of War
- `Player_AI_Input.cs`: AI 플레이어 입력 생성
- `Player_Hero_Damage.cs`: 피격, 방어, 패링, 실드 차감 흐름

## 링크

- [Steam](https://store.steampowered.com/app/4371940/Dragon_Arena/)
- [Gameplay Video](https://youtu.be/okkGrxD1XE0)
