# 게임 오브젝트 컨트롤러

## 상속 구조

```
GameObjectController (MonoBehaviour)
├── PlayerController         : ICombatTarget
├── OppoPlayerController     : ICombatTarget
└── InteractableGameObjectController
      ├── ContainerController
      │     └── TestItemBoxController
      └── RecallSpotController
```

- **`GameObjectController.DisableWeaponColliders()`**: 손에 든 무기의 콜라이더를 끄는 공용 헬퍼. 양쪽 `EquipWeapon()`이 인스턴스화 직후 호출한다. **무기 프리팹에 딸려 오는 콜라이더는 쓰이는 용도가 없는데**(`Weapon_1_AK`는 4개, `Weapon_2_M4`·`Weapon_3_M16`은 3개) 켜둔 채로 손에 들면 ① 몸 대신 총에 히트스캔이 걸리고 — 총은 `ICombatTarget` 하위라 **그대로 피격 판정이 된다** ② 발사 원점이 가슴팍이라 자기 무기에 자탄이 걸린다. 프리팹에서 지우지 않고 장착 시점에만 끄는 것은 바닥 무기 습득 용도가 생기면 되살리기 위함이다
- **`GameObjectController.FindMuzzlePoint()`**: 무기 프리팹의 `MuzzlePoint`를 찾는 공용 헬퍼. 양쪽 `EquipWeapon()`이 `DisableWeaponColliders()` 직후에 호출한다. **재귀 탐색이다**(`Util.FindChild<Transform>(..., recursive: true)`) — 모델 하위에 중첩돼 있을 수 있어 `transform.Find`로는 못 찾는다. 못 찾으면 `LogError` — 조용히 넘어가면 "그 무기만 궤적이 안 보인다"가 원인 없이 남는다
- **`InteractableGameObjectController`**: `_onInteract` 델리게이트 패턴. `Interact()` 호출 시 구독된 액션 실행
- **`ContainerController`**: `_onInteract`에 `RequestOpenContainer` 구독
- **`RecallSpotController`**: `_onInteract`에 `RequestRecall` 구독. 서버 스폰(`ObjectData`) 대상이 아니라 **맵 씬에 직접 배치**하고 `[SerializeField] _recallSpotIndex`로 맵별 귀환 영역 테이블 인덱스를 부여한다. `_objectId`는 사용하지 않음
  - 상호작용 판정이 `hit.collider.transform.position` 기준 2m 고정이므로(`PlayerController.CheckInteractable`), 스팟은 **작은 프롭**(단말기 등)으로 배치할 것. 넓은 트리거 존은 조준·거리 판정이 성립하지 않는다
  - 중복 요청 차단은 스팟이 아닌 `IngameScene._recallRequested`가 담당 (스팟별로 두면 다른 스팟에서 재요청 가능)

## DeathCameraController (독립 MonoBehaviour)

`Assets/Scripts/Controller/DeathCameraController.cs` — 사망 유예 동안 재생되는 탑뷰 연출. `GameObjectController` 계층이 아니며 프리팹도 없다. `DeathCameraController.Play(sourceCamera, deadPlayer)` 정적 호출이 런타임에 `@DeathCamera` 리그를 만든다.

- **기존 카메라를 움직이지 않고 같은 시점의 카메라를 새로 만들어 전환한다.** 기존 카메라는 `PlayerController` 하위에 있고 `ApplyViewRotation()`이 매 프레임 시점을 덮어써서, 그대로 보간하면 되돌려진다
- **`AudioListener`는 옮기지 않는다** — 새 카메라에 붙이면 씬에 둘이 되어 경고가 뜨고, 그대로 두면 소리가 시신 위치에서 들려 연출과도 맞는다
- 화각·클리핑·컬링 마스크는 원본 카메라에서 복사한다. **로컬 플레이어 모델이 원본 카메라의 컬링에서 빠져 있으면 탑뷰에도 안 보인다**
- 도착 시점 시선이 수직이라 `LookRotation`의 up 힌트를 원래 보던 수평 방향으로 잡는다. 안 그러면 forward와 up이 평행해져 회전이 튄다
- 연출 시간은 `IngameScene.MATCH_EXIT_DELAY`보다 짧아야 한다

