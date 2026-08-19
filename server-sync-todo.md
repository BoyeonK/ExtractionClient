# 서버 변경분 클라이언트 반영 작업 리스트

> 작성: 2026-08-19
> 대상: `Assets/Scripts/Network/External_Protocol.proto`의 `[작업사항]` 주석 블록 (서버 2026-08-12 이후 변경분)
> 성격: **임시 문서**. 전 항목 반영이 끝나면 proto의 `[작업사항]` 블록과 함께 이 파일도 삭제하고, 결과는 `progress.md`에 옮겨 적을 것

---

## 선행 확인 사항 (조사 완료)

- `Assets/Scripts/Network/ExternalProtocol.cs`는 **이미 새 proto 기준으로 재생성되어 있다**. `.gitignore` 대상이라 git diff에는 안 잡히지만 `PktId` 35~40, `D2CNotifyWeaponChanged`, `C2DRequestSwitchWeapon`, `D2CNotifyHealthChange.AttackerObjectId` 등 C# 스텁이 전부 존재 → **protoc 재실행 불필요**
- 이미 구현되어 있어 손댈 필요 없는 것:
  - `D2CNotifyRecallResult` 핸들러 + 2단계 귀환 흐름 (`HandleRecallResponse` / `HandleRecallResult`)
  - `D2CSpawnPlayerObject` 핸들러 + `weapon_id` 처리 + 중복 스폰 방어(`_oppoPlayers.ContainsKey`)
  - 히트스캔의 `ICombatTarget` 기반 피격 대상 판정
  - `OppoPlayerController.EquipWeapon(0)` → 기존 무기 파괴 후 return, 즉 맨손 처리가 이미 올바름

---

## 0순위 — 안 하면 세션이 끊긴다

### [x] T1. `timestampEcho`를 모든 수신 패킷에서 갱신 — 완료 (2026-08-19)
`Assets/Scripts/Network/PacketHandler.cs`

`_timestampEcho`가 **`FLAG_RELIABLE` 패킷을 받을 때만** 갱신되던 문제. 서버가 unreliable로만 보내는 구간(`D2CUpdatePlayerStates`, `D2CHeartBeat`, `D2CBroadcastWeaponFire`)이 6초 이어지면 세션이 강제 이탈된다.

- `UpdateTimestampEcho()` 신설, reliable 분기 밖에서 전 패킷 호출. `UpdateRecvAckState()`는 ACK 상태만 담당하도록 축소
- 역행 방지: 더 큰 값일 때만 갱신 (세션 최대 15분이라 랩어라운드 미고려)
- 갱신 스레드는 기존 방식대로 `ExecuteAtMainThread` 위임 유지. 송신 경로가 전부 메인 스레드라 워커 스레드 직접 대입은 이득이 없음
- 송신 측은 원래부터 `BuildPacketInto`에서 전 패킷에 에코를 싣고 있었음 — 수정 불필요
- 서버 규칙은 `Network/CLAUDE.md`에 기록

### [x] T2. 재전송 한도 폐기 + 수신 워치독 도입 — 완료 (2026-08-19)
`Assets/Scripts/Network/PacketHandler.cs`, `Assets/Scripts/Network/UDPManager.cs`

ACK 실패 횟수로 연결 생사를 판정하던 방식을 폐기하고, 수신 여부 단독 판정으로 교체.

- `MAX_RETRY` 제거. `CollectRetransmits`의 `out shouldDisconnect`도 함께 제거 — 연결이 살아있다고 보는 동안에는 무한 재시도
- RTO는 기존 RTT 기반 값 고정. 백오프 미도입 — `_retransmitCache`·`byte[]` 재사용으로 CPU/GC 비용이 사실상 0이고 실사용 in-flight가 0~2개라 대역폭 부담이 없다
- `PacketHandler.LastRecvSec` 신설(서명 검증 통과분만 기록, T1의 per-패킷 훅에 합류). `UDPManager.OnUpdate`에서 `RECV_TIMEOUT_SEC`(10초) 초과 시 `Disconnect()`
- 워치독 시드는 `SetSessionVariable`. 0으로 두면 접속 즉시 오탐
- in-flight 32개 초과 시 슬롯 덮어쓰기 경고를 런타임 에러 로그로 승격 — 재전송 한도가 겸하던 상한이 사라져 유실이 무증상이 되기 때문

