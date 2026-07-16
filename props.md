# Props — 상위 세션에서 전달된 컨텍스트

## 총알 발사 시스템 설계 (2026-07-16)

### 작업 의도

플레이어의 총알 발사 → 서버 검증 → 다른 플레이어에게 브로드캐스트하는 전체 파이프라인 구축.
루트 세션에서 proto 정의 + 양쪽 핸들러 뼈대까지 완료. 하위 세션에서 TODO로 남긴 세부 구현을 마무리할 것.

### 추가된 프로토콜

`LinuxServerTest/Protocol/ExternalProtocol/External_Protocol.proto`에 다음 패킷이 추가됨:

- `C2D_REQUEST_WEAPON_FIRE` (PktId = 29)
- `D2C_BROADCAST_WEAPON_FIRE` (PktId = 30)

### 패킷 흐름

```
Client → C2D_REQUEST_WEAPON_FIRE → Server (DedicateProcess)
  - fire_sequence: 발사 시퀀스 (중복/리플레이 방지)
  - weapon_dbid: 장착 총기 blueprint_id
  - hit_point: Raycast 피격 좌표 (하늘에 쏜 경우 미설정, proto3 message 필드 null)
  - hit_object_id: 피격 대상 object_id (없으면 0xFFFFFFFF)

Server → D2C_BROADCAST_WEAPON_FIRE → 발사자를 제외한 다른 모든 플레이어
  - shooter_object_id: 발사자 object_id
  - hit_point: 탄착 좌표 (없으면 미설정)
```

### 이미 작업된 뼈대 (루트 세션에서 완료)

| 파일 | 변경 내용 |
|------|-----------|
| `Assets/Scripts/Controller/GameObjectControllers/GameObjectController.cs` | `GetObjectId()` public 접근자 추가 |
| `Assets/Scripts/Controller/GameObjectControllers/PlayerController.cs` | `ProcessHit()` 구현 — Raycast 결과에서 hitObjectId 추출 + `SendC2DRequestWeaponFire()` 호출 |
| `Assets/Scripts/Network/UDPManager.cs` | `SendC2DRequestWeaponFire()` 추가. fireSequence는 내부에서 `Handler.NextFireSequence()`로 자동 할당 |
| `Assets/Scripts/Network/PacketHandler.cs` | `_fireSequence` 필드 + `NextFireSequence()` 추가. `Handle_D2CBroadcastWeaponFire()` 핸들러 등록 + 구현 → `IngameScene.HandleWeaponFireBroadcast()` 호출 |
| `Assets/Scripts/Scenes/IngameScene.cs` | `HandleWeaponFireBroadcast()` 뼈대 — shooterObjectId로 OppoPlayerController 조회까지 완료, 이펙트 TODO |

### 클라이언트 측 TODO (세부 구현)

#### 발사 측 (PlayerController.cs)

1. **로컬 히트 이펙트**: `ProcessHit()` 내 TODO — 자신의 탄착점에 파티클 이펙트 재생 (탄착 먼지, 스파크, 피 등)
2. **탄약 차감 주석 해제**: `Fire()` 내 magazine.quantity-- 주석 해제 (현재 테스트용으로 주석 처리됨, line 273 부근)

#### 수신 측 (IngameScene.cs → OppoPlayerController.cs)

1. **발사자 이펙트**: `HandleWeaponFireBroadcast()` 내 TODO — 머즐 플래시, 총성 사운드. 이미 동기화된 해당 플레이어의 장착 총기 정보를 활용하여 총기별 다른 효과 재생 가능
2. **탄착 이펙트**: `HandleWeaponFireBroadcast()` 내 TODO — hasHitPoint가 true일 때 hitPoint 좌표에 이펙트 재생

### 설계 결정사항 (하위 세션에서 변경하지 말 것)

- **fire_sequence 위치**: `PacketHandler`에서 관리 (`_rSeqNum`, `_uSeqNum`과 동일 레벨). PlayerController가 아님
- **브로드캐스트에 총기 정보 미포함**: 수신 클라이언트는 이미 동기화된 장착 총기 정보를 보유하고 있으므로 대역폭 절약
- **브로드캐스트 unreliable 전송**: 발사 빈도가 높고 유실되어도 치명적이지 않음
- **hit_point 미설정 = 하늘에 쏨**: Raycast가 아무 surface에도 맞지 않은 경우. 이때 탄착 이펙트 불필요

### 탄약 동기화 정책

- 장전 시점: 엄격하게 수량 동기화
- 발사 중: 느슨한 동기화 허용 (클라이언트-서버 간 일시적 불일치 가능)
- 클라이언트는 로컬에서 탄약 차감하되, 서버가 최종 권한을 가짐
