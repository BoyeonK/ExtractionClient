# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`LobbyScene`**: 로비 씬 진입점 — `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching). 모든 인증/매칭 흐름 및 ESC 키 동작을 담당
- **`LoadingScene`**: 매칭 성공 후 전환되는 로딩 씬 — `Managers.Scene.LoadSceneAsync()`로 GameScene 비동기 로딩, 90% 도달 시 `SendC2DRequestBlueprint()`, `staticObjectsLoadFlag = true`가 되면 `CompleteLoadSceneAsync()` 호출
- 씬 전환: 반드시 `Managers.Scene` 사용
