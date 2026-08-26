# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`LobbyScene`**: `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching)
- **`LoadingScene`**: 비동기 로딩 → 90% 도달 시 Blueprint 요청 → 응답 완료 시 씬 전환
- **`IngameScene`**: 인게임 맵 씬들의 공통 베이스. 정적 오브젝트 스폰 + 씬 내장 UI 바인딩 + 스폰 요청. 실제 맵 씬은 이 클래스를 상속
- **`GameResultScene`**: 매치 결과 표시 + 엔터로 로비 복귀. 진입 경로는 `IngameScene.CompleteMatchExit()` 하나뿐
- 씬 전환: 반드시 `Managers.Scene` 사용
- **입력 리스너·업데이트 루프 안에서는 씬을 직접 내리지 말고 `Managers.ExecuteAtMainThread`로 예약할 것** — `LoadScene()`의 `Managers.Clear()`가 순회 중인 구독 목록을 비운다 (`Managers/CLAUDE.md` 참조)

## 씬 이름 규칙

**`SceneManagerEx.GetSceneName()`이 `Define.Scene` 항목 이름을 그대로 `SceneManager.LoadScene()`에 넘긴다.** 따라서 enum 항목 이름 = `.unity` 파일 이름이어야 하며, 파일 이름은 모두 `Scene`으로 끝낸다. 씬을 추가·리네임하면 enum도 같이 고치고 Build Settings 등재를 확인할 것 — 어긋나면 컴파일은 통과하고 **전환 시점에 런타임으로 터진다**.

씬 담당 컴포넌트 클래스와 이름이 겹치지만(`LobbyScene`, `LoadingScene`, `GameResultScene`, `TestIngameScene`) enum 멤버는 항상 `Define.Scene.X`로 한정 접근되므로 충돌하지 않는다. 깨지는 경우는 `using static Define.Scene;`뿐이니 쓰지 말 것.

## 씬 전환 페이로드 (`GameSceneContext`)

`SceneManagerEx`가 `NextSceneStaticContext`(정적)와 `SceneDynamicContext`(동적) 두 인스턴스를 보유. `PacketHandler`에서 `AddObjectDatas()`로 누적, `IsComplete()`로 수신 완료 판정, `IngameScene.Init()`에서 소비.

### 매치 결과 (`GameResult` / `LastGameResult`)

`SceneManagerEx.cs` 파일 스코프의 `GameResult` 구조체(`MatchExitReason`도 같은 위치). `IngameScene.CompleteMatchExit()`이 `SetGameResult()`로 채우고 결과 씬이 소비한다.

- `LastGameResult`는 `GameResult?` — null이 '결과 없음'. **`ResetLoadSceneOp()`에서 지우지 말 것** — 결과 씬 진입 초기화에서도 불려 소비 전에 날아간다. 제거는 소비자의 `ClearGameResult()` 또는 다음 매치 종료의 덮어쓰기로 한다
- 내용: 이탈 사유 + 인벤토리 25슬롯(배치 유지, 빈 슬롯 null) + 무기 2·방어구(**탄창 제외**) + 킬수 2종. 인벤토리는 클라 로컬 상태 기준이라 서버와 어긋날 수 있으나 표시용으로 감수한다(서버 재조회 없음)
- 같은 아이템 목록이라도 **사유별 의미가 다르다** — Recalled=반출 확정, Dead·ConnectionLost=잃은 것

### 로비 복귀와 세션 유지 (`IsReturnFromGameResult`)

`GameResultScene.MoveToLobby()`가 플래그를 세우고 `LobbyScene.Init()`이 소비한다. 결과 씬 경유(true)와 게임 최초 시작(false)을 구분해, 경유일 때만 `TryResumeSession()`으로 Login 과정을 건너뛴다.

- **`LastGameResult`와 같은 이유로 `ResetLoadSceneOp()`에서 지우지 말 것** — 로비 초기화에서도 불려 소비 전에 날아간다. 소비자인 `LobbyScene.Init()`이 읽은 뒤 직접 false로 되돌린다
- 세션이 살아남는 근거는 `Managers.Clear()`가 `Sound`/`Input`/`Scene`/`UI`만 건드려 **HTTPManager의 `SessionId`·`AuthState`는 씬 전환에서 초기화되지 않는다**는 것이다. 여기에 매니저를 추가할 때 주의할 것
- 버전 체크(`GetVersionCall`)는 프로세스 재시작이 아니므로 이 경로에서 생략한다
- 성공하면 `OnLoginComplete(AuthState 기준 Logined/Guest)`를 재사용한다 — 별도 진입 경로를 만들지 말 것
- 실패 처리와 폴백 정책은 `Network/CLAUDE.md`의 '세션 유지' 절 참조

## IngameScene 스폰 흐름

