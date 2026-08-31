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
  - **인자는 UI 클래스 이름과 글자까지 같아야 한다** — 캐시 키가 타입 이름이라 **오타면 예외도 로그도 없이 아무 일도 하지 않는다.** 기준은 그것을 담은 필드 이름(`_matchProgressUI`)이 아니라 타입 이름(`UI_MatchProcess`)이다
- 정렬 순서: SceneUI는 0부터 증가, PopupUI는 20부터 증가, 긴급 팝업은 +1000
- `UI_Base.BindComponent<T>(path)`: 경로로 자식 컴포넌트를 찾아 반환 (없으면 예외 발생)
- **로비 슬롯**: `ISlot` (일반) / `LSlot : ISlot` (로드아웃 전용, 타입 제약). 아이템 타입은 `ItemDBHelper`로 item_id 기반 판별. 로드아웃 인덱스는 `UI_Inventory.LOADOUT_START(=25)` 오프셋 사용. Weapon/Equipment는 `ISlot.CanMerge()`로 어느 슬롯에서도 수량 합산 불가
- **인게임 슬롯**: `IngameISlot` (일반, slotIndex 0~) / `IngameLSlot : IngameISlot` (장비 전용, slotIndex=-1, `_acceptedType`으로 타입 제약)
  - **`SlotOwnerType` enum** (`PlayerInventory`, `Container`): 슬롯 소유자 구분. 서버 요청 시 `object_id` 결정에 사용
  - **`IngameISlot.Init(index, scene, ownerType)`**: 슬롯 인덱스·씬 참조·소유자 타입 설정 + eventHandler 바인딩
  - **`IngameLSlot.Init(scene, acceptedType, equipmentSlotType)`**: `base.Init(-1, scene, SlotOwnerType.PlayerInventory)` 호출. `_equipmentSlotType`(`uint`: 0=주무기, 1=보조무기, 2=방어구)·`_acceptedType` 설정. `CanAcceptItem()` 오버라이드로 아이템 타입 검증
  - **드래그 핸들러**: `OnBeginDrag` → `_scene.BeginDrag(this)`, `OnDrag` → `_scene.UpdateDragPosition()`, `OnEndDrag` → `_scene.EndDrag()`
  - **`OnDrop()` 분기**: source/target 조합에 따라 서버 요청 결정 — 장비↔일반: `RequestEquipItem()`, 일반↔일반: `RequestInteractContainerObject()` (get=빈 슬롯 이동, swap=교환, merge=합산)
- **슬롯 아이콘의 표시·숨김은 `Image.enabled`다 — 색을 건드리지 말 것**(`ISlot`·`IngameISlot`·`SSlot`·`LootContainerSlot` 공통). 알파를 토글하던 옛 방식은 `SetItem`이 RGB를 남기고 `ClearSlot`이 흰색으로 덮어, **슬롯 이력에 따라 같은 아이콘의 밝기가 달라졌다**(비워진 적 있는 칸과 처음부터 채워진 칸이 나란히 다르게 보인다)
  - **그래서 프리팹 `Fill`의 색이 곧 아이콘 틴트다.** 어두운 회색을 쓰는 것은 심미 판단으로 확정된 것이고 결함이 아니다 — 코드에서 흰색으로 덮어 "원래 밝기"로 되돌리지 말 것
  - **네 프리팹이 같은 값을 써야 한다** — 갈리면 전리품 아이콘만 인벤토리와 다른 밝기로 보인다
  - **스프라이트를 못 찾아도 `sprite`에 대입한다** — 안 그러면 **직전 아이템의 아이콘이 새 아이템의 수량과 함께 남는다.** 스프라이트 없는 `Image`는 흰 사각형으로 그려지므로 그때는 `enabled`를 꺼서 누락을 드러낸다(`IngameWeaponUI`와 같은 규칙)
- **인벤토리 데이터 소유**: `LobbyScene`이 `_inventorySlots`, `_loadoutSlots`, `_warehouseSlots` 배열 소유. UI는 뷰 역할만 — `SetItemAtSlot()`이 scene setter + Refresh 담당. `SyncSlot` 없음
- **Shift+클릭 분할**: `LobbyScene.OnSlotClick()` — 수량을 절반으로 나눠 `FirstEmptySlot`에 배치. 인벤토리/창고 각각 독립 처리

