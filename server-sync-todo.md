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
| 37 | `D2CNotifyWeaponChanged` | T8 | 등록 완료 (2026-08-21) |
| 39 | `D2CNotifyDespawnObject` | T6 | 대기 |
| 40 | `D2CNotifyPlayerKilled` | T10 | 등록 완료 (2026-08-21) |

전부 reliable 수신. 38 `C2DRequestSwitchWeapon`은 송신 전용(T9).

**다섯 개를 미리 몰아 등록하지 않는다.** 구현 없이 빈 스텁으로 등록하면 패킷이 조용히 삼켜져 처리된 것처럼 보인다. 미등록으로 두면 `PacketHandler.cs`의 디스패치가 `등록되지 않은 패킷 ID` 경고를 남겨 아직 안 붙었다는 게 드러난다. 각 담당 작업에서 구현과 같이 등록할 것.

### [x] T4. objectId → GameObject 레지스트리 신설 — 완료 (2026-08-20)
`Assets/Scripts/Scenes/IngameScene.cs`, `Assets/Scripts/Network/PacketHandler.cs`

- `_sceneObjects`(`Dictionary<uint, GameObjectController>`) 신설 — `_oppoPlayers`와 같은 패턴
- **실제 작업은 딕셔너리 추가가 아니라 스폰 경로 일원화였다.** `Handle_D2CResponseSpawnByObjectId`가 씬을 거치지 않고 `Managers.Resource.InstantiateFromObjectDataStruct()`를 직접 호출하고 있었다. 그대로 뒀다면 등록되지 않은 오브젝트가 생겨 레지스트리에 구멍이 났을 것
- `IngameScene.SpawnObject(ObjectData)`를 **유일한 스폰 경로**로 삼고 호출부 3곳을 전부 라우팅: `Init()`의 정적 오브젝트, `SpawnMeAndRequestPlayerObjects()`의 동적 오브젝트, 지연 스폰 응답 핸들러
- 중복 검사·`_despawnedObjectIds` 차단·`ObjectPaths` 매핑 검사가 이 함수 하나에 모인다

### [x] T5. `D2CNotifySpawnObject`(36) 처리 — 완료 (2026-08-20)
게임 도중 생긴 오브젝트가 질의 없이 서버 push로 도착한다(시신 컨테이너 + 런타임 스폰 오브젝트 전부).

- 페이로드가 `D2CResponseSpawnByObjectId`와 동일해 `UnityGameObject` → `ObjectData` 변환부를 `PacketHandler.ToObjectData()`로 분리해 양쪽이 재사용
- 처리는 T4의 `SpawnObject()` 재사용 — 중복 도착·디스폰 차단이 자동으로 따라온다
- **매핑 없는 `object_type`을 명확한 에러 로그로 처리.** 원래는 빈 경로로 로드에 실패해 `Failed to load prefab : `만 남았다. 시신 컨테이너(type 3)가 프리팹 제작 전까지 이 경로를 타므로 타입 번호와 objectId를 찍는다. `Define.ObjectPaths`의 `Undefined`는 키는 있으나 경로가 null이라 함께 걸러낸다

### [x] T6. `D2CNotifyDespawnObject`(39) 처리 — 완료 (2026-08-20)
비플레이어 오브젝트 파괴 통보. 사유 필드 없음(현재 제거 경로가 '파괴' 하나뿐).

- `IngameScene.DespawnObject(objectId)` — `_despawnedObjectIds` 등록 → 레지스트리에서 제거 → `Managers.Resource.Destroy`
- **판단 확정: 이미 사라진 컨테이너에 `C2DCloseContainer`를 보내지 않는다.** 서버가 이미 파괴한 오브젝트라 처리 대상이 없고 실패 응답만 유발할 여지가 있다. `CloseContainer()`에서 서버 통보를 분리해 `CloseContainerLocal()` 신설, 파괴 경로는 이쪽만 호출
- 차단 가드는 T4의 `SpawnObject()`가 이미 들고 있어 별도 작업이 없었다

### [x] T7. `D2CDespawnPlayerObject`(35) 처리 — 완료 (2026-08-20)
`Assets/Scripts/Network/PacketHandler.cs`, `Assets/Scripts/Scenes/IngameScene.cs`

지금까지 **다른 플레이어가 사라지는 경로 자체가 없었다**(스폰만 있고 디스폰이 없음). 당사자는 받지 않고 룸의 나머지에게만 온다.

