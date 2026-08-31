# 게임 오브젝트 컨트롤러

## 상속 구조

```
GameObjectController (MonoBehaviour)
├── PlayerController         : ICombatTarget
├── OppoPlayerController     : ICombatTarget
└── InteractableGameObjectController
      ├── ContainerController
      │     ├── TestItemBoxController
      │     ├── PlayerLootController              (전리품 컨테이너 — 사망 시 서버가 스폰)
      │     ├── GreenBoxContainerController
      │     ├── YellowBoxContainerController
      │     ├── SmallYellowBoxContainerController
      │     └── SmallWhiteBoxContainerController
      └── RecallSpotController
```

- **`InteractableGameObjectController`**: `_onInteract` 델리게이트 패턴. `Interact()`가 구독된 액션을 실행한다
- **`ContainerController`**: `_onInteract`에 `RequestOpenContainer` 구독
  - **표시 거리 2m는 서버 허용 거리 3m와 짝이다**(`PlayerController.CheckInteractable`). 서버가 조작 시점 좌표로 매번 검사하므로 **2m보다 넓히려면 서버에 먼저 알릴 것** — 서버 쪽이 좁으면 열 수 있다고 보여준 컨테이너가 `DENY_OUT_OF_RANGE`로 거부된다
- **`RecallSpotController`**: `_onInteract`에 `RequestRecall` 구독. 서버 스폰 대상이 아니라 **맵 씬에 직접 배치**하고 `[SerializeField] _recallSpotIndex`로 귀환 영역 테이블 인덱스를 준다. `_objectId`는 쓰지 않는다
  - 상호작용 판정이 `hit.collider.transform.position` 기준 2m 고정이라(`PlayerController.CheckInteractable`) 스팟은 **작은 프롭**이어야 한다. 넓은 트리거 존은 조준·거리 판정이 성립하지 않는다
  - 중복 요청 차단은 스팟이 아니라 `IngameScene._recallRequested`가 한다 — 스팟별로 두면 다른 스팟에서 재요청된다

### 컨테이너 파생 클래스

`Define.ObjectType` 항목당 하나씩이며 하는 일은 `Init()`에서 `base.Init()`을 부른 뒤 `_objectType`을 대입하는 것뿐이다.

- **`base.Init()`을 빠뜨리면 상호작용이 통째로 죽는다** — `_onInteract += RequestOpenContainer` 구독이 `ContainerController.Init()`에 있어서 **E키를 눌러도 아무 일이 없고 에러도 남지 않는다.** 이 계층에서 유일하게 밟기 쉬운 자리다
- **`_objectType`은 읽는 코드가 없지만 지우지 말 것** — 컨테이너 종류별 분기가 생길 때의 자리다
- **`PlayerLoot`(`object_type` 3)는 사망 지점에 스폰되는 전리품 컨테이너다.** 사망자의 인벤토리·장착·탄창이 전부 여기로 옮겨지고 스폰은 `D2CNotifySpawnObject`로 통보된다(서버 계약)
- **`PlayerLoot`의 상호작용 텍스트도 "열어보기"다** — 여닫기·집기가 일반 컨테이너와 완전히 같으므로 파생 클래스에서 `_interactText`를 건드리지 않는다
- 프리팹 이름에는 `Controller` 접미사가 없다(`GameObject/PlayerLoot` ↔ `PlayerLootController`). 대응은 `Define.ObjectPaths`가 갖는다

## 공용 헬퍼 (`GameObjectController`)

로컬과 오포가 **같은 값·같은 규칙**을 쓰게 하는 자리다. 상태(`_soundAudio`·`_equippedWeaponId`)와 재생 메서드는 각 컨트롤러가 갖고, **여기 있는 것은 값·규칙을 정하는 헬퍼뿐이다** — 대부분의 `GameObjectController`가 쓰지 않는 것을 공용 인터페이스로 올리지 않는다.

### `DisableWeaponColliders()` / `FindMuzzlePoint()`

양쪽 `EquipWeapon()`이 무기 인스턴스화 직후 차례로 호출한다.

