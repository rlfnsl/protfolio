# 데몬스쿼드 키우기 글로벌

기존 출시 프로젝트의 글로벌 대응과 라이브 운영 기능 추가를 단독으로 진행했습니다. GPM 웹뷰, 광고/SDK, 로컬라이징, 시즌/패스 이벤트, 최적화와 유지보수를 담당했습니다.

## 샘플 코드

- `SuperService_GpmWebView.cs`: GPM WebView 호출, 콜백, 화면 방향 복구
- `LocalizationService_Loading.cs`: 서버/로컬 CSV 기반 다국어 로드
- `PassEventPanel_Rewards.cs`: 패스 이벤트 보상 수령과 일괄 수령
- `PassEventSlot_Rewards.cs`: 보상 슬롯 UI 상태 갱신
