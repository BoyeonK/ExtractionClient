# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`LobbyScene`**: 로비 씬 진입점 — `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching). 모든 인증/매칭 흐름 및 ESC 키 동작을 담당
- **`LoadingScene`**: 매칭 성공 후 전환되는 로딩 씬 — `Managers.Scene.LoadSceneAsync()`로 GameScene 비동기 로딩, 90% 도달 시 `SendC2DRequestBlueprint()`, `staticObjectsLoadFlag = true`가 되면 `CompleteLoadSceneAsync()` 호출
- **`IngameScene`**: 인게임 맵 씬들의 공통 베이스(`BaseScene` 상속). `Init()`에서 `NextSceneStaticContext.ObjectDatas`를 순회해 정적 오브젝트 일괄 스폰 후 `ResetLoadSceneOp()` 호출, 이어서 씬 내장 UI 4종(`IngameInventoryUI`, `IngameDragGhost`, `InteractUI`, `IngameHealthBarUI`)을 `GameObject.Find()` + `Init()` 패턴으로 바인딩. `RequestSpawnMe()`로 서버에 스폰 요청 전송. 실제 맵 씬은 이 클래스를 상속해 구현한다
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
3. 두 조건(`_isGetResponseSpawnMe && SceneDynamicContext.IsComplete()`) 충족 시 `SpawnMeAndRequestPlayerObjects()` 호출 (이중 호출은 `_spawnCompleted` guard로 방지)
4. `SpawnMeAndRequestPlayerObjects()`: `PlayerObject` 인스턴스화 → `Setup` · `SetObjectId` → `_spawnCompleted = true` → `TryInitWeapon()` → 동적 오브젝트 스폰 → `SendC2DRequestSpawnPlayerObjects()` 전송
5. 서버 응답 `D2CSpawnPlayerObjects` → `PacketHandler`에서 `PlayerSpawnData` 리스트 변환 → 메인 스레드에서 `IngameScene.SpawnPlayerObjects()` 호출
6. `SpawnPlayerObjects()` 완료 시 `_operationFlag = true` → `OnUpdate()`의 플레이어 상태 전송 루프(0.1초 주기) 활성화 + `C2DNotifyLoadingComplete` Reliable 전송으로 서버에 로딩 완료 통보

### 초기 무기 장착 흐름

`_spawnCompleted`(스폰 완료)와 `_itemLoaded`(`D2CFullInventorySync` 수신 완료) 두 조건이 모두 충족되는 시점에 `TryInitWeapon()`이 `_inventory.InitWeapon()`을 1회 호출 (`_weaponInitialized` guard). 두 이벤트의 도착 순서가 보장되지 않으므로 양쪽 시점에서 모두 `TryInitWeapon()`을 호출한다:
- `SpawnMeAndRequestPlayerObjects()` — `_spawnCompleted = true` 직후
- `PacketHandler.Handle_D2CFullInventorySync` — `_itemLoaded = true` 직후

## 무기 프리팹 캐시 (`WeaponPrefabCache`)

`IngameScene`이 `Dictionary<int, GameObject> _weaponPrefabCache`를 보유. `Resources.LoadAll<GameObject>("Prefabs/Weapons")`로 전체 무기 프리팹을 1회 로드하여 `weaponId → prefab` 매핑. lazy init getter `WeaponPrefabCache`로 접근.

- `PlayerController.EquipWeapon()`과 `OppoPlayerController.EquipWeapon()` 모두 이 공유 캐시를 참조
- 프리팹 네이밍: `Weapon_{id}_{name}` — `_` 기준 split 후 두 번째 파트를 int 파싱하여 key로 사용
- 씬 언로드 시 자동 GC

## 인게임 인벤토리 (`IngameInventory`)

`IngameScene`이 `_inventory` 멤버로 보유하는 순수 C# 클래스 (MonoBehaviour 아님). 서버 주도의 `D2CFullInventorySync` 패킷으로 동기화된다.