- **무기 프리팹의 콜라이더는 쓰이는 용도가 없고, 켜둔 채 손에 들면** ① 몸 대신 총에 히트스캔이 걸린다(총은 `ICombatTarget` 하위라 그대로 피격 판정이 된다) ② 발사 원점이 가슴팍이라 자기 무기에 자탄이 걸린다. **프리팹에서 지우지 않는 것은 바닥 무기 습득 용도가 생기면 되살리기 위함이다**
- `MuzzlePoint` 탐색은 **재귀다**(`Util.FindChild<Transform>(..., recursive: true)`) — 모델 하위에 중첩될 수 있어 `transform.Find`로는 못 찾는다. 못 찾으면 `LogError` — 조용히 넘기면 "그 무기만 궤적이 안 보인다"가 원인 없이 남는다

### `UpdateFootstep(source, isStepping, isRunning)` — 발소리

파생 클래스는 이동 상태 둘과 `AudioSource`만 넘긴다. 상수를 양쪽에 두면 같은 동작의 발소리가 조용히 갈린다.

| 상태 | 클립 | 간격 |
|---|---|---|
| 걷기 | `foot_step1~3` 중 랜덤 | 0.6초 |
| 달리기 | `run_foot_step` | 0.25초 |

- **`_footstepTimer`가 곧 연타 가드다**(발사 타이머와 같은 방식: 상한 `Mathf.Min` 고정 + `-=` 차감). 호출부가 아무리 자주 상태를 오가도 최소 간격이 보장되므로 **호출부에 별도 플래그를 얹지 말 것**
- **걷기·달리기가 타이머 하나를 공유하고 간격만 바꾼다. 둘로 나누지 말 것** — 각자 차올라서 달리기가 끝나는 순간 두 발소리가 겹쳐 난다
- **타이머 갱신이 재생 조건 검사보다 먼저다.** 멈춰 있는 동안에도 상한까지 차올라 첫 걸음이 즉발이 된다 — 순서를 뒤집거나 정지 중 타이머를 0으로 죽이면 첫 걸음이 한 간격만큼 밀린다
- **인스턴스 필드다.** `static`으로 바꾸면 모든 적의 발소리가 한 타이머를 두고 싸워 대부분이 사라진다
- 랜덤은 직전 클립을 제외하지 않는다(균등) — 같은 소리가 연속으로 나올 수 있고 의도된 단순화다

### `GetGunShotSound(weaponId)` / `GetReloadSound(weaponId, sequenceNum)` — 무기별 분기 자리

**클립 이름을 호출부에서 만들지 말 것.** 무기별 소리를 넣는 순간 로컬·오포 두 곳이 갈린다. 지금은 무기 3종이 같은 소리라 발사음의 분기가 `default`뿐이다.

- **모르는 `weaponId`는 기본 발사음으로 떨어진다** — 새 무기가 소리 없이 조용해지는 것보다 낫다
- **재장전은 번호가 0·1·15로 띄엄띄엄해 배열이 아니라 `switch`다.** 15(완료)는 서버 전용이고 상수는 `Define.RELOAD_SEQUENCE_COMPLETE` — 네트워크(`UDPManager` 송신 가드)와 컨트롤러가 함께 참조한다
- **모르는 `sequenceNum`은 `null`이다** — 이 값은 네트워크에서 오므로 서버가 단계를 늘리면 실제로 들어온다. 그래서 `SoundManager.PlayOneShotAt`에 `path` null 가드가 함께 있다(없으면 NRE)
- **클립 이름이 계약이다**(`m4_reload_start`/`m4_reload_sequence1`/`m4_reload_complete`, `gun_shot_1`). 한 글자만 어긋나도 **그 소리만 영구히 무음이고 로그가 남지 않는다**
- 단계 번호와 흐름은 `Scenes/CLAUDE.md`의 '재장전 연출 단계'에 있다

## DeathCameraController (독립 MonoBehaviour)

사망 유예 동안의 탑뷰 연출. `GameObjectController` 계층이 아니고 프리팹도 없다 — 정적 `Play(sourceCamera, deadPlayer)`가 런타임에 `@DeathCamera` 리그를 만든다.

