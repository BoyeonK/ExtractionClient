# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`LobbyScene`**: `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching)
- **`LoadingScene`**: 비동기 로딩 → 90% 도달 시 Blueprint 요청 → 응답 완료 시 씬 전환
- **`IngameScene`**: 인게임 맵 씬들의 공통 베이스. 정적 오브젝트 스폰 + 씬 내장 UI 바인딩 + 스폰 요청. 실제 맵 씬은 이 클래스를 상속
- 씬 전환: 반드시 `Managers.Scene` 사용

## 씬 전환 페이로드 (`GameSceneContext`)

`SceneManagerEx`가 `NextSceneStaticContext`(정적)와 `SceneDynamicContext`(동적) 두 인스턴스를 보유. `PacketHandler`에서 `AddObjectDatas()`로 누적, `IsComplete()`로 수신 완료 판정, `IngameScene.Init()`에서 소비.

## IngameScene 스폰 흐름

1. `RequestSpawnMe()` → `C2DRequestSpawnMe` 전송
2. 서버 응답: `D2CResponseSpawnMeSpawnSpot`(스폰 좌표) + `D2CResponseSpawnMeDynamicObjects`(동적 오브젝트)
3. 두 응답 모두 수신 시 `SpawnMeAndRequestPlayerObjects()` → 플레이어 인스턴스화 + `TryInitWeapon()` + 동적 오브젝트 스폰
4. 서버 응답 `D2CSpawnPlayerObjects` → 다른 플레이어 일괄 스폰
5. 완료 시 상태 전송 루프(0.1초) 활성화 + 로딩 완료 통보

### 초기 무기 장착 흐름

`_spawnCompleted`와 `_itemLoaded` 두 조건이 모두 충족될 때 `TryInitWeapon()` 1회 호출. 도착 순서가 보장되지 않으므로 양쪽 시점에서 모두 호출한다.

## 무기 프리팹 캐시 (`WeaponPrefabCache`)

`IngameScene`이 보유. `Resources.LoadAll<GameObject>("Prefabs/Weapons")`로 1회 로드, `weaponId → prefab` 매핑. 프리팹 네이밍: `Weapon_{id}_{name}`.

## 인게임 인벤토리 (`IngameInventory`)

`IngameScene`이 보유하는 순수 C# 클래스. 서버 주도 `D2CFullInventorySync`로 동기화.

- 슬롯 구조: `_inventorySlots[25]` + 무기 2 + 방어구 1 + 탄창 2 + 컨테이너 슬롯
- `_isPrimaryWeaponApplyed`: 현재 주무기/보조무기 전환 상태
- `PLAYER_OBJECT_ID(0xFFFFFFFF)`: objectId로 플레이어/컨테이너 슬롯을 구분하는 규칙
- 외부 접근: `ingameScene.Inventory.XXX`

## 다른 플레이어 관리

- `_oppoPlayers`: objectId → `OppoPlayerController` 매핑
- `UpdatePlayerStates()`: 미등록 objectId 수신 시 `C2DRequestSpawnByObjectId` 전송 (지연 스폰 대응)
- `PlayerSpawnData`/`PlayerStateData` 구조체(`SceneManagerEx.cs`): Protobuf 타입 격리를 위한 중간 변환 타입

## UI 열림 상태 관리 (`_uiOpenCount`)

레퍼런스 카운팅 방식. 새 UI 추가 시 반드시 따를 패턴:
- 열 때 `OnUIOpened()`, 닫을 때 `OnUIClosed()` 호출
- `IsAnyUIOpen`이 true이면 마우스룩·발사 차단

## 상호작용 상태 관리

`IngameScene`이 `_canInteract`/`_interactTarget`을 중앙 보유. `PlayerController.CheckInteractable()`이 매 프레임 갱신.

- `TryInteract()`: 컨테이너 열려있으면 닫기, 아니면 대상의 `Interact()` 호출 (E키)
- `_isContainerOpen`: objectId가 0일 수 있으므로 별도 bool 플래그 사용
- Deny 수신 시 `RequestRecentInventoryInfo()`로 재동기화

### 드래그 + 서버 요청/응답

- 드래그: `BeginDrag`/`UpdateDragPosition`/`EndDrag` → 고스트 제어
- 아이템 조작: `RequestInteractContainerObject()` (get=0, swap=1, merge=2)
- 장비 장착: `RequestEquipItem()` (장착=0, 해제=1)
- Deny 수신 시 `RequestRecentInventoryInfo()`로 서버 상태 재동기화