## UI 사운드 (`ui_submit` / `ui_return` / `inventory_change`)

전부 2D `Managers.Sound.Play` 계열이고 **`SoundPoint`(월드 3D)와 경계를 넘기지 말 것** — 넘기면 클릭음이 가슴팍에서 3D로 난다. **클립 이름을 호출부에서 만들지 않는다**(`SoundManager.PlayUISubmit`/`PlayUIReturn`/`PlayInventoryChange`) — 못 찾은 클립은 무음이고 로그도 안 남아, 호출부가 40곳이 넘는 상황에서 오타 하나가 그 버튼만 영구히 죽인다.

**분기 기준(확정)** — 확인·진입·적용·값 변경은 `ui_submit`, 취소·뒤로가기·닫기는 `ui_return`. **기준 없이 자리마다 고르지 말 것**: 같은 성격의 버튼이 화면마다 다른 소리를 낸다.

- **조기 return 가드 뒤에서 낼 것** — 앞에 두면 아무 일도 안 하는 클릭(빈 로그인 폼, 이미 선택된 탭, 접속 중 연타)이 성공한 것처럼 들린다
- **팝업을 여는 버튼은 소리를 내지 않는다** — 팝업이 자기 확인·취소음을 내므로 두 번 울린다. 해당하는 곳은 헤더 로그아웃과 `LobbySettingUI`의 적용·취소(변경이 있을 때)다
- **`Hide()`처럼 코드가 부르는 경로가 있는 함수 안에 넣지 말 것** — `IngameSettingUI.Hide()`는 `BeginMatchExit()`도 부르므로 죽거나 귀환할 때마다 UI음이 난다. 버튼 등록 지점에서 낸다. `ChangeTab()`도 같은 이유로 `Init()`에서 불린다
- **씬을 바꾸는 `ui_submit`은 소리가 잘린다** — `Managers.Clear()`가 `SoundManager.Clear()`로 모든 소스를 `Stop()`한다. 매치 시작·로비 복귀·로그아웃이 해당하며 코드로 막을 방법이 마땅치 않다

**`inventory_change`는 자리가 둘로 갈린다 — 판정 주체가 다르기 때문이다.**

| | 판정 | 소리를 내는 곳 |
|---|---|---|
| 로비 (`ISlot.OnDrop`, `LobbyScene.OnSlotClick`) | 로컬 | 조작 지점 그대로 |
| 인게임 (`IngameISlot.OnDrop`) | **서버** | `ApplyInteractContainerObject` / `ApplyEquipItem` |

- **인게임은 요청 지점에서 내지 말 것** — 거부된 조작이 성공한 것처럼 들린다
- **`ApplyFullSync`에는 붙이지 말 것** — 조작이 아니라 동기화라 재장전 응답·재동기화마다 소리가 난다

## 씬 내장 UI (`IngameSceneUI/`)

UIManager가 아닌 씬 자체에 존재하는 MonoBehaviour UI 오브젝트. `IngameScene.Init()`에서 바인딩 + `Init()` 호출:

> **바인딩은 반드시 `BaseScene.BindSceneComponent<T>(오브젝트명)`로 할 것.** 규칙·근거는 `Scenes/CLAUDE.md`의 '씬 배치 오브젝트 바인딩'에 있다 — **UI 전용 함수가 아니다.**

**이 UI들은 각자 자기 `Canvas`를 들고 `sortingOrder`를 90~100대로 쓴다.** `UIManager`가 매기는 값(SceneUI 0~, PopupUI 20~)보다 크지만 **결함이 아니라 이쪽의 관례다** — 인게임 HUD는 `UIManager` 계열과 같은 스택에서 겨루지 않는다. 값이 커 보인다고 낮추지 말 것.