**남은 것**: 끊김 판정 후 처리가 없다. `Disconnect()`는 소켓만 정리하고 플레이어는 인게임 씬에 그대로 남는다. 안내 UI·씬 전환은 T15(귀환 성공 후 매치 종료 화면)와 묶어 처리할 것.

---

## 1순위 — 신규 패킷 배선

### [ ] T3. 핸들러 등록 5개
`Assets/Scripts/Network/PacketHandler.cs` 생성자 (`:94-113`)

| PktId | 메시지 | 비고 |
|-------|--------|------|
| 35 | `D2CDespawnPlayerObject` | T7 |
| 36 | `D2CNotifySpawnObject` | T5 |
| 37 | `D2CNotifyWeaponChanged` | T8 |
| 39 | `D2CNotifyDespawnObject` | T6 |
| 40 | `D2CNotifyPlayerKilled` | T10 |

전부 reliable 수신. 38 `C2DRequestSwitchWeapon`은 송신 전용(T9).

### [ ] T4. objectId → GameObject 레지스트리 신설 (T5/T6 선행)
`Assets/Scripts/Scenes/IngameScene.cs`

현재 스폰된 비플레이어 오브젝트를 objectId로 되찾을 수단이 **전혀 없다**. `ResourceManager.InstantiateFromObjectDataStruct`(`Assets/Scripts/Managers/ResourceManager.cs:35`)는 컨트롤러에 `_objectId`만 심고 어디에도 등록하지 않는다. `_oppoPlayers`와 같은 패턴으로 씬이 보유할 것.

### [ ] T5. `D2CNotifySpawnObject`(36) 처리 — 신규
게임 도중 생긴 오브젝트가 질의 없이 서버 push로 도착한다(시신 컨테이너 + 런타임 스폰 오브젝트 전부).

- 페이로드가 `D2CResponseSpawnByObjectId`와 **동일** → `Handle_D2CResponseSpawnByObjectId`(`PacketHandler.cs:562`)의 `UnityGameObject` → `ObjectData` 변환부를 공용 함수로 분리해 재사용
- 같은 objectId가 중복 도착할 수 있으므로 이미 있으면 무시(T4 레지스트리로 판정)

### [ ] T6. `D2CNotifyDespawnObject`(39) 처리 — 신규
비플레이어 오브젝트 파괴 통보. 사유 필드 없음(현재 제거 경로가 '파괴' 하나뿐).

- objectId로 오브젝트 제거 (T4 레지스트리 + `Managers.Resource.Destroy`)
- 그 컨테이너 UI가 열려 있었다면 닫기
- **판단 필요**: 이미 사라진 컨테이너에 `C2DCloseContainer`를 보낼지. 현재 `IngameScene.CloseContainer()`(`:244`)는 무조건 전송한다

### [ ] T7. `D2CDespawnPlayerObject`(35) 처리 — 신규
지금까지 **다른 플레이어가 사라지는 경로 자체가 없었다**(스폰만 있고 디스폰이 없음). 당사자는 받지 않고 룸의 나머지에게만 온다.

- `_oppoPlayers`에서 제거 + 오브젝트 파괴
- `DespawnReason`별 연출 분기: `RECALLED`(탈출 연출 후 제거) / `DEAD`(사망 연출, 동시에 T10 + 시신 스폰이 온다) / `DISCONNECTED`(연출 없이 제거)
- **부수 문제**: `UpdatePlayerStates()`(`IngameScene.cs:163-173`)는 미등록 objectId를 보면 `C2DRequestSpawnByObjectId`를 쏜다. 디스폰 직후 잔여 상태 패킷이 도착하면 유령 재스폰이 발생 → 최근 디스폰 objectId 차단 처리 필요

### [ ] T8. `D2CNotifyWeaponChanged`(37) 처리 — 신규
도착 경로가 두 가지이며 `object_id`로 구분한다.