| 필드 | 타입 | 설명 |
|------|------|------|
| `_owner` | `IngameScene` | 소유 씬 (늦은 참조, `GetIngameScene()`으로 접근) |
| `_inventoryVersion` | `uint` | 서버 주도 인벤토리 버전 |
| `_inventorySlots[25]` | `InventoryItem[]` | 범용 인벤토리 슬롯 |
| `_primaryWeapon` | `InventoryItem` | 주무기 슬롯 |
| `_secondaryWeapon` | `InventoryItem` | 보조무기 슬롯 |
| `_armor` | `InventoryItem` | 방어구 슬롯 |
| `_primaryWeaponMagazine` | `InventoryItem` | 주무기 탄창 슬롯 |
| `_secondaryWeaponMagazine` | `InventoryItem` | 보조무기 탄창 슬롯 |
| `_emptySlotIdx` | `int` | 비어있는 슬롯 인덱스 최솟값 (없으면 -1) |
| `_isPrimaryWeaponApplyed` | `bool` | 현재 주무기 적용 중 여부 (true=주무기, false=보조무기) |
| `_interactingContainerObjectId` | `uint` | 현재 열려있는 컨테이너의 오브젝트 ID (0이면 미열림) |
| `_interactingContainerVolume` | `uint` | 현재 컨테이너의 사용 가능 슬롯 수 |

- **`GetIngameScene()`**: 늦은 참조로 `Managers.Scene.CurrentScene as IngameScene`을 캐싱하여 반환
- **`ApplyFullSync()`**: 전체 인벤토리 일괄 덮어쓰기 (version + slots + weapon + armor + magazine), 완료 후 `FindEmptySlotIdx()` 자동 호출
- **`FindEmptySlotIdx()`**: `_inventorySlots` 순회하여 첫 번째 빈 슬롯 인덱스를 `_emptySlotIdx`에 갱신 후 반환
- **`InitWeapon()`**: 초기 무기 장착. 주무기 우선, 없으면 보조무기로 폴백하여 `EquipWeapon()` 호출 + `_isPrimaryWeaponApplyed` 설정
- **`ApplyWeapon(bool primary)`**: 주/보조무기 전환. `_isPrimaryWeaponApplyed` 상태와 무기 존재 여부를 검증 후 `IngameScene.PlayerController.EquipWeapon()` 호출
- **`SetInventorySlot(index, item)`** / **`SetPrimaryWeapon()`** / **`SetSecondaryWeapon()`** / **`SetArmor()`** / **`SetPrimaryWeaponMagazine()`** / **`SetSecondaryWeaponMagazine()`**: 개별 슬롯 갱신
- **`ApplyContainerSync(objectId, version, volume, slots)`**: 컨테이너 데이터 일괄 덮어쓰기 (메타데이터 + 슬롯 30개)
- **`ClearContainer()`**: 컨테이너 메타데이터·슬롯 전체 초기화
- 외부 접근: `ingameScene.Inventory.XXX`

## 다른 플레이어 관리