| 클래스 | Init 시그니처 | 역할 |
|--------|---------------|------|
| `IngameInventoryUI` | `Init(IngameScene)` | 인게임 인벤토리 그리드 + 장비 슬롯. `SyncMyInventory()`·`SyncEquipment()`·`SyncContainer()`로 `IngameInventory` 데이터→UI 동기화 |
| `IngameDragGhost` | `Init()` | 드래그 중 아이템 고스트 이미지 |
| `InteractUI` | `Init(IngameScene)` | 상호작용 안내 텍스트 — `Show(text)`로 텍스트 설정+활성화, `Hide()`로 비활성화. `IngameScene.OnUpdate()`에서 `_canInteract` 상태에 따라 호출 |
| `IngameCrosshair` | `static Create(IngameScene)` | **이 표에서 유일하게 씬 오브젝트가 없는 항목.** 아래 절 참조 |
| `IngameHealthBarUI` | `Init()` | HP/방어구 게이지. Fill 이미지의 `fillAmount`를 조작하므로 **대상 Image의 Type이 `Filled`여야 한다** — `Simple`이면 대입이 조용히 무시된다. 최대치는 `SetMaxHP`/`SetMaxShield`로 별도 주입하며, 최대 실드가 0이면(방어구 해제) 바를 비운다 |
| `IngameSettingUI` | `Init(IngameScene)` | 인게임 설정 창(ESC). 마우스 감도 + 볼륨 3종. 아래 절 참조 |
| `IngameKillLogUI` | `Init(IngameScene)` | 킬 피드. `SingleKillLog`를 찍어내고 목록으로 관리한다. 아래 절 참조 |
| `IngameWeaponUI` | `Init()` | 손에 든 무기 이름 + 탄창 잔량 + 예비탄. 아래 절 참조 |
| `IngameEscapeCountdownUI` | `Init()` | 귀환 승인 후 5초 카운트다운. **값을 스스로 구하지 않고 `IngameScene`이 민다** — 켜고 끄는 것도 씬의 `SetEscapeSequence()` 한 곳이다(`Scenes/CLAUDE.md`의 '귀환 카운트다운'). `FixedText`는 코드가 건드리지 않는 고정 문안이라 **에디터에서 채워둘 것** |
| `IngameDamageIndicatorUI` | `Init()` | 피격 방향 표시. **자신은 컨테이너일 뿐이고 `ShowIndicator(degree)`가 호출마다 `IngameDamageIndicatorContent`를 찍어낸다** — 개수 상한이 없고 각 인스턴스가 0.3초 뒤 **스스로** 파괴된다(`SingleKillLog`가 부모를 거치는 것과 갈린다: 여기는 부모가 목록을 들지 않는다). 자식이 없으면 아무것도 그리지 않으므로 `Init()`이 끄지 않는다 |
| `IngameEscUI` | `Init(IngameScene)` | ESC로 여는 옵션 / 게임 종료 2지 선택. 한 오브젝트가 **선택 패널과 종료 확인 패널 둘을 갈아 끼운다**(`ActiveOptionOrExitUI`/`ActiveExitConfirmOrCancelUI`). 열림 카운트·종료 실행 규칙은 `Scenes/CLAUDE.md`의 'ESC 창과 게임 종료' |
| `IngameTimeoutUI` | `Init()` | 매치 잔여 시간을 `mm:ss`로 표시. **값을 스스로 구하지 않고 `IngameScene.UpdateTimeoutDisplay()`가 민다**(규칙은 `Scenes/CLAUDE.md`의 '매치 마감 시각'). **이 표에서 유일하게 `Init()`이 스스로 끄지 않는 항목** — 매치 내내 떠 있으므로 비활성화 호출을 넣지 말 것 |
| `IngameStaminaBarUI` | `Init()` | 달리기 스태미나 게이지. `SetStamina(current, max)` + `SetVisible(bool)`로 씬이 민다. **평소에는 숨어 있고 달리는 중이거나 스태미나가 문턱 이하일 때만 보인다** — 조건 판단은 씬에 있다(아래). **`StaminaBarFill`의 Image Type이 `Filled`여야 한다**(체력바와 같은 함정) |

### `IngameKillLogUI` / `SingleKillLog` — 킬 피드

