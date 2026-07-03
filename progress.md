# 프로젝트 진행 상황

> 최종 수정: 2026-07-03
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-06-10 #1) 컨테이너 열기/닫기 흐름 연결 — `IngameScene`에 `ShowOpenedContainer()`·`CloseContainer()`·`SyncInventoryUI()` 프록시 메서드 추가, `PacketHandler`에서 D2CFullInventorySync→SyncInventoryUI, D2CResponseOpenContainer→ShowOpenedContainer 호출 연결
- [x] (2026-06-10 #2) IngameInventory 컨테이너 메타데이터 추가 — `_interactingContainerObjectId`·`_interactingContainerVolume` 필드 추가, `ApplyContainerSync()` 시그니처 확장, `ClearContainer()` 메서드 추가
- [x] (2026-06-15 #2) UI 열림 시 커서 해제 + 마우스룩 중단 — `_uiOpenCount` 레퍼런스 카운팅, `IsAnyUIOpen` 프로퍼티, `OnUIOpened()`/`OnUIClosed()` 메서드 추가. `PlayerController.ProcessMouseLook()`에서 `IsAnyUIOpen` 체크 추가
- [x] (2026-06-18 #0) 드래그&드롭 아이템 이동 — `IngameISlot`에 `SlotOwnerType` enum·드래그 핸들러·`OnDrop()` 서버 요청 분기 구현, `IngameLSlot`에 `equipmentSlotType` 추가, `IngameScene`에 드래그 상태 관리+서버 요청/응답 메서드 추가, `PacketHandler`에 D2CResponseInteractContainerObject·D2CResponseEquipItem·D2CResponseInteractItemDeny 핸들러 등록, `UDPManager`에 SendC2DRequestInteractContainerObject·SendC2DRequestEquipItem 추가

### 네트워크
- [x] (2026-06-15 #0) E/I키로 컨테이너 UI 닫기 — `IngameScene`에 `_isContainerOpen` 플래그·`IsContainerOpen` 프로퍼티·`TryCloseContainerUI()` 추가. E키는 `TryInteract()` 내 분기, I키는 `IngameScene.Init()`에서 리스너 등록
- [x] (2026-06-15 #3) ContainerController 중복 상호작용 검사 — 서버 측에서 중복 요청을 무시하므로 클라이언트 측 수정 불필요 확인
- [x] (2026-07-01 #0) C2DRequestEquipItem.my_inventory_version 직렬화 추가 — `UDPManager.SendC2DRequestEquipItem`에 `myInventoryVersion` 파라미터 추가, `IngameScene.RequestEquipItem`에서 컨테이너 케이스일 때 `_inventory.InventoryVersion` 전달
- [x] (2026-07-01 #1) D2CResponseEquipItem.my_inventory_version 역직렬화 반영 — `PacketHandler.Handle_D2CResponseEquipItem`에서 추출 후 `ApplyEquipItem`에 전달, 컨테이너 케이스에서 `SetVersionByObjectId(PLAYER_OBJECT_ID, myInventoryVersion)` 추가 호출
- [x] (2026-07-01 #2) Deny 패킷 분리 — `D2CResponseInteractItemDeny` → `D2CResponseInteractContainerObjectDeny`(PktId 25) + `D2CResponseEquipItemDeny`(PktId 26) 두 핸들러로 교체, `IngameScene` 메서드도 `HandleInteractContainerObjectDeny` / `HandleEquipItemDeny`로 분리
- [x] (2026-07-03 #0) C2DRequestRecentInventoryInfo 패킷 추가 — Deny 수신 시 서버에 최신 인벤토리 재요청. `External_Protocol.proto`에 PktId 27 + 메시지 정의, `UDPManager`에 Send 함수, `IngameScene`에 `RequestRecentInventoryInfo()` 추가 (플레이어 인벤토리 항상 + 컨테이너 열림 시 컨테이너도 요청)

### 버그 수정
- [x] (2026-06-15 #1) IsContainerOpen 판정 버그 수정 — objectId=0인 컨테이너에서 `InteractingContainerObjectId != 0` 판정이 항상 false. `_isContainerOpen` bool 플래그 방식으로 교체

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

1. **인벤토리 열기/닫기 키바인딩** — Tab키로 MyInventory 토글 등 추가 입력 연결 (컨테이너 E/I키 닫기는 완료)
2. **실제 맵 씬에서 IngameScene 상속 완성** — `IngameScene`을 상속하는 맵별 씬 컴포넌트 구현
3. **설정값 실제 적용** — 해상도/창모드/FOV 변경이 `Screen.SetResolution()`, `Camera.fieldOfView` 등에 반영되도록 구현
