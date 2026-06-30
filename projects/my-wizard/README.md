# 이멋대로 마법사

모바일 뱀서라이크 Unity URP 프로젝트입니다. 개발과 기획을 함께 맡아 Addressables/Resources 기반 런타임 에셋 로딩, Google Sheet 데이터 파이프라인, 암호화 저장, 스킬/시너지 구조, 가챠/IAP/광고, Editor 자동화를 구현했습니다.

## Source Scripts

### Data / Runtime Assets

- [`source/data/SheetDataController.cs`](source/data/SheetDataController.cs): 시트 버전 확인, 병렬 다운로드, 암호화 저장, 비동기 복호화
- [`source/data/AesCryptoUtil.cs`](source/data/AesCryptoUtil.cs): AES 기반 데이터 암호화/복호화
- [`source/data/EncryptionConfig.cs`](source/data/EncryptionConfig.cs): 암호화 키/IV 설정
- [`source/data/GameDataManager.cs`](source/data/GameDataManager.cs): 게임 데이터 테이블 로딩 관리
- [`source/data/GameDataTableBase.cs`](source/data/GameDataTableBase.cs): 데이터 테이블 공통 베이스
- [`source/runtime-assets/RuntimeAssetProvider.cs`](source/runtime-assets/RuntimeAssetProvider.cs): Addressables/Resources 런타임 로딩 브리지
- [`source/runtime-assets/AddressableManager.cs`](source/runtime-assets/AddressableManager.cs): Addressables 다운로드/로드 관리
- [`source/runtime-assets/RuntimeAssetCatalog.cs`](source/runtime-assets/RuntimeAssetCatalog.cs): 런타임 에셋 카탈로그

### Skills / Combat

- [`source/skills/SkillManager_WS.cs`](source/skills/SkillManager_WS.cs): 스킬 생성, 런타임 로드, 오브젝트 풀, 시너지 스킬 관리
- [`source/skills/SkillDamageResolver.cs`](source/skills/SkillDamageResolver.cs): 스킬 공통 대미지 계산
- [`source/skills/StandaloneRuntimeSkill.cs`](source/skills/StandaloneRuntimeSkill.cs): 런타임 스킬 대미지 처리와 비주얼/투사체형 스킬 처리
- [`source/skills/SkillInterFace.cs`](source/skills/SkillInterFace.cs): 스킬 공통 인터페이스와 기본 매핑
- [`source/skills/HitCollider.cs`](source/skills/HitCollider.cs): 스킬 히트 판정 콜라이더
- [`source/skills/ObjectPool_WS.cs`](source/skills/ObjectPool_WS.cs): 스킬 오브젝트 풀
- [`source/skills/SkillButtonManager.cs`](source/skills/SkillButtonManager.cs): 스킬 선택/강화 UI 버튼
- [`source/skills/SkillAudioPlayer.cs`](source/skills/SkillAudioPlayer.cs): 스킬 효과음 재생

### Synergy / Gameplay

- [`source/synergy/SynergyOverheat.cs`](source/synergy/SynergyOverheat.cs): 과열 시너지 스킬
- [`source/synergy/SynergySteam.cs`](source/synergy/SynergySteam.cs): 증기 시너지 스킬
- [`source/synergy/SynergyDischarge.cs`](source/synergy/SynergyDischarge.cs): 방전 시너지 스킬
- [`source/synergy/SynergyConduction.cs`](source/synergy/SynergyConduction.cs): 전도 시너지 스킬
- [`source/gameplay/PoolManager.cs`](source/gameplay/PoolManager.cs): 인게임 오브젝트 풀
- [`source/gameplay/DamageFormula.cs`](source/gameplay/DamageFormula.cs): 대미지 공식

### LiveOps / Monetization

- [`source/liveops/GachaManager.cs`](source/liveops/GachaManager.cs): 가챠 연출과 보상 처리
- [`source/liveops/IAPManager.cs`](source/liveops/IAPManager.cs): 인앱 결제 초기화, 영수증 처리, 구매 실패 대응
- [`source/liveops/AdmobManager.cs`](source/liveops/AdmobManager.cs): 보상형 광고 로드/표시/보상 콜백

### Editor / Build Tools

- [`source/editor/RuntimeAssetCatalogBuilder.cs`](source/editor/RuntimeAssetCatalogBuilder.cs): Addressables 카탈로그 생성
- [`source/editor/RuntimeAssetResourcesExporter.cs`](source/editor/RuntimeAssetResourcesExporter.cs): Addressables 리소스 내보내기
- [`source/editor/GoogleSheetToPersistentEncryptor.cs`](source/editor/GoogleSheetToPersistentEncryptor.cs): Google Sheet 전체 탭 암호화 저장 Editor 툴
- [`source/editor/CustomBuild.cs`](source/editor/CustomBuild.cs): Android 빌드 설정 자동화
- [`source/editor/CodexAndroidBuild.cs`](source/editor/CodexAndroidBuild.cs): CLI 기반 Android 빌드 보조
- [`source/editor/AutoAssignDrawer.cs`](source/editor/AutoAssignDrawer.cs): SerializeField 자동 할당 Editor Drawer

## 링크

- [플레이 영상](https://youtu.be/0XKcMJXp39Y)
- [가챠 연출](https://youtu.be/IwaR_6xQkjY)
