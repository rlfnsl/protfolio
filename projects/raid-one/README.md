# RaidOne

Steam PC용 1대5 보스 레이드 프로젝트입니다. Photon Fusion 2 기반 세션 유지, 호스트 마이그레이션, 재접속 대기와 스냅샷 복원 로직을 담당했습니다.

## 샘플 코드

- `PhotonManager_ClientReattach.cs`: Cloud connection lost 이후 클라이언트 재접속 대기와 로비 복귀 처리
- `PhotonManager_HostMigration.cs`: 기존 Runner 종료, 새 Runner 생성, HostMigrationResume 기반 오브젝트 복원

## 링크

- [Steam](https://store.steampowered.com/app/3896480/Raid_One__1_vs_5_Online_Boss_Battle/)