1. `RequestSpawnMe()` → `C2DRequestSpawnMe` 전송
2. 서버 응답: `D2CResponseSpawnMeSpawnSpot`(스폰 좌표) + `D2CResponseSpawnMeDynamicObjects`(동적 오브젝트)
3. 두 응답 모두 수신 시 `SpawnMeAndRequestPlayerObjects()` → 플레이어 인스턴스화 + `TryInitWeapon()` + 동적 오브젝트 스폰
4. 서버 응답 `D2CSpawnPlayerObjects` → 다른 플레이어 일괄 스폰
5. 완료 시 상태 전송 루프(0.1초) 활성화 + 로딩 완료 통보

### 초기 무기 장착 흐름

`_spawnCompleted`와 `_itemLoaded` 두 조건이 모두 충족될 때 `TryInitWeapon()` 1회 호출. 도착 순서가 보장되지 않으므로 양쪽 시점에서 모두 호출한다.

## 무기 프리팹 캐시 (`WeaponPrefabCache`)

`IngameScene`이 보유. `Resources.LoadAll<GameObject>("Prefabs/Weapons")`로 1회 로드, `weaponId → prefab` 매핑. 프리팹 네이밍: `Weapon_{id}_{name}`.

## 인게임 인벤토리 (`IngameInventory`)

`IngameScene`이 보유하는 순수 C# 클래스. 서버 주도 `D2CFullInventorySync`로 동기화.

- 슬롯 구조: `_inventorySlots[25]` + 무기 2 + 방어구 1 + 탄창 2 + 컨테이너 슬롯
- `_isPrimaryWeaponApplyed`: 현재 주무기/보조무기 전환 상태
- `PLAYER_OBJECT_ID(0xFFFFFFFF)`: objectId로 플레이어/컨테이너 슬롯을 구분하는 규칙
- 외부 접근: `ingameScene.Inventory.XXX`

## 체력 상태 관리

`IngameScene`이 `_currentHealthPoint`/`_currentShieldPoint`로 서버 절대값을 보관. `D2CNotifyHealthChange` 수신 시 갱신되며 이 패킷은 **피해 입은 본인에게만** 온다(남의 HP·실드는 어떤 패킷으로도 오지 않는다).

- **`0xFFFFFFFF`는 문맥에 따라 의미가 다르다.** 인벤토리 문맥의 `PLAYER_OBJECT_ID`(내 인벤토리)와 전투 문맥의 `NO_ATTACKER_OBJECT_ID`(가해자 없음)는 값이 같아도 별개 상수로 유지할 것. `killer_object_id`, `hit_object_id`도 전투 문맥 쪽이다
- **`0`은 실재하는 objectId다.** proto3 기본값이라고 '미설정'으로 해석하면 오귀속이 된다
- 교전 상대 추적: `_lastAttackerObjectId`는 `ATTACKER_TRACK_DURATION` 안에서만 유효하며 `LastAttackerObjectId`/`HasRecentAttacker`로 조회

### 실드 재생 예측

전용 통보 패킷이 없다. 서버는 매 틱 회복만 시키고 아무것도 보내지 않으므로 클라가 같은 공식으로 예측하고 피격 통보마다 서버 절대값으로 리셋한다.

- `UpdateShieldRegen()`이 `(재생량 × 경과ms)`를 누적해 `SHIELD_REGEN_ACCUM_UNIT`(1000)마다 1 회복. **실수 보간으로 바꾸지 말 것** — 서버가 정수 누적이라 값이 어긋난다
- 방어구 스펙 캐시는 `SyncHealthBarMax()`가 갱신하며, `SyncInventoryUI()`의 UI null 가드보다 **앞에서** 호출된다. 전투 예측을 UI 오브젝트 존재 여부에 묶지 말 것
- `SyncHealthBarMax()`는 최대치를 넣은 뒤 현재값까지 게이지로 민다. 이게 없으면 첫 피격 전까지 프리팹에 저장된 `fillAmount`가 그대로 보인다. **최대치 → 현재값 순서를 지킬 것** — 뒤집으면 `SetArmor`가 최대 실드 0에 걸린다
- `_currentHealthPoint`의 초기값은 `MAX_HEALTH_POINT`다. HP는 스폰 시 어떤 패킷으로도 오지 않으므로 0으로 두면 사망으로 오판해 재생이 멈춘다
- **`MAX_HEALTH_POINT`(20000 = 200.00 HP)는 서버 `PlayerObject::DEFAULT_MAX_HP`와 손으로 맞추는 값이다.** 최대치를 보내는 패킷이 없어 클라 쪽 유일한 출처이며, 어긋나도 컴파일·통신은 정상이고 체력바 비율만 조용히 틀어진다. 한쪽만 고치지 말 것. 전투 수치는 전부 1/100 고정소수점(실제값 × 100)이라 HP 관련 로직에 숫자 리터럴을 쓰면 출처가 갈린다
- **스폰 시 실드는 최대치다(서버 규칙).** 최대치의 출처가 방어구 스펙뿐이라 HP처럼 필드 초기값으로 둘 수 없어, `SyncHealthBarMax()`가 `_maxShieldPoint`를 채운 직후 `TryInitShield()`가 일회성으로 적용한다. **`_itemLoaded` 가드를 빼지 말 것** — 인벤토리 도착 전 호출에서는 방어구가 null이라 최대치가 0이고, 그대로 플래그가 소진되면 실드가 끝까지 0이 된다
- 방어구는 착용·해제·교체 어느 경로든 실드가 0에서 다시 찬다(서버 규칙). `ApplyEquipItem`의 `equipmentSlotType == 2`에서 `ResetShieldPrediction()`. **최초 스폰과 갈리는 지점이며 `TryInitShield()`의 일회성 플래그가 이 둘을 가른다**