- **기존 카메라를 움직이지 않고 같은 시점의 카메라를 새로 만들어 전환한다.** 기존 카메라는 `PlayerController` 하위이고 `ApplyViewRotation()`이 매 프레임 시점을 덮어써서, 그대로 보간하면 되돌려진다
- **`AudioListener`는 옮기지 않는다** — 새 카메라에 붙이면 씬에 둘이 되어 경고가 뜨고, 두면 소리가 시신 위치에서 들려 연출과도 맞는다
- 화각·클리핑·컬링 마스크는 원본에서 복사한다. **로컬 플레이어 모델이 원본 카메라의 컬링에서 빠져 있으면 탑뷰에도 안 보인다**
- 도착 시점 시선이 수직이라 `LookRotation`의 up 힌트를 원래 보던 수평 방향으로 잡는다 — 안 그러면 forward와 up이 평행해져 회전이 튄다
- 연출 시간은 `IngameScene.MATCH_EXIT_DELAY`보다 짧아야 한다

## BulletTracer (독립 MonoBehaviour)

총알 궤적. `DeathCameraController`와 같은 패턴이다(계층 밖 + 프리팹 없음 + 정적 `Play(from, to)`가 `@BulletTracer` 리그를 만든다).

- **그리는 선과 판정선은 다르다.** 궤적은 `MuzzlePoint` → 탄착점, 판정은 `_shotPoint`(가슴팍) → 탄착점이다. 일부러 분리한 것이므로 **맞추려 들지 말 것**(아래 '발사선')
- **`LineRenderer` 풀 16개를 링버퍼로 돌린다** — 한 프레임에 나와 여러 오포가 동시에 쏠 수 있다
- **1프레임 지속은 `Time.frameCount` 비교다.** **`LateUpdate`로 옮기지 말 것 — 렌더링보다 먼저 돌아 선이 한 번도 그려지지 않는다.** 끄는 조건이 `Update()` 한 곳뿐이라 시간 기반+페이드로 바꿀 때도 거기만 고친다
- 씬 전환에 파괴되지만 파괴된 오브젝트도 `== null`이 true라 다음 `Play()`에서 재생성된다 — **별도 정리 코드를 넣지 말 것**
- 머티리얼은 `Shader.Find("Sprites/Default")`로 만들어 **프로세스당 하나만 공유한다**(매치마다 만들면 쌓인다). **빌드에서 선이 마젠타면 셰이더가 스트립된 것** — Graphics Settings의 Always Included Shaders를 볼 것

## ICombatTarget 인터페이스

```csharp
public interface ICombatTarget { int GetObjectId(); }
```

히트스캔이 `GetComponentInParent<ICombatTarget>()`으로 찾는다. 구현하지 않은 오브젝트(컨테이너·지형)는 null → `hitObjectId = 0xFFFFFFFF`(미피격)이다. **새 전투 대상은 반드시 구현할 것** — `GetObjectId()`는 `GameObjectController`에 있으므로 상속 계층 내 클래스는 별도 구현이 필요 없다.

**부모로 거슬러 올라가므로 하위에 붙은 것이면 무엇이든 피격 판정이 된다.** 전투 대상 하위에 콜라이더를 새로 붙일 때는 그것이 피격 부위로 취급돼도 되는지 먼저 볼 것(위 `DisableWeaponColliders`).

## PlayerController

### 발사 시스템

- **발사 타이머**: `_fireInterval = 60f / RPM`, 차감 방식(`-=`)으로 프레임 오차를 보정한다
- **발사 차단**: UI 열림 전환 시 block, 마우스 재클릭(release→press) 시 해제. UI를 닫고 마우스를 유지하는 것만으로는 안 풀린다
- **WeaponSpec 캐시**: `EquipWeapon()`에서 정수값을 `/100f`로 바꿔 도(degree) 단위로 캐싱한다
- **`_fireInterval`이 맨손 차단을 겸한다** — `EquipWeapon()`이 무기 GO를 없애는 자리에서 0으로 비우고 스펙 조회에 성공할 때만 채운다. 없으면 맨손·프리팹 미스로 조기 return할 때 **직전 무기 값이 남아 총도 없는데 발사가 나간다**(탄약 차감·반동·`weapon_dbid=0` 패킷까지)
- **`ProcessFire()`는 맨손이어도 중간에 빠져나가지 않는다** — 나가면 `_fireBlocked` 갱신과 스프레드 회복까지 멈춘다. 발사 조건에서만 막을 것