## BulletTracer (독립 MonoBehaviour)

`Assets/Scripts/Controller/BulletTracer.cs` — 총알 궤적 시각화. `DeathCameraController`와 같은 패턴이다(`GameObjectController` 계층이 아니고 프리팹도 없으며, 정적 `BulletTracer.Play(from, to)`가 런타임에 `@BulletTracer` 리그를 만든다).

- **그리는 선과 판정선은 다르다.** 궤적은 `MuzzlePoint` → 탄착점, 피격 판정은 `_shotPoint`(가슴팍) → 탄착점이다. 판정과 표현을 일부러 분리한 것이므로 **맞추려 들지 말 것**(아래 '발사선' 참조)
- **`LineRenderer` 풀 16개를 링버퍼로 돌린다.** 한 프레임에 나와 여러 오포가 동시에 쏠 수 있어 하나로는 부족하다
- **1프레임 지속은 `Time.frameCount` 비교다.** `Play()`가 프레임 번호를 기록하고 `Update()`가 번호가 다른 것만 끈다. **`LateUpdate`로 옮기지 말 것 — 렌더링보다 먼저 돌아서 선이 한 번도 그려지지 않는다.** `Update()` 실행 순서가 발사부보다 앞이든 뒤든 프레임 번호로 비교하므로 결과는 같다
- 끄는 조건이 `Update()` 한 곳뿐이다. 지속을 시간 기반 + 페이드로 바꾸게 되면 거기만 고친다
- 씬 오브젝트라 씬 전환에 파괴되는데, 파괴된 오브젝트에도 `== null`이 true라 다음 `Play()`에서 자연히 재생성된다 — **별도 정리 코드를 넣지 말 것**
- 머티리얼은 `Shader.Find("Sprites/Default")`로 만들어 **프로세스당 하나만 공유한다**(매치마다 만들면 그만큼 쌓인다). **빌드에서 선이 마젠타면 셰이더가 스트립된 것** — Graphics Settings의 Always Included Shaders를 볼 것

## ICombatTarget 인터페이스

`Assets/Scripts/Controller/ICombatTarget.cs`

```csharp
public interface ICombatTarget {
    int GetObjectId();
}
```

히트스캔 피격 판정 시 `GetComponentInParent<ICombatTarget>()`으로 탐색. `ICombatTarget`을 구현하지 않은 오브젝트(컨테이너, 지형 등)는 null → `hitObjectId = 0xFFFFFFFF`(미피격)으로 처리된다. 새 전투 대상 오브젝트 추가 시 반드시 이 인터페이스를 구현할 것. `GetObjectId()`는 `GameObjectController`에 이미 구현되어 있으므로 상속 계층 내 클래스는 별도 구현 불필요.

**부모로 거슬러 올라가므로 하위에 붙은 것이면 무엇이든 피격 판정이 된다.** 손에 든 무기가 대표적인 사고 사례였다(위 `DisableWeaponColliders` 참조) — 전투 대상 하위에 콜라이더를 새로 붙일 때는 그것이 피격 부위로 취급되어도 되는지 먼저 볼 것.

## PlayerController

### 발사 시스템

- **발사 타이머**: `_fireInterval = 60f / RPM`, 차감 방식(`-= _fireInterval`)으로 프레임 오차 보정
- **발사 차단**: UI 열림 전환 시 block, 마우스 재클릭(release→press) 시 해제. UI 닫힘 후 마우스 유지만으로는 해제되지 않음
- **WeaponSpec 캐시**: `EquipWeapon()` 시 정수값을 `/100f`로 변환하여 도(degree) 단위로 캐싱
- **`_fireInterval`이 맨손 차단을 겸한다** — `EquipWeapon()`이 무기 GO를 없애는 자리에서 0으로 비우고 스펙 조회에 성공할 때만 다시 채운다. 이게 없으면 맨손·프리팹 미스로 조기 return할 때 **직전 무기의 값이 남아 총도 없는데 발사가 나간다**(탄약 차감·반동·`weapon_dbid=0` 패킷까지). `ProcessFire()`는 맨손이어도 **중간에 빠져나가지 않는다** — 나가면 `_fireBlocked` 갱신과 스프레드 회복까지 멈추므로, 발사 조건에서만 막는다