## 킬 피드 (`HandlePlayerKilled`)

`D2CNotifyPlayerKilled`는 **피해자를 포함한** 룸 전체에 온다. 남의 캐릭터 제거는 `D2CDespawnPlayerObject`가 담당하고 이 패킷은 표기를 다룬다.

- **이 패킷이 피해자 본인의 '사망 확정' 신호를 겸한다.** `victim_object_id`가 내 objectId면 자기 사망이고 그 시점부터 **5초 유예**가 시작된다. 사망 기점을 `D2CNotifyHealthChange`(HP 0)로 잡지 말 것 — 두 패킷이 모두 오므로 기점이 이중화된다
- **자기 캐릭터의 디스폰 통보는 오지 않는다.** 유예 동안 화면에 남겨두고 스스로 치워야 한다
- **사망 판정을 HP 0으로 하지 말 것.** `HandleHealthChange`에서 함께 감지하면 두 패킷이 모두 도착해 기점이 이중화되고 순서에 따라 흔들린다

## 매치 이탈 (`BeginMatchExit` / `IsInputLocked`)

사망·귀환 성공·연결 끊김 셋이 **하나의 출구**를 공유한다. `BeginMatchExit(reason)` → `MATCH_EXIT_DELAY`(4초) → `CompleteMatchExit()`(UDP 종료). 새 이탈 사유가 생기면 여기에 붙일 것.

- **4초는 서버 계약(5초)보다 짧게 잡은 값이다.** 서버가 세션을 정리하기 전에 클라가 먼저 정리하고, 통보 ACK가 나갈 시간을 번다. 서버 값에 맞춰 5초로 늘리지 말 것
- **이탈 시작 시 하트비트를 강제로 한 번 보낸다**(`SendHeartbeatNow`). ACK는 다음 송신에 piggyback되는데 유예 중에는 송신이 하트비트뿐이고 주기가 3초라, 그냥 두면 ACK가 최대 3초 늦는다
- **입력 잠금은 `IsInputLocked` 하나로 본다** — 사격·시점·이동·조준/상호작용 판정과 패킷을 보내는 `RequestXXX` 전부. 서버가 하트비트를 뺀 모든 요청을 버리므로, 클라가 막지 않으면 반응 없는 조작이 된다. 이동은 입력만 끊고 **중력은 유지**한다
- **수신은 막지 않는다.** 브로드캐스트가 계속 오는 것이 관전 유지의 근거다. `OnUpdate`는 이탈 중 상태 전송·상호작용 갱신만 건너뛴다
- **연결 끊김 통보는 `UDPManager`의 수신 워치독 지점에 있다.** `Disconnect()` 안에 넣지 말 것 — 재연결 직전 정리에서도 불려 접속할 때마다 이탈 처리가 돈다
- 사망 사유일 때 `DeathCameraController.Play()`로 탑뷰 연출을 시작한다. 자기 캐릭터는 디스폰 통보가 오지 않아 유예 동안 그 자리에 남으므로 그대로 연출 대상이 된다
- 씬 정리(커서·UDP)는 `IngameScene.Clear()` 오버라이드가 맡는다. `Managers.Clear()`가 씬 전환 때 자동으로 부르므로 유예를 다 쓰지 않는 경로에서도 보장된다
- **`CompleteMatchExit()`이 `GameResult` 스냅샷을 뜬 뒤 연결을 끊는다.** 스냅샷을 `BeginMatchExit`으로 당기지 말 것 — 킬 통보가 사격보다 한 틱 뒤라 죽기 직전에 쏜 탄의 킬(상호 킬)이 유예 중에 도착한다. 반대로 유예를 다 쓰지 않는 강제 씬 전환은 이 함수를 건너뛰어 결과가 저장되지 않으므로, **결과 씬 전환은 반드시 `CompleteMatchExit()`을 거칠 것**
- `killer_object_id == 0xFFFFFFFF`는 가해자 없는 죽음이며 `NO_ATTACKER_OBJECT_ID`와 같은 의미다(전투 문맥 공용). `0`은 실재 objectId
- **킬러 무기는 실려 오지 않는다** — 통보가 사격보다 한 틱 뒤라 그 사이 교체되면 틀린 값이 되기 때문이다. `EquippedWeaponId`로 추적해둔 값을 쓴다
- 모르는 **킬러**는 `RequestSpawnIfUnknown()`으로 채우고 **피해자는 요청하지 않는다**(같은 타이밍에 디스폰이 온다)