1. **남의 objectId** → 무기 변경 통보. `OppoPlayerController.EquipWeapon(weaponId)` 호출 (`weaponId = 0`은 맨손, 기존 코드가 이미 올바르게 처리)
2. **자기 objectId** → `C2DRequestSwitchWeapon` **거부 응답**. 서버가 보는 현재 무기가 담겨 오므로 로컬 예측을 이 값으로 롤백

`armor_id`는 스펙에서 삭제되었다(구 `D2CNotifyEquipmentChanged` 기준으로 작업한 게 있으면 폐기). 남의 방어구는 어떤 패킷으로도 오지 않는다.

### [ ] T9. `C2DRequestSwitchWeapon`(38) 송신 — 신규
- `Assets/Scripts/Network/UDPManager.cs`에 송신 함수 추가 (reliable)
- `IngameInventory.ApplyWeapon()`의 TODO 해소 (`Assets/Scripts/Scenes/IngameInventory.cs:112`, `:118`)
- **1/2 키 바인딩 추가** — 현재 `ApplyWeapon` 호출자가 아예 없다
- `target_slot`은 토글이 아닌 절대 지정(0=주무기, 1=보조무기). 재전송·순서 역전에 안전
- `my_inventory_version`에 가장 최근 서버 인벤토리 버전(`Inventory.InventoryVersion`)을 실을 것
- 성공/거부 모두 `D2CNotifyWeaponChanged`로 돌아오므로 성공·실패 분기 불필요
- **설계 판단 필요**: 응답에 `weapon_id`만 오고 slot이 없어 주/보조가 같은 blueprint면 구분 불가 → 보낸 `target_slot`을 로컬에 기억해두고 대조하는 방식 권장

### [ ] T10. `D2CNotifyPlayerKilled`(40) 처리 / 킬 피드 UI — 신규
- 피해자 본인 제외, 룸 전체 수신. `killer_object_id == 0xFFFFFFFF`면 가해자 없는 죽음(**0이 아니다**)
- 캐릭터 제거 연출은 T7이 담당, 이 패킷은 킬 피드 표기만
- 킬러 무기는 안 실려 온다 → 표기가 필요하면 `D2CSpawnPlayerObject.weapon_id` + T8로 추적한 값 사용
- **판단 필요**: 킬 피드 UI를 이번에 붙일지(프리팹·씬 오브젝트 필요 = `Assets/Scripts` 밖 작업), 로그만 남기고 미룰지

---

## 2순위 — 기존 패킷 의미 변경

### [ ] T11. `D2CNotifyHealthChange.attacker_object_id` 반영
`Assets/Scripts/Network/PacketHandler.cs:1042`, `Assets/Scripts/Scenes/IngameScene.cs:437`

- 필드 4번 추가됨. 핸들러 파싱 + `HandleHealthChange()` 시그니처 확장
- **`0xFFFFFFFF` = 가해자 없음(회복 등). `0`은 실재하는 objectId이므로 '미설정'으로 해석 금지**
- 용도: 피격 방향 표시, 교전 상대 추적, 사망 시 킬러 표기
- 이 패킷은 여전히 피해 입은 본인에게만 온다

### [ ] T12. 실드 재생 로컬 예측
전용 통보 패킷이 없다. 서버는 매 틱 회복만 시키고 아무 패킷도 보내지 않으므로 클라가 직접 계산해야 한다.

- **선행 버그**: `Assets/Scripts/UI/IngameScene/IngameHealthBarUI.cs:5-6`의 `_hpFillImage` / `_armorFillImage`가 `[SerializeField]` 없는 private 필드라 **영구 null → 게이지가 전혀 갱신되지 않는다**. 여기부터 고쳐야 결과가 눈에 보인다
- 현재 실드값을 `IngameScene`이 보관 (지금은 UI로 흘려보내기만 함, `:437-442`)
- 서버 규칙 그대로 구현: `(재생량 × 경과ms)`를 누적해 1000이 될 때마다 1 회복, `ArmorSpec.MaxShieldPoint` 상한, 사망 시 중단
- 재생량 출처: `ItemDBHelper.TryGetArmorSpec()` → `ArmorSpec.RegenerationPerSecond`
- `D2CNotifyHealthChange` 수신 시 서버 절대값으로 리셋
- **방어구 신규 착용 시 실드 0 초기화** → `ApplyEquipItem()`(`IngameScene.cs:392`)의 `equipmentSlotType == 2` 분기에 추가. UI를 0으로 떨어뜨리고 예측 재시작
- 남의 실드·HP·방어구는 어떤 패킷으로도 오지 않는다

