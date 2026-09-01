# 리소스 (`Assets/Resources/`)

`ResourceManager`·`SoundManager`·`Resources.Load` 직접 호출부가 읽는 유일한 자산 폴더다. **프리팹은 사용자가 에디터에서 만들고 관리한다 — 이 세션은 읽어서 확인하는 것이 목적이며, 수정이 필요해 보이면 고치지 말고 말할 것**(`.prefab`은 GUID·fileID로 얽힌 YAML이라 텍스트 편집이 참조를 조용히 깬다).

## 조사 비용 규칙 — 먼저 읽을 것

**프리팹 전체를 `Read` 하는 것은 마지막 수단이다.** Unity YAML은 정보 밀도가 극단적으로 낮아(TextMeshPro 컴포넌트 하나가 수백 줄) 자식 이름 하나를 확인하려고 통째로 읽으면 컨텍스트를 크게 낭비한다. **`Glob`(존재·경로) → 경로를 지정한 `Grep`(필요한 줄만)** 순으로 갈 것.

| 알고 싶은 것 | 질의 |
|---|---|
| 자식 오브젝트 구성 | `m_Name:` |
| 스크립트 부착 여부 | 대상 `.cs.meta`에서 `guid`를 얻어 프리팹에서 그 guid를 찾는다 |
| `Image`의 Type | `m_Type:` (0=Simple, 1=Sliced, 2=Tiled, **3=Filled**) |
| 슬라이더 범위 | `m_MinValue:` / `m_MaxValue:` |
| 콜라이더가 트리거인지 | `m_IsTrigger:` |

## 폴더 구조

```
Assets/Resources/
├ Prefabs/
│  ├ GameObject/            월드 오브젝트(플레이어·컨테이너). Define.ObjectPaths가 가리킨다
│  │  └ PlayerObject_ingredient/   캐릭터 모델
│  ├ UI/{Scene, Popup}      UIManager가 클래스명으로 찾는다
│  ├ UI/{LobbySceneUI, IngameSceneUI, LoadingSceneUI, GameResultSceneUI}
│  │                        주로 씬에 배치된다(아래)
│  ├ Weapons/               무기 3종
│  ├ Scene/, System/, TestLobbyScene/   코드 참조 없음(아래 '참조 없는 자산')
├ Images/{Items, MapSprites, WeaponSprites}
├ Fonts/                    7종. 코드 경로 없이 프리팹이 직접 참조한다
└ Sounds/                   12종 전부 재생 호출부가 있다(아래)
```

## 경로 접두어가 호출부마다 다르다

같은 자산을 가리키는 문자열이 진입점에 따라 한 겹씩 다르다. **이걸 모르면 경로가 어긋난다.**

| 진입점 | 넘기는 문자열 | 실제 경로 |
|---|---|---|
| `Managers.Resource.Instantiate(path)` | `GameObject/PlayerLoot` | `Prefabs/`를 **자동으로 붙인다** |
| `Managers.Sound.Play(path)` | `empty_gun_shot` | `Sounds/`를 **없을 때만 붙인다** |
| `Managers.Sound.PlayOneShotAt(path, source)` | `foot_step1` | 위와 같다. 차이는 경로가 아니라 **어디서 울리는가**(아래) |
| `Resources.Load<T>()` 직접 | `Images/Items/icon_item_3` | **전체 경로 그대로** |

## 이름이 곧 계약인 자리

파일 이름을 바꾸면 컴파일은 통과하고 **런타임에 그 자산만 조용히 빠진다.**