## 킬 카운트 (`RecordPlayerKill` / `RecordObjectKill`)

내가 죽인 대상의 objectId를 담는 `HashSet<uint>` 2개(플레이어/오브젝트). 킬수는 `Count`로 읽는다.

- **카운터 증가가 아니라 Set인 이유** — 수신 reliable에 중복 제거가 없어 재전송된 킬 통보가 두 번 디스패치될 수 있다. objectId는 재사용되지 않으므로 Set이면 멱등이다
- 플레이어 킬은 `HandlePlayerKilled`에서만 기록. 가드는 `_spawnCompleted` → `killer == _myObjectId` → `victim != _myObjectId` 순 (`_myObjectId` 초기값 0은 실재 objectId)
- 오브젝트 킬은 `HandleObjectKilled`(`D2CNotifyObjectKilled`, PktId 41)에서 기록한다. 피해자가 내가 될 수 없어 `victim != _myObjectId` 가드가 없는 것 외에는 플레이어 킬과 같다
- **`ObjectKillCount`는 `ICombatTarget` 보급에 종속된다** — 전투 오브젝트가 이 인터페이스를 구현하지 않으면 `PlayerController`가 `hit_object_id`에 `0xFFFFFFFF`를 실어 보내 서버가 데미지를 넣지 않는다. 패킷 배선이 끝나도 그때까지는 항상 0이다
- **`HandleObjectKilled`는 제거까지 책임진다** — 처치로 인한 제거에는 `D2CNotifyDespawnObject`가 오지 않으므로 여기서 `DespawnObject()`를 부른다. 빠뜨리면 파괴된 오브젝트가 맵에 영원히 남는다
- 오브젝트 킬에서는 **미스폰 킬러를 요청하지 않는다** — 표시부가 없어 소비처가 없고, 근처 플레이어는 상태 스트림이 채운다. 불필요한 reliable을 늘리지 않는다는 판단이며, 오브젝트 킬 피드를 붙이면 그때 재검토할 것

## 다른 플레이어 관리

- `_oppoPlayers`: objectId → `OppoPlayerController` 매핑
- `UpdatePlayerStates()`: 미등록 objectId 수신 시 `RequestSpawnIfUnknown()` (지연 스폰 대응)
- `PlayerSpawnData`/`PlayerStateData` 구조체(`SceneManagerEx.cs`): Protobuf 타입 격리를 위한 중간 변환 타입

### 지연 스폰 요청 (`RequestSpawnIfUnknown` / `_pendingSpawnRequests`)

모르는 objectId를 가리키는 패킷이 오면 `C2DRequestSpawnByObjectId`를 보내는 유일한 경로. 상태 스트림·무기 변경 통보가 쓰며, 새 통보 패킷을 붙일 때도 직접 전송하지 말고 이 함수를 쓸 것.

- **같은 objectId로 두 번 보내지 않는다.** 요청은 reliable이라 ACK될 때까지 알아서 재전송되므로 한 번이면 충분하다. 매 틱 다시 만들면 같은 내용이 서로 다른 시퀀스로 쌓여 in-flight 32슬롯을 채우고, 넘치는 순간 아직 ACK되지 않은 **다른** 패킷이 덮어써진다(`Network/CLAUDE.md` 참조). 상태 스트림이 10Hz라 이 경로는 특히 위험하다
- `_pendingSpawnRequests`에 만료를 두지 않는다. 응답도 디스폰 통보도 오지 않는 objectId는 서버가 모르는 것이라 재요청해도 결과가 같다
- 해제는 4곳 — `SpawnPlayerObject`/`SpawnObject`(응답 도착 = 요청 종료. 중복·차단으로 조기 반환하는 경우도 있으므로 **가드보다 앞에서** 제거한다), `DespawnPlayerObject`/`DespawnObject`
- **내 objectId를 걸러낸다.** 나는 `_oppoPlayers`에 없으므로 가드가 없으면 나 자신의 스폰을 요청하게 된다(킬러가 나인 킬 피드 등)
- 디스폰 목록·`_oppoPlayers`·`_sceneObjects`를 모두 확인한다. objectId 공간이 플레이어·비플레이어 공용이고 이 요청도 공용이라(응답이 `D2CSpawnPlayerObject` 또는 `D2CResponseSpawnByObjectId`로 갈린다) 한쪽만 보면 이미 아는 오브젝트를 다시 요청하게 된다

## 행동 잠금 (`TryBeginAction` / `UpdateAction` / `ClearAction`)

재장전·무기 교체처럼 **유예를 거쳐 서버로 나가는 행동**을 `IngameScene`의 잠금 하나로 묶는다. 잠금은 **사유**(`PlayerActionKind`)와 **단계**(`PlayerActionPhase`)를 함께 들고 있다 — 사유만으로는 "R 중 R은 무시, R 중 무기 교체는 우선"이 갈리지 않고, 단계가 없으면 **이미 서버로 나간 요청을 취소할 수 있다고 착각**하게 된다.