#### `IsFireInput` / `IsShooting` — 갈라 놔야 하는 두 개념

| 프로퍼티 | 뜻 | 쓰는 곳 |
|---|---|---|
| `IsFireInput` | **쏘려는 의사** — 마우스 + UI 안 열림 + 안 달림 + 행동 중 아님(`IsActionBusy`) + 이탈 중 아님 | `ProcessFire()`의 발사 게이트 |
| `IsShooting` | **실제로 총알이 나가는 중** — 위 + `!_fireBlocked` + 무기 있음 + 탄약 있음 | 발사 모션, `ActionState` |

- **탄약·무기 조건을 `IsFireInput`에 합치지 말 것** — `Fire()`에 도달하지 못해 `EmptyAmmoFire()`가 영영 불리지 않고 빈 탄창 딸깍이 통째로 사라진다
- **`IsShooting`은 `ActionState`를 겸한다** — 이 값이 상태 스트림으로 나가 상대 클라가 발사 모션에 쓴다. **조건을 잘못 잡으면 남의 화면에도 그대로 보이고**, 반대로 여기만 고치면 오포 쪽은 따라온다
- `_fireTimer >= _fireInterval`은 `IsShooting`에 넣지 않는다 — 발사 간격 사이에도 연사 모션은 이어져야 한다
- 탄창 선택 규칙의 출처는 **`IngameInventory.CurrentMagazine` 한 곳**이다. `PlayerController.CurrentMagazine`도 무기 UI 표시도 그것을 읽기만 한다 — 두 벌이 되면 **쏘는 탄창과 보여주는 탄창이 갈린다**
- **`IsRunning`은 `public`이다** — `IngameScene`이 재장전 진입·유지 조건과 스태미나 판정에 읽는다. 다시 감추지 말 것

#### 달리기는 파생값이 아니라 상태다 (`_isRunning` / `ProcessRun`)

스태미나가 진입 조건(20 이상)과 강제 종료(0)를 걸어 `IsMoving && _shift`로는 표현되지 않는다. 전이는 `ProcessRun()` 한 곳에서만 하고, `Update()`에서 **`ProcessMovement()`보다 먼저** 돈다(속도를 고르는 쪽이 이 값을 읽는다).

- **`_shift`를 직접 보는 자리를 새로 만들지 말 것.** 이동 속도·`MovementState`·애니메이션 배속·발사 차단이 전부 `_isRunning`을 본다 — 한 자리라도 `_shift`로 되돌리면 **스태미나가 0인데 달리는 속도로 움직이거나, 남의 화면에만 RUN으로 보인다**
- **진입에만 20을 요구하고 유지는 0 초과다** — 달리는 중 20 아래로 떨어져도 0까지 계속 달린다
- **Shift를 누른 채 20까지 회복되면 자동으로 다시 달린다**(확정). 그래서 진입 조건에 '재입력' 항이 없다 — 재입력을 요구하려면 `_fireBlocked` 같은 별도 플래그가 필요해지므로 조건을 늘리지 말 것
- 스태미나 **값과 판정은 `IngameScene`에 있고 여기서는 `CanStartRunning`/`HasStamina`로 묻기만 한다**

#### 점프도 스태미나를 쓴다 (`ProcessMovement`)

요구치와 소모량이 같은 값 하나(`JUMP_STAMINA_COST` 12)이고, 뛰는 순간 12를 깎으며 회복 지연 1초도 함께 건다.

- **검사·차감은 키 입력(`TryJump`)이 아니라 실제로 뛰는 자리에 있다** — `_jump`는 눌림만 나르는 1회용 플래그이고 접지·입력잠금 판정이 `ProcessMovement`에 있어서, 입력 시점에 깎으면 **공중에서 누른 Space가 뛰지도 않고 스태미나만 가져간다**
- 달리기와 같은 규칙으로 **`CanJump`로 묻고 `ConsumeJumpStamina()`로 알리기만 한다**

#### 탄약 동기화 정책 (느슨한 동기화)

