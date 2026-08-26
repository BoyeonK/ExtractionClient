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
| `IngameCrosshair` | `static Create(IngameScene)` | **이 표에서 유일하게 씬 오브젝트가 없는 항목.** 아래 절 참조 |
| `IngameHealthBarUI` | `Init()` | HP/방어구 게이지. Fill 이미지의 `fillAmount`를 조작하므로 **대상 Image의 Type이 `Filled`여야 한다** — `Simple`이면 대입이 조용히 무시된다. 최대치는 `SetMaxHP`/`SetMaxShield`로 별도 주입하며, 최대 실드가 0이면(방어구 해제) 바를 비운다 |
| `IngameSettingUI` | `Init(IngameScene)` | 인게임 설정 창(ESC). 마우스 감도 + 볼륨 3종. 아래 절 참조 |

### `IngameCrosshair` — 규칙의 유일한 예외 (유지 확정)

**씬 오브젝트도 프리팹도 스프라이트도 없이 코드가 런타임에 세운다.** 자기 `Canvas`를 만들고 스프라이트 없는 `Image` 4개를 배치한다(스프라이트를 안 준 `Image`는 단색 사각형으로 그려져 자산이 필요 없다).

- **이 예외를 선례로 삼지 말 것.** 히트박스 반지름 튜닝에 조준 기준선이 급해서 씬 오브젝트 제작을 기다리지 않으려고 택한 방식이다. **런타임 검증에서 사용감이 나쁘지 않아 이대로 유지하기로 확정됐고**(2026-08-26, 사용자 판단), 정식 `IngameSceneUI` 자산으로 다시 만드는 것은 **`OPTION:`으로 내려갔다** — 없어도 동작에 문제가 없다. 모양·색·두께를 에디터에서 못 만지는 것이 대가이며, 그게 아쉬워지는 시점이 곧 이 옵션을 집어들 시점이다. **`IngameSettingUI`는 이 예외를 따르지 않고 프리팹으로 갔다**(아래 절) — 선례가 되지 않았다는 뜻이다
- **대체·롤백은 세 곳뿐이다** — 파일 삭제 / `IngameScene.Init()`의 `IngameCrosshair.Create(this)` 한 줄 / `PlayerController.CurrentSpread` 접근자(새 UI도 스프레드를 읽으므로 대체 시에는 남긴다). **`IngameScene`이 필드로 들고 있지 않은 것도 같은 이유** — 이 클래스가 씬을 참조해 스스로 갱신하므로 지울 때 씬 쪽에 남는 상태가 없다
- **`CanvasScaler`를 붙이지 않는다** — 1 유닛 = 1 픽셀이어야 각도 → 픽셀 환산이 스케일러 설정에 휘둘리지 않는다. 정식 자산으로 옮길 때 스케일러를 쓰면 **환산식을 캔버스 기준으로 다시 잡아야 한다**
- 벌어진 정도는 `PlayerController.CurrentSpread`(발사 원뿔 **반각**, 도)를 화면 절반 높이 기준으로 환산한다 — 같은 각도라도 화각·해상도에 따라 화면에서 차지하는 크기가 다르기 때문
- 숨김 조건은 `IsAnyUIOpen`과 `IsInputLocked`(사망 연출이 카메라를 가져간다) 둘뿐이다. **달리는 중에는 발사가 막히지만 숨기지 않는다** — 이동할 때마다 깜빡여 거슬린다

### `IngameSettingUI` — 인게임 설정 창 (ESC)

`LobbySettingUI`를 재사용하지 않고 **별도 UI로 만들었다.** 로비 쪽은 창모드·해상도·프레임·FOV까지 다루는데 **매치 중에 바꾸면 안 되는 값들이라**(사용자 확정), 인게임에는 **마우스 감도 + 볼륨 3종만** 담는다. 로비 씬을 필드로 직접 참조하는 것도 재사용을 막는 이유였다.

- **로비와 반영 정책이 갈린다 — 여기는 슬라이더를 움직이는 즉시 `Managers.Setting`에 반영하고, 취소하면 `Show()` 시점 스냅샷으로 되돌린다.** 볼륨은 귀로 들어야 맞출 수 있어서다. 그래서 **'적용'은 확정이 아니라 '이대로 닫기'**이고 로비처럼 재확인 팝업을 띄우지 않는다(인게임에 그 팝업이 없기도 하다)
- **되돌릴 때는 마스터 볼륨을 먼저 넣는다** — `SetMasterVolume()`이 효과·BGM을 다시 밀어내므로 순서가 뒤집히면 복원값이 덮인다
- 슬라이더 갱신은 전부 `SetValueWithoutNotify` — 그냥 넣으면 `onValueChanged`를 타고 **되돌리는 값이 다시 '변경'으로 재적용된다**
- **씬에는 활성 상태로 두고 `Init()`이 끈다.** `GameObject.Find()`는 비활성 오브젝트를 못 찾는다(`IngameInventoryUI`와 같은 규칙)
- 매치 이탈이 시작되면 `BeginMatchExit()`이 `Hide()`로 닫는다 — **`CancelAndHide()`가 아니다.** 이미 반영된 값을 되돌릴 이유가 없다

**프리팹 계층 규격** (`LobbySettingWindow`를 복제해 그래픽 탭을 들어낸 형태. 경로가 하나라도 어긋나면 `Util.BindComponent`가 `LogError`를 남기고 그 항목만 죽는다):

```
IngameSettingUI                         ← 루트. GameObject.Find로 잡으므로 이름 고정 + 씬에서 활성
└ IngameSettingWindow
  ├ TabBar/Tab_General                  (UI_EventHandler + Image)  └ TabText, ActiveIndicator
  ├ TabBar/Tab_Audio                    (UI_EventHandler + Image)  └ TabText, ActiveIndicator
  ├ ContentArea/GeneralContent
  │  └ MouseSensitivityRow/{SensitivitySlider, SensitivityValue}
  ├ ContentArea/AudioContent
  │  ├ MasterVolumeRow/{MasterVolumeSlider, MasterVolumeValue}
  │  ├ EffectVolumeRow/{EffectVolumeSlider, EffectVolumeValue}
  │  └ BgmVolumeRow/{BgmVolumeSlider, BgmVolumeValue}
  └ Footer/ButtonContainer/{ApplyButton, CancelButton}
```

- 볼륨 슬라이더는 Min 0 / Max 100(로비와 동일). **감도 슬라이더는 Min 0.1 / Max 3.0 / Value 1.0** — 값 × `PlayerController.MOUSE_SENSITIVITY_DEG_PER_PIXEL`(0.1)이 실제 도/픽셀이므로 이 범위가 0.01~0.3 deg/px에 해당한다

## 씬 내장 UI (`LobbySceneUI/`)

UIManager가 아닌 씬 자체에 존재하는 MonoBehaviour UI 오브젝트. `LobbyScene.Init()`에서 `GameObject.Find()`로 바인딩 + `Init()` 호출:

| 클래스 | 역할 |
|--------|------|
| `LobbyReconfirmUI` | 확인/취소 팝업 |
| `LobbySettingUI` | 설정 패널 (Show/Hide) |
| `SelectedCharacter` | 선택된 캐릭터 모델 표시 (HB0/1/2Selected 활성화 전환). `LobbyScene.SetCharacterType()` → `SelectedCharacter.SetCharacterType()` 연동 |
