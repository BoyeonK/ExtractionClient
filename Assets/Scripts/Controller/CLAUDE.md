# 게임 오브젝트 컨트롤러

## 상속 구조

```
GameObjectController (MonoBehaviour)
├── PlayerController
├── OppoPlayerController
└── InteractableGameObjectController
      └── ContainerController
            └── TestItemBoxController
```

- **`InteractableGameObjectController`**: `_onInteract` 델리게이트 패턴. `Interact()` 호출 시 구독된 액션 실행
- **`ContainerController`**: `_onInteract`에 `RequestOpenContainer` 구독

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

### 반동 보간 (`ProcessRecoil`)

`Update()`에서 `ProcessMouseLook()` 직후 호출. `_recoilCurrent`를 `Lerp`로 `_recoilTarget`까지 보간하고, 매 프레임 델타만큼 카메라에 적용. `_recoilApplySpeed`(기본 15)로 체감 속도 조절.

### 스텁 메서드 (미구현)

- `EmptyAmmoFire()`: 빈 탄창 사운드, UI 등
- `ProcessHit()`: 데미지, 이펙트, 서버 히트 검증

## OppoPlayerController

- `PlayerController`와 동일 모델/Rig 패턴 (Camera/ViewPoint/CharacterController 제외)
- `ApplyState()`: 첫 수신 또는 대규모 이동(sqrMagnitude>100) 시 즉시 텔레포트, 그 외 매 프레임 Lerp 보간
- `ProcessAim()`: yaw+pitch → 방향 벡터 → `_aimTarget` 배치(가슴 높이 yOffset=0.58f + 100m 전방)
