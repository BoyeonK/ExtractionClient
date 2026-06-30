# UI 시스템

```
UI_Base (abstract)
├── UI_Scene    → Managers.UI.ShowSceneUI<T>() 로 표시
│   ├── UI_TestStart, UI_Auth, UI_Login, UI_Register
│   ├── UI_Header, UI_Inventory, UI_Warehouse, UI_Shop
└── UI_Popup    → Managers.UI.ShowPopupUI<T>() 로 표시
    ├── UI_OnlyConfirm, UI_ConfirmOrCancel
```

- 프리팹 경로: `Resources/Prefabs/UI/Scene/` 또는 `Resources/Prefabs/UI/Popup/`
- 모든 UI는 `@UI_Root` 하위에 배치됨 (`DontDestroyOnLoad`)
- **UI는 Destroy 금지 — SetActive 사용**. UIManager가 인스턴스를 캐시하므로 `DisableUI` / `EnableUI` 활용
- 정렬 순서: SceneUI는 0부터 증가, PopupUI는 20부터 증가, 긴급 팝업은 +1000
- `UI_Base.BindComponent<T>(path)`: 경로로 자식 컴포넌트를 찾아 반환 (없으면 예외 발생)
- **로비 슬롯**: `ISlot` (일반) / `LSlot : ISlot` (로드아웃 전용, 타입 제약). 아이템 타입은 `ItemDBHelper`로 item_id 기반 판별. 로드아웃 인덱스는 `UI_Inventory.LOADOUT_START(=25)` 오프셋 사용. Weapon/Equipment는 `ISlot.CanMerge()`로 어느 슬롯에서도 수량 합산 불가
- **인게임 슬롯**: `IngameISlot` (일반, slotIndex 0~) / `IngameLSlot : IngameISlot` (장비 전용, slotIndex=-1, `_acceptedType`으로 타입 제약)
  - **`SlotOwnerType` enum** (`PlayerInventory`, `Container`): 슬롯 소유자 구분. 서버 요청 시 `object_id` 결정에 사용
  - **`IngameISlot.Init(index, scene, ownerType)`**: 슬롯 인덱스·씬 참조·소유자 타입 설정 + eventHandler 바인딩
  - **`IngameLSlot.Init(scene, acceptedType, equipmentSlotType)`**: `base.Init(-1, scene, SlotOwnerType.PlayerInventory)` 호출. `_equipmentSlotType`(`uint`: 0=주무기, 1=보조무기, 2=방어구)·`_acceptedType` 설정. `CanAcceptItem()` 오버라이드로 아이템 타입 검증
  - **드래그 핸들러**: `OnBeginDrag` → `_scene.BeginDrag(this)`, `OnDrag` → `_scene.UpdateDragPosition()`, `OnEndDrag` → `_scene.EndDrag()`
  - **`OnDrop()` 분기**: source/target 조합에 따라 서버 요청 결정 — 장비↔일반: `RequestEquipItem()`, 일반↔일반: `RequestInteractContainerObject()` (get=빈 슬롯 이동, swap=교환, merge=합산)
- **인벤토리 데이터 소유**: `LobbyScene`이 `_inventorySlots`, `_loadoutSlots`, `_warehouseSlots` 배열 소유. UI는 뷰 역할만 — `SetItemAtSlot()`이 scene setter + Refresh 담당. `SyncSlot` 없음
- **Shift+클릭 분할**: `LobbyScene.OnSlotClick()` — 수량을 절반으로 나눠 `FirstEmptySlot`에 배치. 인벤토리/창고 각각 독립 처리

## 씬 내장 UI (`IngameSceneUI/`)

UIManager가 아닌 씬 자체에 존재하는 MonoBehaviour UI 오브젝트. `IngameScene.Init()`에서 `GameObject.Find()`로 바인딩 + `Init()` 호출:

| 클래스 | Init 시그니처 | 역할 |
|--------|---------------|------|
| `IngameInventoryUI` | `Init(IngameScene)` | 인게임 인벤토리 그리드 + 장비 슬롯. `SyncMyInventory()`·`SyncEquipment()`·`SyncContainer()`로 `IngameInventory` 데이터→UI 동기화 |
| `IngameDragGhost` | `Init()` | 드래그 중 아이템 고스트 이미지 |
| `InteractUI` | `Init(IngameScene)` | 상호작용 안내 텍스트 — `Show(text)`로 텍스트 설정+활성화, `Hide()`로 비활성화. `IngameScene.OnUpdate()`에서 `_canInteract` 상태에 따라 호출 |
| `IngameHealthBarUI` | 없음 | HP/방어구 게이지 |

## 씬 내장 UI (`LobbySceneUI/`)

UIManager가 아닌 씬 자체에 존재하는 MonoBehaviour UI 오브젝트. `LobbyScene.Init()`에서 `GameObject.Find()`로 바인딩 + `Init()` 호출:

| 클래스 | 역할 |
|--------|------|
| `LobbyReconfirmUI` | 확인/취소 팝업 |
| `LobbySettingUI` | 설정 패널 (Show/Hide) |
| `SelectedCharacter` | 선택된 캐릭터 모델 표시 (HB0/1/2Selected 활성화 전환). `LobbyScene.SetCharacterType()` → `SelectedCharacter.SetCharacterType()` 연동 |