#### `IsFireInput` / `IsShooting` — 갈라 놔야 하는 두 개념

| 프로퍼티 | 뜻 | 쓰는 곳 |
|---|---|---|
| `IsFireInput` | **쏘려는 의사** — 마우스 + UI 안 열림 + 안 달림 + **행동 중 아님**(`IngameScene.IsActionBusy`) + 이탈 중 아님 | `ProcessFire()`의 발사 게이트 |
| `IsShooting` | **실제로 총알이 나가는 중** — 위 + `!_fireBlocked` + 무기 있음 + 탄약 있음 | 발사 모션(`_anim.SetBool("IsShooting", …)`), `ActionState` |

- **탄약·무기 조건을 `IsFireInput`에 합치지 말 것.** 그러면 `Fire()`에 도달하지 못해 **`EmptyAmmoFire()`가 영영 불려지지 않고**, 빈 탄창 사운드·재장전 유도 UI가 설 자리가 사라진다
- **`IsShooting`은 `ActionState`를 겸한다** — 이 값이 상태 스트림으로 나가 상대 클라의 `OppoPlayerController`가 `_anim.SetBool("IsShooting", _actionState == 1)`로 쓴다. 즉 **발사 모션 조건을 잘못 잡으면 남의 화면에도 그대로 보인다.** 반대로 여기만 고치면 오포 쪽은 손대지 않아도 따라온다
- `_fireTimer >= _fireInterval`은 `IsShooting`에 넣지 않는다 — 발사 간격 사이에도 연사 모션은 이어져야 한다
- 탄창 선택 규칙은 `CurrentMagazine` 한 곳에 있고 `Fire()`도 같은 것을 쓴다. 두 벌로 만들면 판정과 표시가 갈린다
- **`IsRunning`은 `public`이다** — `IngameScene`이 재장전의 진입·유지 조건으로 읽는다(달리면 재장전이 취소된다). 이 파일 안에서만 쓰이는 값으로 보고 다시 감추지 말 것

#### 탄약 동기화 정책 (느슨한 동기화)

`Fire()`가 발사 직전 `magazine.quantity`를 검사하고 차감한다. 탄창은 손에 든 슬롯 기준(`IsPrimaryWeaponApplyed`)으로 고른다.

- **매 발사마다 서버와 동기화하지 않는다.** 발사 1회당 인벤토리 왕복은 낭비라는 판단이며, 장전 등 확실한 동기화 시점 전까지 **클라와 서버의 탄약 수치가 어긋나는 것을 정상 범위로 본다**
- **최종 판정 권한은 서버에 있다.** 서버는 발사 패킷 수신 시 가능한 경우 차감하되 패킷 유실과 클라 값 조작을 전제로 판정한다. 클라의 차감은 예측이고 클라의 발사 차단은 UX·트래픽 절약용이다
- 따라서 `D2CFullInventorySync`가 오면 탄약이 서버 값으로 덮이는 것이 **정상 동작**이다 — 버그로 보지 말 것
- **확실한 동기화 시점이 곧 재장전이다.** `R` 키 → `IngameScene.RequestReload()` → `D2CResponseReload`가 인벤토리 전체 스냅샷을 실어 오며, 그 시점에 어긋나 있던 탄약 수치가 서버 값으로 정렬된다 (`Scenes/CLAUDE.md`의 '재장전' 참조)
- **`_ingameScene.Inventory` 참조는 매번 다시 읽는다.** 재장전 응답이 `ApplyFullSync`로 `InventoryItem` 인스턴스를 통째로 갈아치우므로, `CurrentMagazine`처럼 그때그때 조회하는 형태를 유지할 것 — 인스턴스를 필드에 캐시하면 재장전 뒤에도 버려진 옛 탄창을 차감하게 된다

### 손에 든 무기 (`_equippedWeaponId`)

`EquipWeapon()`이 갱신하는 '손에 든 무기' blueprint_id(0=맨손). `C2DRequestWeaponFire.weapon_dbid`는 **인벤토리에서 다시 유도하지 말고 이 값을 쓸 것** — 서버가 확정해 장착시킨 값이라야 발사가 버려지지 않는다.