- `Handle_D2CDespawnPlayerObject` + `IngameScene.DespawnPlayerObject()` — `_oppoPlayers`에서 제거 후 `Managers.Resource.Destroy`
- `DespawnReason`별 연출은 `TODO:` 마커로 보류. 연출 에셋이 `Assets/Scripts` 밖이라 현재는 사유와 무관하게 즉시 제거한다
- **유령 재스폰 차단은 두 곳이다.** 문서가 원래 지목한 `UpdatePlayerStates()`의 요청 억제만으로는 부족하다 — 더 위험한 건 **이미 보낸 요청의 응답**이다. `C2DRequestSpawnByObjectId`를 쏜 뒤 디스폰이 먼저 도착하면, 뒤늦게 온 `D2CSpawnPlayerObject`(13)가 `SpawnPlayerObject()`로 들어가 그대로 되살린다. 따라서 `UpdatePlayerStates()`(요청 억제)와 `SpawnPlayerObject()`(스폰 무시) 양쪽에 가드를 둔다

**`_despawnedObjectIds` 설계 — 만료 없는 씬 수명 `HashSet<uint>`**

- **전제 확인 완료 (2026-08-20)**: objectId는 한 게임 안에서 **단조 증가**하며 재사용되지 않고, 죽은 오브젝트가 살아나지도 않는다. proto에 문서화된 보장은 아니므로 서버 계약이 바뀌면 이 설계를 다시 볼 것
- 만료 창을 두지 않은 이유: 창을 몇 초로 잡든 그보다 늦게 도착한 패킷이 뚫는다. 특히 재전송 한도를 없앤 뒤(T2) reliable 패킷이 아주 늦게 도착하는 경로가 넓어졌다. 영구 목록은 도착 시점과 무관하게 정확하다
- 비용은 논점이 아니다 — 한 판에 쌓여야 플레이어 수십 + 파괴 오브젝트 수백, 매치 최대 15분이라 무한 증가도 아니다
- 씬 인스턴스 필드라 매치가 바뀌면 자연히 비므로 별도 초기화가 없다
- **플레이어·비플레이어 공용 목록이다**(objectId 공간이 공용). T5·T6에서 비플레이어 스폰 경로에도 같은 가드를 얹을 것
- 차단 발동 시 로그는 남기지 않는다. 정상 상황(인플라이트 잔여분)에서도 똑같이 찍혀 오작동을 가려내지 못하고, 규약을 벗어난 서버 오작동까지 클라가 감당할 범위가 아니다

### [x] T8. `D2CNotifyWeaponChanged`(37) 처리 — 완료 (2026-08-21)
`Assets/Scripts/Network/PacketHandler.cs`, `Assets/Scripts/Scenes/IngameScene.cs`, `Assets/Scripts/Controller/GameObjectControllers/OppoPlayerController.cs`

**도착 경로는 둘이 아니라 셋이다.** 남의 경로가 두 갈래라 T9 없이도 이 작업이 필요했다 — 남이 `C2DRequestEquipItem`(슬롯 0/1)으로 무기를 장착·해제할 때도 통보가 온다(proto 245~249). 지금까지는 스폰 시점 외형이 그대로 남아 있었다.

`IngameScene.HandleWeaponChanged(objectId, weaponId)` 분기:

1. `_despawnedObjectIds` → 무시 (로그 없음, T7 규약)
2. `_spawnCompleted && objectId == _myObjectId` → `RollbackWeaponPrediction()`. **본인 수신은 항상 거부다** — 성공은 룸의 '나머지'에게만 가므로 성공/실패 분기가 필요 없다. `_spawnCompleted` 가드는 `_myObjectId` 초기값 `0`이 실재하는 objectId여서 스폰 전 비교가 성립하지 않기 때문
3. `_oppoPlayers`에 있으면 `EquipWeapon((int)weaponId)` — **`weaponId != 0` 가드를 붙이면 안 된다.** 걸러내면 맨손 전환이 반영되지 않는다
4. 그 외 → `RequestSpawnIfUnknown()`. 이번 `weaponId`는 버린다. 스폰 응답의 `weapon_id`가 이 통보보다 최신이라 pending 맵을 둘 이유가 없다

**⚠ 아래 2·4항과 롤백 설명은 2026-08-21 proto 변경으로 폐기됐다 — T9 항목을 볼 것.** 성공도 요청자에게 오게 되어 "본인 수신 = 거부"가 성립하지 않고, `slot` 필드가 생겨 `item_id` 추정도 필요 없어졌다. 여기 남겨두는 이유는 당시 판단 근거를 잃지 않기 위해서다.

