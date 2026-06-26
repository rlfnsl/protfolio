# 내맘대로마법사

모바일 핵앤슬래시 Unity URP 프로젝트입니다. 아웃게임 대부분의 기능, Addressables 기반 리소스 로딩, 시트/서버 데이터 연동, 광고/상점, 스킬 구조와 일부 VFX 구현을 담당했습니다.

## 샘플 코드

- `RuntimeAssetProvider.cs`: Addressables 사용부를 한 곳으로 모으고 Resources fallback을 지원하는 런타임 에셋 로더
- `SheetDataController.cs`: 시트 버전 확인, 병렬 다운로드, 암호화 저장, 비동기 복호화
- `LoadingManager.cs`, `LoadingRuneEffects.cs`: 로딩 진행률과 룬 게이트 연출
- `SkillManager_WS_RuntimeLoad.cs`: 스킬 프리팹 런타임 로드와 풀 생성
- `SkillDamageResolver.cs`: 스킬 공통 데미지 계산
- `RuntimeSkillDamageUtility.cs`, `LightningTurretRuntime.cs`, `StoneBulletRuntime.cs`: 독립 런타임 스킬 처리 예시
