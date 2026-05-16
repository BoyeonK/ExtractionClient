# 프로젝트 진행 상황

> 최종 수정: 2026-05-16
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI

### 네트워크
- [x] (2026-05-15 #0) `C2DUpdatePlayerState` 패킷 구조 수정 — proto 변경(`PlayerState` 타입 분리)에 맞춰 `UDPManager.SendC2DUpdatePlayerState()` 내 패킷 조립을 `State = new PlayerState { ... }` 래핑 구조로 교체
- [x] (2026-05-15 #1) `Handle_D2CUpdatePlayerStates` 빈 핸들러 추가 — `PacketHandler` 생성자 핸들러 등록, 파싱 + 예외 처리만 갖춘 빈 구현
- [x] (2026-05-15 #2) `Handle_D2CSpawnPlayerObjects` 구현 — `PlayerSpawnData` 구조체 추가, Protobuf→Unity 변환 후 메인 스레드에서 `IngameScene.SpawnPlayerObjects()` 호출, `_oppoPlayers` Dictionary로 OppoPlayerController 관리
- [x] (2026-05-15 #3) `Handle_D2CSpawnPlayerObject` 구현 — 단일 플레이어 스폰 동일 패턴, `SpawnPlayerObject()` 메서드 추출 및 `SpawnPlayerObjects()`에서 재사용
- [x] (2026-05-15 #4) `Handle_D2CUpdatePlayerStates` 로직 구현 — `PlayerStateData` 구조체 추가, Protobuf→Unity 변환 후 메인 스레드에서 `IngameScene.UpdatePlayerStates()` 호출, `OppoPlayerController.ApplyState()` 구현, 미등록 objectId는 `C2DRequestSpawnByObjectId` 전송
- [x] (2026-05-16 #1) `OppoPlayerController` 보간/애니메이션/에임 구현 — `ProcessMovement()`에 `Vector3.Lerp`+`LerpAngle` 위치·회전 보간, `ProcessAnimation()`에 velocity→로컬좌표 변환 후 Animator 파라미터 구동(MoveX/MoveY/MovingSpeed), `ProcessAim()`에 yaw+pitch→aimDir 계산으로 `_aimTarget` 배치. `ApplyState()`를 보간 지원 구조로 수정(첫 수신/대규모 이동만 즉시 텔레포트)

### 기타

- [x] (2026-05-16 #0) `D2CSpawnPlayerObjects` 스폰 흐름 완성 — `OppoPlayerController.Setup()` 주석 해제(모델·RigBuilder·MultiAimConstraint·Animator 바인딩 활성화), `_operationFlag = true` 설정 추가(재진입 방지 + 상태 전송 루프 활성화), `SpawnPlayerObject()`에 중복 ObjectId 방어 추가
- [x] (2026-05-16 #2) `_operationFlag` 플래그 분리 + `C2DNotifyLoadingComplete` 패킷 추가 — re-entrance guard를 `_spawnCompleted`로 분리, `_operationFlag`는 `SpawnPlayerObjects()` 완료 후 설정으로 이동(전체 초기화 완료 게이트), `C2DNotifyLoadingComplete` proto 정의 + `UDPManager.SendC2DNotifyLoadingComplete()` 추가, `_operationFlag = true` 시점에 서버로 Reliable 전송
- [x] (2026-05-16 #3) HeartBeat 주기 및 Reliable 재전송 상수 튜닝 — 글로벌 환경(RTT ~50-200ms) 타겟으로 `HEARTBEAT_INTERVAL_SEC` 1→3초, `MIN_RTO_MS` 300→250ms, `MAX_RETRY` 10→7회 조정

### 버그 수정

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

1. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
2. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