`Fire()`가 발사 직전 `magazine.quantity`를 검사·차감한다. 탄창은 손에 든 슬롯 기준으로 고른다.

- **매 발사마다 서버와 동기화하지 않는다.** 발사 1회당 인벤토리 왕복은 낭비라는 판단이며, 확실한 동기화 시점 전까지 **양쪽 탄약 수치가 어긋나는 것을 정상 범위로 본다**
- **최종 판정 권한은 서버에 있다.** 클라의 차감은 예측이고 클라의 발사 차단은 UX·트래픽 절약용이다
- 따라서 `D2CFullInventorySync`가 오면 탄약이 서버 값으로 덮이는 것이 **정상 동작**이다 — 버그로 보지 말 것
- **확실한 동기화 시점이 곧 재장전이다** — `D2CResponseReload`가 인벤토리 전체 스냅샷을 실어 오며 그 시점에 정렬된다
- **`_ingameScene.Inventory` 참조는 매번 다시 읽는다.** 재장전 응답이 `InventoryItem` 인스턴스를 통째로 갈아치우므로, 필드에 캐시하면 **재장전 뒤에도 버려진 옛 탄창을 차감한다**

### 발소리 (`ProcessFootstep`)

`SoundPoint`(가슴팍 3D 소스)에서 낸다. 타이머·간격·클립은 공용 헬퍼에 있고 여기서는 이동 상태만 넘긴다.

- **`IsStepping`의 `IsInputLocked` 항을 빼지 말 것** — 이탈·사망 중에는 `ProcessMovement`가 이동만 끊고 `_w` 등은 눌린 채 남아, **제자리에 선 시신이 계속 걷는 소리를 낸다**
- **로컬은 접지(`isGrounded`)를 요구하고 오포는 요구하지 않는다 — 의도된 비대칭이다**(근거는 아래 `OppoPlayerController`)
- **걷기·달리기 구분은 `_isRunning`이 한다** — 스태미나가 바닥나면 소리와 간격도 함께 걷기로 돌아온다. `_shift`를 보면 그 연동이 깨진다
- **`Update()`에서 `ProcessMovement()`·`ProcessRun()`보다 뒤다** — `isGrounded`를 갱신하는 것이 `Move()`이고 간격을 고르는 쪽이 `_isRunning`을 읽는다

### 손에 든 무기 (`_equippedWeaponId`)

`EquipWeapon()`이 갱신하는 '손에 든 무기' blueprint_id(0=맨손). `C2DRequestWeaponFire.weapon_dbid`는 **인벤토리에서 다시 유도하지 말고 이 값을 쓸 것** — 서버가 확정해 장착시킨 값이라야 발사가 버려지지 않는다.

- 같은 무기면 파괴·재생성하지 않는다. 인벤토리 조작마다 호출되므로, 없으면 손에 안 든 슬롯을 건드려도 무기가 다시 만들어진다
- **`_muzzlePointTr`는 무기 GO가 사라지는 경로마다 함께 비운다.** 기존 무기 파괴 시 한 번 비우면 맨손 return과 프리팹 미스 return이 모두 그 뒤라 한 자리로 덮인다 — **새 조기 return을 추가할 때 이 지점보다 뒤인지 확인할 것.** 빠뜨리면 파괴된 트랜스폼을 가리킨 채 남는다(시각화 전용 값이라 피격 판정에는 영향이 없다)
- 프리팹 캐시에 없는 id는 에러 로그 — 조용히 return하면 맨손으로 보이기만 하고 원인이 남지 않는다
- **매치 이탈 중 입력 차단은 `IsInputLocked` 하나만 본다** — `IsFireInput`·`ProcessMouseLook`·`ProcessMovement`·`ProcessAim`이 모두 이 값을 참조한다. 이동은 입력만 끊고 **중력은 유지**해 공중에서 죽어도 시신이 떠 있지 않게 한다
- 발사 차단에 `IsActionBusy`(재장전·무기 교체 중)가 포함되는 것도 `IsFireInput`이다. **`_fireBlocked`는 마우스 재클릭으로 풀려 이 용도로 쓸 수 없다**

### Fire() 흐름

