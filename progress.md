# 프로젝트 진행 상황

> 최종 수정: 2026-05-14
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI

### 네트워크
- [x] (2026-05-13 #0) Disconnect 시 PacketHandler 초기화 — `PacketHandler.Reset()` 추가, `UDPManager.Disconnect()`에서 호출해 세션 간 상태 격리 (`_pendingSlots`, ACK 상태, RTT, 시퀀스 번호 등 전체 클리어)
- [x] (2026-05-14 #3) Unreliable 패킷 uSeqNum 중복 전송 버그 수정 — `_unreliableScratch` 단일 공유 버퍼를 10-슬롯 링 풀(`_unreliablePool`)로 교체. HeartBeat·PlayerState가 같은 프레임에 enqueue될 때 버퍼 덮어쓰기로 동일 uSeqNum이 두 번 전송되던 문제 해결
- [x] (2026-05-14 #4) `C2DRequestSpawnPlayerObjects` / `D2CSpawnPlayerObject` / `D2CSpawnPlayerObjects` 빈 핸들러·송신 함수 추가 — proto 신규 패킷 3종에 대해 `PacketHandler` 생성자 핸들러 등록, `Handle_D2CSpawnPlayerObject` · `Handle_D2CSpawnPlayerObjects` 빈 구현 추가, `UDPManager.SendC2DRequestSpawnPlayerObjects()` 추가

### 기타

- [x] (2026-05-13 #1) SpawnMe 응답 흐름 버그 수정 및 초기 구현 — `TryCompleteSpawnMe()`에 `_operationFlag` guard 추가(이중 호출 방지), `ResetLoadSceneOp()`에서 `SceneDynamicContext.Clear()` 누락 수정, `GameSceneContext.OnObjectsSpawned()` → `Clear()` 리네임, `SpawnMeAndStartGame()`에 `_operationFlag = true` 및 `SceneDynamicContext.Clear()` 추가
- [x] (2026-05-13 #2) `SpawnMeAndStartGame()` 구현 완성 — 플레이어 스폰 위치(`_spawnPoint`) 적용, `SetCursorLock(true)` `SpawnMeAndStartGame` 내부로 이동, `PlayerController.SetAppearance()`의 미사용 `modelGo` 변수 정리
- [x] (2026-05-13 #3) `PlayerController` SpineAim 및 이중 초기화 버그 수정 — `MultiAimConstraintData` struct 복사-수정-재할당 패턴 적용(SpineAim 미적용 수정), `_isInit = true`를 `Init()` 끝으로 이동해 `Start()` 이후 `Init(int)`호출 시 키 리스너 이중 등록 방지
- [x] (2026-05-13 #4) `PlayerController` 초기화 구조 리팩토링 — `Init(int)` 제거 후 `Setup(int characterType)` 도입(컴포넌트 바인딩·SetAppearance 편입), `_isInit` 제거, `ProcessMovement()` null guard 추가
- [x] (2026-05-14 #0) `C2DRequestSpawnByObjectId` / `D2CResponseSpawnByObjectId` 패킷 구현 — `UDPManager.SendC2DRequestSpawnByObjectId(int objectId)` 추가, `PacketHandler.Handle_D2CResponseSpawnByObjectId` 구현 (worker thread에서 역직렬화·`ObjectData` 변환, main thread에서 `IngameScene` 확인 후 `InstantiateFromObjectDataStruct` 호출)
- [x] (2026-05-14 #1) `C2DUpdatePlayerState` 패킷 구현 및 0.1초 주기 송신 — proto에 메시지 정의(`GameObjectMovementInfo` + `pitch` + `velocity`), `PlayerController`에 네트워크 상태 프로퍼티 추가(`ObjectId`/`Yaw`/`Pitch`/`Velocity`/`MovementState`), `UDPManager.SendC2DUpdatePlayerState()` 추가, `IngameScene`에서 0.1초 타이머로 Unreliable 송신
- [x] (2026-05-14 #2) `D2CResponseSpawnMeSpawnSpot`에 objectId 추가 반영 — `HandleSpawnSpot`에 `uint objectId` 파라미터 추가, `_myObjectId` 필드 저장, `SpawnMeAndStartGame()`에서 `_playerController.SetObjectId((int)_myObjectId)` 호출

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
2. **`Handle_D2CSpawnPlayerObject` / `Handle_D2CSpawnPlayerObjects` 로직 구현** — 다른 플레이어 오브젝트 스폰 처리 (현재 빈 핸들러 상태)
3. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
