# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`LobbyScene`**: 로비 씬 진입점 — `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching). 모든 인증/매칭 흐름 및 ESC 키 동작을 담당
- **`LoadingScene`**: 매칭 성공 후 전환되는 로딩 씬 — `Managers.Scene.LoadSceneAsync()`로 GameScene 비동기 로딩, 90% 도달 시 `SendC2DRequestBlueprint()`, `staticObjectsLoadFlag = true`가 되면 `CompleteLoadSceneAsync()` 호출
- 씬 전환: 반드시 `Managers.Scene` 사용

## 씬 전환 페이로드 (`GameSceneContext`)

`SceneManagerEx.NextSceneContext`(`GameSceneContext` 타입)에 다음 씬에서 필요한 데이터를 보관한다.

| 필드 | 타입 | 설명 |
|------|------|------|
| `SpawnPoint` | `D2CResponseBlueprintSpawnPoint` | 플레이어 스폰 위치 |
| `StaticObjectPackets` | `List<D2CResponseBlueprintStaticObjects>` | 정적 오브젝트 패킷 누적 목록 |

- **쓰기**: `PacketHandler`의 Blueprint 응답 핸들러에서 `LoadingScene` 존재 확인 후 저장
- **읽기**: `GameScene.Init()`에서 `Managers.Scene.NextSceneContext`로 접근해 오브젝트 생성
- **초기화**: `SceneManagerEx.ResetLoadSceneOp()` 호출 시 새 인스턴스로 교체
