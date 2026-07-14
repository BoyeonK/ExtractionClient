# 프로젝트 진행 상황

> 최종 수정: 2026-07-15
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### 플레이어/애니메이션
- [x] (2026-07-07 #0) UI 열림 시 사격 애니메이션 차단 — `PlayerController.ProcessAnimation()`에서 `_ingameScene.IsAnyUIOpen` 체크 추가, UI 표시 중 좌클릭 사격 애니메이션 미재생
- [x] (2026-07-07 #1) PlayerState에 action_state 필드 추가 — `External_Unity_Object.proto`에 `action_state` 필드(0=NONE, 1=SHOOTING) 추가, 송신 체인(`PlayerController.ActionState` → `IngameScene.SendPlayerState` → `UDPManager`) 및 수신 체인(`PacketHandler` → `PlayerStateData` → `OppoPlayerController`) 전체 연결, 다른 플레이어의 사격 애니메이션 동기화

### 데이터
- [x] (2026-07-13 #0) WeaponSpec에 HRecoilMax 필드 추가 검증 — DB에서 Python 스크립트로 생성된 `ItemDBHelper.cs`에 `HRecoilMax` 필드가 올바르게 반영되었음을 확인 (AK-47: 50, M4A1: 40, M16: 50)
- [x] (2026-07-13 #1) IngameInventory에 CurrentWeapon 프로퍼티 추가 — `_isPrimaryWeaponApplyed`에 따라 현재 장착 무기 반환, 사격 로직에서 무기 스펙 조회용

### 플레이어/사격
- [x] (2026-07-13 #2) PlayerController에 RPM 기반 발사 타이머 구현 — `_fireTimer`/`_fireInterval` 도입, cap+차감 방식으로 RPM 정확도 보장, `Fire()` 메서드 뼈대 추가. `IsMoving`/`IsRunning`/`IsShooting` 프로퍼티로 중복 상태 계산 통합
- [x] (2026-07-13 #3) Fire() 내부 구현 — 탄약 소모(매거진 quantity--), 스프레드 적용 히트스캔(`CalculateSpreadRay` 원뿔형 분포), 수직 반동(VRecoilMin~Max → xRotation), 수평 반동(0~HRecoilMax 랜덤 좌/우), 스프레드 증가(SpreadIncreasePerShot, SpreadMax cap). `EquipWeapon()`에서 WeaponSpec 캐싱+단위 변환(/100→도). `EmptyAmmoFire()`/`ProcessHit()` 스텁 추가
- [x] (2026-07-13 #4) 발사 차단(fireBlocked) 메커니즘 — `_fireBlocked`/`_wasMousePressed`/`_wasUIOpen` 도입. 빈 탄창 시 block, UI 열림 전환 시 block, 마우스 재클릭(release→press) 시 해제. EmptyAmmoFire 반복 호출 방지 및 UI 닫힘 후 의도치 않은 즉시 발사 방지
- [x] (2026-07-13 #5) 스프레드 회복 로직 — `ProcessFire()`에서 매 프레임 `_currentSpread -= _spreadRecoveryRate * deltaTime`, `_spreadBase` 하한. `_spreadRecoveryRate`를 `EquipWeapon()`에서 캐싱
- [x] (2026-07-15 #0) 반동 보간(Smooth Recoil) 구현 — `Fire()`에서 즉시 적용하던 수직/수평 반동을 `_recoilTarget`에 목표만 누적, `ProcessRecoil()`에서 매 프레임 `Vector2.Lerp`로 보간 적용. 1프레임 즉시 튀김 → 여러 프레임에 걸친 부드러운 반동으로 개선

---

## 진행 중 / 미완성

### 매칭 성공시 씬 전환
/connect요청을 통해서 ip와 port를 받았을 경우
1. ~~workerThread를 살려내고 루프 작동. (ping 작동)~~ ← **완료**
    - workerThread내에서 ReliableFlag로 C2DHeartBeat전송, D2CHeartBeat로 응답 받음.
2. ~~Scene을 LoadingScene으로 변경하고, GameScene의 비동기 로딩 시작.~~ ← **완료**
3. ~~비동기 로딩 완료되었을 경우, C2DRequestBluePrint 전송~~ ← **완료**
4. ~~3의 패킷의 응답을 받았을 경우, 해당 내용을 역직렬화해서 보관하고 Scene교체 진행.~~ ← **완료**
    - `D2CResponseBlueprintSpawnPoint` / `D2CResponseBlueprintStaticObjects` 핸들러 구현, `SceneManagerEx.NextSceneContext`(`GameSceneContext`)에 누적 저장
5. 교체된 Scene의 Init() 함수에서 C2DRequestBluePrint에서 받아온 친구들 까지 포함해서 그려냄 ← **진행 중** (`IngameScene.Init()`에서 정적 오브젝트 스폰 연결 완료, 실제 맵 씬에서 `IngameScene` 상속 후 `RequestSpawnMe()` 호출 필요)
6. ~~Init함수가 실행된 이후, 서버에 Scene 로딩 완료됬음을 알려줌과 동시에 동적인 정보를 다시 요청.~~ ← **완료**
    - C2DRequestSpawnMe (`TestIngameScene.Start()`에서 호출, 응답 핸들러·`TryCompleteSpawnMe` 흐름·`SpawnMeAndStartGame()` 모두 구현 완료)

---

## 다음 작업 우선순위 (제안)

1. **ProcessHit() 구현** — 피격 대상에 따른 데미지 계산, 히트 이펙트, 서버 히트 검증
2. **EmptyAmmoFire() 구현** — 빈 탄창 사운드, 재장전 유도 UI
3. **인벤토리 열기/닫기 키바인딩** — Tab키로 MyInventory 토글 등 추가 입력 연결 (컨테이너 E/I키 닫기는 완료)
4. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
5. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현