~~**롤백은 T9와 맞물린다.** 통보에 `weapon_id`만 있고 슬롯이 없어, 지금은 주/보조의 `item_id`와 대조해 한쪽만 일치할 때만 `IsPrimaryWeaponApplyed`를 확정한다.~~

**부수 작업 3건**

- `RequestSpawnIfUnknown()` 신설 + `_pendingSpawnRequests` — 아래 별도 항목
- `OppoPlayerController._equippedWeaponId` 보관 — 킬 피드가 킬러 무기를 싣지 않아 T10이 이 값을 쓴다(proto 481·485). 프리팹 캐시 미스는 조용한 return이었는데 맨손으로 보이고 원인이 안 남아 T5와 같은 방식의 에러 로그로 승격
- **기존 버그 수정** — `ApplyEquipItem`이 `currentWeapon != null`일 때만 `EquipWeapon`을 호출해, **내가 무기를 해제하면 손에 든 무기가 그대로 남았다**. 서버는 남들에게 맨손을 통보하므로 T8을 붙이는 순간 화면이 어긋난다. `EquipWeapon(currentWeapon != null ? item_id : 0)`으로 해소

`armor_id`는 스펙에서 삭제되었다. 남의 방어구는 어떤 패킷으로도 오지 않는다.

### [x] T8-a. 지연 스폰 요청 1회 제한 (`_pendingSpawnRequests`) — 완료 (2026-08-21)
`Assets/Scripts/Scenes/IngameScene.cs`

T8의 분기 4를 붙이면서 드러난 기존 문제. `UpdatePlayerStates()`가 미스폰 objectId를 볼 때마다 **10Hz로 `C2DRequestSpawnByObjectId`를 새로 만들고 있었다.** 재전송이 아니라 매번 새 시퀀스라, 응답이 3초만 늦어도 서로 다른 reliable 30개가 in-flight에 쌓인다. T2에서 재전송 한도를 없애며 32슬롯 상한을 지키던 장치가 사라져, 넘치는 순간 `MakeReliablePacket`이 아직 ACK되지 않은 **다른** 패킷을 덮어쓴다(장착·귀환 요청이 조용히 유실된다).

- `RequestSpawnIfUnknown(objectId)` 하나로 판정을 모았다: 디스폰 목록 → `_oppoPlayers` → `_sceneObjects` → `_pendingSpawnRequests.Add()` 성공 시에만 전송
- `_sceneObjects`까지 보는 이유는 `C2DRequestSpawnByObjectId`가 플레이어·비플레이어 공용이기 때문(응답이 `D2CSpawnPlayerObject` / `D2CResponseSpawnByObjectId`로 갈린다). T10에서 또 판단할 일이 없도록 헬퍼가 두 공간을 다 덮는다
- **만료를 두지 않는다.** 요청은 reliable이라 ACK될 때까지 알아서 재전송되므로 한 번이면 충분하고, 응답도 디스폰 통보도 오지 않는 objectId는 서버가 모르는 것이라 재요청해도 결과가 같다
- 해제 지점 4곳: `SpawnPlayerObject` / `SpawnObject`(응답 도착 = 요청 종료, 스폰 여부와 무관하게 조기 반환보다 앞에서 제거) / `DespawnPlayerObject` / `DespawnObject`
- `UpdatePlayerStates()`도 이 헬퍼를 쓰도록 교체했다

### [x] T9. `C2DRequestSwitchWeapon`(38) 송신 + 무기 전환 — 완료 (2026-08-21)
`Assets/Scripts/Network/UDPManager.cs`, `Assets/Scripts/Network/PacketHandler.cs`, `Assets/Scripts/Scenes/IngameScene.cs`, `Assets/Scripts/Scenes/IngameInventory.cs`, `Assets/Scripts/Controller/GameObjectControllers/PlayerController.cs`

**착수 직전에 서버가 계약을 바꿨다(proto 2026-08-21).** `D2CNotifyWeaponChanged`에 `slot`·`inventory_version`이 추가되고 **성공도 요청자에게 오게 되면서**, T8이 세워둔 "본인 수신 = 거부" 전제와 `item_id` 추정 롤백이 통째로 폐기됐다. 아래는 새 계약 기준이다.

