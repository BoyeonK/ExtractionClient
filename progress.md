# 프로젝트 진행 상황

> 최종 수정: 2026-05-19
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
### 기타


- [x] (2026-05-18 #6) LobbyScene Init()에 `Application.runInBackground = true` 추가 — 포커스를 잃어도 게임이 계속 실행되도록 설정

- [x] (2026-05-19 #0) `LobbySettingUI` General 탭 — 마우스 감도 슬라이더(0.1~5.0, 0.1단위 스냅) + 값 텍스트 구현, `SettingManager` 연동
- [x] (2026-05-19 #1) `LobbySettingUI` Graphic 탭 — 화면 모드(창/전체화면 토글), 해상도(좌우 순환, `Define.Resolution` 참조), 프레임레이트(좌우 순환, `Define.FrameRate` 참조), FOV 슬라이더(60~90) 구현
- [x] (2026-05-19 #2) `LobbySettingUI` Audio 탭 — 마스터/이펙트/음악 볼륨 슬라이더(0~100) 구현 + `SettingManager.SetVolume()`에 마스터 볼륨 곱 적용, `SetMasterVolume()` 호출 시 Effect/Bgm 즉시 재적용
- [x] (2026-05-19 #3) `LobbySettingUI` 세팅 변경 감지 — `HasChanges()` 메서드 추가(8개 설정값을 SettingManager와 비교), `OnClickApply`/`OnClickCancel`에 적용하여 변경 없으면 팝업 없이 바로 닫기

### 버그 수정

- [x] (2026-05-18 #7) 빌드 환경 걷기 속도 이상 수정 — `SettingManager.Init()`에서 `Application.targetFrameRate` 기본 적용, `LobbySettingUI.ApplySetting()` 구현. 빌드 시 무제한 fps로 인해 walkSpeed의 프레임당 이동량이 CharacterController.minMoveDistance 이하로 떨어지는 문제 해결

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
2. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현