**이 표에서 유일하게 자식 프리팹을 런타임에 찍어내는 항목이다.** `IngameScene.HandlePlayerKilled()`가 `MakeSingleKillLog(killer, victim)`를 부르면 `KillLogContainer` 하위에 `SingleKillLog`가 하나 생기고, 각 로그가 5초 뒤 스스로 사라진다.

- **부모가 목록을 들고 자식이 자기 수명을 관리한다.** `SingleKillLog`는 `Invoke`로 시간을 재다가 **스스로 파괴하지 않고 `_parentUI.RemoveSingleKillLog(_idx)`를 거친다** — 목록에서 빼는 것과 파괴가 한 곳에서 일어나야 딕셔너리에 죽은 참조가 남지 않는다
- **`_killLogs` 등록은 `Init()`보다 먼저 해야 한다** — `Init()`이 수명 타이머를 거는 지점이라, 등록이 늦으면 만료 시점에 목록에서 못 찾아 오브젝트가 화면에 영영 남는다
- **표시명은 `IngameScene.KillLogName()` 한 곳에서만 만든다.** 서버가 통보에 실어주는 이름(userId)을 그대로 쓰고, 이름이 비어 오면 `objectId=N`으로 폴백한다. **자기 자신도 특별 취급하지 않는다** — 로그용 `DescribePlayer()`가 `나`·`weaponId`·미스폰 여부까지 붙이는 것과 갈리는 지점이며, 그쪽은 화면에 올리기엔 길다
- **가해자 이름이 비면 빈 문자열을 넘긴다**(`KillerId` 텍스트가 비어 보인다). 판정은 `HandlePlayerKilled` 한 곳에서 **objectId가 아니라 이름으로** 하며, `killer_object_id == 0xFFFFFFFF`도 이름이 비어 오므로 함께 걸린다

**프리팹 계층 규격** (전부 `transform.Find`라 **직계 자식만 본다** — 어긋나면 `Init()`에서 NRE):

> **이 트리는 `Assets/Resources/CLAUDE.md`에도 같은 내용이 있다. 한쪽만 고치지 말 것.** 그쪽이 자산을 만들 때의 규격이고, 여기는 코드가 그것을 어떻게 찾는지와 어겼을 때의 증상이다.

```
IngameKillLogUI                 ← GameObject.Find로 잡으므로 이름 고정 + 씬에서 활성
└ KillLogContainer              ← SingleKillLog가 이 아래에 붙는다
SingleKillLog                   ← Resources/Prefabs/UI/IngameSceneUI/
├ KillIcon                      코드가 찾지 않는 장식 오브젝트
├ KillerId                      (TextMeshProUGUI)
└ VictimId                      (TextMeshProUGUI)
```

- **가해자 이름이 비면 `KillerId`만 빈 문자열이 되고 `KillIcon`은 그대로 남는다** — 가해자 없는 죽음은 `[아이콘] [피해자]` 꼴로 보인다. 아이콘까지 숨기려면 `SingleKillLog.Init()`에서 함께 꺼야 한다

### `IngameWeaponUI` — 무기·탄약 표시

**값을 스스로 구하지 않는다.** `IngameScene.SyncWeaponUI()`가 이름·무기 id·탄창 잔량·예비탄을 계산해 `SetWeapon(name, weaponId, magazine, spare)`로 밀어넣고, 이 클래스는 대입과 스프라이트 로드만 한다. 그래서 `Init()`이 무인자이고 씬 참조를 들지 않는다.