- **로컬 예측을 쓰지 않는다.** 키 입력 시점에는 요청만 보내고, 손의 무기는 서버 통보가 온 뒤에만 바꾼다. 확정 전 재요청을 막으므로 in-flight 요청이 항상 1개이고, 그 결과 **정상 경로에서는 통보 순서 역전이 발생하지 않는다**
- 판정: `slot == 보낸 target_slot`이면 성공, 아니면 거부. 다만 **상태 반영은 성공·거부가 같다**(도착한 `slot`/`weapon_id`가 항상 권위값). 갈리는 것은 재동기화 여부뿐이라 `ApplyServerWeaponState()` 하나로 처리하고 분기는 그 밖에 둔다
- 거부 중 **버전 불일치일 때만** `RequestRecentInventoryInfo()`. **자동 재요청은 하지 않는다**(버전이 계속 움직이면 루프가 된다) — 재입력은 사용자에게 맡긴다
- **통보의 `inventory_version`으로 로컬 버전을 갱신하지 않는다.** 버전만 맞추고 슬롯 내용이 낡으면 다음 요청이 '버전은 맞는데 내용은 틀린' 상태로 통과한다. 갱신은 재동기화 응답에 맡긴다
- **교체 확정 전 발사 차단** — `IngameScene.IsWeaponSwitchPending`을 `PlayerController.IsShooting` 조건에 추가. reliable(교체)과 unreliable(사격) 사이에 순서 보장이 없어 사격이 먼저 처리되면 서버가 조용히 버린다. `_fireBlocked`는 마우스 재클릭으로 풀려 재사용할 수 없다
- **TEMP 워치독(3초)** — 통보가 오지 않으면 발사가 그 판 내내 막힌다. 만료 시 잠금만 풀고 무기 상태는 건드리지 않는다(판정 권한은 서버 유지). T15 귀환 워치독과 같은 비대칭 논리
- `C2DRequestWeaponFire.weapon_dbid`를 인벤토리에서 다시 유도하지 않고 `PlayerController._equippedWeaponId`(서버가 확정해 장착시킨 값)로 교체. proto가 '장착한'이 아니라 **'손에 든'** 무기를 요구하도록 바뀌었다
- 1/2 키(`Key.Digit1`/`Digit2`) 바인딩. UI 열림 중에도 허용한다
- **`IngameInventory.ApplyWeapon()`은 TODO 해소가 아니라 삭제했다** — 예측을 쓰지 않으면서 호출자가 하나도 없어졌고, 하는 일이 `ApplyServerWeaponState()`와 완전히 겹친다
- `PlayerController.EquipWeapon()`에 같은 무기면 재생성하지 않는 가드 + 프리팹 캐시 미스 에러 로그 추가(T8에서 `OppoPlayerController`에 넣은 것과 대칭). 인벤토리 조작마다 손에 안 든 슬롯을 건드려도 무기가 파괴·재생성되고 있었다

**`OPTION:` 통보 순서 역전 방어** — 클라가 수신 reliable을 중복 제거하지 않아(`PacketHandler`가 디스패치 전 중복 검사를 하지 않는다), ACK 유실로 재전송된 옛 통보가 새 통보 뒤에 도착하면 낡은 슬롯이 다시 적용된다. 다음 교체에서 교정되므로 방치 가능. 막으려면 마지막 반영 `rSeqNum`을 기억해야 하는데 `HandlerFunc`가 페이로드만 받아 **핸들러 25개의 시그니처 변경**이 선행된다. 로컬 예측을 도입하면 그때는 필수다.

**`OPTION:` 무기 전환 로컬 예측** — 즉시 적용 후 거부 시 롤백. 서버 통보가 정확히 오는 것이 확인된 뒤 도입한다. 도입하면 발사 차단 게이트와 재요청 차단이 함께 풀리므로 위 순서 방어가 전제 조건이 된다.

### [x] T9-a. 장착 조작 시 '손에 든 슬롯' 규칙 반영 — 완료 (2026-08-21)
`Assets/Scripts/Scenes/IngameScene.cs`

proto가 규칙을 명문화하면서(`C2DRequestEquipItem` 주석) T8 시점 구현이 서버와 어긋나 있던 것이 드러났다. 무기 슬롯 조작으로 손에 든 슬롯이 바뀌는 경우 **본인에게는 통보가 오지 않아** 클라가 직접 반영해야 한다.

