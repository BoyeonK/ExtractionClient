# 프로젝트 진행 상황

> 최종 수정: 2026-05-13
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-05-12 #1) LobbyScene 캐릭터 선택 UI 추가 — HB0/HB1/HB2 `SelectedCharacterAnim` 컨트롤러 및 `HBxSelected` 프리팹 생성, LobbyScene에 캐릭터 선택 기능 연결

### 네트워크
- [x] (2026-05-06 #0) 프로토콜 변경사항 적용 — `External_Protocol.proto` / `External_Unity_Object.proto` 수정, `Define.cs` 업데이트
- [x] (2026-05-10 #0) 매치메이킹 `/start` 요청에 `characterType` 추가 — `GameReadyRequest` 스펙 반영, `MatchStartRequest`·`StartMatchCall`·`TryMatchMake` 수정, `_selectedCharacterType`을 `LobbyScene`으로 이동 및 `SetCharacterType()` 추가
- [x] (2026-05-12 #0) 매치 상태 응답 `mapId` 필드 추가 — `MatchStatusData`에 `mapId` 추가, `HTTPManager`에 `MapId` 프로퍼티 추가 및 SUCCESS 분기에서 저장
- [x] (2026-05-13 #0) Disconnect 시 PacketHandler 초기화 — `PacketHandler.Reset()` 추가, `UDPManager.Disconnect()`에서 호출해 세션 간 상태 격리 (`_pendingSlots`, ACK 상태, RTT, 시퀀스 번호 등 전체 클리어)

### 기타
- [x] (2026-05-06 #1) 압축된 Quaternion값 복원 로직 — `PacketHandler.cs`에 Quaternion 역직렬화·복원 구현
- [x] (2026-05-06 #2) `ObjectData` struct 기반 gameObject 생성 메서드 정의 — `ResourceManager`에 생성 메서드 추가, `TestIngame.cs` → `TestIngameScene.cs` 리네임, `TestIngameScene` 프리팹 추가
- [x] (2026-05-07 #0) `PlayerObject` 모든 Animation 등록 및 적용 — 애니메이션 클립 연결 및 상태 머신 설정 완료
- [x] (2026-05-12 #2) `IngameScene` 기본 클래스 정의 — `BaseScene` 상속, `Init()`에서 정적 오브젝트 일괄 스폰, `RequestSpawnMe()` 제공. `TestIngameScene`이 이를 상속하도록 리팩토링
- [x] (2026-05-13 #1) SpawnMe 응답 흐름 버그 수정 및 초기 구현 — `TryCompleteSpawnMe()`에 `_operationFlag` guard 추가(이중 호출 방지), `ResetLoadSceneOp()`에서 `SceneDynamicContext.Clear()` 누락 수정, `GameSceneContext.OnObjectsSpawned()` → `Clear()` 리네임, `SpawnMeAndStartGame()`에 `_operationFlag = true` 및 `SceneDynamicContext.Clear()` 추가

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
6. Init함수가 실행된 이후, 서버에 Scene 로딩 완료됬음을 알려줌과 동시에 동적인 정보를 다시 요청.
    - C2DRequestSpawnMe (`TestIngameScene.Start()`에서 호출 연결 완료, 응답 핸들러 및 `TryCompleteSpawnMe` 흐름 구현 완료, `SpawnMeAndStartGame()` 내부 구현 진행 중)

---

## 다음 작업 우선순위 (제안)

1. **`SpawnMeAndStartGame()` 구현 완성** — 플레이어 캐릭터 스폰(`_spawnPoint`, `_characterType` 활용) 및 동적 오브젝트 일괄 생성 구현
2. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
3. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