- **갱신 지점이 다섯인데 전부 `SyncWeaponUI()` 하나를 거친다** — `SyncInventoryUI()`(전체 동기화·재장전 응답·아이템 조작이 이미 여기로 모인다) / `SyncHeldWeapon()` / `ApplyServerWeaponState()` / `TryInitWeapon()` / `PlayerController.Fire()`의 탄약 차감 직후. **각자 텍스트를 건드리게 만들지 말 것** — 한 경로만 빠져도 표시가 조용히 어긋난다
- **`SyncHeldWeapon()` 쪽을 빠뜨리면 안 된다.** `ApplyEquipItem()`이 `SyncInventoryUI()`를 먼저 부르고 `SyncHeldWeapon()`을 나중에 부르는 순서라, 앞의 호출 시점에는 손에 든 무기가 아직 낡은 값이다. 양쪽 다 걸어 **마지막 호출이 이기게** 둔다(텍스트 대입이라 중복 1회는 비용이 없다)
- **`SyncWeaponUI()` 호출은 `SyncInventoryUI()`의 `_ingameInventoryUI` null 가드보다 앞이다** — 무기 표시는 인벤토리 UI 존재에 종속되지 않는다(`SyncHealthBarMax()`와 같은 이유)
- **탄창 잔량의 출처는 `IngameInventory.CurrentMagazine`이고 발사 판정과 같은 것을 쓴다.** 여기서 `IsPrimaryWeaponApplyed`를 다시 판단하지 말 것 — 쏘는 탄창과 보여주는 탄창이 갈린다
- **예비탄은 인벤토리 25칸만 센다**(`IngameInventory.CountAmmo`). 장착된 탄창은 `MagazineAmmoCount` 쪽이라 이중 계상이 되고, 컨테이너 슬롯은 내 소지품이 아니다. 탄종은 `WeaponSpec.AmmoType`(= 탄약 item_id)
- **무기 이미지는 `weaponId`가 바뀔 때만 로드한다**(`_shownWeaponId` 가드). `SetWeapon()`이 발사마다 불리므로 가드가 없으면 총을 쏠 때마다 `Resources.Load`가 돈다. **이미지 전용 갱신 경로를 따로 만들지 말 것** — id의 출처가 서버가 확정한 '손에 든 무기'라 **가드를 통과하는 시점이 곧 교체 확정 시점**이고, 경로를 나누면 위의 '갱신 지점 다섯' 함정이 그대로 되살아난다
- **탄창 용량은 표시하지 않는다**(확정). 프리팹에 자리가 없고 `30/30` 꼴로 합치지도 않는다
- **맨손은 설계상 없는 상태다.** 숨김 처리를 만들지 말 것 — 무기 슬롯 둘을 모두 비우는 인벤토리 조작 중에만 잠깐 지나가며, 그때는 값만 비우고 창은 그대로 둔다

**프리팹 계층 규격** (전부 `transform.Find`라 **직계 자식만 본다**):

> **이 트리는 `Assets/Resources/CLAUDE.md`에도 같은 내용이 있다. 한쪽만 고치지 말 것.**

```
IngameWeaponUI                  ← GameObject.Find로 잡으므로 이름 고정 + 씬에서 활성
└ WeaponUIWindow
  ├ Header/{AccentLine, WeaponName}       AccentLine은 코드가 찾지 않는 장식
  └ WeaponInfoPanel/{WeaponImage, MagazineAmmoCount, RemainAmmoCount}
```

- `WeaponImage`에는 `Images/WeaponSprites/weapon_sprite_{무기 item_id}`를 코드가 넣는다. **`Preserve Aspect`는 꺼둔 것이 의도다** — 스프라이트가 √2:1이고 슬롯 비율이 그에 가까워 약간의 찌그러짐을 감수하기로 했다(빠뜨린 설정으로 보고 켜지 말 것)
- **스프라이트가 없으면 `Image.enabled`를 끈다** — 스프라이트 없는 `Image`는 흰 사각형으로 그려지므로 맨손·파일 누락이 그대로 노출된다. **창을 숨기지 말라는 위 규칙과 다른 이야기다**(창은 그대로 두고 이미지만 끈다)
- 그래픽 7개 모두 `raycastTarget`이 꺼져 있다. HUD가 클릭을 가로채지 않아야 하기 때문이며, **새 요소를 추가할 때도 꺼둘 것**(인벤토리를 열어 커서가 풀린 상태에서 겹치는 영역의 드래그를 먹는다)

### `IngameCrosshair` — 규칙의 유일한 예외 (유지 확정)

**씬 오브젝트도 프리팹도 스프라이트도 없이 코드가 런타임에 세운다.** 자기 `Canvas`를 만들고 스프라이트 없는 `Image` 4개를 배치한다(스프라이트를 안 준 `Image`는 단색 사각형으로 그려져 자산이 필요 없다).