- 규칙: **들고 있던 슬롯이 비었을 때만 반대쪽으로 옮긴다**(양쪽 다 비면 맨손). 그 외에는 손에 든 슬롯을 유지하고, 그 슬롯의 무기가 바뀌었으면 새 무기로 갱신
- T8에서 "해제하면 맨손"으로 고쳐둔 것이 이 규칙과 어긋났다. 서버가 보조무기를 손에 들려준 상태에서 클라만 맨손이 되면 **`weapon_dbid` 불일치로 사격이 통째로 무시된다**. 그전 코드(해제해도 이전 무기가 손에 남음)도 방향만 다른 오류였다
- `SyncHeldWeapon()`으로 분리해 `ApplyEquipItem`에서 호출

### [~] T10. `D2CNotifyPlayerKilled`(40) 처리 — 데이터 경로 완료 (2026-08-21) / **UI 대기**
`Assets/Scripts/Network/PacketHandler.cs`, `Assets/Scripts/Scenes/IngameScene.cs`, `Assets/Scripts/Controller/GameObjectControllers/PlayerController.cs`

- **판단 확정**: 킬 피드 UI는 붙이지 않는다(프리팹 = `Assets/Scripts` 밖). 패킷 처리·킬러 무기 추적·표기 문자열까지 코드로 완성하고 출력만 `Util.Log`로 둔다. 프리팹이 생기면 `HandlePlayerKilled`의 `TODO:` 자리에서 표시부만 갈아끼우면 된다
- `killer_object_id == 0xFFFFFFFF`는 가해자 없는 죽음이며 **`NO_ATTACKER_OBJECT_ID`를 그대로 재사용**했다 — `attacker_object_id`와 의미가 같은 전투 문맥 상수다. 세 번째 동일 값 상수를 만들지 않는다
- 킬러 무기는 실려 오지 않으므로(통보가 사격보다 한 틱 뒤라 그 사이 교체되면 틀린 값) T8이 만든 `OppoPlayerController.EquippedWeaponId`를 쓴다. 본인이 킬러인 경로를 위해 `PlayerController.EquippedWeaponId`도 공개
- **미스폰 킬러는 `RequestSpawnIfUnknown()`으로 채운다**(살아있는 플레이어라 다음 표기부터 무기가 맞는다). **피해자는 요청하지 않는다** — 같은 타이밍에 디스폰 통보가 오므로 요청해도 버려지고, T7의 차단 목록에 걸릴 뿐이다
- ~~**내 죽음에는 이 패킷이 오지 않아**(피해자 제외) 킬 피드의 내 사망 줄은 `HandleHealthChange`에서 만든다.~~ **⚠ 2026-08-21 proto 재변경으로 폐기 — T18을 볼 것.** 이 패킷이 **피해자에게도 오게 되었고** 사망 확정 신호를 겸한다. `HandleHealthChange`의 `_deathReported` 블록은 기점이 이중화되어 제거 대상이다(두 패킷이 모두 도착하므로 로그가 중복되고 순서에 따라 기점이 흔들린다)
- 캐릭터 제거 연출은 T7(`D2CDespawnPlayerObject`) 담당이며 이 패킷은 표기만 한다. **단 자기 캐릭터는 그 통보가 오지 않아 스스로 치워야 한다**(T18)

**남은 것**: 킬 피드 UI 프리팹 + 표시부. 사망 처리는 T18, 매치 종료 화면은 T15 소관이다.

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

### [~] T12. 실드 재생 로컬 예측 — 구현 (2026-08-19) / 게이지 연결 (2026-08-20) / Editor 설정 (2026-08-21) — **런타임 실측 대기**
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

**실측 경과**

- (2026-08-20) **값은 정상적으로 들어오는데 이미지가 변하지 않았다.** 스크립트 경로는 `fillAmount` 대입 직전까지 정상이고, 원인은 `HealthBarFill`/`ArmorBarFill`의 **Image Type이 `Filled`가 아닌 것** 하나로 좁혀졌다(`Simple`/`Sliced`면 대입이 에러 없이 무시된다)
- (2026-08-21) **Editor 작업 완료** — 두 Fill 이미지의 Type을 `Filled`로 맞추고 스프라이트를 신규 `Assets/Resources/White_Square.png`(Sprite, border 0)로 지정. 코드 변경 없음. 씬 오버라이드(`TestIngame.unity`)에는 한쪽 Image의 `m_Type`만 실리는데, 나머지는 프리팹 기본값이 이미 `Filled`라 정상이다. 이 제약은 앞으로 추가될 게이지에도 그대로 적용되므로 `Assets/Scripts/UI/CLAUDE.md`의 `IngameHealthBarUI` 항목에 상시 규칙으로 남겨둔다

