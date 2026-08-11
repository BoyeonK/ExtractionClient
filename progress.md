# 프로젝트 진행 상황

> 최종 수정: 2026-08-12
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### 플레이어/사격
- [x] (2026-07-15 #4) 시점 분리(View Separation) 리팩터링 — 마우스 에임(`_aimPitch`/`_aimYaw`)과 반동 오프셋(`_recoilPitch`/`_recoilYaw`)을 별도 변수로 분리, `ApplyViewRotation()`에서 합산 후 1회만 회전 적용. 프레임 내 반대 방향 경쟁으로 인한 떨림 해소. 피치 클램프에 반동 오프셋 반영하여 반동 누적 시 마우스 조작 불능 버그 수정
- [x] (2026-07-22 #2) ICombatTarget 인터페이스 도입 — 히트스캔 피격 대상 식별을 `GameObjectController` 대신 `ICombatTarget` 인터페이스 기반으로 전환. `PlayerController`/`OppoPlayerController`에 구현 추가, `ProcessHit()`의 `GetComponentInParent` 타입 교체. 비전투 오브젝트(Container 등) 오판정 방지

### 네트워크/UI
- [x] (2026-07-22 #0) D2CNotifyHealthChange 핸들러 구현 — PacketHandler에 핸들러 등록+파싱, IngameScene.HandleHealthChange()로 전달, IngameHealthBarUI를 max/current 분리 구조로 리팩터링, SyncHealthBarMax()로 장비 변경 시 최대값 자동 갱신
- [x] (2026-07-22 #1) IngameScene 미사용 HP 필드 제거 — 서버 주도 HP 관리 전환에 따라 `_maxHp`/`_currentHp` 삭제
- [x] (2026-08-12 #0) 귀환(Recall) 프로토콜 뼈대 구현 — `External_Protocol.proto`에 `C2DRequestRecall`/`D2CResponseRecall` 메시지 및 `PktId` 32·33 추가, `PacketHandler`에 `Handle_D2CResponseRecall` 등록·구현(파싱·로깅까지), `UDPManager.SendC2DRequestRecall()` 추가. 귀환은 일회성 비가역 결정이므로 reliable 전송 선택. 승인/거부 실처리와 호출부는 미연결
- [x] (2026-08-12 #1) 귀환 상호작용 파이프라인 연결 — `RecallSpotController` 신규(`InteractableGameObjectController` 상속, `_interactText="귀환하기"`, `[SerializeField] _recallSpotIndex`). `IngameScene.RequestRecall()`/`HandleRecallResponse()` 추가, `Handle_D2CResponseRecall`을 씬으로 연결. 중복 요청 차단은 스팟별이 아닌 씬 단위 `_recallRequested` 플래그가 담당
- [x] (2026-08-12 #2) D2CNotifyRecallResult 프로토콜 및 핸들러 추가 — `PktId` 34 + `RecallResultReason` enum(SUCCESS/OUT_OF_ZONE/PLAYER_DEAD/SESSION_LOST/SERVER_INTERNAL) 정의. `Handle_D2CNotifyRecallResult` 등록·구현 후 `IngameScene.HandleRecallResult()`로 연결. 성공/취소 분기는 TEMP 로그 + 플래그 해제까지만 구현
- [x] (2026-08-12 #3) 귀환 응답 워치독 추가 (TEMP) — 전송 시점부터 `RECALL_TIMEOUT`(10초) 타이머를 돌려 응답 유실 시 `_recallRequested`를 해제. 서버 통지가 유실되면 그 판 탈출이 영구 불가해지는 것을 막는 임시 안전장치로, 결과를 추측하지 않고 로컬 잠금만 푼다

### 문서/설정
- [x] (2026-07-15 #2) 상위 CLAUDE.md 로드 제외 설정 — `.claude/settings.local.json`에 `claudeMdExcludes` 추가, 클라이언트 세션에서 서버 컨텍스트(`Extraction/CLAUDE.md`) 로드 방지. 머신 종속 경로이므로 `.local`에 배치
- [x] (2026-07-15 #3) CLAUDE.md 과도한 서술 정리 — 코드에서 바로 확인 가능한 필드 테이블·getter/setter 나열·상수값 등 제거. 설계 의도·규칙·흐름·함정만 잔류. 전체 471줄 → 269줄(−43%)

---

## 다음 작업 우선순위 (제안)

1. **귀환 최종 결과 실처리 (TEMP 해소)** — `HandleRecallResult`의 TEMP 로그를 실제 처리로 교체. 성공 시 탈출 연출·씬 전환 + 잠금 유지(이미 맵을 떠나므로 해제하면 전환 지연 중 재요청 가능). 취소 시 `reason`별 분기(`OUT_OF_ZONE`·`SERVER_INTERNAL`은 재시도 허용, `PLAYER_DEAD`·`SESSION_LOST`는 각 흐름에 위임)
2. **귀환 스팟 씬 배치** — 맵 씬에 귀환 단말기 오브젝트 배치, `RecallSpotController` 부착 후 인스펙터에서 `_recallSpotIndex`를 서버 테이블 값에 맞춤. 조준 레이가 맞아야 하므로 트리거가 아닌 일반 콜라이더 사용
3. **귀환 진행 중 UI 피드백** — 승인~결과 사이 5초 구간 표시(카운트다운 등). `InteractText`는 매 프레임 재조회되므로 `virtual` 프로퍼티화하면 동적 텍스트 전환 가능
4. **TEMP 귀환 워치독 제거 검토** — 서버 통지 신뢰성이 검증되면 `RECALL_TIMEOUT`/`_recallTimer` 및 `OnUpdate()`의 TEMP 블록 제거. 1번이 끝나기 전까지는 워치독이 정상 경로까지 떠받치므로 먼저 제거하지 말 것
5. **발사 이펙트 프리팹 준비 → 이펙트 구현** — 로컬 탄착 이펙트(`ProcessHit`), 수신 측 머즐 플래시/총성/탄착 이펙트(`HandleWeaponFireBroadcast`). 파티클·사운드 에셋이 `Resources/Prefabs/` 아래에 필요
6. **탄약 차감 주석 해제** — `Fire()` 내 `magazine.quantity--` 및 빈 탄창 가드. 테스트 완료 후 활성화
7. **EmptyAmmoFire() 구현** — 빈 탄창 사운드, 재장전 유도 UI
8. **인벤토리 열기/닫기 키바인딩** — Tab키로 MyInventory 토글 등 추가 입력 연결 (컨테이너 E/I키 닫기는 완료)
9. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
10. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현

---

## 메모

### 귀환(Recall) 설계 확정 사항
- **2단계 프로토콜** — ① `C2DRequestRecall` → `D2CResponseRecall`(승인/거부) ② 승인 시 서버가 1초 간격 5회 검사 후 `D2CNotifyRecallResult`(성공/취소 + 사유)
- **사유 전달 범위가 단계별로 다르다 (의도된 설계)** — **요청 거부**(`D2CResponseRecall`)는 사유를 알리지 않고 bool만. **진행 중 취소**(`D2CNotifyRecallResult`)는 `RecallResultReason`으로 사유를 알린다
- **복수 요청 허용 안 함** — 서버가 멱등성으로 처리, 클라도 `IngameScene._recallRequested`로 막는다. 스팟별이 아닌 씬 단위 플래그여야 다른 스팟 재요청 경로가 막힌다. 요청/응답 대조는 `recall_spot_index` 에코로 충분
- **응답 도달 시점에 씬은 항상 `IngameScene`** — 로딩 중 도달은 논리적으로 불가하므로 핸들러의 씬 가드는 방어 코드일 뿐, 별도 pending 처리 불필요
- **귀환 스팟은 작은 프롭** — 상호작용 판정이 `hit.collider.transform.position` 기준 2m 고정(`PlayerController.CheckInteractable`)이라 넓은 트리거 존은 성립하지 않는다. 영역형으로 바꾸려면 `InteractDistance` virtual화 + `hit.point` 기준 거리로 판정부 리팩터링 필요
- **귀환 스팟은 서버 스폰 대상이 아님** — `ObjectData`에 인덱스 필드가 없으므로 맵 씬에 직접 배치하고 `[SerializeField] _recallSpotIndex`로 부여
- **"실패 시 서버가 반드시 알린다"는 전제 + 워치독** — 전제 자체는 유지하되, 전제가 깨졌을 때 복구 불가가 되지 않도록 클라에 타임아웃을 둔다. `SESSION_LOST`·`SERVER_INTERNAL`은 사유 자체가 통지 경로의 불안정을 가리키고, 서버가 이미 멱등 처리하므로 안전장치 비용은 사실상 0. 반대로 없을 때의 손실은 "그 판 탈출 영구 불가"로 최대치라 비용이 비대칭
- **알려진 한계** — 타임아웃 후 재요청 시 1차 시도의 늦은 응답을 2차 결과로 오인할 수 있다(`recall_spot_index` 에코만으로는 시도 구분 불가). 타임아웃을 10초로 넉넉히 잡아 실무상 회피 중. 엄밀히 하려면 `C2DRequestWeaponFire`의 `FireSequence`처럼 요청 시퀀스를 proto에 추가해야 함