- **이 예외를 선례로 삼지 말 것.** 히트박스 반지름 튜닝에 조준 기준선이 급해서 씬 오브젝트 제작을 기다리지 않으려고 택한 방식이다. **런타임 검증에서 사용감이 나쁘지 않아 이대로 유지하기로 확정됐고**(2026-08-26, 사용자 판단), 정식 `IngameSceneUI` 자산으로 다시 만드는 것은 **`OPTION:`으로 내려갔다** — 없어도 동작에 문제가 없다. 모양·색·두께를 에디터에서 못 만지는 것이 대가이며, 그게 아쉬워지는 시점이 곧 이 옵션을 집어들 시점이다. **`IngameSettingUI`는 이 예외를 따르지 않고 프리팹으로 갔다**(아래 절) — 선례가 되지 않았다는 뜻이다
- **대체·롤백은 세 곳뿐이다** — 파일 삭제 / `IngameScene.Init()`의 `IngameCrosshair.Create(this)` 한 줄 / `PlayerController.CurrentSpread` 접근자(새 UI도 스프레드를 읽으므로 대체 시에는 남긴다). **`IngameScene`이 필드로 들고 있지 않은 것도 같은 이유** — 이 클래스가 씬을 참조해 스스로 갱신하므로 지울 때 씬 쪽에 남는 상태가 없다
- **`CanvasScaler`를 붙이지 않는다** — 1 유닛 = 1 픽셀이어야 각도 → 픽셀 환산이 스케일러 설정에 휘둘리지 않는다. 정식 자산으로 옮길 때 스케일러를 쓰면 **환산식을 캔버스 기준으로 다시 잡아야 한다**
- 벌어진 정도는 `PlayerController.CurrentSpread`(발사 원뿔 **반각**, 도)를 화면 절반 높이 기준으로 환산한다 — 같은 각도라도 화각·해상도에 따라 화면에서 차지하는 크기가 다르기 때문
- 숨김 조건은 `IsAnyUIOpen`과 `IsInputLocked`(사망 연출이 카메라를 가져간다) 둘뿐이다. **달리는 중에는 발사가 막히지만 숨기지 않는다** — 이동할 때마다 깜빡여 거슬린다

### `IngameSettingUI` — 인게임 설정 창 (ESC)

`LobbySettingUI`를 재사용하지 않고 **별도 UI로 만들었다.** 로비 쪽은 창모드·해상도·프레임·VSync·FOV까지 다루는데 **매치 중에 바꾸면 안 되는 값들이라**(사용자 확정), 인게임에는 **마우스 감도 + 볼륨 3종만** 담는다. 로비 씬을 필드로 직접 참조하는 것도 재사용을 막는 이유였다.

- **로비와 반영 정책이 갈린다 — 여기는 슬라이더를 움직이는 즉시 `Managers.Setting`에 반영하고, 취소하면 `Show()` 시점 스냅샷으로 되돌린다.** 볼륨은 귀로 들어야 맞출 수 있어서다. 그래서 **'적용'은 확정이 아니라 '이대로 닫기'**이고 로비처럼 재확인 팝업을 띄우지 않는다(인게임에 그 팝업이 없기도 하다)
- **되돌릴 때는 마스터 볼륨을 먼저 넣는다** — `SetMasterVolume()`이 효과·BGM을 다시 밀어내므로 순서가 뒤집히면 복원값이 덮인다
- 슬라이더 갱신은 전부 `SetValueWithoutNotify` — 그냥 넣으면 `onValueChanged`를 타고 **되돌리는 값이 다시 '변경'으로 재적용된다**
- **씬에는 활성 상태로 두고 `Init()`이 끈다.** `GameObject.Find()`는 비활성 오브젝트를 못 찾는다(`IngameInventoryUI`와 같은 규칙)
- 매치 이탈이 시작되면 `BeginMatchExit()`이 `Hide()`로 닫는다 — **`CancelAndHide()`가 아니다.** 이미 반영된 값을 되돌릴 이유가 없다