- 같은 무기면 파괴·재생성하지 않는다. 인벤토리 조작마다 호출되므로 없으면 손에 안 든 슬롯을 건드려도 무기가 다시 만들어진다(이 경로는 무기 GO가 살아 있어 `_muzzlePointTr` 캐시도 그대로 유효하다)
- **`_muzzlePointTr`(궤적 원점)는 무기 GO가 사라지는 경로마다 함께 비운다** — 기존 무기 파괴 시 한 번 비우면 맨손 return과 프리팹 캐시 미스 return이 모두 그 뒤라 한 자리로 커버된다. 새 조기 return을 추가할 때는 이 지점보다 뒤인지 확인할 것. 빠뜨리면 **파괴된 트랜스폼을 가리킨 채 남는다**. 피격 판정에는 쓰이지 않는 시각화 전용 값이다
- 프리팹 캐시에 없는 id는 에러 로그 — 조용히 return하면 맨손으로 보이기만 하고 원인이 남지 않는다
- 발사 차단 조건에 `IngameScene.IsActionBusy`(재장전·무기 교체 진행 중)와 `IngameScene.IsInputLocked`(매치 이탈)가 포함된다(`IsFireInput`). `_fireBlocked`는 마우스 재클릭으로 풀려서 이 용도로 쓸 수 없다
- **매치 이탈 중 입력 차단은 `IsInputLocked` 하나만 본다** — `IsFireInput`·`ProcessMouseLook`·`ProcessMovement`·`ProcessAim`이 모두 이 값을 참조한다. 이동은 입력만 끊고 중력은 유지해 공중에서 죽어도 시신이 떠 있지 않게 한다. 사망 연출이 카메라를 가져가므로 시점 입력도 함께 끊긴다

### Fire() 흐름

1. 탄약 확인 → 없으면 `EmptyAmmoFire()` + `_fireBlocked = true`
2. 히트스캔: `CalculateFireRay()`로 발사선 산출 + 원뿔형 랜덤 오프셋 적용
3. 반동 목표 누적: `_recoilTarget`에만 추가, 즉시 적용하지 않음
4. 스프레드 증가

#### 발사선 (`CalculateFireRay`)

**원점은 카메라가 아니라 `_shotPoint`(플레이어 루트 직속, 가슴팍)이고, 방향은 `_aimTarget`으로 수렴한다.** 카메라 축과 평행하지 않다.

- `_shotPoint`는 **피치를 따라가지 않는다**(루트 자식이라 요만 따라감). 피격 판정용 원점이라 의도된 것이고, **시각 표현은 총구(`MuzzlePoint`) 기준으로 따로 간다** — `DrawTracer()`가 총구에서 탄착점까지를 그린다(위 `BulletTracer`). 두 선이 근거리에서 눈에 띄게 벌어지는 것은 정상이다
- 엄폐물 뒤에서 카메라는 위를 보고 총구는 가려져 있으면 **총구 앞의 벽에 맞는다 — 의도된 동작이다.** 조준점과 탄착이 다를 수 있다
- 조준점이 총구보다 뒤에 있거나 `MIN_CONVERGE_DIST`(0.5m)보다 가까우면 수렴이 성립하지 않으므로 카메라 forward로 대체한다
- **`Update()`에서 `ProcessAim()`이 `ProcessFire()`보다 먼저 와야 한다.** `_aimTarget`을 전자가 쓰고 후자가 읽으므로, 순서가 뒤집히면 발사가 직전 프레임 조준점을 쓴다
- 스프레드 기저축은 `dir.y` 크기를 보고 `Vector3.up`/`Vector3.right` 중에 고른다 — 시점 피치가 ±90에 닿으면 `Cross`가 영벡터가 된다
- **자기 PlayerObject 제외 `layerMask`가 없는 것은 의도다**(2026-08-27 검증에서 자탄 0건). 자기 하위에 남은 콜라이더가 `CharacterController` 하나뿐인데 **레이가 그 캡슐 안에서 출발해 Unity가 보고하지 않는다.** 전제 셋(무기 콜라이더는 장착 시 꺼짐 / 로컬에는 히트박스를 만들지 않음 / 발사 원점이 몸 안) 중 하나라도 깨지면 그때 전용 레이어가 필요해진다
  - **그때 비용을 잘못 잡지 말 것 — 이 프로젝트에는 쓸 수 있는 레이어가 없다.** 커스텀 레이어가 `ClickableUI` 하나뿐이고 `Assets/Scripts` 전체에 `LayerMask`·`gameObject.layer` 코드가 0건이라, 레이어 기반 해법은 **신설부터 해야 하고 에디터 작업이 선행된다**