| 자산 | 규약 | 출처 |
|---|---|---|
| `Prefabs/UI/Scene/*`, `Prefabs/UI/Popup/*` | **파일명 = UI 클래스명** | `UIManager`가 `typeof(T).Name`으로 조립 |
| `Prefabs/Weapons/Weapon_{weaponId}_{name}` | 이름에서 id를 파싱 | `IngameScene`의 `LoadAll("Prefabs/Weapons")` |
| `Prefabs/GameObject/{ObjectType 이름}` | 타입 ↔ 경로 대응 | `Define.ObjectPaths` |
| `Prefabs/GameObject/PlayerObject_ingredient/HB{0\|1\|2}{Player\|OppoPlayer\|Selected}` | 캐릭터 타입 × 용도 | `PlayerController`·`OppoPlayerController`·`SelectedCharacter` |
| `PlayerObject`·`OppoPlayerObject`의 직계 자식 `SoundPoint`, `OppoPlayerObject`의 `GunSoundPoint` | 이름 고정 + `AudioSource` 부착 | 양쪽 `Setup()`의 `transform.Find` (아래) |
| `Images/Items/icon_item_{item_id}` | item_id | 슬롯 4종(`ISlot`·`IngameISlot`·`SSlot`·`LootContainerSlot`) |
| `Images/MapSprites/map_sprite_{mapId}` | mapId | `UI_MapSelect` |
| `Images/WeaponSprites/weapon_sprite_{무기 item_id}` | item_id | `IngameWeaponUI` |

## 경로로 로드되는 프리팹 vs 씬에 배치되는 프리팹

**두 방식은 개수가 정해져 있지 않다 — 아래는 규칙이 아니라 현재 상태의 목록이다.** 어느 쪽으로 만들지는 그 UI를 **런타임에 찍어내야 하는지**로 갈린다(찍어내야 하면 경로 로드, 처음부터 한 벌만 있으면 씬 배치).

지금 경로로 로드하는 것: `UI/Scene/{클래스명}`, `UI/Popup/{클래스명}`, `UI/EventSystem`, `UI/IngameSceneUI/SingleKillLog`(킬 로그 한 줄), `UI/IngameSceneUI/IngameDamageIndicatorContent`(피격 방향 표시 하나), `UI/GameResultSceneUI/LootContainerSlot`(전리품 한 칸).

나머지 씬 내장 UI(`IngameInventoryUI`·`IngameDragGhost`·`InteractUI`·`IngameHealthBarUI`·`IngameSettingUI`·`IngameKillLogUI`·`IngameWeaponUI`·`IngameStaminaBarUI`·`IngameEscapeCountdownUI`·`IngameTimeoutUI`·`IngameEscUI`·`IngameDamageIndicatorUI`·`GameResultSceneUI`·`LobbySceneUI` 계열)는 **씬에 배치돼 `GameObject.Find`로 잡힌다.** 따라서

- **계약은 프리팹 파일 이름이 아니라 씬 오브젝트 이름이다.** 프리팹 이름을 맞춰도 씬의 인스턴스 이름이 다르면 못 찾는다
- **씬에는 활성 상태로 저장해야 한다** — `GameObject.Find`는 비활성 오브젝트를 못 찾는다. 필요하면 각 `Init()`이 바인딩 직후 스스로 끈다
- **스크립트가 붙어 있어야 한다** — 이름만 맞고 컴포넌트가 없으면 그 UI가 죽는다. 원인은 `BaseScene.BindSceneComponent<T>`가 `LogError`로 드러내므로 **콘솔부터 볼 것**(`Assets/Scripts/Scenes/CLAUDE.md`)

## 씬 내장 UI 프리팹 계층 규격

> **이 두 트리는 `Assets/Scripts/UI/CLAUDE.md`에도 같은 내용이 있다. 한쪽만 고치지 말 것.**
> 여기는 **만들 때의 규격**이고, 어겼을 때의 증상과 코드 쪽 규칙은 그 문서에 있다.

전부 `transform.Find`/`Util.BindComponent`로 찾으므로 **직계 자식만 본다** — 한 단계라도 감싸면 못 찾는다.

```
IngameKillLogUI                 ← GameObject.Find 대상. 이름 고정 + 씬에서 활성
└ KillLogContainer              ← SingleKillLog가 이 아래에 붙는다

SingleKillLog                   ← Prefabs/UI/IngameSceneUI/ (런타임에 찍어내므로 경로로 로드한다)
├ KillIcon                      코드가 건드리지 않는 장식. 가해자 이름이 비어도 이것만 남는다
├ KillerId                      (TextMeshProUGUI)
└ VictimId                      (TextMeshProUGUI)
```

