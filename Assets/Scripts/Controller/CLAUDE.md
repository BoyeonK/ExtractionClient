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

## ICombatTarget 인터페이스

`Assets/Scripts/Controller/ICombatTarget.cs`

```csharp
public interface ICombatTarget {
    int GetObjectId();
}
```

히트스캔 피격 판정 시 `GetComponentInParent<ICombatTarget>()`으로 탐색. `ICombatTarget`을 구현하지 않은 오브젝트(컨테이너, 지형 등)는 null → `hitObjectId = 0xFFFFFFFF`(미피격)으로 처리된다. 새 전투 대상 오브젝트 추가 시 반드시 이 인터페이스를 구현할 것. `GetObjectId()`는 `GameObjectController`에 이미 구현되어 있으므로 상속 계층 내 클래스는 별도 구현 불필요.

## PlayerController

### 발사 시스템

- **발사 타이머**: `_fireInterval = 60f / RPM`, 차감 방식(`-= _fireInterval`)으로 프레임 오차 보정
- **발사 차단**: UI 열림 전환 시 block, 마우스 재클릭(release→press) 시 해제. UI 닫힘 후 마우스 유지만으로는 해제되지 않음
- **WeaponSpec 캐시**: `EquipWeapon()` 시 정수값을 `/100f`로 변환하여 도(degree) 단위로 캐싱

#### 탄약 동기화 정책 (느슨한 동기화)

`Fire()`가 발사 직전 `magazine.quantity`를 검사하고 차감한다. 탄창은 손에 든 슬롯 기준(`IsPrimaryWeaponApplyed`)으로 고른다.

- **매 발사마다 서버와 동기화하지 않는다.** 발사 1회당 인벤토리 왕복은 낭비라는 판단이며, 장전 등 확실한 동기화 시점 전까지 **클라와 서버의 탄약 수치가 어긋나는 것을 정상 범위로 본다**
- **최종 판정 권한은 서버에 있다.** 서버는 발사 패킷 수신 시 가능한 경우 차감하되 패킷 유실과 클라 값 조작을 전제로 판정한다. 클라의 차감은 예측이고 클라의 발사 차단은 UX·트래픽 절약용이다
- 따라서 `D2CFullInventorySync`가 오면 탄약이 서버 값으로 덮이는 것이 **정상 동작**이다 — 버그로 보지 말 것
- **재장전 수단이 아직 없다.** 탄창 교체 경로가 클라에 없고(`SetPrimaryWeaponMagazine`/`SetSecondaryWeaponMagazine`은 호출자 0개, 장비 슬롯은 `0/1/2`뿐) proto에도 관련 메시지가 없다. **탄창이 비면 그 판 내내 못 쏜다** — 전투 테스트 계획을 세울 때 이 제약을 먼저 볼 것

### 손에 든 무기 (`_equippedWeaponId`)

`EquipWeapon()`이 갱신하는 '손에 든 무기' blueprint_id(0=맨손). `C2DRequestWeaponFire.weapon_dbid`는 **인벤토리에서 다시 유도하지 말고 이 값을 쓸 것** — 서버가 확정해 장착시킨 값이라야 발사가 버려지지 않는다.

- 같은 무기면 파괴·재생성하지 않는다. 인벤토리 조작마다 호출되므로 없으면 손에 안 든 슬롯을 건드려도 무기가 다시 만들어진다
- 프리팹 캐시에 없는 id는 에러 로그 — 조용히 return하면 맨손으로 보이기만 하고 원인이 남지 않는다
- 발사 차단 조건에 `IngameScene.IsWeaponSwitchPending`과 `IngameScene.IsInputLocked`(매치 이탈)가 포함된다(`IsShooting`). `_fireBlocked`는 마우스 재클릭으로 풀려서 이 용도로 쓸 수 없다
- **매치 이탈 중 입력 차단은 `IsInputLocked` 하나만 본다** — `IsShooting`·`ProcessMouseLook`·`ProcessMovement`·`ProcessAim`이 모두 이 값을 참조한다. 이동은 입력만 끊고 중력은 유지해 공중에서 죽어도 시신이 떠 있지 않게 한다. 사망 연출이 카메라를 가져가므로 시점 입력도 함께 끊긴다

### Fire() 흐름