**⚠ (2026-08-25) 서버 계약 추가 — 스폰 시 실드는 최대치다.** 0에서 차오르는 게 아니다. 최대치의 출처가 방어구 스펙뿐이라 인벤토리 도착 후에야 채울 수 있어, `SyncHealthBarMax()` 직후 일회성 `TryInitShield()`로 적용한다(`_itemLoaded` 가드 필수 — 없으면 최대치 0이 적용되며 플래그가 소진된다). **방어구 착용·해제·교체가 0에서 다시 차는 규칙은 그대로**이며 일회성 플래그가 둘을 가른다.

**남은 것**: 런타임 시각 검증만. 매치 진입 직후 HP 만피 + **실드도 만피**(`[ShieldInit]` 로그와 대조) → 방어구 해제 시 0 → 재착용 후 초당 100(=1%) 상승 → 피격 시 서버값 점프(`[HealthChange]` 로그와 대조). 이게 통과하면 T11·T12를 함께 종료 처리한다. **어긋나면 원인은 이제 스크립트 쪽이다** — Editor 설정이 유일한 잔여 변수였으므로, 남는 후보는 `SyncHealthBarMax()` 호출 시점과 실드 예측 누적 로직이다.

### [ ] T13. `object_type = 3` (Corpse) 매핑 — **프리팹 대기**
`Assets/Scripts/Utils/Define.cs:29-40`

- **결정 (2026-08-20): 시신 컨테이너 프리팹을 새로 만든다. 제작은 나중에 추가 예정** — 기존 컨테이너 재사용 아님
- `Define.ObjectType`에 `Corpse` 추가 + `ObjectPaths` 항목 추가. **프리팹이 생기기 전까지 `ObjectPaths` 항목은 넣을 수 없다**(없는 경로를 넣으면 로드 실패만 남는다)
- 시신은 플레이어 사망 시 그 자리에 스폰되는 동적 오브젝트. 사망자의 인벤토리·장착·탄창이 전부 이 컨테이너로 옮겨진다
- 스폰은 T5 경로로 통보되고, 열고 집는 조작은 일반 컨테이너와 동일(`C2DRequestOpenContainer` / `C2DRequestInteractContainerObject`) → 프리팹에 `ContainerController`가 붙어야 한다
- **T5와의 관계**: 프리팹이 없는 동안 시신 스폰 패킷이 오면 인스턴스화가 실패한다. T5에서 매핑 없는 `object_type`을 명확한 경고로 처리해 원인이 드러나게 할 것

### [ ] T14. 비플레이어 전투 대상에 `ICombatTarget` 구현 — **정보 필요**
`Assets/Scripts/Controller/ICombatTarget.cs`

- 서버가 이제 HP를 가진 비플레이어 오브젝트도 피격 대상으로 처리한다
- 클라 Raycast(`PlayerController.ProcessHit`, `:313-337`)는 `ICombatTarget` 구현체만 objectId를 싣고, 아니면 `0xFFFFFFFF`(미피격)로 보낸다
- `GetObjectId()`는 `GameObjectController`에 이미 있으므로 대상 클래스에 인터페이스 선언만 추가하면 됨
- **어떤 오브젝트가 HP를 갖는지 서버 목록 확인 필요**

### [ ] T15. 귀환 성공 후처리 (TEMP 해소) — **T18과 출구를 공유한다**
`Assets/Scripts/Scenes/IngameScene.cs:282-294`

- `D2CResponseRecall(result=true)`는 이제 '귀환 완료'가 아니라 '귀환 시작 승인'(클라는 이미 이 전제로 구현됨)
- ✅ (2026-08-21) `HandleRecallResult` 실처리 — 성공 시 `BeginMatchExit(Recalled)` + **잠금 유지**(이미 맵을 떠났으므로 재요청 경로가 열려선 안 된다)
- ✅ (2026-08-21) 취소 `reason`별 분기 — `PLAYER_DEAD`는 사망 흐름이 이미 출구를 잡고 있어 아무것도 하지 않고, `SESSION_LOST`는 재요청이 불가능하므로 출구로, 나머지(`OUT_OF_ZONE`·`SERVER_INTERNAL`·`UNKNOWN`)는 잠금 해제해 재시도를 허용한다
- ✅ (2026-08-21) 연결 끊김 출구 — 수신 워치독 만료 시 `HandleConnectionLost()`로 같은 출구를 탄다
- **남은 것**: 성공 시 갈 **게임 결과 씬**(T18과 공유하는 `TODO:`)
- 성공 통보의 ACK는 기존 파이프라인이 자동 처리하므로 추가 작업 없음. **(2026-08-21 갱신) ACK는 세션이 언제 끊기느냐만 좌우한다** — 인벤토리 반출 자체는 귀환 성공 시점에 확정되며, 늦어도 3초 뒤에는 끊긴다
- 진행 중 재요청은 서버가 조용히 무시한다 → 응답 대기 상태로 두면 안 된다. TEMP 워치독(`RECALL_TIMEOUT`)이 이 경로를 떠받치고 있으므로 T15 완료 전까지 제거 금지