| 단계 | 뜻 | 취소 |
|---|---|---|
| `Local` | 유예 중(모션 자리). 아직 **전송 전** | 가능 — 서버에 흔적이 없다 |
| `Pending` | 전송 후 응답 대기 | **불가** — 서버가 이미 처리했다 |

진입 판정은 `TryBeginAction()` 한 곳뿐이다.

| 현재 | 새 요청 | 결과 |
|---|---|---|
| 없음 | 무엇이든 | 진입 |
| 같은 사유 + 같은 대상 | R 중 R, 1 중 1 | **무시** — 재요청이 자기 행동을 스스로 무효화하지 않게 한다 |
| 같은 사유 + 다른 대상 | | **재타게팅** (호출부가 `ClearAction()` 후 다시 진입) |
| 다른 사유 + `Local` | 재장전 중 무기 교체 | **취소 후 진입** |
| 다른 사유 + `Pending` | | **무시** — 서버 확정 전에는 새 행동의 전제(손에 든 무기·인벤토리 버전)가 미정이다 |

- **`IsActionBusy`가 발사 차단의 단일 근거다**(`PlayerController.IsFireInput`). 재장전은 유예가 곧 모션 시간이고, 교체는 확정 전 사격이 `weapon_dbid` 불일치로 버려지기 때문이다
- **in-flight 요청은 항상 1개로 유지된다.** 재타게팅이 `Local`(전송 전)에서만 일어나기 때문이며, 이것이 `rSeqNum` 순서 방어를 끌어오지 않아도 되는 근거다. `Pending` 중 재전송을 허용하는 변경은 그 방어를 함께 가져와야 한다
- **요청에 실을 값은 전송 시점에 읽는다.** 유예 중 인벤토리를 조작하면 버전이 오르는데, 시작 시점 값을 캐시하면 자기 조작 때문에 `DENY_VERSION_MISMATCH`로 거부된다
- **유예 시간은 클라 상수다**(`RELOAD_LOCAL_SEC` 2초 / `SWITCH_LOCAL_SEC` 0.5초). `WeaponSpec`에 재장전·교체 시간 필드가 없어 DB에서 오지 않으며, 애니메이션이 붙으면 클립 길이로 대체할 `TODO:`가 걸려 있다
- **달리기는 재장전의 진입 조건이자 유지 조건이다.** `RequestReload()`가 `IsRunning`이면 시작하지 않고, `UpdateAction()`이 `Local` 중 `IsRunning`이 되면 취소한다. 한 규칙을 양쪽에서 같은 식으로 본다 — 달리기를 코드가 강제로 해제하는 방식은 쓰지 않는다(Shift를 누른 채인데 안 달리는 상태가 남는다)
- **`Pending` 워치독은 `ACTION_PENDING_TIMEOUT`(3초) 하나로 통합돼 있다**(`TEMP:`). 행동을 추가할 때 워치독을 따로 만들지 말 것
- 매치 이탈(`BeginMatchExit`)은 잠금을 지운다. 이탈 중에는 `IsInputLocked`가 진입을 막는다
- **`TODO:` 귀환·상호작용의 편입 범위는 미결.** 귀환(`_recallRequested`)은 구조가 같아 흡수 가능하지만, 상호작용은 컨테이너가 열려 있는 동안 지속되는 상태라 `Pending`으로 잡으면 여는 내내 모든 행동이 잠긴다

## 재장전 (`RequestReload` / `HandleReloadResponse`)

`R` 키 → 2초 유예 → `C2DRequestReload`(42, reliable) → `D2CResponseReload`(43). **탄창 잔량과 무관하게 보낸다** — 가득 찼는지, 인벤토리에 해당 탄종이 있는지는 서버가 판정한다. 대상은 '손에 든 무기'이며 서버가 정하므로 슬롯을 싣지 않는다.

- **응답은 성공·거부 모두 인벤토리 전체 스냅샷이다**(델타가 아니다). 서버가 여러 칸에서 탄약을 나눠 빼고 0이 된 칸을 비우는데 그 스캔 순서를 클라가 재현할 수 없어서다 — proto가 명시한 의도이므로 `D2CResponseEquipItem`(단일 스왑, 인덱스만 통보)과 **형태를 통일하려 들지 말 것**
- **`D2CFullInventorySync`의 수신 경로를 재사용하지 않는다.** 담긴 메시지는 같지만 ① 전체 동기화에 딸린 최초 1회용 초기화(`_itemLoaded`·`TryInitWeapon`)가 재장전마다 돌게 되고 ② 그쪽에는 **낡은 스냅샷을 버릴 버전 비교가 없다**
- **버전 역전 방어**: `inventory_version`이 로컬보다 낮으면 스냅샷을 통째로 버린다. reliable은 전달과 중복 제거만 보장하고 순서는 보장하지 않는다
- **`fire_sequence`는 버전 가드보다 먼저, 스냅샷을 버리는 경우에도 반영한다.** 인벤토리와 무관한 값이고 거부 응답에도 채워져 오며, 되돌아간 시퀀스를 복구하는 **유일한 경로**다. 반영은 `PacketHandler.RaiseFireSequenceTo()`로 `max()` 단조 상승만 — 대입 setter를 만들지 말 것
- **`DENY_VERSION_MISMATCH`는 실린 스냅샷이 곧 재동기화다.** `C2DRequestRecentInventoryInfo`를 따로 보내지 않고, 반영한 뒤 새 버전으로 **1회만** 재요청한다(`_reloadRetried`). 무한 재시도는 버전이 계속 바뀔 때 루프가 된다
- **`DENY_SLOT_EMPTY`는 재요청하지 않는다** — 맨손·이미 가득·탄종 없음이라 결과가 같다. 표시는 스냅샷 반영으로 이미 서버 값에 맞춰졌다

