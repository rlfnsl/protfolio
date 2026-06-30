# 최우석 Unity 게임 클라이언트 포트폴리오

Unity/C# 기반 게임 클라이언트 포트폴리오입니다. 프로젝트별로 직접 다뤘던 클라이언트 코드를 기능 단위로 묶었습니다.

각 프로젝트의 코드는 `projects/<project>/source/` 아래에 배치했습니다. 운영 URL, Google Sheet 주소, 광고 ID, 암호화 키처럼 그대로 올리기 어려운 값은 비공개 값으로 바꿔 두었습니다.

## 프로젝트

| 프로젝트 | 플랫폼 | 역할 | 주요 코드 |
| --- | --- | --- | --- |
| [내맘대로마법사](projects/my-wizard) | Mobile / Unity URP | 클라이언트 구현 | Addressables, 암호화 데이터 로딩, 스킬/시너지, IAP/광고, Editor Tool |
| [Dragon Arena](projects/dragon-arena) | Steam PC / Photon Fusion 2 | 클라이언트 구현 | 매칭, Host Migration, 네트워크 오브젝트 풀, 플레이어 로직, 스킬, Steam 연동 |
| [RaidOne](projects/raid-one) | Steam PC / Photon Fusion 2 | 클라이언트 구현 | 1 vs 5 보스 레이드, 재접속 대기, Host Migration, 히어로 전투 |
| [메탈카드봇 듀얼아레나](projects/metal-cardbot-dual-arena) | Arcade / Unity | 클라이언트/하드웨어 연동 구현 | 시리얼 통신, RF 카드, QR/카드 배출, 데이터, 운영자 모드 |

## 코드 구성

- `projects/<project>/README.md`: 프로젝트별 코드 목록
- `projects/<project>/source/`: C# 스크립트
- `source/networking`: Photon Fusion, 매칭, Host Migration, 네트워크 오브젝트 처리
- `source/skills`, `source/gameplay`, `source/player`: 인게임 전투, 스킬, 플레이어, 투사체
- `source/liveops`, `source/platform`: 라이브 이벤트, IAP, 광고, SDK, 플랫폼 대응
- `source/data`, `source/runtime-assets`: 데이터 로딩, 암호화, Addressables/Resources 대응
- `source/hardware`: 시리얼 포트, RF 카드, QR/외부 장치 연동
- `source/editor`: 빌드 툴, 데이터 변환, 자동 할당, 리소스 내보내기

## 링크

- GitHub: [rlfnsl](https://github.com/rlfnsl)
- 내맘대로마법사 플레이: [YouTube](https://youtu.be/0XKcMJXp39Y)
- 내맘대로마법사 가챠 연출: [YouTube](https://youtu.be/IwaR_6xQkjY)
- Dragon Arena: [Steam](https://store.steampowered.com/app/4371940/Dragon_Arena/) / [YouTube](https://youtu.be/okkGrxD1XE0)
- RaidOne: [Steam](https://store.steampowered.com/app/3896480/Raid_One__1_vs_5_Online_Boss_Battle/)
- 메탈카드봇 듀얼아레나: [Drive](https://drive.google.com/drive/folders/1TLnTK4uIL6UceQMtY69cZcjtKPVeOF7L)