**프리팹 계층 규격** (`LobbySettingWindow`를 복제해 그래픽 탭을 들어낸 형태. 경로가 하나라도 어긋나면 `Util.BindComponent`가 `LogError`를 남기고 그 항목만 죽는다):

> **이 트리는 `Assets/Resources/CLAUDE.md`에도 같은 내용이 있다. 한쪽만 고치지 말 것.**

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

- 볼륨 슬라이더는 Min 0 / Max 100(로비와 동일). **감도 슬라이더는 Min 0.1 / Max 5.0 / Value 1.0이고 로비 쪽과 같아야 한다** — 값 × `PlayerController.MOUSE_SENSITIVITY_DEG_PER_PIXEL`(0.1)이 실제 도/픽셀이므로 이 범위가 0.01~0.5 deg/px에 해당한다. 갈리면 **같은 설정을 어느 창에서 만졌느냐로 상한이 달라진다**

## 씬 내장 UI (`GameResultSceneUI/`)

`GameResultSceneUI`(씬 배치) + `LootContainerSlot`(경로 로드로 찍어낸다). `GameResultScene.Init()`이 `BindSceneComponent`로 잡고 `Init(scene, result)`를 부른다.

**연출 순서**: 0.3초 → 결과 문구 → 0.3초 → **성공이면** 전리품 슬롯 0.2초 간격 / **실패면** 분실 문구 → 0.3초 → 확인 버튼. 상수 넷은 이 클래스가 유일한 출처다. 프리팹 계층 규격은 `Assets/Resources/CLAUDE.md`에 있다.

- **문구·색과 전리품/분실 갈래는 `ClassifyResult()` 한 곳에서 나온다**(`_isExtractSuccess`). 갈래를 따로 판단하면 사유가 추가될 때 **"탈출 성공"인데 분실 문구가 뜨는** 상태가 된다
- **실패일 때 전리품을 안 그리는 근거는 가드가 아니라 빈 목록이다** — `BuildLootList()`를 아예 부르지 않아 생성 루프 둘(코루틴·`CompleteImmediately`)이 자연히 비워진다. **루프에 갈래 가드를 덧대지 말 것**(두 곳이 되고 한쪽이 빠진다)
- **연출의 각 단계는 메서드 하나이고 코루틴과 `CompleteImmediately()`가 같은 메서드를 부른다.** 단계를 추가할 때도 이 형태를 지킬 것 — 한쪽에만 넣으면 건너뛰기에서만 빠지는 표시가 생긴다
- **분실 문구는 전리품 생성이 시작될 자리에서 켠다.** 요약 문구와 함께 켜면 "전리품을 채우는 대신"이라는 시점이 어긋난다. `_hasResult == false`(결과 없이 진입)에서는 켜지 않는다 — 잃은 것이 없는데 잃었다고 표시하는 꼴이 된다
- 실패일 때 `LootContainer`는 **비어 있는 채로 남는다**(확정). 숨기지 않는다

- **결과는 씬이 값으로 넘기고 UI는 그것만 본다. `Managers.Scene.LastGameResult`를 UI가 다시 읽지 말 것** — `MoveToLobby()`가 `ClearGameResult()`를 즉시 부르고 `LoadScene`만 다음 프레임으로 미루므로, **연출이 도는 중에 원본이 null이 되는 창이 실재한다**
- **중단 지점은 `CompleteImmediately()` 하나다.** 연출을 끊고 남은 슬롯을 즉시 다 만든 뒤 버튼을 켠다. 지금 소비자는 '결과 없이 진입'뿐이지만 **연출 건너뛰기가 붙어도 여기를 부른다** — 단계마다 중단 분기를 흩으면 그때 고칠 곳이 넷으로 늘어난다
- **결과가 null이면 연출을 건너뛰고 즉시 버튼을 켜야 한다.** Enter가 버튼 활성 전까지 막히므로, 안 켜면 **로비로 돌아갈 수단이 아예 없어 영구 정지한다**
- **표시 순서는 `BuildLootList()`가 먼저 확정한다**(주무기 → 보조무기 → 방어구 → 인벤토리 25칸, null 제외). 루프 안에서 갈래를 판단하면 `CompleteImmediately()`가 '남은 것부터 끝까지'를 표현할 수 없다
- **`SpawnNextLootSlot()`은 카운터를 먼저 올린다** — 조기 반환에서도 전진해야 `CompleteImmediately()`의 루프가 멈춘다
- **`MatchExitReason.ConnectionLost`는 `Dead`와 같은 표시이되 `default`에 묻지 않는다**(확정) — 묻어두면 사유가 추가될 때 새 사유가 조용히 '사망'으로 뜬다. `default`는 `LogError` + 사망 표시다
- **수량 표기는 슬롯 출처가 아니라 아이템 타입으로 가른다** — `ItemDBHelper.GetType`이 `Weapon`·`Armor`면 빈 문자열이다(`ISlot.CanMerge`와 같은 술어·같은 이유: 합산되지 않는 타입이라 수량 개념이 없다). 인벤토리 칸에 들어 있던 장비도 같은 규칙을 받는다
- **`Fill`은 스프라이트 로드 실패 시 `Image.enabled`를 끈다** — 스프라이트 없는 `Image`는 흰 사각형으로 그려진다(`IngameWeaponUI`와 같은 규칙)