## 손에 든 무기

**'장착한 무기'와 '손에 든 무기'는 다른 개념이다.** 인벤토리의 무기 슬롯 2개는 장착이고, 그중 손에 든 것은 하나뿐이다(`IsPrimaryWeaponApplyed`). `C2DRequestWeaponFire.weapon_dbid`는 **손에 든** 쪽이어야 하며, 어긋나면 서버가 발사를 조용히 버린다. 추적 출처가 셋이므로 한 묶음으로 볼 것.

| 상황 | 반영 경로 |
|------|-----------|
| **내 초기 무기** | 통보 없음. `InitWeapon()`이 **주무기 우선, 없으면 보조무기**(양쪽 비면 맨손) — 서버와 같은 규칙이며 한쪽만 바꾸면 매치 시작부터 어긋난다 |
| 남의 초기 무기 | `D2CSpawnPlayerObject.weapon_id` |
| 남의 변경 | `D2CNotifyWeaponChanged` (장착·해제·전환 전부) |
| 내 전환 | `C2DRequestSwitchWeapon` → `D2CNotifyWeaponChanged`(성공·거부 모두 본인에게) |
| **내 장착·해제** | **통보 없음. 클라가 서버 규칙대로 직접 반영** (`SyncHeldWeapon`) |

### 내 장착·해제 규칙 (`SyncHeldWeapon`)

**들고 있던 슬롯이 비었을 때만 반대쪽으로 옮긴다**(양쪽 다 비면 맨손). 그 외에는 손에 든 슬롯을 유지하고, 그 슬롯의 무기가 바뀌었으면 새 무기로 갱신한다. 서버 규칙이며 임의로 바꾸면 `weapon_dbid`가 어긋나 사격이 통째로 무시된다.

### 무기 전환 (`RequestSwitchWeapon` / `HandleWeaponChanged`)

**로컬 예측을 쓰지 않는다.** 키 입력 시점에는 행동 잠금만 잡고, 손의 무기는 서버 통보 후에만 바꾼다.

- **즉시 전송이 아니라 `SWITCH_LOCAL_SEC`(0.5초) 유예 뒤에 전송한다.** 유예 구간이 모션 자리이자 취소 창이다 — 자세한 것은 위 '행동 잠금' 참조
- **전송 시점 값으로 다시 검증한다.** 유예 중 장착 조작으로 대상 슬롯이 비거나 이미 손에 들린 슬롯이 되었으면 보내지 않고 행동을 소멸시킨다. `my_inventory_version`도 전송 시점 값이어야 한다
- **확정(`Pending`) 중에는 재요청을 막는다.** in-flight 요청이 항상 1개가 되어 통보 순서 역전이 정상 경로에서 발생하지 않는다 — 이 잠금을 푸는 변경(예: 로컬 예측 도입)은 `rSeqNum` 기반 순서 방어를 함께 가져와야 한다. **유예 중 재타게팅은 이 잠금을 푸는 것이 아니다**(전송 전이라 in-flight가 늘지 않는다)
- **확정 전에는 발사도 막는다.** reliable(교체)과 unreliable(사격) 사이에 순서 보장이 없다. `PlayerController._fireBlocked`는 마우스 재클릭으로 풀리므로 재사용 금지
- 통보의 `slot`/`weapon_id`가 **성공·거부 구분 없이 항상 권위값**이다. 상태 반영은 한 경로(`ApplyServerWeaponState`)로 처리하고, 갈리는 것은 재동기화 여부뿐이다
- 판정은 `slot == 보낸 target_slot`. **`object_id`가 본인이라고 거부가 아니다**(구 스펙). 거부 중 **버전 불일치일 때만** 재동기화하고 **자동 재요청은 하지 않는다**
- 통보의 `inventory_version`으로 로컬 버전을 갱신하지 않는다. 버전만 맞추고 슬롯 내용이 낡으면 다음 요청이 '버전은 맞는데 내용은 틀린' 상태로 통과한다
- `weapon_id = 0`은 맨손이다. `EquipWeapon`에 그대로 넘길 것 — `weaponId != 0` 가드를 붙이면 맨손 전환이 반영되지 않는다
- 남의 통보에는 `inventory_version`이 `0xFFFFFFFF`로 온다. `0`은 실재하는 버전(세션 시작값)이라 미설정으로 읽으면 안 된다
- 남의 방어구·실드·HP는 어떤 패킷으로도 오지 않는다(`armor_id`는 스펙에서 삭제됨)

