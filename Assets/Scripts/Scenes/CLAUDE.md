# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`LobbyScene`**: 로비 씬 진입점 — `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching). 모든 인증/매칭 흐름 및 ESC 키 동작을 담당
- **`LoadingScene`**: 매칭 성공 후 전환되는 로딩 씬 — `Managers.Scene.LoadSceneAsync()`로 GameScene 비동기 로딩, 90% 도달 시 `SendC2DRequestBlueprint()`, `staticObjectsLoadFlag = true`가 되면 `CompleteLoadSceneAsync()` 호출
- **`IngameScene`**: 인게임 맵 씬들의 공통 베이스(`BaseScene` 상속). `Init()`에서 `NextSceneStaticContext.ObjectDatas`를 순회해 정적 오브젝트 일괄 스폰 후 `ResetLoadSceneOp()` 호출. `RequestSpawnMe()`로 서버에 스폰 요청 전송. 실제 맵 씬은 이 클래스를 상속해 구현한다
- 씬 전환: 반드시 `Managers.Scene` 사용

## 씬 전환 페이로드 (`GameSceneContext`)

`SceneManagerEx`는 두 개의 `GameSceneContext` 인스턴스를 보유한다:

| 프로퍼티 | 용도 |
|----------|------|
| `NextSceneStaticContext` | Blueprint 패킷으로 수신한 정적 오브젝트 목록 |
| `SceneDynamicContext` | SpawnMe 패킷으로 수신한 동적 오브젝트 목록 |

**`GameSceneContext` 주요 API**:
- `AddObjectDatas(index, isLast, objects)` — 패킷 인덱스 기반 누적 저장 (중복 수신 방어 내장)
- `IsComplete()` — IsLast 패킷 수신 + 0~lastIndex 전부 수신 시 true
- `Clear()` — ObjectDatas·인덱스 추적 상태 전체 해제

**쓰기**: `PacketHandler` 핸들러에서 Protobuf → `ObjectData` 변환 후 `AddObjectDatas()` 호출. Protobuf 타입은 핸들러 밖으로 노출하지 않는다

**읽기**: `IngameScene.Init()`에서 `NextSceneStaticContext.ObjectDatas` 순회해 정적 오브젝트 생성

**전체 초기화**: `SceneManagerEx.ResetLoadSceneOp()` — `NextSceneStaticContext`와 `SceneDynamicContext` 모두 `Clear()` 호출

## IngameScene 스폰 흐름

1. `TestIngameScene.Start()` → `RequestSpawnMe()` → `C2DRequestSpawnMe` 전송
2. 서버 응답:
   - `D2CResponseSpawnMeSpawnSpot` → `HandleSpawnSpot(spawnPoint, characterType, objectId)` — `_spawnPoint`, `_characterType`, `_myObjectId` 저장, `_isGetResponseSpawnMe = true`
   - `D2CResponseSpawnMeDynamicObjects` (1개 이상) → `SceneDynamicContext.AddObjectDatas()` 누적
3. 두 조건(`_isGetResponseSpawnMe && SceneDynamicContext.IsComplete()`) 충족 시 `SpawnMeAndStartGame()` 호출 (이중 호출은 `_operationFlag` guard로 방지)
4. `SpawnMeAndStartGame()`: `_operationFlag = true` 설정 → `PlayerObject` 인스턴스화 후 `_spawnPoint`로 위치 지정 → `Setup(_characterType)` 호출(컴포넌트 바인딩·외형 설정 일괄 처리) → `SetObjectId((int)_myObjectId)` 호출 → `SetCursorLock(true)` → `SceneDynamicContext.ObjectDatas` 순회해 동적 오브젝트 스폰 → `SceneDynamicContext.Clear()`
