# 메탈카드봇 듀얼아레나

아케이드 하드웨어 연동 Unity 프로젝트입니다. 현재 저장소에는 클라이언트/하드웨어 연동 담당 범위에서 공개 가능한 시리얼 통신, RF 카드 인식, QR/카드 배출, 데이터 로딩, 운영자 모드, 로컬라이징 스크립트만 정리했습니다.

## Source Scripts

### Hardware

- [`source/hardware/SerialPortManager.cs`](source/hardware/SerialPortManager.cs): IO 보드 초기화, XML 기반 포트 설정, read/write 버퍼, 카드 배출/입력 이벤트 처리
- [`source/hardware/SerialPortUnit.cs`](source/hardware/SerialPortUnit.cs): 시리얼 포트 단위 연결/수신/전송 처리
- [`source/hardware/RFCard.cs`](source/hardware/RFCard.cs): ISO15693 RF 카드 태그 인벤토리와 UID 연결 흐름
- [`source/hardware/CardoutQRChecker.cs`](source/hardware/CardoutQRChecker.cs): 카드 배출/QR 체크 연동

### Data / Mode / Player

- [`source/data/DataManager.cs`](source/data/DataManager.cs): Google Sheet TSV 기반 게임 데이터 로딩
- [`source/data/LegendManager.cs`](source/data/LegendManager.cs): 레전드 데이터 관리
- [`source/mode/ModeManager.cs`](source/mode/ModeManager.cs): 게임 모드 상태 전환
- [`source/player/MetalBot.cs`](source/player/MetalBot.cs): 메탈봇 캐릭터 상태와 연출
- [`source/player/MetalBotManager.cs`](source/player/MetalBotManager.cs): 메탈봇 생성/관리
- [`source/player/PlayerInfoManager.cs`](source/player/PlayerInfoManager.cs): 플레이어 정보 표시와 갱신

### UI / Localization / Admin

- [`source/ui/BattleUIManager.cs`](source/ui/BattleUIManager.cs): 배틀 UI 상태 갱신
- [`source/ui/CardSetting.cs`](source/ui/CardSetting.cs): 카드 UI와 배틀 카드 세팅
- [`source/localization/LanguageSingleton.cs`](source/localization/LanguageSingleton.cs): 언어 데이터 로딩
- [`source/localization/LangText.cs`](source/localization/LangText.cs): 텍스트 로컬라이징 컴포넌트
- [`source/admin/AdminManager.cs`](source/admin/AdminManager.cs): 운영자 모드와 점검/설정 UI
- [`source/intro/IntroManager.cs`](source/intro/IntroManager.cs): 인트로 흐름 제어
- [`source/editor/CustomBuild.cs`](source/editor/CustomBuild.cs): 커스텀 빌드 자동화

## 링크

- [Drive](https://drive.google.com/drive/folders/1TLnTK4uIL6UceQMtY69cZcjtKPVeOF7L)