## 발사 브로드캐스트 (`HandleWeaponFireBroadcast`)

`D2CBroadcastWeaponFire`(발사자 objectId + 탄착 좌표)를 받아 **상대의 총알 궤적**을 그린다. 내 궤적은 `PlayerController.DrawTracer()`가 직접 그리며 이 경로를 타지 않는다.

- **미등록 발사자에서 반환하는 것이 이중 렌더를 막는 가드를 겸한다.** 룸 전체 브로드캐스트라 내 발사도 여기로 돌아오는데, 나는 `_oppoPlayers`에 없어서 걸러진다. **이 가드를 풀면 내 발사가 두 겹으로 그려진다**
- **모르는 발사자에게 `RequestSpawnIfUnknown()`을 부르지 않는다** — 발사는 빈도가 높아 reliable이 폭주하고, 근처 플레이어라면 상태 스트림이 곧 채운다. 오브젝트 킬 피드와 같은 판단이다
- **`hit_point`가 없으면(빗나감) 궤적을 그리지 않는다.** 좌표가 안 오면 방향 자체를 모른다. 내 궤적은 레이를 직접 갖고 있어 빗나가도 그리는데, **정보량 차이에서 오는 의도된 비대칭**이므로 맞추려 들지 말 것

## 비플레이어 오브젝트 관리

- `_sceneObjects`: objectId → `GameObjectController` 매핑
- **`SpawnObject(ObjectData)`가 유일한 스폰 경로다.** 정적·동적 초기 스폰, 지연 스폰 응답(`D2CResponseSpawnByObjectId`), 런타임 스폰 통보(`D2CNotifySpawnObject`)가 전부 여기로 모인다. 새 스폰 경로가 생겨도 `Managers.Resource.InstantiateFromObjectDataStruct()`를 직접 부르지 말 것 — 레지스트리 등록과 차단 검사를 건너뛰게 된다
- **`DespawnObject(objectId)`가 유일한 제거 경로다.** 차단 목록 등록 → 레지스트리 제거 → 파괴. 파괴된 컨테이너를 열어둔 상태였다면 `CloseContainerLocal()`로 UI만 닫고 **`C2DCloseContainer`는 보내지 않는다**(서버가 이미 없앤 오브젝트)
- **제거 통보 패킷이 둘로 갈린다** — 처치는 `D2CNotifyObjectKilled`(41), 그 외 사유는 `D2CNotifyDespawnObject`(39). 후자는 **현재 서버가 보내지 않는다**(컨테이너가 게임 종료까지 유지되어 처치 외 제거 사유가 없다). 사유가 생길 때를 위한 예약이므로 핸들러는 유지하고, 새 제거 경로가 생겨도 둘 다 `DespawnObject()`로 모을 것
- 컨테이너 닫기 가드는 위 이유로 지금은 도달하지 않지만 **제거하지 말 것** — 컨테이너가 파괴 대상이 되는 순간 되살아나는 방어 코드다
- `Define.ObjectPaths`에 매핑이 없는 `object_type`은 에러 로그로 드러낸다. `Undefined`는 키가 있어도 경로가 null이므로 함께 걸러낸다

### 디스폰과 유령 재스폰 차단 (`_despawnedObjectIds`)

디스폰된 objectId를 만료 없이 씬 수명 내내 보관하는 `HashSet<uint>`. **플레이어·비플레이어 공용**이며(objectId 공간이 공용) 새 스폰 경로를 추가할 때마다 여기에 가드를 얹어야 한다.

- **차단 지점은 "요청"과 "응답" 두 곳이다.** 요청 억제(`UpdatePlayerStates`)만으로는 부족하다 — 이미 보낸 `C2DRequestSpawnByObjectId`의 응답이 디스폰보다 늦게 도착하면 `SpawnPlayerObject()`가 그대로 되살린다. 스폰을 실제로 수행하는 함수에도 반드시 가드를 둘 것
- **전제: objectId는 한 게임 안에서 단조 증가하며 재사용되지 않고, 죽은 오브젝트가 살아나지도 않는다** (2026-08-20 서버 확인). proto에 문서화된 보장은 아니므로, 서버 계약이 바뀌면 설계를 다시 봐야 한다(증상은 "특정 오브젝트가 끝까지 안 보임")
- 만료 창을 쓰지 않는다. 창보다 늦게 오는 패킷이 뚫으며, 재전송 한도가 없어(`Network/CLAUDE.md`) reliable 패킷이 아주 늦게 도착할 수 있다
- 씬 인스턴스 필드라 매치가 바뀌면 자연히 비워진다 — 별도 초기화 불필요

