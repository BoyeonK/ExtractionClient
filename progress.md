# 프로젝트 진행 상황

> 최종 수정: 2026-06-01
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-06-01 #4) IngameScene Init() UI 바인딩 — `IngameInventoryUI`, `IngameDragGhost`, `InteractUI`, `IngameHealthBarUI` 4개 MonoBehaviour UI를 `GameObject.Find()` + `Init()` 패턴으로 연결. `InteractUI.cs` 신규 생성 (CanInteract 상태에 따라 텍스트 표시/숨김)
- [x] (2026-06-01 #5) InteractUI Show/Hide + OnUpdate 연동 — `InteractUI`에 `Show(text)`/`Hide()` 메서드 추가, `IngameScene.OnUpdate()`에서 `_canInteract` 상태에 따라 호출

### 네트워크
- [x] (2026-05-26 #0) `IngameInventory` 탄창 슬롯 추가 — `_primaryWeaponMagazine`·`_secondaryWeaponMagazine` 필드/프로퍼티/setter 추가, `ApplyFullSync()` 파라미터 확장, `PacketHandler`에서 `pkt.PrimaryWeaponMagazine`·`pkt.SecondaryWeaponMagazine` 변환·전달
- [x] (2026-06-01 #0) 컨테이너 패킷 핸들러/헬퍼 구현 — `UDPManager`에 `SendC2DRequestOpenContainer`·`SendC2DCloseContainer` 추가, `PacketHandler`에 `Handle_D2CResponseOpenContainer` 구현·등록, `IngameInventory.ApplyContainerSync()` 추가(슬롯 30개), `ContainerController.RequestOpenContainer()` 연결
- [x] (2026-06-01 #1) 네트워크 호출 IngameScene 경유 리팩토링 — `ContainerController.RequestOpenContainer()`의 직접 UDP 호출을 `IngameScene.RequestOpenContainer(uint)` 경유로 변경. 네트워크 호출 추적성 향상
- [x] (2026-06-01 #2) `InteractableGameObjectController` 중간 클래스 도입 — `GameObjectController` → `InteractableGameObjectController` → `ContainerController` 상속 구조. `_ingameScene` 참조·`_interactText`·`_onInteract` 델리게이트를 중간 클래스에서 관리, `Interact()` 메서드로 다형적 상호작용 호출
- [x] (2026-06-01 #3) PlayerController Raycast 기반 상호작용 감지 — `ProcessAim()` Raycast 결과를 활용한 `CheckInteractable()` 구현. 거리 2 이내 `InteractableGameObjectController` 감지 시 `IngameScene.SetInteractState()`로 상호작용 가능 여부·텍스트·대상 참조 전달

### 상호작용
- [x] (2026-06-01 #6) `IngameScene.TryInteract()` 메서드 추가 — `_canInteract` + `_interactTarget` null 가드 후 `_interactTarget.Interact()` 호출. 입력 바인딩의 진입점 역할
- [x] (2026-06-01 #7) E키 → TryInteract 바인딩 — `PlayerController.Init()`에서 `Key.E` → `TryInteract` 등록, `OnDestroy()`에서 해제. `_ingameScene.TryInteract()` 호출

### 기타

- [x] (2026-05-27 #0) `ItemTypeHelper` → `ItemDBHelper` 대체 — `ItemDB`/`WeaponSpec`/`ArmorSpec` 구조체 포함 통합 아이템 DB 헬퍼 도입. `ISlot`, `LSlot`, `UI_Shop` 참조 교체, `ItemTypeHelper.cs` 삭제

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
2. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현