## 씬 내장 UI (`LobbySceneUI/`)

UIManager가 아닌 씬 자체에 존재하는 MonoBehaviour UI 오브젝트. `LobbyScene.Init()`에서 `GameObject.Find()`로 바인딩 + `Init()` 호출:

| 클래스 | 역할 |
|--------|------|
| `LobbyReconfirmUI` | 확인/취소 팝업 |
| `LobbySettingUI` | 설정 패널 (Show/Hide) |
| `SelectedCharacter` | 선택된 캐릭터 모델 표시 (HB0/1/2Selected 활성화 전환). `LobbyScene.SetCharacterType()` → `SelectedCharacter.SetCharacterType()` 연동 |

### 로비 ESC 우선순위 (`LobbyScene.OnEscapeInput`)

한 번의 입력은 가장 위에 있는 것 하나만 소비한다. 순서는 **팝업 → 설정 창 → `_lobbyState` 분기**이고, 앞의 둘은 `_lobbyState`와 무관한 오버레이라 **분기 바깥**에 있다 — 안으로 넣으면 상태마다 가드가 복제되고 새 상태에서 하나가 빠져 **팝업이 뜬 채 뒤 화면만 바뀐다.**

- **ESC는 모든 층에서 '취소'다**(확정). 설정 창은 취소 버튼과 같은 경로(`CancelByEscape` → `OnClickCancel`)라 변경이 있으면 재확인 팝업이 뜬다. **그 결과로 변경사항이 있는 설정 창은 ESC만으로 닫히지 않는다 — 결함이 아니라 이 규칙의 귀결이다**("변경사항을 취소하시겠습니까?"에 대한 ESC는 "아니오"다)
- **`LobbyOnlyConfirm`만 예외로 확인이다** — 결말이 하나뿐이라 그게 곧 '정하고 가는 것'이고, 무동작으로 두면 그 자리에서 ESC가 죽은 키가 된다. 확인 콜백이 실제 흐름을 들고 있는 자리가 있으므로(로그아웃 완료) 그대로 태우는 것이 맞다
- **팝업 해제는 반드시 `Button.onClick.Invoke()`를 거칠 것**(`DismissByEscape`). 리스너에 소리·비활성화·`isActive` 해제가 함께 달려 있어, 콜백만 직접 부르면 **`isActive`가 true로 굳어 그 뒤 모든 팝업이 뜨지 않는다**
- **`ui_return`은 호출부에 둔다** — `BackToLobbyMain()`·`BackToAuthNoneSelected()`는 헤더·닫기 버튼도 부르고 그쪽은 이미 자기 소리를 낸다. 함수 안에 넣으면 버튼 클릭에서 두 번 울린다. 같은 함수인데 헤더는 `ui_submit`(진입), ESC는 `ui_return`(뒤로가기)으로 갈리는 것은 분기 기준대로이며 **통일 대상이 아니다**
- 팝업을 여는 갈래(로그아웃·종료·연결 화면 복귀)는 **무음**이다