```
IngameSettingUI                         ← 루트. 이름 고정 + 씬에서 활성
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

```
IngameWeaponUI                  ← GameObject.Find 대상. 이름 고정 + 씬에서 활성
└ WeaponUIWindow
  ├ Header/{AccentLine, WeaponName}       AccentLine은 코드가 찾지 않는 장식
  └ WeaponInfoPanel/{WeaponImage, MagazineAmmoCount, RemainAmmoCount}
```

```
IngameEscapeCountdownUI                 ← 루트. 이름 고정 + 씬에서 활성(코드가 Init에서 끈다)
└ TextArea
  ├ CountdownText                       (TextMeshProUGUI) 코드가 5→1을 넣는다
  └ FixedText                           코드가 건드리지 않는 고정 문안 — 에디터에서 채워둘 것
```

```
IngameTimeoutUI                         ← 루트. 이름 고정 + 씬에서 활성
└ TextArea
  └ CountdownText                       (TextMeshProUGUI) 코드가 mm:ss를 넣는다
```

- **이것만 `Init()`이 끄지 않는다** — 매치 내내 떠 있으므로 씬에 저장한 활성 상태가 곧 최종 상태다. 다른 씬 내장 UI를 따라 비활성화를 넣지 말 것

```
IngameEscUI                             ← 루트. 이름 고정 + 씬에서 활성(코드가 Init에서 끈다)
├ OptionOrExitUI                        선택 패널 — 코드가 이 둘을 갈아 끼운다
│ ├ OptionButton                        (Button)
│ └ ExitButton                          (Button)
└ ExitConfirmOrCancelUI                 종료 확인 패널
  └ ButtonRow/{ConfirmButton, CancelButton}   (Button)
```

- **패널 둘의 이름이 계약이다** — `Util.BindComponent`가 못 찾으면 `LogError`를 남기고 `null`을 돌려주므로 **터지지 않고 그 버튼만 조용히 죽는다**

```
IngameDamageIndicatorUI                 ← 루트. 이름 고정 + 씬에서 활성(코드가 끄지 않는다)
                                          Content가 이 아래에 직접 붙는다

IngameDamageIndicatorContent            ← Prefabs/UI/IngameSceneUI/ (호출마다 찍어내므로 경로로 로드)
└ Image                                   (Image) 위를 가리키도록 저작한다 — 회전의 기준이다
```

- **Content 루트는 `anchor 0.5,0.5 / pivot 0.5,0.5 / size 0`이어야 한다.** 자식 이미지가 그 지점에서 위로 떨어져 있어야 루트를 돌렸을 때 화면 중앙을 축으로 공전한다. **루트에 크기를 주면 회전 중심이 어긋난다**
- 루트의 `anchoredPosition`은 **코드가 0으로 덮으므로** 저작값이 무엇이든 상관없다

```
GameResultSceneUI                       ← 루트. 이름 고정 + 씬에서 활성
├ GameResultSummary/GameResultText      (TextMeshProUGUI) 코드가 문구와 색을 함께 넣는다
├ LootLists/LootContainer               ← LootContainerSlot이 이 아래에 붙는다
├ LootLists/LostItemsText               (TextMeshProUGUI) 사망 시 전리품을 대신한다. 문안은 고정
└ BottomBar/ConfirmButton               (Button) 코드가 꺼둔 뒤 연출 끝에 켠다

