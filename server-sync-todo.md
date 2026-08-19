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

### [~] T3. 핸들러 등록 5개 — **구현과 함께 하나씩 등록하는 방식으로 변경**
`Assets/Scripts/Network/PacketHandler.cs` 생성자

| PktId | 메시지 | 담당 | 상태 |
|-------|--------|------|------|
| 35 | `D2CDespawnPlayerObject` | T7 | 등록 완료 (2026-08-20) |
| 36 | `D2CNotifySpawnObject` | T5 | 대기 |
| 37 | `D2CNotifyWeaponChanged` | T8 | 대기 |
| 39 | `D2CNotifyDespawnObject` | T6 | 대기 |
| 40 | `D2CNotifyPlayerKilled` | T10 | 대기 |

전부 reliable 수신. 38 `C2DRequestSwitchWeapon`은 송신 전용(T9).

**다섯 개를 미리 몰아 등록하지 않는다.** 구현 없이 빈 스텁으로 등록하면 패킷이 조용히 삼켜져 처리된 것처럼 보인다. 미등록으로 두면 `PacketHandler.cs`의 디스패치가 `등록되지 않은 패킷 ID` 경고를 남겨 아직 안 붙었다는 게 드러난다. 각 담당 작업에서 구현과 같이 등록할 것.

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
- **`_despawnedObjectIds`에 등록 + 비플레이어 스폰 경로에 가드를 얹을 것** — T7이 만든 공용 목록을 그대로 쓴다. 늦게 도착한 스폰 응답이 파괴된 오브젝트를 되살리는 레이스가 플레이어와 동일하게 존재한다
- **판단 필요**: 이미 사라진 컨테이너에 `C2DCloseContainer`를 보낼지. 현재 `IngameScene.CloseContainer()`(`:244`)는 무조건 전송한다 — `_despawnedObjectIds`로 판정 가능

### [x] T7. `D2CDespawnPlayerObject`(35) 처리 — 완료 (2026-08-20)
`Assets/Scripts/Network/PacketHandler.cs`, `Assets/Scripts/Scenes/IngameScene.cs`

지금까지 **다른 플레이어가 사라지는 경로 자체가 없었다**(스폰만 있고 디스폰이 없음). 당사자는 받지 않고 룸의 나머지에게만 온다.

- `Handle_D2CDespawnPlayerObject` + `IngameScene.DespawnPlayerObject()` — `_oppoPlayers`에서 제거 후 `Managers.Resource.Destroy`
- `DespawnReason`별 연출은 `TODO:` 마커로 보류. 연출 에셋이 `Assets/Scripts` 밖이라 현재는 사유와 무관하게 즉시 제거한다
- **유령 재스폰 차단은 두 곳이다.** 문서가 원래 지목한 `UpdatePlayerStates()`의 요청 억제만으로는 부족하다 — 더 위험한 건 **이미 보낸 요청의 응답**이다. `C2DRequestSpawnByObjectId`를 쏜 뒤 디스폰이 먼저 도착하면, 뒤늦게 온 `D2CSpawnPlayerObject`(13)가 `SpawnPlayerObject()`로 들어가 그대로 되살린다. 따라서 `UpdatePlayerStates()`(요청 억제)와 `SpawnPlayerObject()`(스폰 무시) 양쪽에 가드를 둔다

**`_despawnedObjectIds` 설계 — 만료 없는 씬 수명 `HashSet<uint>`**

- **전제: 서버는 한 게임 안에서 objectId를 재사용하지 않는다.** proto에 명시된 보장이 아니라 구두 전제이므로 서버 확인 필요. 이 전제가 깨지면 해당 오브젝트가 끝까지 안 보이며, 증상이 원인에서 가장 먼 곳에 나타난다
- 만료 창을 두지 않은 이유: 창을 몇 초로 잡든 그보다 늦게 도착한 패킷이 뚫는다. 특히 재전송 한도를 없앤 뒤(T2) reliable 패킷이 아주 늦게 도착하는 경로가 넓어졌다. 영구 목록은 도착 시점과 무관하게 정확하다
- 비용은 논점이 아니다 — 한 판에 쌓여야 플레이어 수십 + 파괴 오브젝트 수백, 매치 최대 15분이라 무한 증가도 아니다
- 씬 인스턴스 필드라 매치가 바뀌면 자연히 비므로 별도 초기화가 없다
- **플레이어·비플레이어 공용 목록이다**(objectId 공간이 공용). T5·T6에서 비플레이어 스폰 경로에도 같은 가드를 얹을 것
- 차단 발동 시 로그는 남기지 않는다. 정상 상황(인플라이트 잔여분)에서도 똑같이 찍혀 오작동을 가려내지 못하고, 규약을 벗어난 서버 오작동까지 클라가 감당할 범위가 아니다

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

### [x] T11. `D2CNotifyHealthChange.attacker_object_id` 반영 — 완료 (2026-08-19)
`Assets/Scripts/Network/PacketHandler.cs`, `Assets/Scripts/Scenes/IngameScene.cs`