- **`_oppoPlayers`** (`Dictionary<uint, OppoPlayerController>`): objectId → OppoPlayerController 매핑
- **`SpawnPlayerObject(PlayerSpawnData)`**: 단일 OppoPlayer 스폰 (`_myObjectId` 필터링 + 중복 ObjectId 방어, 프리팹 `GameObject/OppoPlayerObject` 인스턴스화 → `Setup(characterType)` → `WeaponId != 0`이면 `EquipWeapon(weaponId)` → Dictionary 등록)
- **`SpawnPlayerObjects(List<PlayerSpawnData>)`**: 복수 플레이어 일괄 스폰 (내부에서 `SpawnPlayerObject()` 순회 호출)
- **`UpdatePlayerStates(List<PlayerStateData>)`**: 수신한 플레이어 상태 일괄 적용 (`_myObjectId` skip, `_oppoPlayers`에서 찾아 `ApplyState()` 호출, 미등록 objectId는 `C2DRequestSpawnByObjectId` 전송)
- **`PlayerSpawnData`** 구조체 (`SceneManagerEx.cs`): `ObjectId`, `CharacterType`, `WeaponId`, `Position`, `Rotation` — Protobuf 타입 격리를 위해 핸들러에서 변환 후 전달
- **`PlayerStateData`** 구조체 (`SceneManagerEx.cs`): `ObjectId`, `Position`, `Yaw`, `Pitch`, `Velocity`, `MovementState` — Protobuf 타입 격리를 위해 핸들러에서 변환 후 전달
- **`OppoPlayerController.Setup(characterType)`**: `HB{characterType}OppoPlayer` 모델 인스턴스화, RigBuilder·MultiAimConstraint·Animator 바인딩. `PlayerController.Setup()`과 동일 패턴 (Camera/ViewPoint/CharacterController 제외)
- **`OppoPlayerController.EquipWeapon(int weaponId)`**: `IngameScene.WeaponPrefabCache`에서 프리팹 조회 후 `WeaponSocket`에 인스턴스화. 기존 무기가 있으면 파괴 후 교체
- **`OppoPlayerController.ApplyState(PlayerStateData)`**: 수신한 상태를 `_targetPosition`·`_yaw`·`_pitch`·`_velocity`·`_movementState`에 저장. 첫 수신 또는 대규모 이동(sqrMagnitude>100)시에만 즉시 텔레포트, 그 외에는 `ProcessMovement()`가 `Vector3.Lerp`+`LerpAngle`로 매 프레임 보간
- **`OppoPlayerController.ProcessAnimation()`**: `_velocity`를 yaw 기준 로컬 좌표로 변환하여 Animator 파라미터(`MoveX`/`MoveY`/`MovingSpeed`) 구동. PlayerController와 동일 damping(0.1f) 적용
- **`OppoPlayerController.ProcessAim()`**: yaw+pitch 각도에서 방향 벡터를 계산하여 `_aimTarget`을 가슴 높이(yOffset=0.58f) + 100m 전방에 배치 → MultiAimConstraint가 상체 회전 처리

## 상호작용 상태 관리

`IngameScene`이 현재 상호작용 대상의 상태를 중앙에서 보유한다:

| 필드 | 타입 | 설명 |
|------|------|------|
| `_canInteract` | `bool` | 상호작용 가능 여부 |
| `_interactText` | `string` | 대상의 상호작용 안내 텍스트 |
| `_interactTarget` | `InteractableGameObjectController` | 현재 상호작용 대상 참조 |

- **`SetInteractState(bool, InteractableGameObjectController)`**: `PlayerController.CheckInteractable()`에서 매 프레임 호출하여 상태 갱신
- **`TryInteract()`**: `_canInteract` + `_interactTarget` null 가드 후 `_interactTarget.Interact()` 호출. `PlayerController`의 E키 입력(`Key.E`, `KeyState.Down`)에 바인딩됨
- **`OnUpdate()` 인터랙션 UI**: `_canInteract` 상태에 따라 `InteractUI.Show(text)` / `Hide()` 호출
- **`RequestOpenContainer(uint containerObjectId)`**: 컨테이너 열기 요청을 UDP로 전송 — `InteractableGameObjectController`의 `_onInteract` 델리게이트를 통해 간접 호출됨
- **`ShowOpenedContainer()`**: 컨테이너 응답 수신 후 호출. MyInventory+Equipment+Container 동기화 후 LootBox UI 활성화
- **`CloseContainer()`**: `C2DCloseContainer` 패킷 전송 + `Inventory.ClearContainer()` + UI 숨김 + 커서 잠금 복원
- **`SyncInventoryUI()`**: `D2CFullInventorySync` 수신 후 호출. MyInventory+Equipment UI 동기화

### 게임 오브젝트 컨트롤러 상속 구조

```
GameObjectController (MonoBehaviour)
├── PlayerController
├── OppoPlayerController
└── InteractableGameObjectController
      └── ContainerController
            └── TestItemBoxController
```

- **`InteractableGameObjectController`**: `_ingameScene` 참조(Init에서 획득), `_interactText`, `Action _onInteract` 델리게이트, `Interact()` 메서드
- **`ContainerController`**: `Init()`에서 `_onInteract += RequestOpenContainer` 구독
- **`PlayerController.CheckInteractable(RaycastHit)`**: `ProcessAim()` Raycast 결과를 활용, `GetComponentInParent<InteractableGameObjectController>()` + 거리 2 이하 체크 → `IngameScene.SetInteractState()` 호출