1. 탄약 확인 → 없으면 `EmptyAmmoFire()`로 빠지고 끝
2. 히트스캔: `CalculateFireRay()` + 원뿔형 랜덤 오프셋
3. 발사음(`SoundPoint`) + 궤적(`MuzzlePoint` → `BulletTracer`)
4. 반동 목표 누적 — `_recoilTarget`에만 더하고 즉시 적용하지 않는다
5. 스프레드 증가

- **발사음은 탄약이 있을 때만 난다.** 빈 탄창은 `EmptyAmmoFire()`의 딸깍(2D)이고 이쪽은 월드 소리다 — 다른 경로이며 합치지 말 것
- **`Max Distance`(30)를 발소리와 공유한다 — 총성만 따로 조정할 수단이 없다.** 30 자체는 실측에서 거슬리지 않았지만(2026-08-31) 확정된 값이 아니므로 **"거리가 충분하니 해소됐다"로 읽지 말 것.** 카테고리별 분리는 `SoundManager.PlayOneShotAt`의 `OPTION:`에 있다(거리 값을 재생마다 갈아끼우는 방식이 왜 답이 아닌지도 거기 있다)

#### 발사선 (`CalculateFireRay`)

**원점은 카메라가 아니라 `_shotPoint`(플레이어 루트 직속, 가슴팍)이고 방향은 `_aimTarget`으로 수렴한다.** 카메라 축과 평행하지 않다.

- `_shotPoint`는 **피치를 따라가지 않는다**(루트 자식이라 요만 따라감). 피격 판정용 원점이라 의도된 것이고 **시각 표현은 총구 기준으로 따로 간다** — 두 선이 근거리에서 눈에 띄게 벌어지는 것은 정상이다
- 엄폐물 뒤에서 카메라는 위를 보고 총구가 가려져 있으면 **총구 앞의 벽에 맞는다 — 의도된 동작이다.** 조준점과 탄착이 다를 수 있다
- 조준점이 총구보다 뒤이거나 `MIN_CONVERGE_DIST`(0.5m)보다 가까우면 수렴이 성립하지 않아 카메라 forward로 대체한다
- **`Update()`에서 `ProcessAim()`이 `ProcessFire()`보다 먼저여야 한다** — `_aimTarget`을 전자가 쓰고 후자가 읽으므로, 뒤집히면 발사가 직전 프레임 조준점을 쓴다
- 스프레드 기저축은 `dir.y` 크기를 보고 `Vector3.up`/`Vector3.right` 중에 고른다 — 피치가 ±90에 닿으면 `Cross`가 영벡터가 된다
- **자기 PlayerObject 제외 `layerMask`가 없는 것은 의도다**(2026-08-27 검증에서 자탄 0건). 자기 하위 콜라이더가 `CharacterController` 하나뿐인데 **레이가 그 캡슐 안에서 출발해 Unity가 보고하지 않는다.** 전제 셋(무기 콜라이더는 장착 시 꺼짐 / 로컬에는 히트박스 없음 / 발사 원점이 몸 안) 중 하나라도 깨지면 그때 전용 레이어가 필요하다
  - **그때 비용을 잘못 잡지 말 것 — 이 프로젝트에는 쓸 수 있는 레이어가 없다.** 커스텀 레이어가 `ClickableUI` 하나뿐이고 `LayerMask`·`gameObject.layer` 코드가 0건이라 **신설부터 해야 하고 에디터 작업이 선행된다**
- 서버는 발사선을 검증하지 않는다 — `hit_object_id`·`hit_point`만 본다

### 시점 시스템 (View Separation)

마우스 에임과 반동을 별도 변수로 분리해 간섭을 막는다.