- 핸들러에서 `attacker_object_id` 파싱 + `HandleHealthChange()` 시그니처 확장
- `NO_ATTACKER_OBJECT_ID`를 `PLAYER_OBJECT_ID`와 **별도 상수로** 신설 — 값은 같지만 의미가 다르다(가해자 없음 vs 내 인벤토리). `0`은 실재하는 objectId이므로 '미설정' 해석 금지. T10 `killer_object_id`도 같은 규칙
- `_currentHealthPoint` / `_currentShieldPoint` 신설해 서버 절대값 보관 → **T12 실드 예측이 이 필드를 이어받는다**
- 교전 상대 추적: `_lastAttackerObjectId` + `ATTACKER_TRACK_DURATION`(5초) 만료 창. `LastAttackerObjectId` / `HasRecentAttacker` 노출 → T10 킬 피드, T15 킬러 표기에서 재사용
- 피격 방향 각도 산출은 **`OPTION:` 마커로 보류**. 표시 UI(프리팹)가 없어 지금 계산해도 쓸 곳이 없고, 가해자가 미스폰이거나 비플레이어 전투 오브젝트(T14)인 경로를 UI 작업 때 같이 다루는 게 맞다
- `reason`은 `int` 유지 — `REASON_ITEM_HEAL`을 발생시키는 서버 경로가 아직 없어 enum 승격 시 빈 분기만 늘어난다
- 검증용 `Util.Log` 1줄 추가. 게이지 갱신은 T12에서 연결됨
- 이 패킷은 여전히 피해 입은 본인에게만 온다

### [x] T12. 실드 재생 로컬 예측 — 구현 완료 (2026-08-19) / 게이지 연결 (2026-08-20)
`Assets/Scripts/Scenes/IngameScene.cs`, `Assets/Scripts/UI/IngameScene/IngameHealthBarUI.cs`

- 서버 공식 그대로 구현: `UpdateShieldRegen()`이 `(재생량 × 경과ms)`를 `_shieldRegenAccum`에 누적해 1000에 도달할 때마다 1 회복. 실수 보간이 아닌 정수 회복이라 서버와 어긋나지 않는다
- 중단 조건: 사망(`_currentHealthPoint <= 0`), 방어구 미착용, 상한 도달
- `_currentHealthPoint` 초기값은 `MAX_HEALTH_POINT`. **0으로 두면 첫 피격 전까지 사망으로 오판해 재생이 아예 안 돈다** (HP는 스폰 시 어떤 패킷으로도 오지 않는다)
- 방어구 스펙 캐시(`_maxShieldPoint`/`_shieldRegenPerSecond`)는 `SyncHealthBarMax()`가 갱신. **`SyncInventoryUI()`의 `_ingameInventoryUI == null` 가드보다 앞으로 옮겼다** — 전투 예측이 UI 오브젝트 존재 여부에 묶이면 안 된다
- 리셋 3곳: 피격 수신(서버 절대값 + 누적기 0) / `ApplyEquipItem`의 `equipmentSlotType == 2`(착용·해제·교체 전부 `ResetShieldPrediction()`) / 스폰 시 필드 초기값
- 남의 실드·HP·방어구는 어떤 패킷으로도 오지 않는다

**선행 버그 해소 (2026-08-20)** — `IngameHealthBarUI`의 문제는 `[SerializeField]` 누락이 아니라 **`Init()` 자체가 없는 것**이었다. `IngameSceneUI` 폴더에 `[SerializeField]`는 하나도 없고 전부 `Init()` + `transform.Find` 패턴이다. `Init()`(`HealthBarBg/HealthBarFill`, `ArmorBarBg/ArmorBarFill`) 신설 + `IngameScene.Init()`에서 호출로 해결.

연결 과정에서 나온 문제 2건도 함께 고쳤다:

- **초기 게이지 값이 한 번도 안 밀렸다** — `SetHP`/`SetArmor` 호출부가 피격·재생 틱뿐이라 첫 피격 전까지 프리팹에 저장된 `fillAmount`가 그대로 보였다. `SyncHealthBarMax()`에서 최대치 세팅 직후 현재값도 밀도록 추가(최대치 → 현재값 순서여야 `SetArmor`가 최대 실드 0 가드에 안 걸린다)
- **최대 실드가 0이 될 때 실드 바가 안 비워졌다** — `SetArmor()`의 `_maxShield <= 0f` 조기 반환 때문에 벗기 직전 `fillAmount`가 남았다. 반환 대신 `fillAmount = 0`으로 교체. 조건은 '방어구 교체'가 아니라 **최대 실드가 0이 되는 것**이며 해당 경로는 셋 — 해제 / `_armorSpecs`에 없는 방어구로 교체(현재 Armor 아이템이 id 4 하나뿐이라 도달 불가하나 스펙 등록 누락 시 재현) / equip 분기에 빈 슬롯이 소스로 들어와 실질 해제가 되는 경우. **정상 교체는 새 방어구 최대치가 들어와 가드를 통과하므로 원래부터 문제없다**

**실측 결과 (2026-08-20)**: **값은 정상적으로 들어오는데 이미지가 변하지 않는다.** 즉 스크립트 경로는 `fillAmount` 대입 직전까지 정상이고, 남은 원인은 `HealthBarFill`/`ArmorBarFill`의 **Image Type이 `Filled`가 아닌 것** 하나다(`Simple`/`Sliced`면 대입이 에러 없이 무시된다).

**남은 것**: Editor에서 Image Type을 `Filled`로 변경 + Fill Method·Origin 설정. **코드 작업 아님** — 후순위로 미뤄둠(`progress.md` 우선순위 4번). 이후 시각 검증: 매치 진입 직후 HP 만피·실드 0 → 방어구 착용 후 초당 100(=1%) 상승 → 피격 시 서버값 점프(`[HealthChange]` 로그와 대조) → 방어구 해제 시 0.

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