---

## 1순위 (2026-08-21 추가) — 사망 유예

### [~] T18. 사망 후 유예 처리 — 구현 완료 (2026-08-21) / **연출·결과 씬 대기**
`Assets/Scripts/Scenes/IngameScene.cs`, `Assets/Scripts/Controller/GameObjectControllers/PlayerController.cs`, `Assets/Scripts/Network/UDPManager.cs`

**사망·탈출·끊김이 하나의 출구를 공유한다.** `BeginMatchExit(reason)` → `MATCH_EXIT_DELAY`(4초) → `CompleteMatchExit()`(UDP 종료).

- **유예를 4초로 둔 이유**(서버 계약은 5초): 서버가 세션을 정리하기 전에 클라가 먼저 정리하고, 통보 ACK가 나갈 시간을 번다. 상수 주석에 근거를 남겼다 — 서버 값에 맞춰 5초로 "고치면" 순서가 뒤집힌다
- **ACK 타이밍 문제와 `SendHeartbeatNow()`**: ACK는 다음 송신에 piggyback되는데, 유예 중에는 상태 전송이 멈춰 송신이 하트비트뿐이고 주기가 3초다. 그대로 두면 통보 ACK가 최대 3초 늦어 4초 예산의 여유가 1초뿐이다. 이탈 시작 시점에 하트비트를 한 번 강제로 보내 해소했다(끊김 사유는 보낼 곳이 없어 제외)
- **입력 잠금은 `IsInputLocked` 하나로 모았다.** 사격(`IsShooting`)·시점(`ProcessMouseLook`)·이동·조준/상호작용 판정(`ProcessAim`)과 패킷을 보내는 `RequestXXX` 전부가 이 값을 본다. 이동은 입력만 끊고 **중력은 유지**한다(공중에서 죽어도 시신이 떠 있지 않게)
- **수신은 아무것도 막지 않는다** — 브로드캐스트가 계속 오는 것이 관전 유지의 근거다. `OnUpdate`는 이탈 중 상태 전송·상호작용 갱신만 건너뛴다
- **연결 끊김 통보는 `UDPManager.OnUpdate`의 워치독 지점에 뒀다.** `Disconnect()` 안에 넣으면 재연결 직전 정리(`RegisterEndPointAndStart`)에서도 불려 접속할 때마다 이탈 처리가 돈다
- **`IngameScene.Clear()` 오버라이드**가 커서·UDP 정리를 맡는다. `Managers.Clear()` → `Scene.Clear()` → `CurrentScene.Clear()`로 씬 전환 시 자동으로 불리므로, 유예를 다 쓰지 않고 나가는 경로에서도 정리가 보장된다
- 내 시신 컨테이너는 `D2CNotifySpawnObject`로 오지만 프리팹이 없어 T5의 에러 로그를 탄다(T13 대기, 예상된 동작)

- **사망 연출 구현 완료 (2026-08-21)** — `DeathCameraController`(독립 MonoBehaviour, 프리팹 없음). 기존 카메라를 옮기지 않고 **같은 시점의 카메라를 새로 만들어 전환**한다. 옮기면 `PlayerController.ApplyViewRotation()`이 매 프레임 되돌린다. 상세는 `Assets/Scripts/Controller/CLAUDE.md`

**남은 것**: **게임 결과 씬 전환**과 **탈출(귀환) 연출**이 `TODO:`로 남아 있다. 결과 씬이 없어 지금은 4초 뒤 연결만 끊고 인게임 씬에 그대로 머문다 — **테스트 종료는 당분간 강제 종료로 한다(합의된 상태)**.