LootContainerSlot                       ← Prefabs/UI/GameResultSceneUI/ (런타임에 찍어내므로 경로로 로드)
├ Fill                                  (Image) 코드가 icon_item_{item_id}를 넣는다
└ Quantity                              (TextMeshProUGUI)
```

- `GameResultSceneUI`의 **`SubLabel`·`BG`·`DivTop`/`DivBottom`·`TopAccentLine`은 코드가 찾지 않는다.** `SubLabel`은 고정 문안이므로 **에디터에서 채워둘 것** — 코드가 넣어주길 기다리면 영영 비어 있다
- **`ConfirmButton`과 `LostItemsText`는 씬에 활성으로 저장한다** — 코드가 `Init()`에서 끄고 필요할 때 켠다. 비활성으로 저장해도 `Util.BindComponent`는 찾지만(루트 기준 경로 조회라 활성 여부와 무관), 규칙을 갈라두면 다음 사람이 헷갈린다
- **`LostItemsText`의 문안은 고정이라 에디터에서 채워둔다** — 코드는 켜고 끄기만 하고 텍스트를 넣지 않는다(`SubLabel`과 같다)
- `LootContainerSlot`의 **`Fill`에 스프라이트를 물려두지 않는다** — 코드가 넣고, 파일이 없으면 `Image.enabled`를 꺼서 흰 사각형을 막는다
- 볼륨 슬라이더는 Min 0 / Max 100(로비와 동일), **감도 슬라이더는 Min 0.1 / Max 5.0 / Value 1.0** — `SettingManager.MIN/MAX_MOUSE_SENSITIVITY` 및 **로비 프리팹의 같은 슬라이더와 손으로 맞추는 값**이다(맞출 곳이 셋)
- `IngameHealthBarUI`의 Fill 이미지는 **Type이 `Filled`여야 한다** — `Simple`이면 `fillAmount` 대입이 조용히 무시된다. **`IngameStaminaBarUI`(`StaminaBarBg/StaminaBarFill`)도 같은 규칙**이며 이쪽은 이미 `Filled`로 맞춰져 있다
- **슬롯 프리팹 4종(`ISlot`·`SSlot`·`IngameISlot`·`LootContainerSlot`)의 `Fill` 색이 곧 아이템 아이콘의 틴트다** — 코드는 색을 건드리지 않고 `Image.enabled`만 토글한다. 어두운 회색(`0.235`)은 **심미 판단으로 확정된 값**이며, **네 프리팹이 같아야** 전리품 아이콘만 다른 밝기로 보이지 않는다(근거는 `UI/CLAUDE.md`의 슬롯 항목)
- `IngameWeaponUI`의 `WeaponImage`는 **스프라이트를 물려두지 않는다** — 코드가 `weapon_sprite_{item_id}`를 넣는다. **`Preserve Aspect`는 꺼둔 것이 의도다**(스프라이트 √2:1, 슬롯 비율이 그에 가까워 찌그러짐을 감수했다). 스프라이트가 순백 실루엣이라 **`Color`가 곧 표시색이고 코드는 색을 건드리지 않는다**
- `IngameWeaponUI`는 **그래픽 7개 모두 `raycastTarget`을 꺼둔다.** HUD가 클릭을 가로채면 인벤토리를 열어 커서가 풀린 상태에서 겹치는 영역의 드래그를 먹는다. 요소를 추가할 때도 함께 끌 것

## `SoundPoint` / `GunSoundPoint` — 월드 소리를 내보내는 3D 소스 둘

`PlayerObject`·`OppoPlayerObject`의 **직계 자식**(`ViewPoint`·`ShotPoint`·`Aim`과 같은 층)이며 각각 `AudioSource` 하나를 갖는다. 위치는 발이 아니라 **가슴 부근**(local y=0.5)이다 — 스테레오 패닝은 방위각만 주고 고도는 HRTF 없이 실리지 않으므로 원거리 청자에게 가슴↔발 1m는 들리지 않고, 대신 **리스너(머리 카메라)와 가까워져 자기 발소리가 발밑에서 작게 나는 문제가 없어진다.**

- **`GunSoundPoint`는 `OppoPlayerObject`에만 있다 — 의도된 비대칭이다.** 로컬은 리스너가 자기 가슴팍 바로 위(약 1m대)라 두 커브 모두 **첫 키 이전 구간**이고, 감쇠가 시작되기 전이라 원거리 소스를 둬도 결과가 완전히 같다. **`PlayerObject`에 만들지 말 것** — 코드가 읽지 않아 쓰이지 않는 채 남는다
- `SoundPoint`는 **두 프리팹의 이름·설정을 같이 유지할 것** — 코드가 같은 이름으로 찾고 같은 헬퍼로 재생한다

| 설정 | `SoundPoint` | `GunSoundPoint` | 어기면 |
|---|---|---|---|
| Play On Awake | off | off | 스폰마다 빈 재생이 돈다 |
| Spatial Blend | **1.0 (3D)** | **1.0 (3D)** | 0이면 위치가 무의미해진다 |
| Volume Rolloff | **Custom** | **Custom** | 아래 |
| Min Distance | **2** | 5 | 커브 저작의 출발점 — **볼륨을 정하는 것은 커브다**(아래) |
| Max Distance | 30 | 120 | 아래 |
| Volume / Pitch | 손대지 않음 | 손대지 않음 | `PlayOneShotAt`이 매 재생마다 `volume`을 덮는다. 밸런스는 코드의 `volumeScale`로 잡을 것 |
| AudioClip / Output | 비움 | 비움 | 클립은 `PlayOneShot`이 넘긴다 |

- **Rolloff가 Custom이고 커브가 `Max Distance`에서 0에 닿는다 — Logarithmic 프리셋으로 되돌리지 말 것.** 그 프리셋에서 `Max Distance`는 컷오프가 아니라 **감쇠가 멈추는 지점**이라, 그 너머로 `Min/Max` 비율의 볼륨이 고정된 채 거리와 무관하게 계속 들린다. 지금 값에서는 발소리 바닥값(2/30)이 총성 바닥값(5/120)보다 커져 **맵 반대편 발소리가 총성보다 크게 들리는 역전**이 난다
- **`SoundPoint` 커브의 첫 키프레임(2m, 볼륨 1.0)을 로컬 리스너~가슴팍 거리(스케일 2에서 약 1m대)보다 앞으로 당기지 말 것** — 첫 키 이전 구간은 그 값으로 평평해서 지금은 내 소리가 항상 최대 음량인데, 당기면 **내 발소리만 감쇠 구간에 들어가 조용해진다**
- **상대 음량을 정하는 것은 두 커브이지 `Min Distance` 비율이 아니다.** Custom Rolloff에서 커브의 x축은 **각자의 `Max Distance`로 정규화**되므로(30 vs 120), 같은 모양의 커브라도 절대 거리로는 4배 다르게 동작한다. `Min Distance` 2·5는 커브를 저작할 때 첫 키가 놓인 자리일 뿐이다
- **지금 두 커브는 같은 모양이고 거리 축만 늘어나 있다 — 같은 볼륨에 도달하는 거리가 총성 쪽이 약 2.6배 멀다**(발소리 2·4·8.6·15.7m = 1.0·0.50·0.21·0.074 ↔ 총성 5·10.3·23.2·43.8m = 1.0·0.50·0.22·0.094). **같은 거리에서의 음량비는 고정이 아니다** — 4m에서 2배, 16m 부근에서 4배, 30m 너머는 발소리가 0이라 비율이 성립하지 않는다
- 이 균형을 `volumeScale`로 다시 잡으려 들지 말 것 — 거리 감쇠가 아니라 전체 음량을 균일하게 낮춘다

- **경계선: 월드에서 난 소리는 `SoundPoint`, UI 피드백(`ui_submit`·`inventory_change`)은 2D `Managers.Sound.Play`.** 안 그으면 클릭음이 가슴에서 3D로 나는 쪽으로 흘러간다. `empty_gun_shot`은 남에게 들릴 소리가 아닌 1인칭 피드백이라 의도적으로 2D에 남아 있다
- **소스 하나에 여러 소리를 태운다.** `PlayOneShot`은 재생 중인 것을 끊지 않고 섞으므로 발소리·발사음이 서로 잘라먹지 않는다. 대신 `volume`·`pitch`·`min/maxDistance`는 **소스 단위 속성이라 재생 중인 원샷에까지 소급 적용된다** — pitch 랜덤화를 붙이면 그 직후 나가는 발사음까지 같이 흔들린다
- **소스를 가르는 축은 가청 거리다.** 감쇠 곡선과 `maxDistance`가 소스당 하나뿐이라, 30m에서 끊겨야 하는 발소리와 그보다 멀리 가야 하는 총성이 한 소스에 같이 탈 수 없어 둘로 쪼갠 것이다(`volumeScale`은 곡선을 균일하게 낮출 뿐 모양을 바꾸지 않는다). **거리 값을 재생할 때마다 갈아끼우는 방식은 쓰지 말 것** — 걸으며 쏘는 상황이 상시라 울리던 소리의 거리감이 중간에 튄다
- **재장전음은 `SoundPoint`(근거리)에 남는다** — 통보 패킷이 unreliable이라 단계가 통째로 빠지는 것이 정상 계약이어서, 멀리서 듣는 단편적 재장전음은 없는 정보를 만든다
- **오브젝트가 파괴되면 재생 중인 소리도 끊긴다.** 발소리는 티가 안 나지만 "죽는 순간 나야 하는 소리"를 여기 태우면 안 된다 — 총성은 사거리가 길어 **먼 거리에서 쏘고 바로 죽은 오포의 마지막 총성이 잘린다**(감수)
- **오포에는 여러 인스턴스가 동시에 존재한다.** 소스가 오브젝트마다 하나씩이라 서로 간섭하지 않는다

## 로드 실패는 반드시 드러난다

`ResourceManager`의 두 자리가 `Util.LogError`를 남긴다 — 경로 오타·프리팹 미제작(`Instantiate`)과 `ObjectPaths`에 경로는 있는데 프리팹이 없는 경우(`InstantiateFromObjectDataStruct`)다. **`Debug.Log`로 낮추지 말 것** — 호출부 대부분이 반환값을 그대로 참조해서, 묻히면 원인이 아니라 NRE부터 보게 된다. 실제로 `UI/IngameScene` ↔ `UI/IngameSceneUI` 오타가 정확히 그 경로로 샜다.

## Resources 폴더는 전량 빌드에 포함된다

참조 여부와 무관하게 이 폴더의 모든 자산이 빌드에 들어간다(Unity 특성). 쓰지 않는 자산을 여기 두면 그대로 용량이 되므로, 아래 목록은 그 관점에서 의미가 있다.

## 코드 참조가 없는 자산 (관찰 — 삭제 제안이 아니다)

- `Prefabs/System/@Managers` — `Managers`는 `GameObject.Find` 후 없으면 **런타임 생성**이라 이 프리팹을 경로로 로드하지 않는다
- `Prefabs/System/EventSystem` — 코드가 쓰는 것은 `UI/EventSystem` 쪽이다. **같은 것이 두 벌 있다**
- `Prefabs/Scene/*`(4종), `Prefabs/TestLobbyScene/`, `Prefabs/TestPlayer`, `Prefabs/UI/LobbySceneUI/LobbySettingUIBackup`
- **`Sounds/`는 12종 전부 호출부가 있다** — 월드 3D(`SoundPoint`)는 `foot_step1~3`·`run_foot_step`·`gun_shot_1`·`m4_reload_{start,sequence1,complete}`, 2D는 `empty_gun_shot`·`ui_submit`·`ui_return`·`inventory_change`다
  - **클립 이름은 코드 한 곳에만 둔다** — 월드 쪽은 `GameObjectController.GetGunShotSound`/`GetReloadSound`, UI 쪽은 `SoundManager.PlayUISubmit`/`PlayUIReturn`/`PlayInventoryChange`다. **호출부에서 문자열을 조립하지 말 것**(UI음은 호출부가 40곳이 넘는다)
  - **클립을 못 찾아도 아무 소리 없이 넘어간다** — `SoundManager.Play()`가 null이면 그대로 return하고 `GetOrAddAudioClip()`은 **그 null을 딕셔너리에 캐시까지 한다.** 즉 파일명을 한 글자 틀리면 그 소리만 영구히 무음이고 로그도 남지 않는다. 위 '이름이 곧 계약인 자리'가 사운드에도 그대로 걸리며, 로드 실패가 `LogError`로 드러나는 프리팹 쪽과 여기가 다르다