### [ ] T13. `object_type = 3` (Corpse) 매핑 — **판단 필요**
`Assets/Scripts/Utils/Define.cs:29-40`

- `Define.ObjectType`에 `Corpse` 추가 + `ObjectPaths` 항목 추가
- 시신은 플레이어 사망 시 그 자리에 스폰되는 동적 오브젝트. 사망자의 인벤토리·장착·탄창이 전부 이 컨테이너로 옮겨진다
- 스폰은 T5 경로로 통보되고, 열고 집는 조작은 일반 컨테이너와 동일(`C2DRequestOpenContainer` / `C2DRequestInteractContainerObject`) → 프리팹에 `ContainerController`가 붙어야 한다
- **프리팹 제작은 `Assets/Scripts` 밖이라 먼저 문의 필요**. 기존 컨테이너 프리팹 재사용으로 갈지 결정할 것

### [ ] T14. 비플레이어 전투 대상에 `ICombatTarget` 구현 — **정보 필요**
`Assets/Scripts/Controller/ICombatTarget.cs`

- 서버가 이제 HP를 가진 비플레이어 오브젝트도 피격 대상으로 처리한다
- 클라 Raycast(`PlayerController.ProcessHit`, `:313-337`)는 `ICombatTarget` 구현체만 objectId를 싣고, 아니면 `0xFFFFFFFF`(미피격)로 보낸다
- `GetObjectId()`는 `GameObjectController`에 이미 있으므로 대상 클래스에 인터페이스 선언만 추가하면 됨
- **어떤 오브젝트가 HP를 갖는지 서버 목록 확인 필요**

### [ ] T15. 귀환 성공 후처리 (TEMP 해소)
`Assets/Scripts/Scenes/IngameScene.cs:282-294`

- `D2CResponseRecall(result=true)`는 이제 '귀환 완료'가 아니라 '귀환 시작 승인'(클라는 이미 이 전제로 구현됨)
- `HandleRecallResult`가 아직 TEMP 로그뿐 → 성공 시 매치 종료 화면 전환 + 잠금 유지(이미 맵을 떠나므로)
- 취소 시 `reason`별 분기: `OUT_OF_ZONE`·`SERVER_INTERNAL`은 재시도 허용, `PLAYER_DEAD`·`SESSION_LOST`는 각 흐름에 위임
- 성공 통보의 ACK는 기존 파이프라인이 자동 처리하므로 추가 작업 없음(서버가 최대 3초 대기 후 세션 정리)
- 진행 중 재요청은 서버가 조용히 무시한다 → 응답 대기 상태로 두면 안 된다. TEMP 워치독(`RECALL_TIMEOUT`)이 이 경로를 떠받치고 있으므로 T15 완료 전까지 제거 금지

---

## 3순위 — 확인만

### [ ] T16. 윈체스터 맵 이동 동기화 실측
서버가 윈체스터 룸에서 `D2CUpdatePlayerStates`를 아예 브로드캐스트하지 않던 버그를 고쳤다. 클라 수정 사항 없음 — 두 맵에서 타 플레이어 이동 동기화가 동일하게 동작하는지 확인만.

### [ ] T17. proto `[작업사항]` 주석 블록 삭제
전 항목 반영 후 `External_Protocol.proto`의 `[작업사항]`으로 시작하는 주석만 일괄 삭제(그 외 주석은 유지). 이 파일도 함께 삭제하고 결과를 `progress.md`로 이관.

---

## 착수 전 결정해야 할 것

1. **T2** — 클라 재전송 한도(상향 / 제거 / 유지)
2. **T13** — 시신 컨테이너 프리팹을 새로 만들지, 기존 컨테이너 프리팹을 재사용할지
3. **T9** — 무기 전환의 로컬 예측 방식(예측 후 롤백 / 응답 대기 후 적용)
4. **T10** — 킬 피드 UI를 이번에 붙일지, 로그만 남기고 미룰지
5. **T14** — HP를 갖는 비플레이어 오브젝트 목록(서버 확인)