1. 탄약 확인 → 없으면 `EmptyAmmoFire()` + `_fireBlocked = true`
2. 히트스캔: `CalculateFireRay()`로 발사선 산출 + 원뿔형 랜덤 오프셋 적용
3. 반동 목표 누적: `_recoilTarget`에만 추가, 즉시 적용하지 않음
4. 스프레드 증가

#### 발사선 (`CalculateFireRay`)

**원점은 카메라가 아니라 `_shotPoint`(플레이어 루트 직속, 가슴팍)이고, 방향은 `_aimTarget`으로 수렴한다.** 카메라 축과 평행하지 않다.

- `_shotPoint`는 **피치를 따라가지 않는다**(루트 자식이라 요만 따라감). 피격 판정용 원점이라 의도된 것이고, 시각 이펙트는 추후 총구 기준으로 별도 처리한다
- 엄폐물 뒤에서 카메라는 위를 보고 총구는 가려져 있으면 **총구 앞의 벽에 맞는다 — 의도된 동작이다.** 조준점과 탄착이 다를 수 있다
- 조준점이 총구보다 뒤에 있거나 `MIN_CONVERGE_DIST`(0.5m)보다 가까우면 수렴이 성립하지 않으므로 카메라 forward로 대체한다
- **`Update()`에서 `ProcessAim()`이 `ProcessFire()`보다 먼저 와야 한다.** `_aimTarget`을 전자가 쓰고 후자가 읽으므로, 순서가 뒤집히면 발사가 직전 프레임 조준점을 쓴다
- 스프레드 기저축은 `dir.y` 크기를 보고 `Vector3.up`/`Vector3.right` 중에 고른다 — 시점 피치가 ±90에 닿으면 `Cross`가 영벡터가 된다
- **자기 PlayerObject 제외가 아직 없다**(`TODO:`). 전용 레이어 + `layerMask`가 필요하고 에디터 작업이 선행되어야 한다
- 서버는 발사선을 검증하지 않는다. `hit_object_id`·`hit_point`만 본다

### 시점 시스템 (View Separation)

마우스 에임과 반동을 별도 변수로 분리하여 간섭 방지:
- **`_aimPitch` / `_aimYaw`**: 마우스 입력에 의한 순수 에임 각도. 즉시 반영, Lerp 없음
- **`_recoilPitch` / `_recoilYaw`**: 반동에 의한 오프셋. `ProcessRecoil()`에서 Lerp 보간(`_recoilApplySpeed` 기본 15)
- **`ApplyViewRotation()`**: 두 값을 합산(`_aimPitch - _recoilPitch`, `_aimYaw + _recoilYaw`)하여 한 프레임에 **1회만** 회전 적용
- **피치 클램프**: `ProcessMouseLook()`에서 `_aimPitch`를 `[-80 + _recoilPitch, 90 + _recoilPitch]`로 클램프 — 반동 누적 시에도 마우스 조작 범위 보장

### 미구현이 남은 메서드

- `EmptyAmmoFire()`: 실동작은 `_fireBlocked = true` + `TEMP:` 로그 1줄뿐. 빈 탄창 사운드·재장전 유도 UI가 `TODO:`
- `ProcessHit()`: **스텁이 아니다** — `SendC2DRequestWeaponFire()`로 발사 패킷을 보내는 본 기능을 수행한다. 남은 것은 탄착 이펙트·데미지 표시
- `ProcessFire()`: 타이머·차단·발사 트리거는 동작한다. 남은 것은 발사 연출·이펙트

## OppoPlayerController

- `PlayerController`와 동일 모델/Rig 패턴 (Camera/ViewPoint/CharacterController 제외)
- `ApplyState()`: 첫 수신 또는 대규모 이동(sqrMagnitude>100) 시 즉시 텔레포트, 그 외 매 프레임 Lerp 보간
- `ProcessAim()`: yaw+pitch → 방향 벡터 → `_aimTarget` 배치(가슴 높이 yOffset=0.58f + 100m 전방)
- `EquipWeapon(weaponId)`: `weaponId = 0`은 맨손이라 기존 무기만 파괴하고 끝낸다. 프리팹 캐시에 없는 id는 에러 로그 — 조용히 return하면 맨손으로 보이기만 하고 원인이 남지 않는다
- `_equippedWeaponId`: `D2CSpawnPlayerObject.weapon_id` + `D2CNotifyWeaponChanged`로 추적한 현재 무기. 킬 피드가 킬러 무기를 싣지 않으므로 표기가 필요하면 이 값을 쓴다
