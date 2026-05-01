# 프로젝트 진행 상황

> 최종 수정: 2026-05-02
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI

### 네트워크
- [x] (2026-05-02 #0) Blueprint 응답 핸들러 추가 — `Handle_D2CResponseBlueprintSpawnPoint` / `Handle_D2CResponseBlueprintStaticObjects` 구현, LoadingScene 씬 체크 후 `Managers.Scene.NextSceneContext`에 저장 + 로그
- [x] (2026-05-02 #2) `GameSceneContext` Protobuf → Unity 타입 리팩터 — `StaticObjectData` 도입, `IsComplete()` 캡슐화, 핸들러에서 파싱 후 저장, `TryCompleteBlueprint` 간소화
- [x] (2026-05-02 #3) Network CLAUDE.md에 Protobuf 타입 격리 규칙 추가
- [x] (2026-05-02 #4) `GameSceneContext.OnStaticObjectsSpawned()` 추가 — 정적 오브젝트 스폰 완료 후 SpawnPoint·StaticObjects·인덱스 추적 상태 전체 초기화

### 기타
- [x] (2026-04-29 #2) .proto 파일로 전환 및 캐시 정리 — `External_Protocol.proto` / `External_Unity_Object.proto` 추가, 기존 `ExternalProtocol.cs` / `ExternalUnityObject.cs` 삭제
- [x] (2026-04-30 #0) `LoadingScene1` 씬 전환 코드 완성 — 비동기 로딩 90% 도달 시 `C2DRequestBlueprint` 전송, `staticObjectsLoadFlag` 세팅 후 `CompleteLoadSceneAsync` 호출
- [x] (2026-04-30 #1) TestIngame 전환 코드 작성 및 작동 확인 — D2CResponseBlueprint 수신 후 TestIngame 씬으로 전환하는 흐름 구현 및 검증
- [x] (2026-05-02 #1) `GameSceneContext` + `SceneManagerEx.NextSceneContext` 추가 — 씬 전환 페이로드 보관 구조 도입, `ResetLoadSceneOp()`에서 초기화

### 버그 수정
- [x] (2026-04-30 #2) DragGhost null 참조 버그 수정 — LobbyScene 탈출 시 DragGhost가 null 참조되던 문제 해결
- [x] (2026-04-30 #3) allowSceneActivation 버그 수정 — 비동기 씬 전환 플래그가 변경되지 않던 문제 해결

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
5. 교체된 Scene의 Init() 함수에서 C2DRequestBluePrint에서 받아온 친구들 까지 포함해서 그려냄 ← **다음**
6. Init함수가 실행된 이후, 서버에 Scene 로딩 완료됬음을 알려줌과 동시에 동적인 정보를 다시 요청.
    - C2DRequestSpawnMe

---

## 다음 작업 우선순위 (제안)

1. **GameScene Init()** — Blueprint 데이터 기반 정적 오브젝트 렌더링
2. **C2DRequestSpawnMe 전송** — Init() 완료 후 서버에 로딩 완료 알림 및 동적 정보 요청
3. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
