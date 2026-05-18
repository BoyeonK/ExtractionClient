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
- **슬롯**: `ISlot` (일반) / `LSlot : ISlot` (로드아웃 전용, 타입 제약). 아이템 타입은 `ItemTypeHelper`로 item_id 범위 기반 판별. 로드아웃 인덱스는 `UI_Inventory.LOADOUT_START(=25)` 오프셋 사용. Weapon/Equipment는 `ISlot.CanMerge()`로 어느 슬롯에서도 수량 합산 불가
- **인벤토리 데이터 소유**: `LobbyScene`이 `_inventorySlots`, `_loadoutSlots`, `_warehouseSlots` 배열 소유. UI는 뷰 역할만 — `SetItemAtSlot()`이 scene setter + Refresh 담당. `SyncSlot` 없음
- **Shift+클릭 분할**: `LobbyScene.OnSlotClick()` — 수량을 절반으로 나눠 `FirstEmptySlot`에 배치. 인벤토리/창고 각각 독립 처리

## 씬 내장 UI (`LobbySceneUI/`)

UIManager가 아닌 씬 자체에 존재하는 MonoBehaviour UI 오브젝트. `LobbyScene.Init()`에서 `GameObject.Find()`로 바인딩 + `Init()` 호출:

| 클래스 | 역할 |
|--------|------|
| `LobbyReconfirmUI` | 확인/취소 팝업 |
| `LobbySettingUI` | 설정 패널 (Show/Hide) |
| `SelectedCharacter` | 선택된 캐릭터 모델 표시 (HB0/1/2Selected 활성화 전환). `LobbyScene.SetCharacterType()` → `SelectedCharacter.SetCharacterType()` 연동 |
