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

### Fire() 흐름

1. 탄약 확인 → 없으면 `EmptyAmmoFire()` + `_fireBlocked = true`
2. 히트스캔: `CalculateSpreadRay()`로 원뿔형 랜덤 오프셋 적용
3. 반동 목표 누적: `_recoilTarget`에만 추가, 즉시 적용하지 않음
4. 스프레드 증가

### 시점 시스템 (View Separation)

마우스 에임과 반동을 별도 변수로 분리하여 간섭 방지:
- **`_aimPitch` / `_aimYaw`**: 마우스 입력에 의한 순수 에임 각도. 즉시 반영, Lerp 없음
- **`_recoilPitch` / `_recoilYaw`**: 반동에 의한 오프셋. `ProcessRecoil()`에서 Lerp 보간(`_recoilApplySpeed` 기본 15)
- **`ApplyViewRotation()`**: 두 값을 합산(`_aimPitch - _recoilPitch`, `_aimYaw + _recoilYaw`)하여 한 프레임에 **1회만** 회전 적용
- **피치 클램프**: `ProcessMouseLook()`에서 `_aimPitch`를 `[-80 + _recoilPitch, 90 + _recoilPitch]`로 클램프 — 반동 누적 시에도 마우스 조작 범위 보장

### 스텁 메서드 (미구현)

- `EmptyAmmoFire()`: 빈 탄창 사운드, UI 등
- `ProcessHit()`: 데미지, 이펙트, 서버 히트 검증

## OppoPlayerController

- `PlayerController`와 동일 모델/Rig 패턴 (Camera/ViewPoint/CharacterController 제외)
- `ApplyState()`: 첫 수신 또는 대규모 이동(sqrMagnitude>100) 시 즉시 텔레포트, 그 외 매 프레임 Lerp 보간
- `ProcessAim()`: yaw+pitch → 방향 벡터 → `_aimTarget` 배치(가슴 높이 yOffset=0.58f + 100m 전방)
