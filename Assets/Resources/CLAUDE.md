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
└ Sounds/                   12개. 재생 호출부는 아직 `empty_gun_shot` 하나뿐이다(아래)
```

## 경로 접두어가 호출부마다 다르다

같은 자산을 가리키는 문자열이 진입점에 따라 한 겹씩 다르다. **이걸 모르면 경로가 어긋난다.**

| 진입점 | 넘기는 문자열 | 실제 경로 |
|---|---|---|
| `Managers.Resource.Instantiate(path)` | `GameObject/PlayerLoot` | `Prefabs/`를 **자동으로 붙인다** |
| `Managers.Sound.Play(path)` | `empty_gun_shot` | `Sounds/`를 **없을 때만 붙인다** |
| `Resources.Load<T>()` 직접 | `Images/Items/icon_item_3` | **전체 경로 그대로** |

## 이름이 곧 계약인 자리

파일 이름을 바꾸면 컴파일은 통과하고 **런타임에 그 자산만 조용히 빠진다.**

| 자산 | 규약 | 출처 |
|---|---|---|
| `Prefabs/UI/Scene/*`, `Prefabs/UI/Popup/*` | **파일명 = UI 클래스명** | `UIManager`가 `typeof(T).Name`으로 조립 |
| `Prefabs/Weapons/Weapon_{weaponId}_{name}` | 이름에서 id를 파싱 | `IngameScene`의 `LoadAll("Prefabs/Weapons")` |
| `Prefabs/GameObject/{ObjectType 이름}` | 타입 ↔ 경로 대응 | `Define.ObjectPaths` |
| `Prefabs/GameObject/PlayerObject_ingredient/HB{0\|1\|2}{Player\|OppoPlayer\|Selected}` | 캐릭터 타입 × 용도 | `PlayerController`·`OppoPlayerController`·`SelectedCharacter` |
| `Images/Items/icon_item_{item_id}` | item_id | 슬롯 3종(`ISlot`·`IngameISlot`·`SSlot`) |
| `Images/MapSprites/map_sprite_{mapId}` | mapId | `UI_MapSelect` |
| `Images/WeaponSprites/weapon_sprite_{id}` | 위와 같은 꼴이나 **아직 참조하는 코드가 없다** | — |

## 경로로 로드되는 프리팹 vs 씬에 배치되는 프리팹

**두 방식은 개수가 정해져 있지 않다 — 아래는 규칙이 아니라 현재 상태의 목록이다.** 어느 쪽으로 만들지는 그 UI를 **런타임에 찍어내야 하는지**로 갈린다(찍어내야 하면 경로 로드, 처음부터 한 벌만 있으면 씬 배치).

지금 경로로 로드하는 것: `UI/Scene/{클래스명}`, `UI/Popup/{클래스명}`, `UI/EventSystem`, `UI/IngameSceneUI/SingleKillLog`(킬 로그 한 줄).

나머지 씬 내장 UI(`IngameInventoryUI`·`IngameDragGhost`·`InteractUI`·`IngameHealthBarUI`·`IngameSettingUI`·`IngameKillLogUI`·`IngameWeaponUI`·`LobbySceneUI` 계열)는 **씬에 배치돼 `GameObject.Find`로 잡힌다.** 따라서

- **계약은 프리팹 파일 이름이 아니라 씬 오브젝트 이름이다.** 프리팹 이름을 맞춰도 씬의 인스턴스 이름이 다르면 못 찾는다
- **씬에는 활성 상태로 저장해야 한다** — `GameObject.Find`는 비활성 오브젝트를 못 찾는다. 필요하면 각 `Init()`이 바인딩 직후 스스로 끈다
- **스크립트가 붙어 있어야 한다** — 이름만 맞고 컴포넌트가 없으면 `Init()` 호출에서 NRE가 나고, 킬 피드 외 6종은 아직 그 가드가 없다(`Assets/Scripts/UI/CLAUDE.md`)

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

- 볼륨 슬라이더는 Min 0 / Max 100(로비와 동일), **감도 슬라이더는 Min 0.1 / Max 3.0 / Value 1.0** — `SettingManager.MIN/MAX_MOUSE_SENSITIVITY`와 **손으로 맞추는 값**이다
- `IngameHealthBarUI`의 Fill 이미지는 **Type이 `Filled`여야 한다** — `Simple`이면 `fillAmount` 대입이 조용히 무시된다
- `IngameWeaponUI`의 `WeaponImage`는 자리만 있고 **아직 코드가 채우지 않는다**(`TODO:`) — **쓸 이미지가 정해지지 않았고, `Images/WeaponSprites/*`는 여기에 쓰는 것이 아니다**
- `IngameWeaponUI`는 **그래픽 7개 모두 `raycastTarget`을 꺼둔다.** HUD가 클릭을 가로채면 인벤토리를 열어 커서가 풀린 상태에서 겹치는 영역의 드래그를 먹는다. 요소를 추가할 때도 함께 끌 것

## 로드 실패는 반드시 드러난다

`ResourceManager`의 두 자리가 `Util.LogError`를 남긴다 — 경로 오타·프리팹 미제작(`Instantiate`)과 `ObjectPaths`에 경로는 있는데 프리팹이 없는 경우(`InstantiateFromObjectDataStruct`)다. **`Debug.Log`로 낮추지 말 것** — 호출부 대부분이 반환값을 그대로 참조해서, 묻히면 원인이 아니라 NRE부터 보게 된다. 실제로 `UI/IngameScene` ↔ `UI/IngameSceneUI` 오타가 정확히 그 경로로 샜다.

## Resources 폴더는 전량 빌드에 포함된다

참조 여부와 무관하게 이 폴더의 모든 자산이 빌드에 들어간다(Unity 특성). 쓰지 않는 자산을 여기 두면 그대로 용량이 되므로, 아래 목록은 그 관점에서 의미가 있다.

## 코드 참조가 없는 자산 (관찰 — 삭제 제안이 아니다)

- `Prefabs/System/@Managers` — `Managers`는 `GameObject.Find` 후 없으면 **런타임 생성**이라 이 프리팹을 경로로 로드하지 않는다
- `Prefabs/System/EventSystem` — 코드가 쓰는 것은 `UI/EventSystem` 쪽이다. **같은 것이 두 벌 있다**
- `Prefabs/Scene/*`(4종), `Prefabs/TestLobbyScene/`, `Prefabs/TestPlayer`, `Prefabs/UI/LobbySceneUI/LobbySettingUIBackup`
- `Images/WeaponSprites/*` — 명명 규약은 아이템·맵과 같은 꼴이라 쓰일 자리를 예비해 둔 것으로 보인다
- **`Sounds/` 12개 중 11개** — 재생 호출부가 있는 것은 `empty_gun_shot` 하나뿐이다(`PlayerController.EmptyAmmoFire()`). 발사음·발소리·재장전음은 파일로만 준비돼 있고 재생을 붙이면 되는 상태이며, 이건 `progress.md`의 이펙트 항목과 같은 자리다
  - **클립을 못 찾아도 아무 소리 없이 넘어간다** — `SoundManager.Play()`가 null이면 그대로 return하고 `GetOrAddAudioClip()`은 **그 null을 딕셔너리에 캐시까지 한다.** 즉 파일명을 한 글자 틀리면 그 소리만 영구히 무음이고 로그도 남지 않는다. 위 '이름이 곧 계약인 자리'가 사운드에도 그대로 걸리며, 로드 실패가 `LogError`로 드러나는 프리팹 쪽과 여기가 다르다