- 서버는 발사선을 검증하지 않는다. `hit_object_id`·`hit_point`만 본다

### 시점 시스템 (View Separation)

마우스 에임과 반동을 별도 변수로 분리하여 간섭 방지:
- **마우스 감도**: `MouseSensitivity` = `Managers.Setting.GetMouseSensitivity()` × `MOUSE_SENSITIVITY_DEG_PER_PIXEL`(0.1). 앞이 설정 슬라이더 값(기본 1.0), 뒤가 **슬라이더 1.0에서의 도/픽셀**이다. 계수가 없던 시절엔 1픽셀 = 1도라 세로 가동 범위 170도 전체가 마우스 170픽셀이었다. **매 프레임 설정에서 읽는다** — 캐시하면 설정 창(`IngameSettingUI`)에서 바꾼 값이 다음 매치까지 반영되지 않는다
- **`_aimPitch` / `_aimYaw`**: 마우스 입력에 의한 순수 에임 각도. 즉시 반영, Lerp 없음
- **`_recoilPitch` / `_recoilYaw`**: 반동에 의한 오프셋. `ProcessRecoil()`에서 Lerp 보간(`_recoilApplySpeed` 기본 15)
- **`ApplyViewRotation()`**: 두 값을 합산(`_aimPitch - _recoilPitch`, `_aimYaw + _recoilYaw`)하여 한 프레임에 **1회만** 회전 적용
- **피치 클램프**: `ProcessMouseLook()`에서 `_aimPitch`를 `[-80 + _recoilPitch, 90 + _recoilPitch]`로 클램프 — 반동 누적 시에도 마우스 조작 범위 보장
- **반동은 시간 경과로 회복하지 않는다 — 플레이어가 마우스로 직접 잡는 것이 확정된 설계다.** `_recoilTarget`은 `Fire()`의 `+=`로만 커지고 줄어드는 경로가 없는데 **이건 결함이 아니다**(한 번 결함으로 오진된 적이 있다). 자동 복귀를 추가하지 말 것 — 넣으면 플레이어가 보정한 만큼 시점이 아래로 처진다
- 누적이 무한히 커져도 **위 피치 클램프가 `_recoilPitch`만큼 같이 밀리므로 가동 범위는 보존된다.** 이 클램프에서 `_recoilPitch` 항을 빼면 그때 실제로 조작 범위가 잘린다
- `_recoilApplySpeed`의 Lerp는 **회복이 아니라 킥을 몇 프레임에 걸쳐 얹는 용도**다. 반면 `_currentSpread`는 `_spreadRecoveryRate`로 자동 회복한다 — **스프레드와 반동은 회복 정책이 다르며, 비대칭을 맞추려 들지 말 것**

### 미구현이 남은 메서드

- `EmptyAmmoFire()`: 실동작은 `_fireBlocked = true` + `TEMP:` 로그 1줄뿐. 빈 탄창 사운드·재장전 유도 UI가 `TODO:`
- `ProcessHit()`: **스텁이 아니다** — `SendC2DRequestWeaponFire()`로 발사 패킷을 보내는 본 기능을 수행한다. 남은 것은 탄착 이펙트·데미지 표시
- `ProcessFire()`: 타이머·차단·발사 트리거는 동작한다. 남은 것은 발사 연출·이펙트

## OppoPlayerController

### 히트박스 (`BuildHitboxes`) — 적을 맞힐 수 있는 유일한 수단

