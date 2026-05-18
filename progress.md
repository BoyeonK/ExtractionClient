# 프로젝트 진행 상황

> 최종 수정: 2026-05-18
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-05-18 #2) `UserState.Character` 추가 — `LobbyScene`에 `ShowCharacter()` 메서드 + ESC 복귀 처리, `UI_Header`에 `Btn_CHARACTER` 바인딩/이벤트/HeaderState별 활성화(Guest/Logined 활성, BeforeAuth/Matching 비활성)
- [x] (2026-05-18 #3) `UI_CharacterSelect` 구현 — HB0/HB1/HB2 텍스트 클릭으로 캐릭터 선택(색상 피드백), SelectBtn으로 `LobbyScene.SetCharacterType()` 호출 후 메인 복귀. `LobbyScene`에 캐싱/표시/비활성화 연동 완료
- [x] (2026-05-18 #4) `SelectedCharacter` 구현 — `SelectedCharacter.cs` 완성(HB0/1/2Selected 바인딩·SetCharacterType으로 활성화 전환), `LobbyScene.Init()`에서 `GameObject.Find`→Init→초기 타입 적용, `SetCharacterType()`에서 연동
- [x] (2026-05-18 #5) `UI_CharacterSelect` 즉시 선택 + Description 연동 — HB클릭 시 즉시 `SetCharacterType` 호출(SelectedCharacter 즉시 반영), SelectBtn→BackToLobbyMain 단순화, `Refresh()`에서 `SelectedCharacterType` Getter로 동기화, `Define.CharacterDescriptions`로 Description 텍스트 표시

### 네트워크
- [x] (2026-05-16 #1) `OppoPlayerController` 보간/애니메이션/에임 구현 — `ProcessMovement()`에 `Vector3.Lerp`+`LerpAngle` 위치·회전 보간, `ProcessAnimation()`에 velocity→로컬좌표 변환 후 Animator 파라미터 구동(MoveX/MoveY/MovingSpeed), `ProcessAim()`에 yaw+pitch→aimDir 계산으로 `_aimTarget` 배치. `ApplyState()`를 보간 지원 구조로 수정(첫 수신/대규모 이동만 즉시 텔레포트)

### 기타

- [x] (2026-05-18 #0) LobbyScene Init()에 창모드(1280x720) 전환 코드 추가 — `Screen.SetResolution(1280, 720, FullScreenMode.Windowed)` 호출
- [x] (2026-05-18 #1) SettingManager에 설정 변수 추가 — `_ingameMouseSensitivity`, `_isWindow`, `_masterVolume`, `_resolution`, `_frameRate`, `_fov` 필드 + 각각 Getter/Setter 메서드 구현. `Define.cs`에 `Resolution`/`FrameRate` enum 및 `ResolutionValues` Dictionary 추가
- [x] (2026-05-16 #2) `_operationFlag` 플래그 분리 + `C2DNotifyLoadingComplete` 패킷 추가 — re-entrance guard를 `_spawnCompleted`로 분리, `_operationFlag`는 `SpawnPlayerObjects()` 완료 후 설정으로 이동(전체 초기화 완료 게이트), `C2DNotifyLoadingComplete` proto 정의 + `UDPManager.SendC2DNotifyLoadingComplete()` 추가, `_operationFlag = true` 시점에 서버로 Reliable 전송
- [x] (2026-05-16 #3) HeartBeat 주기 및 Reliable 재전송 상수 튜닝 — 글로벌 환경(RTT ~50-200ms) 타겟으로 `HEARTBEAT_INTERVAL_SEC` 1→3초, `MIN_RTO_MS` 300→250ms, `MAX_RETRY` 10→7회 조정
- [x] (2026-05-17 #0) RTO/RTT 상수 재조정 — `MIN_RTO_MS` 250→50ms, `MAX_RTO_MS` 1000ms 상한 추가(`Mathf.Clamp` 적용), `MIN_RTT_MS` 20ms RTT 하한 추가(로컬 환경 대비)

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