- **마우스 감도** = `Managers.Setting.GetMouseSensitivity()`(슬라이더, 기본 1.0) × `MOUSE_SENSITIVITY_DEG_PER_PIXEL`(0.1 = 슬라이더 1.0에서의 도/픽셀). **매 프레임 설정에서 읽는다** — 캐시하면 설정 창에서 바꾼 값이 다음 매치까지 반영되지 않는다
- **`_aimPitch`/`_aimYaw`**: 마우스로 제어하는 순수 각도, 즉시 반영
- **`_recoilPitch`/`_recoilYaw`**: 반동 오프셋, `ProcessRecoil()`에서 Lerp(`_recoilApplySpeed` 15)
- **`ApplyViewRotation()`**: 둘을 합산해 한 프레임에 **1회만** 회전을 적용한다
- **피치 클램프**는 `[-80 + _recoilPitch, 90 + _recoilPitch]`다. **`_recoilPitch` 항을 빼면 반동 누적 시 실제로 조작 범위가 잘린다**
- **반동은 시간 경과로 회복하지 않는다 — 플레이어가 마우스로 잡는 것이 확정된 설계다.** `_recoilTarget`이 `+=`로만 커지고 줄어드는 경로가 없는 것은 **결함이 아니다.** 자동 복귀를 넣으면 플레이어가 보정한 만큼 시점이 아래로 처진다
- `_recoilApplySpeed`의 Lerp는 **회복이 아니라 킥을 몇 프레임에 걸쳐 얹는 용도**다. 반면 `_currentSpread`는 자동 회복한다 — **회복 정책이 다르며 비대칭을 맞추려 들지 말 것**

### `EmptyAmmoFire()` — 빈 탄창 피드백

`_fireBlocked = true`와 `Managers.Sound.Play("empty_gun_shot")` 두 줄이 전부다. **이걸로 완결이며 재장전 유도 UI는 만들지 않는다** — 딸깍 소리와 잔탄 표기의 `0`이 그 역할을 한다(확정).

- **`_fireBlocked = true`가 중복 재생 가드를 겸한다.** 해제가 마우스 재클릭뿐이라 **트리거 1회당 1번**이 되고, 연사 중 탄이 떨어져도 한 번만 운다. **별도 쿨다운·타이머를 넣지 말 것**
- 맨손이면 울리지 않고(`_fireInterval > 0f`에서 걸린다) 재장전·무기 교체 중에도 `IsActionBusy`가 막는다. 둘 다 의도다
- **`SoundPoint`가 아니라 2D `Play()`인 것도 의도다** — 남에게 들릴 소리가 아니고 네트워크로도 나가지 않는 1인칭 피드백이다
- 실패가 조용하다 — 클립을 못 찾으면 `Play()`가 그냥 return하고 `GetOrAddAudioClip()`이 **그 null을 캐시까지 해서** 이후 시도도 전부 무음이다. 소리가 안 나면 호출부가 아니라 파일명·`AudioListener`부터 볼 것

### 미구현이 남은 메서드

- `ProcessHit()`: **스텁이 아니다** — `SendC2DRequestWeaponFire()`로 발사 패킷을 보내는 본 기능을 한다. 남은 것은 데미지 표시뿐이다
- **`ProcessFire()`는 완료됐다.** **총구 화염과 탄착 이펙트는 넣지 않기로 확정됐다** — 빠뜨린 것으로 보고 다시 추가하지 말 것. 탄착 쪽은 수신 측(`IngameScene.HandleWeaponFireBroadcast`)에도 같이 적용된다

## OppoPlayerController

### 히트박스 (`BuildHitboxes`) — 적을 맞힐 수 있는 유일한 수단

**프리팹에는 콜라이더가 없다.** `Setup()`이 모델 인스턴스화 직후 **본에 캡슐 11개를 코드로 붙인다**(머리/상체/골반/상완·전완 ×2/허벅지·정강이 ×2). 없으면 총알이 적을 그대로 통과한다.

**캡슐 11개는 부위 구분이 아니라 '맞음/안 맞음'의 실루엣이다.** `C2DRequestWeaponFire`에 부위 필드가 없고 서버는 `hit_object_id`만 보므로 **어느 캡슐에 맞든 데미지가 같다** — 차등 데미지는 구현 예정이 없다(2026-08-27 서버 확인). **머리 캡슐을 특별 취급하는 코드를 넣지 말 것.** 차등이 생기면 proto에 부위 필드가 먼저 추가된다.

