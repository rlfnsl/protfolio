# 최우석 Unity 게임 클라이언트 포트폴리오

상용 프로젝트 전체 소스와 에셋은 공개하지 않고, 주요 프로젝트에서 담당했던 구조와 구현 일부를 민감정보 제거 후 정리한 저장소입니다.

## 문서

- [최우석_포트폴리오.docx](docs/최우석_포트폴리오.docx)
- [최우석_포트폴리오.pdf](docs/최우석_포트폴리오.pdf)

## 메인 프로젝트

| 프로젝트 | 플랫폼 | 역할 | 주요 내용 |
| --- | --- | --- | --- |
| [내맘대로마법사](projects/my-wizard) | Mobile / Unity URP | 개발, 기획, 팀장 | Addressables, 시트/서버 데이터, 상점/광고, 스킬 구조, VFX |
| [Dragon Arena](projects/dragon-arena) | Steam PC / Photon Fusion 2 | 메인 개발 | 매칭, 대기방, 네트워크 오브젝트 풀, 미니맵 Fog of War, AI 입력 |
| [RaidOne](projects/raid-one) | Steam PC / Photon Fusion 2 | 메인 개발 | 호스트 마이그레이션, 재접속 대기, 스냅샷 복원 |
| [메탈카드봇 듀얼아레나](projects/metal-cardbot-dual-arena) | Arcade / Unity | 메인 개발 | 시리얼 통신, RF 카드 인식, QR/카드 배출 하드웨어 연동 |
| [데몬스쿼드 키우기 글로벌](projects/demon-squad-global) | Mobile / LiveOps | 단독 유지보수/글로벌 대응 | GPM 웹뷰, 로컬라이징, 시즌/패스 이벤트, SDK 연동 |

## 링크

- GitHub: [rlfnsl](https://github.com/rlfnsl)
- 내맘대로마법사 플레이: [YouTube](https://youtu.be/0XKcMJXp39Y)
- 내맘대로마법사 가챠 연출: [YouTube](https://youtu.be/IwaR_6xQkjY)
- Dragon Arena: [Steam](https://store.steampowered.com/app/4371940/Dragon_Arena/) / [YouTube](https://youtu.be/okkGrxD1XE0)
- RaidOne: [Steam](https://store.steampowered.com/app/3896480/Raid_One__1_vs_5_Online_Boss_Battle/)
- 메탈카드봇 듀얼아레나: [Drive](https://drive.google.com/drive/folders/1TLnTK4uIL6UceQMtY69cZcjtKPVeOF7L)

## 코드 공개 기준

- 서버 주소, API URL, 키, 토큰, 패스워드, 빌드 인증 정보는 제거했습니다.
- 원본 전체 파일 대신 설명에 필요한 범위 위주로 발췌했습니다.
- 샘플 상단의 `Source`와 `Lines`는 로컬 원본 기준입니다.