## UI 열림 상태 관리 (`_uiOpenCount`)

레퍼런스 카운팅 방식. 새 UI 추가 시 반드시 따를 패턴:
- 열 때 `OnUIOpened()`, 닫을 때 `OnUIClosed()` 호출
- `IsAnyUIOpen`이 true이면 마우스룩·발사 차단, 커서 잠금 해제

**세는 대상은 컨테이너(`_isContainerOpen`) · 내 인벤토리(`_isInventoryOpen`) · 설정 창(`IngameSettingUI.IsOpen`) 셋이다.** 각자 자기 플래그를 갖고 열고 닫을 때 짝을 맞춘다 — **카운트가 0으로 안 떨어지면 그 판 내내 커서가 풀린 채 총이 안 나간다.**

- **컨테이너와 내 인벤토리는 같은 오브젝트(`IngameInventoryUI`)를 레이아웃만 바꿔 쓴다.** 그래서 `ShowOpenedContainer()`는 인벤토리가 열려 있었다면 **그 열림을 반납**해야 한다(안 하면 컨테이너를 닫아도 카운트가 1로 남는다). 반납을 `OnUIOpened()` **뒤**에 두는 것은 카운트가 0을 찍어 커서가 한 번 잠기는 것을 피하려는 것
- `BeginMatchExit()`이 셋을 모두 정리한다. 설정 창은 `CancelAndHide()`가 아니라 **`Hide()`** — 이미 반영된 값을 되돌릴 이유가 없다

### 인게임 키 라우팅

| 키 | 동작 |
|---|---|
| `Tab` / `I` | `ToggleMyInventory()` — **컨테이너 상호작용 중이면 그 상호작용 종료가 우선**(사용자 확정 규칙), 아니면 내 인벤토리 토글. 설정 창이 떠 있으면 무시 |
| `Esc` | `OnEscapeInput()` — **가장 위에 있는 것부터 닫는다**(설정 → 컨테이너 → 인벤토리). 닫을 것이 없을 때만 설정을 연다 |
| `E` | `TryInteract()` |
| `1` / `2` | 무기 전환 (0.5초 유예 뒤 전송) |
| `R` | `RequestReload()` — 2초 유예 뒤 전송. 달리는 중이면 시작하지 않는다 |

- **`Esc`는 `IsInputLocked`(매치 이탈 중)에도 막지 않는다** — 설정은 서버로 나가는 요청이 없어 잠글 이유가 없다. 반대로 `Tab`/`I`는 잠근다(인벤토리는 조작이 서버 요청으로 이어진다)
- **로비의 `Esc`와 같은 동작이 아니다.** `LobbyScene.OnEscapeInput()`은 상태별 뒤로가기·종료 확인이고 설정은 `UI_Header` 버튼으로 연다. 통일 대상으로 보지 말 것

## 상호작용 상태 관리

`IngameScene`이 `_canInteract`/`_interactTarget`을 중앙 보유. `PlayerController.CheckInteractable()`이 매 프레임 갱신.

- `TryInteract()`: 컨테이너 열려있으면 닫기, 아니면 대상의 `Interact()` 호출 (E키). **컨테이너 외의 UI가 떠 있으면 막는다** — 커서가 풀리면 시점이 멈춘 채 직전 조준 대상이 그대로 남아, 창을 띄운 상태로 컨테이너·귀환이 눌린다
- `_isContainerOpen`: objectId가 0일 수 있으므로 별도 bool 플래그 사용
- Deny 수신 시 `RequestRecentInventoryInfo()`로 재동기화

### 귀환(Recall) 상태 관리

`IngameScene`이 `_recallRequested`를 중앙 보유. **스팟별이 아닌 씬 단위 플래그**여야 다른 스팟으로 이동해 재요청하는 경로가 막힌다.

- 2단계 흐름: `RequestRecall()` → `HandleRecallResponse()`(승인/거부) → `HandleRecallResult()`(서버 5초 검사 후 최종 성공/취소)
- 승인 시점에는 잠금을 유지한다. 해제는 거부·취소·워치독 만료 시에만
- **TEMP 워치독**: 전송 시점부터 `RECALL_TIMEOUT`(10초) 타이머를 돌려 응답 유실 시 잠금 해제. 결과를 추측하지 않고 로컬 잠금만 풀므로 판정 권한은 서버에 유지. `HandleRecallResult` 실처리 완료 전까지는 제거 금지

### 드래그 + 서버 요청/응답

- 드래그: `BeginDrag`/`UpdateDragPosition`/`EndDrag` → 고스트 제어
- 아이템 조작: `RequestInteractContainerObject()` (get=0, swap=1, merge=2)
- 장비 장착: `RequestEquipItem()` (장착=0, 해제=1)
- Deny 수신 시 `RequestRecentInventoryInfo()`로 서버 상태 재동기화
