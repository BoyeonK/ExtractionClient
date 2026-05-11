# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`LobbyScene`**: 로비 씬 진입점 — `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching). 모든 인증/매칭 흐름 및 ESC 키 동작을 담당
- **`LoadingScene`**: 매칭 성공 후 전환되는 로딩 씬 — `Managers.Scene.LoadSceneAsync()`로 GameScene 비동기 로딩, 90% 도달 시 `SendC2DRequestBlueprint()`, `staticObjectsLoadFlag = true`가 되면 `CompleteLoadSceneAsync()` 호출
- **`IngameScene`**: 인게임 맵 씬들의 공통 베이스(`BaseScene` 상속). `Init()`에서 `NextSceneContext.StaticObjects`를 순회해 정적 오브젝트 일괄 스폰 후 `ResetLoadSceneOp()` 호출. `RequestSpawnMe()`로 서버에 스폰 요청 전송. 실제 맵 씬은 이 클래스를 상속해 구현한다
- 씬 전환: 반드시 `Managers.Scene` 사용

## 씬 전환 페이로드 (`GameSceneContext`)

`SceneManagerEx.NextSceneContext`(`GameSceneContext` 타입)에 다음 씬에서 필요한 데이터를 보관한다.

| 필드 | 타입 | 설명 |
|------|------|------|
| `SpawnPoint` | `UnityEngine.Vector3` | 플레이어 스폰 위치 |
| `SpawnPointReceived` | `bool` | SpawnPoint 수신 여부 |
| `StaticObjects` | `List<StaticObjectData>` | 정적 오브젝트 목록 (Protobuf 파싱 완료) |

`StaticObjectData`: `ObjectId(uint)`, `ObjectType(uint)`, `Position(Vector3)`, `Front(Vector3)`

- **쓰기**: `PacketHandler` Blueprint 핸들러에서 Protobuf → Unity 타입 변환 후 `SetSpawnPoint()` / `AddStaticObjects()` 호출. Protobuf 타입은 핸들러 밖으로 노출하지 않는다
- **완료 판단**: `IsComplete()` — SpawnPoint 수신 + IsLast 패킷 존재 + 0~lastIndex 전부 수신
- **읽기**: `GameScene.Init()`에서 `Managers.Scene.NextSceneContext`로 접근해 오브젝트 생성
- **스폰 완료 후**: `OnStaticObjectsSpawned()` 호출 — SpawnPoint·StaticObjects·인덱스 추적 상태 전체 해제
- **전체 초기화**: `SceneManagerEx.ResetLoadSceneOp()` 호출 시 새 인스턴스로 교체