**프리팹에는 콜라이더가 없다.** `OppoPlayerObject`·`HB0/1/2OppoPlayer` 어느 쪽에도 없으므로, `Setup()`이 모델 인스턴스화 직후 **본에 캡슐 11개를 코드로 붙인다**(머리 / 상체 / 골반 / 상완·전완 ×2 / 허벅지·정강이 ×2). 이게 없으면 총알이 적을 그대로 통과한다.

**캡슐 11개는 부위 구분이 아니라 '맞음/안 맞음'의 실루엣을 정의할 뿐이다.** `C2DRequestWeaponFire`에 부위 필드가 없고 서버는 `hit_object_id`만 보므로 **어느 캡슐에 맞든 데미지가 같다** — 헤드샷 배율 같은 차등 데미지는 구현 예정이 없다(2026-08-27 서버 확인). 머리 캡슐을 특별 취급하는 코드를 넣지 말 것. 차등이 생기면 proto에 부위 필드가 먼저 추가된다.

- **루트 캡슐 하나로 대체하지 말 것** — 애니메이션을 따라가지 못해 뻗은 팔·손이 판정에서 빠진다. 본에 붙어야 스킨드 메시와 같은 트랜스폼을 따라간다
- **치수를 하드코딩하지 않는다.** 길이는 본↔자식 본 실측 거리, 반지름은 **골반→머리 거리에 대한 비율**(`HITBOX_DEFS.RadiusFactor`). 둘 다 모델에서 유도되므로 `HB0/1/2`의 비율·스케일이 서로 달라도, 모델이 교체돼도 따라간다. **튜닝 대상은 비율뿐이다**
- `height = 본 길이`, `center = 길이/2`라 인접 캡슐이 관절에서 맞물린다 — 부위 사이에 틈이 없다. 손·발은 전완·정강이 캡슐이 관절 너머를 덮어 별도로 두지 않았다
- **`isTrigger = true`** — 오포는 `Rigidbody` 없이 매 프레임 `Lerp`로 위치가 강제되므로, 일반 콜라이더면 로컬 플레이어 `CharacterController`와 밀어내기가 싸워 떨린다. **대신 Project Settings의 `Queries Hit Triggers`에 의존한다**(Unity 기본 켜짐). 꺼면 히트박스가 통째로 죽는다
- 처리한 함정 둘: **`LookRotation`의 up 힌트** — 척추·다리 본은 축이 거의 수직이라 기본 `Vector3.up`과 평행해진다(대부분의 히트박스가 해당). **본 스케일** — 콜라이더 치수는 로컬 단위인데 길이는 월드에서 재므로 `lossyScale`로 나눈다(균등 스케일 가정)
- 본을 못 찾거나 하나도 만들어지지 않으면 `LogError`. 조용히 넘어가면 "그 적만 안 맞는" 상태가 원인 없이 남는다

**로컬 `PlayerController`에는 만들지 않는다 — 의도된 비대칭이다.** 내 클라이언트가 나를 대상으로 레이캐스트할 일이 없고(내가 맞았다는 판정은 상대 클라이언트가 자기 쪽 `OppoPlayer`에 대고 하며, 서버는 발사선을 검증하지 않는다), 발사 원점이 가슴팍이라 **내 팔 히트박스가 내 총알을 막는다.** 대칭으로 맞추려 들지 말 것.

### 그 외

- `PlayerController`와 동일 모델/Rig 패턴 (Camera/ViewPoint/CharacterController 제외)
- `ApplyState()`: 첫 수신 또는 대규모 이동(sqrMagnitude>100) 시 즉시 텔레포트, 그 외 매 프레임 Lerp 보간
- `ProcessAim()`: yaw+pitch → 방향 벡터 → `_aimTarget` 배치(가슴 높이 yOffset=0.58f + 100m 전방)
- `EquipWeapon(weaponId)`: `weaponId = 0`은 맨손이라 기존 무기만 파괴하고 끝낸다. 프리팹 캐시에 없는 id는 에러 로그 — 조용히 return하면 맨손으로 보이기만 하고 원인이 남지 않는다
- `_equippedWeaponId`: `D2CSpawnPlayerObject.weapon_id` + `D2CNotifyWeaponChanged`로 추적한 현재 무기. 킬 피드가 킬러 무기를 싣지 않으므로 표기가 필요하면 이 값을 쓴다