- **루트 캡슐 하나로 대체하지 말 것** — 애니메이션을 따라가지 못해 뻗은 팔·손이 판정에서 빠진다
- **치수를 하드코딩하지 않는다.** 길이는 본↔자식 본 실측, 반지름은 **골반→머리 거리에 대한 비율**(`HITBOX_DEFS.RadiusFactor`)이다. 둘 다 모델에서 유도되므로 `HB0/1/2`의 비율이 달라도 따라간다 — **튜닝 대상은 비율뿐이다**
- `height = 본 길이`, `center = 길이/2`라 인접 캡슐이 관절에서 맞물린다. 손·발은 전완·정강이 캡슐이 관절 너머를 덮어 따로 두지 않았다
- **`isTrigger = true`** — 오포는 `Rigidbody` 없이 매 프레임 `Lerp`로 위치가 강제되므로 일반 콜라이더면 로컬 `CharacterController`와 밀어내기가 싸워 떨린다. **대신 Project Settings의 `Queries Hit Triggers`에 의존한다**(기본 켜짐) — 끄면 히트박스가 통째로 죽는다
- 함정 둘: **`LookRotation`의 up 힌트**(척추·다리 본은 축이 거의 수직이라 기본 `Vector3.up`과 평행해진다) / **본 스케일**(콜라이더 치수는 로컬 단위인데 길이는 월드에서 재므로 `lossyScale`로 나눈다, 균등 스케일 가정)
- 본을 못 찾거나 하나도 만들어지지 않으면 `LogError` — 조용히 넘기면 "그 적만 안 맞는" 상태가 원인 없이 남는다

**로컬 `PlayerController`에는 만들지 않는다 — 의도된 비대칭이다.** 내 클라이언트가 나를 대상으로 레이캐스트할 일이 없고(내가 맞았다는 판정은 상대 클라가 자기 쪽 `OppoPlayer`에 대고 한다), 발사 원점이 가슴팍이라 **내 팔 히트박스가 내 총알을 막는다.** 대칭으로 맞추려 들지 말 것.

### 발소리 — 점프로 소리를 지울 수 없다

`MovementState`만 보고 낸다. 규칙과 간격은 공용이라 **내 발소리와 남의 발소리가 같은 리듬이다.**

- **점프 중에도 소리가 난다 — 로컬(접지 요구)과 갈리는 의도된 비대칭이다.** 공중에서 끊으면 **점프를 연달아 뛰는 것만으로 소리 없이 이동할 수 있게 된다.** 접지 조건으로 맞추려 들지 말 것
- **점프 중에는 `MovementState`가 `JUMP`로 덮여 걷기/달리기 구분이 사라지므로 그 구간만 수평 속도로 가른다**(`FOOTSTEP_MIN_SPEED` 0.5 / `FOOTSTEP_RUN_SPEED` 2 — `walkSpeed` 1과 `runSpeed` 3.5 사이). 제자리 점프는 수평 속도가 0에 가까워 자연히 걸러진다
- **`IsStepping`/`IsRunningStep`은 `ProcessAnimation`의 `isMoving`/`isRunning`과 이름을 갈라 뒀다** — 애니메이션은 점프 중을 이동으로 보지 않는다(공중에서는 idle 블렌드). **한쪽 값을 다른 쪽에 재사용하지 말 것**
- **디스폰되면 재생 중인 소리가 끊긴다**(소스가 오브젝트에 붙어 있다) — 사망음처럼 "죽는 순간 나야 하는 소리"를 이 소스에 태우지 말 것

### 그 외

- `PlayerController`와 동일 모델/Rig 패턴 (Camera/ViewPoint/CharacterController 제외)
- `ApplyState()`: 첫 수신 또는 대규모 이동(sqrMagnitude>100) 시 즉시 텔레포트, 그 외 매 프레임 Lerp
- `ProcessAim()`: yaw+pitch → 방향 벡터 → `_aimTarget` 배치(가슴 높이 yOffset=0.58f + 100m 전방)
- `EquipWeapon(weaponId)`: `0`은 맨손이라 기존 무기만 파괴하고 끝낸다. 프리팹 캐시에 없는 id는 에러 로그
- `_equippedWeaponId`: `D2CSpawnPlayerObject.weapon_id` + `D2CNotifyWeaponChanged`로 추적한 현재 무기. 발사음·재장전음이 이 값을 쓰고, 킬 피드가 킬러 무기를 싣지 않으므로 표기가 필요하면 이것을 쓴다
