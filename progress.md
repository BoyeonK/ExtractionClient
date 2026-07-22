# 프로젝트 진행 상황

> 최종 수정: 2026-07-22
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### 플레이어/사격
- [x] (2026-07-13 #4) 발사 차단(fireBlocked) 메커니즘 — `_fireBlocked`/`_wasMousePressed`/`_wasUIOpen` 도입. 빈 탄창 시 block, UI 열림 전환 시 block, 마우스 재클릭(release→press) 시 해제. EmptyAmmoFire 반복 호출 방지 및 UI 닫힘 후 의도치 않은 즉시 발사 방지
- [x] (2026-07-13 #5) 스프레드 회복 로직 — `ProcessFire()`에서 매 프레임 `_currentSpread -= _spreadRecoveryRate * deltaTime`, `_spreadBase` 하한. `_spreadRecoveryRate`를 `EquipWeapon()`에서 캐싱
- [x] (2026-07-15 #0) 반동 보간(Smooth Recoil) 구현 — `Fire()`에서 즉시 적용하던 수직/수평 반동을 `_recoilTarget`에 목표만 누적, `ProcessRecoil()`에서 매 프레임 `Vector2.Lerp`로 보간 적용. 1프레임 즉시 튀김 → 여러 프레임에 걸친 부드러운 반동으로 개선
- [x] (2026-07-15 #4) 시점 분리(View Separation) 리팩터링 — 마우스 에임(`_aimPitch`/`_aimYaw`)과 반동 오프셋(`_recoilPitch`/`_recoilYaw`)을 별도 변수로 분리, `ApplyViewRotation()`에서 합산 후 1회만 회전 적용. 프레임 내 반대 방향 경쟁으로 인한 떨림 해소. 피치 클램프에 반동 오프셋 반영하여 반동 누적 시 마우스 조작 불능 버그 수정
- [x] (2026-07-22 #2) ICombatTarget 인터페이스 도입 — 히트스캔 피격 대상 식별을 `GameObjectController` 대신 `ICombatTarget` 인터페이스 기반으로 전환. `PlayerController`/`OppoPlayerController`에 구현 추가, `ProcessHit()`의 `GetComponentInParent` 타입 교체. 비전투 오브젝트(Container 등) 오판정 방지

### 네트워크/UI
- [x] (2026-07-22 #0) D2CNotifyHealthChange 핸들러 구현 — PacketHandler에 핸들러 등록+파싱, IngameScene.HandleHealthChange()로 전달, IngameHealthBarUI를 max/current 분리 구조로 리팩터링, SyncHealthBarMax()로 장비 변경 시 최대값 자동 갱신
- [x] (2026-07-22 #1) IngameScene 미사용 HP 필드 제거 — 서버 주도 HP 관리 전환에 따라 `_maxHp`/`_currentHp` 삭제

### 문서/설정
- [x] (2026-07-15 #1) CLAUDE.md 구조 개편 — `Scenes/CLAUDE.md`(204줄)에서 PlayerController/OppoPlayerController 관련 내용을 `Controller/CLAUDE.md`(70줄)로 분리. Scenes 144줄로 축소. 루트 CLAUDE.md 2개에 참조 추가
- [x] (2026-07-15 #2) 상위 CLAUDE.md 로드 제외 설정 — `.claude/settings.local.json`에 `claudeMdExcludes` 추가, 클라이언트 세션에서 서버 컨텍스트(`Extraction/CLAUDE.md`) 로드 방지. 머신 종속 경로이므로 `.local`에 배치
- [x] (2026-07-15 #3) CLAUDE.md 과도한 서술 정리 — 코드에서 바로 확인 가능한 필드 테이블·getter/setter 나열·상수값 등 제거. 설계 의도·규칙·흐름·함정만 잔류. 전체 471줄 → 269줄(−43%)

---

## 다음 작업 우선순위 (제안)

1. **발사 이펙트 프리팹 준비 → 이펙트 구현** — 로컬 탄착 이펙트(`ProcessHit`), 수신 측 머즐 플래시/총성/탄착 이펙트(`HandleWeaponFireBroadcast`). 파티클·사운드 에셋이 `Resources/Prefabs/` 아래에 필요
2. **탄약 차감 주석 해제** — `Fire()` 내 `magazine.quantity--` 및 빈 탄창 가드. 테스트 완료 후 활성화
3. **EmptyAmmoFire() 구현** — 빈 탄창 사운드, 재장전 유도 UI
4. **인벤토리 열기/닫기 키바인딩** — Tab키로 MyInventory 토글 등 추가 입력 연결 (컨테이너 E/I키 닫기는 완료)
5. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
6. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현