**409는 다루지 않기로 확정** — 4초 유예 + 결과 씬 경유 + 로비 재진입이면 DB 반영이 끝나 있다고 본다. 매치메이킹 실패 응답 처리는 나중에 일괄로 묶는다.

### (원문) T18 서버 계약 — proto 공통 사항 6
서버가 사망 즉시 세션을 끊지 않고 **5초의 유예**를 두게 되었다. 목적은 급격한 씬 전환을 막는 것 하나이며, 조작이나 결과를 되돌릴 기회가 아니다.

| 항목 | 내용 |
|------|------|
| 기점 | `D2CNotifyPlayerKilled.victim_object_id == _myObjectId` |
| 유예 중 수신 | 룸 브로드캐스트 **전부 계속 온다**(이동·발사·스폰·킬 피드) → 관전 화면 유지 가능 |
| 유예 중 송신 | `C2DHeartBeat` 외 **전부 응답 없이 버려진다**(사격·교체·장착·귀환·컨테이너) |
| 종료 | 5초 뒤 서버가 세션을 정리하고 하향 트래픽이 끊긴다 |

- **유예 길이는 어떤 패킷으로도 오지 않는다.** 클라 상수 5000ms로 둘 것. 서버가 바꾸면 조용히 어긋나므로 상수 한 곳에 모으고 근거(proto 공통 사항 6)를 주석에 남길 것
- **자기 캐릭터의 `D2CDespawnPlayerObject`는 오지 않는다.** 유예 동안 화면에 남겨두고 사망 연출을 재생하라는 의도이며, 제거는 클라가 스스로 한다
- 유예 중 요청은 서버가 버리므로 **클라도 입력을 막아야 한다** — 안 막으면 반응 없는 조작(사격·상호작용)이 되고, 로컬 예측이 있는 부분은 서버와 어긋난 상태로 보인다
- **T10 수정이 선행된다**: `HandleHealthChange`의 `_deathReported` 블록을 제거하고 사망 기점을 `HandlePlayerKilled`로 일원화
- 결과(인벤토리 소실·시신 스폰·DB 반영·매칭 락 해제)는 **사망하는 순간 이미 확정**된다. 유예 중 연결을 끊어도 결과는 같다
- **재매칭 409 처리** — DB 반영이 비동기라, 유예를 다 쓰지 않고 즉시 재매칭을 걸면 409가 올 수 있다. 실패로 표시하지 말고 잠시 후 재시도할 것(로비 HTTP 흐름)
- **T15와 출구를 공유한다.** 사망·귀환 성공·연결 끊김 셋 다 "인게임 정리 → 로비 복귀"가 필요하며, 이 중 사망만 5초라는 예산이 정해져 있다

---

## 3순위 — 확인만

### [ ] T16. 윈체스터 맵 이동 동기화 실측
서버가 윈체스터 룸에서 `D2CUpdatePlayerStates`를 아예 브로드캐스트하지 않던 버그를 고쳤다. 클라 수정 사항 없음 — 두 맵에서 타 플레이어 이동 동기화가 동일하게 동작하는지 확인만.

### [ ] T17. proto `[작업사항]` 주석 블록 삭제
전 항목 반영 후 `External_Protocol.proto`의 `[작업사항]`으로 시작하는 주석만 일괄 삭제(그 외 주석은 유지). 이 파일도 함께 삭제하고 결과를 `progress.md`로 이관.

---

## 착수 전 결정해야 할 것

1. ~~**T2** — 클라 재전송 한도~~ → 제거로 확정 (2026-08-19)
2. **T13** — 시신 컨테이너 프리팹을 새로 만들지, 기존 컨테이너 프리팹을 재사용할지
3. ~~**T9** — 무기 전환의 로컬 예측 방식~~ → **응답 대기 후 적용**으로 확정 (2026-08-21). 예측은 동작 검증 후 `OPTION:`으로 도입
4. **T10** — 킬 피드 UI를 이번에 붙일지, 로그만 남기고 미룰지
5. **T14** — HP를 갖는 비플레이어 오브젝트 목록(서버 확인)
6. ~~**초기 손 무기 규칙**~~ → **확정 (2026-08-21, 서버 확인)**. 서버도 **주무기가 있으면 주무기, 없으면 보조무기**를 처음 손에 들린다 — 클라 `IngameInventory.InitWeapon()`과 같은 규칙이므로 수정할 것이 없다. 양쪽 다 비면 맨손인 것도 일치
