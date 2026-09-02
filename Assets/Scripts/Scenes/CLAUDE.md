# 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성 + 아래 `BindSceneComponent<T>` 제공
- **`LobbyScene`**: `LobbyState` enum 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching)
- **`LoadingScene`**: 비동기 로딩 → 90%에서 Blueprint 요청 → 응답 완료 시 전환
- **`IngameScene`**: 맵 씬들의 공통 베이스. 정적 오브젝트 스폰 + 씬 내장 UI 바인딩 + 스폰 요청. 실제 맵 씬이 이걸 상속한다
- **`GameResultScene`**: 매치 결과 표시 + 확인 버튼/엔터로 로비 복귀. 진입 경로는 `IngameScene.CompleteMatchExit()` 하나뿐
  - **Enter 잠금의 근거는 확인 버튼의 활성 상태 하나다**(`GameResultSceneUI.IsConfirmActive`). 씬에 별도 플래그를 두면 "버튼은 떴는데 Enter가 안 먹는" 상태가 생긴다. **UI 자체가 없을 때는 막지 않는다** — 막으면 로비로 나갈 수단이 사라진다
  - **버튼도 `MoveToLobby()`를 거치고, 그 안에 1회성 가드가 있다** — 클릭과 Enter가 겹치면 `LoadScene`이 두 번 예약된다
- 씬 전환은 반드시 `Managers.Scene`으로 한다
- **입력 리스너·업데이트 루프 안에서 씬을 직접 내리지 말고 `Managers.ExecuteAtMainThread`로 예약할 것** — `LoadScene()`의 `Managers.Clear()`가 순회 중인 구독 목록을 비운다

## 씬 배치 오브젝트 바인딩 (`BaseScene.BindSceneComponent<T>`)

씬에 미리 놓인 오브젝트를 이름으로 잡을 때 쓴다. **`GameObject.Find` + `GetComponent`를 직접 쓰지 말 것** — 이름은 맞는데 스크립트가 안 붙어 있으면 뒤이은 `Init()` 호출에서 NRE가 나고 **그 아래 초기화가 통째로 안 돈다**(크로스헤어 생성·키 리스너 등록 등).

- **UI 전용이 아니다.** 지금 소비자가 대부분 씬 내장 UI라 UI 함수로 보이기 쉽지만, 함수 안에 UI 개념이 없고 제약도 `where T : Component`다. 씬에 미리 놓인 것을 이름으로 잡는 자리면 무엇이든 여기를 쓸 것 — **"UI용이니까"라며 `Find`를 직접 쓰면 위 구멍이 그대로 되살아난다**
  - 다만 **레이캐스트로 찾는 것은 대상이 아니다**(`RecallSpotController`·컨테이너는 `GetComponentInParent`로 잡힌다). 이름 조회가 아닌 것을 여기로 끌어오지 말 것
- 오브젝트 없음·스크립트 없음 둘 다 `LogError`로 드러내고 `null`을 돌려준다. **호출부의 null 검사는 남길 것** — 원인은 알려주지만 `Init()`은 여전히 NRE가 난다
- **씬에는 활성 상태로 저장할 것** — `GameObject.Find`는 비활성 오브젝트를 못 찾는다. 숨겨야 하면 각 `Init()`이 바인딩 직후 스스로 끈다
- 프리팹 **내부** 경로 조회는 이쪽이 아니라 `Util.BindComponent(path, go)`다. 조회 축이 다르다(씬의 이름 ↔ GameObject 안의 경로)

## 씬 이름 규칙

**`SceneManagerEx.GetSceneName()`이 `Define.Scene` 항목 이름을 그대로 `LoadScene()`에 넘긴다.** enum 항목 이름 = `.unity` 파일 이름이어야 하고 파일 이름은 모두 `Scene`으로 끝낸다. 씬을 추가·리네임하면 enum도 고치고 Build Settings 등재를 확인할 것 — 어긋나면 **컴파일은 통과하고 전환 시점에 런타임으로 터진다.**

씬 컴포넌트 클래스와 이름이 겹치지만 enum 멤버는 항상 `Define.Scene.X`로 한정 접근되므로 충돌하지 않는다. 깨지는 경우는 `using static Define.Scene;`뿐이니 쓰지 말 것.

### 맵 씬 진입 (`Define.MapScenes`)

매칭 성공 응답의 `mapId`(→ `HTTPManager.MapId`)로 진입할 맵 씬을 고른다. 전환 지점은 `HTTPManager.TryConnectCall` 하나뿐이다.

- **새 맵을 추가하면 `Define.Scene`·Build Settings 등재와 함께 `Define.MapScenes`에도 채울 것** — 빠지면 폴백이 먹어 **엉뚱한 지형에 스폰되고 컴파일·통신은 정상이라** 원인을 짚기 어렵다
- **미등재 mapId에서 전환 자체를 막지 말 것** — UDP는 이미 붙은 뒤라 로비에 갇힌다. `LogError` + `TestIngameScene` 폴백이 그 이유다
- 표시명은 `Define.MapNames`이고 `UI_MapSelect`가 스프라이트(`map_sprite_{mapId}`)와 **같은 키로** 읽는다. 한쪽만 채우면 그림과 이름이 어긋난다
- 맵 씬 클래스는 `SceneType` 대입 한 줄만 다르고 나머지는 `IngameScene`이 전부 한다 — **맵별 분기를 씬 클래스에 넣기 전에 서버 주도인지부터 볼 것**(정적 오브젝트는 블루프린트로 내려온다)

## 씬 전환 페이로드 (`GameSceneContext`)

`SceneManagerEx`가 `NextSceneStaticContext`(정적)와 `SceneDynamicContext`(동적)를 보유한다. `PacketHandler`가 `AddObjectDatas()`로 누적, `IsComplete()`로 수신 완료를 판정하고 `IngameScene.Init()`이 소비한다.

### 매치 결과 (`GameResult` / `LastGameResult`)

`SceneManagerEx.cs` 파일 스코프의 구조체. `CompleteMatchExit()`이 `SetGameResult()`로 채우고 결과 씬이 소비한다.

- `LastGameResult`는 `GameResult?`이고 null이 '결과 없음'이다. **`ResetLoadSceneOp()`에서 지우지 말 것** — 결과 씬 진입 초기화에서도 불려 소비 전에 날아간다. 제거는 `ClearGameResult()` 또는 다음 매치 종료의 덮어쓰기로 한다
- 내용: 이탈 사유 + 인벤토리 25슬롯(배치 유지, 빈 슬롯 null) + 무기 2·방어구(**탄창 제외**) + 킬수 2종. 인벤토리는 클라 로컬 상태라 서버와 어긋날 수 있으나 표시용으로 감수한다
- 같은 아이템 목록이라도 **사유별 의미가 다르다** — Recalled=반출 확정, Dead·ConnectionLost=잃은 것

### 로비 복귀와 세션 유지 (`IsReturnFromGameResult`)

`GameResultScene.MoveToLobby()`가 세우고 `LobbyScene.Init()`이 소비한다. 결과 씬 경유(true)일 때만 `TryResumeSession()`으로 Login을 건너뛴다.

- **`LastGameResult`와 같은 이유로 `ResetLoadSceneOp()`에서 지우지 말 것.** 소비자인 `LobbyScene.Init()`이 읽은 뒤 직접 false로 되돌린다
- 세션이 살아남는 근거는 `Managers.Clear()`가 `Sound`/`Input`/`Scene`/`UI`만 건드려 **HTTPManager의 `SessionId`·`AuthState`가 씬 전환에서 초기화되지 않는다**는 것이다 — 여기에 매니저를 추가할 때 주의할 것
- 버전 체크(`GetVersionCall`)는 프로세스 재시작이 아니므로 생략한다
- 성공하면 `OnLoginComplete()`를 재사용한다 — 별도 진입 경로를 만들지 말 것
- 실패 처리·폴백은 `Network/CLAUDE.md`의 '세션 유지' 참조

### 서버 연결 실패 처리 (`LobbyScene.TryConnectToServer`)

버전 확인(`GetVersionCall`)의 실패 갈래가 셋(점검·버전 불일치·그 외)이고 문구만 다르다.

- **UI 복구(`OnConnectedFailed()`)는 갈래 분기보다 앞에서 한 번만 부른다** — 갈래마다 복제하면 새 사유가 추가될 때 하나가 빠지고, 그 순간 **스피너가 계속 돌며 시작 버튼이 영영 굳는다**(`_isRequestSending`을 푸는 곳이 `UI_TestStart.Reload()` 하나뿐이다)
- **자동 재시도를 붙이지 말 것** — 팝업을 닫으면 시작 화면에 남으므로 사용자가 다시 누르면 된다(세션 유지의 `Unreachable`과 같은 판단)

### 세션 만료 처리 (`LobbyScene.OnSessionExpired`)

`HTTPManager.OnSessionExpired` 구독. 로비의 인증 요청이 401을 받으면 로그인 정보를 비우고 `BeforeAuth`로 돌아간다(감지는 `Network/CLAUDE.md` 소관).

- **정리·전이를 먼저 하고 팝업은 통보만 시킨다.** `ActiveOnlyConfirm`은 다른 팝업이 떠 있으면 아무것도 하지 않고 `false`를 돌려주므로, 전이를 버튼 콜백에 걸면 팝업이 묻히는 순간 **전이까지 함께 사라져 죽은 세션인 채로 로비에 남는다**(`TryResumeSession`이 같은 순서를 쓰는 이유)
- **전이는 `OnLogoutComplete()`를 재사용한다** — 헤더 상태·UI 6종 비활성화·슬롯 배열 셋 정리가 거기 모여 있어, 따로 짜면 **이전 계정의 인벤토리가 보이는 채로 Auth 화면이 뜬다**
- 도착지가 `BeforeAuth`인 것은 **401을 받았다는 게 서버와 통신은 된다는 뜻**이라서다. `TryResumeSession`의 폴백이 `BeforeConnect`로 가는 것과 갈리는 지점이며, 그쪽은 서버에 닿지도 못한 `Unreachable`과 폴백을 공유한다
- 구독 해제는 `OnDestroy`의 `Managers.Instance != null` 블록 안이다(키 리스너와 같은 자리)

### 로비 패널 전환 (`UserState`)

`Main` / `Inventory` / `Shop` / `Character` 넷이고, **각 전이 함수가 켤 패널과 끌 패널을 개별로 나열한다.** 헤더 버튼 넷이 전이를 직접 부르므로(`UI_Header`) **모든 상태에서 모든 상태로 한 번의 클릭에 도달한다** — 뒤로가기를 경유한다고 전제하지 말 것.

- **패널을 추가하면 끄는 목록 다섯을 모두 고칠 것** — `ShowInventory`·`ShowShop`·`ShowCharacter`·`BackToLobbyMain`·`EnterMatchingState`(+ `OnLogoutComplete`). 한 곳이라도 빠지면 **그 전이에서만 이전 패널이 화면에 남고**, 뒤로가기를 거치면 정리되므로 재현 경로가 좁아 눈에 잘 띄지 않는다
- **`UI_Warehouse`는 `Inventory`와 `Shop`이 공유한다** — '한 상태에 한 패널'을 전제로 정리하지 말 것
- **`ShowLobby()`는 본문이 비어 있고 호출부가 없다** — `BackToLobbyMain()`이 Main 진입을 맡는다. 이름만 보고 부르면 아무 일도 일어나지 않는다

### 구매 대상 슬롯 (`FindEmptyPurchaseSlotIndex`)

- **구매가 들어갈 자리는 창고(0~79)뿐이다 — 인벤토리에 빈 칸이 있어도 후보가 아니다.** 서버가 그 밖의 인덱스를 `ERR_INVALID_SLOT`으로 거부하므로, 인벤토리 폴백을 되살리면 창고 만재 시 구매가 400으로 조용히 실패한다(스냅샷은 그대로 0~107 전체를 싣는다 — 제한은 대상 슬롯에만 걸린다)

### 매치 시작 선제 검사 (`TryMatchMake`)

로드아웃 모드별 검사를 **요청을 만들기 전에** 여기서 한다. FREE 진입점이 `UI_MapSelect`의 FREE 버튼과 게스트 즉시 시작 **둘**이라 UI 쪽으로 내리면 한쪽이 빠지고, 슬롯 배열 소유자도 이 씬이다.

- **CUSTOM은 무기 슬롯 둘이 모두 비면 막는다**(`ERR_NO_WEAPON_EQUIPPED`)
- **FREE는 인벤토리 25칸 + 로드아웃 3칸이 비어 있어야 한다**(`IsLoadoutAreaEmpty`). **창고를 검사에 넣지 말 것** — 서버 조건이 'slot 80 이상이 빔'이라 창고까지 보면 아이템을 가진 계정이 FREE로 아예 못 들어간다
- **스냅샷은 두 모드 모두 싣는다** — FREE만 `null`로 되돌리면 `inventory`가 필수라 요청이 400으로 떨어진다

## IngameScene 스폰 흐름

1. `RequestSpawnMe()` → `C2DRequestSpawnMe`
2. `D2CResponseSpawnMeSpawnSpot`(좌표) + `D2CResponseSpawnMeDynamicObjects`(동적 오브젝트)
3. 둘 다 오면 `SpawnMeAndRequestPlayerObjects()` → 플레이어 인스턴스화 + `TryInitWeapon()` + 동적 오브젝트 스폰
4. `D2CSpawnPlayerObjects` → 다른 플레이어 일괄 스폰
5. 완료 시 상태 전송 루프(0.1초) 활성화 + 로딩 완료 통보

**초기 무기 장착**: `_spawnCompleted`와 `_itemLoaded`가 모두 충족될 때 `TryInitWeapon()`을 1회 부른다. **도착 순서가 보장되지 않으므로 양쪽 시점에서 모두 호출한다.**

## 매치 마감 시각 (`SetMatchDeadline` / `RemainMatchTimeMs`)

`D2CResponseSpawnMeSpawnSpot.remaining_life_ms`를 받은 시점을 기점으로 `_matchDeadlineBaseMs`·`_matchDeadlineMs`를 잡는다. 마감이 지나면 **서버가 룸 안의 모든 플레이어를 사망 처리한다.**

- **비교가 뺄셈보다 앞이어야 한다** — `uint`라 `MATCH_DEADLINE_SAFETY_MS`를 먼저 빼면 언더플로로 약 49일 뒤가 되고, 증상이 "타이머가 줄지 않는다"라 원인을 짚기 어렵다
- **표시일 뿐 종료를 판정하지 않는다 — 0에서 멈추고 이탈은 서버 통보로만 시작한다.** 클라가 0에서 `BeginMatchExit()`을 부르면 서버 지터에서 화면과 실제가 갈린다(귀환 카운트다운과 같은 이유)
- **시각의 출처는 `NowMs()` 하나다** — 마감을 찍을 때와 남은 시간을 잴 때가 갈리면 표시가 조용히 어긋난다. **`Time.time`을 쓰지 말 것**(`timeScale`에 휘둘려 실제 시계와 갈린다)
- **기점은 '룸이 시작한 시각'이 아니라 '내가 스폰 응답을 받은 시각'이다.** 그래서 `MatchDeadlineSpanMs`는 룸의 총 수명이 아니라 내가 들어온 뒤 남아 있던 시간이며, 늦게 합류해도 진행바는 100%에서 시작한다
- **`MATCH_DEADLINE_SAFETY_MS`(10초)는 실제 마감보다 일찍 0을 보여주려는 클라 쪽 여유폭이다** — 서버 상수와 맞추는 값이 아니므로 서버에 물어볼 것이 아니고, 줄이는 만큼 "00:00인데 아직 안 죽는" 구간이 짧아진다
- `remaining_life_ms == 0`은 '마감을 넘김'과 '서버가 값을 안 실어줌'을 함께 덮어 **스폰 직후 00:00이 된다.** 로직은 정상 동작하므로 결함으로 보지 말 것
- **서버가 보내는 값에 대한 방비는 두지 않는다**(확정). 비정상적으로 큰 `remaining_life_ms`는 `SetMatchDeadline`의 덧셈을 `uint` 밖으로 넘겨 마감이 감기고 **스폰 직후 00:00으로 굳는다.** 실측으로 문제가 드러나면 그때 볼 것 — 포화 덧셈을 선제로 넣지 말 것

### 잔여 시간 표시 (`UpdateTimeoutDisplay` / `IngameTimeoutUI`)

- **초가 바뀔 때만 민다**(`_shownTimeoutSec`, 초기값 `-1` — `0`이 유효값이다). `SetCountdown`이 문자열을 만들므로 매 프레임 부르면 낭비다
- **마감이 잡히기 전에는 밀지 않는다.** 게이트는 `_matchDeadlineBaseMs == 0`이며, **`_matchDeadlineMs`로 잡지 말 것** — 여유값 이하로 남은 채 스폰하면 두 값이 같은 것이 정상이고 그때는 00:00이 맞다. 게이트가 없으면 스폰 응답 전까지 00:00이 떴다가 튄다
- **숫자가 한 칸 건너뛰어도 고치지 말 것**(확정) — 표시 정밀도를 요구하지 않기로 했다. 드리프트 보정을 붙이면 코드만 늘고 얻는 것이 없다
- **매치 이탈 유예 4초 동안 얼어붙는다** — `OnUpdate()`가 조기 반환하기 때문이며 다른 HUD와 같은 동작이다. 판을 떠나는 중이라 문제되지 않으니 **이탈 블록 바깥으로 빼지 말 것**

## 무기 프리팹 캐시 (`WeaponPrefabCache`)

`Resources.LoadAll<GameObject>("Prefabs/Weapons")`로 1회 로드해 `weaponId → prefab`으로 갖는다. 프리팹 이름은 `Weapon_{id}_{name}`.

## 인게임 인벤토리 (`IngameInventory`)

`IngameScene`이 보유하는 순수 C# 클래스. 서버 주도 `D2CFullInventorySync`로 동기화된다.

- 슬롯: `_inventorySlots[25]` + 무기 2 + 방어구 1 + 탄창 2 + 컨테이너 슬롯
- `_isPrimaryWeaponApplyed`: 주무기/보조무기 전환 상태
- `PLAYER_OBJECT_ID(0xFFFFFFFF)`: objectId로 플레이어/컨테이너 슬롯을 구분하는 규칙
- 외부 접근은 `ingameScene.Inventory.XXX`

## 체력 상태 관리

`_currentHealthPoint`/`_currentShieldPoint`로 서버 절대값을 보관한다. `D2CNotifyHealthChange`는 **피해 입은 본인에게만** 온다(남의 HP·실드는 어떤 패킷으로도 오지 않는다).

- **`0xFFFFFFFF`는 문맥에 따라 의미가 다르다.** 인벤토리 문맥의 `PLAYER_OBJECT_ID`와 전투 문맥의 `NO_ATTACKER_OBJECT_ID`는 값이 같아도 **별개 상수로 유지할 것**
- **`0`은 실재하는 objectId다.** proto3 기본값이라고 '미설정'으로 해석하면 오귀속이 된다
- 교전 상대 추적: `_lastAttackerObjectId`는 `ATTACKER_TRACK_DURATION` 안에서만 유효하며 `LastAttackerObjectId`/`HasRecentAttacker`로 조회한다

### 피격 방향 표시 (`ShowDamageIndicator`)

`HandleHealthChange`의 가해자 갱신 자리에서 부른다. 각도는 **플레이어 루트 forward 기준 수평 signed yaw**이고 루트가 요만 따라가므로 `transform.forward`가 곧 수평 시선이다.

- **가해자 위치를 못 찾는 경로가 정상이다** — 아직 스폰되지 않은 플레이어나 비플레이어 전투 오브젝트가 쏘면 `_oppoPlayers`에 없다. 방향을 모르므로 **조용히 표시하지 않는다**(발사 브로드캐스트의 `hit_point` 비대칭과 같은 성격)
- **`reason`을 보지 않는다** — 회복에는 가해자가 없어 `attacker_object_id != 0xFFFFFFFF` 가드가 이미 걸러낸다. 조건을 둘로 늘리면 서버가 사유를 추가할 때 한쪽이 빠진다
- **UI 회전은 반시계가 양수라 월드 signed yaw와 부호가 반대다** — `-Vector3.SignedAngle(...)`의 마이너스를 빼면 좌우가 뒤집힌다

### 실드 재생 예측

전용 통보 패킷이 없다. 서버는 매 틱 회복만 시키고 아무것도 보내지 않으므로 클라가 같은 공식으로 예측하고 피격 통보마다 서버 절대값으로 리셋한다.

- `UpdateShieldRegen()`이 `(재생량 × 경과ms)`를 누적해 `SHIELD_REGEN_ACCUM_UNIT`(1000)마다 1 회복한다. **실수 보간으로 바꾸지 말 것** — 서버가 정수 누적이라 값이 어긋난다
- 방어구 스펙 캐시는 `SyncHealthBarMax()`가 갱신하며 `SyncInventoryUI()`의 UI null 가드보다 **앞에서** 불린다. **전투 예측을 UI 오브젝트 존재에 묶지 말 것**
- `SyncHealthBarMax()`는 최대치를 넣은 뒤 현재값까지 민다. **순서를 뒤집으면 `SetArmor`가 최대 실드 0에 걸린다.** 없으면 첫 피격 전까지 프리팹에 저장된 `fillAmount`가 보인다
- `_currentHealthPoint`의 초기값은 `MAX_HEALTH_POINT`다. HP는 스폰 시 어떤 패킷으로도 오지 않으므로 0으로 두면 **사망으로 오판해 재생이 멈춘다**
- **`MAX_HEALTH_POINT`(20000 = 200.00 HP)는 서버 `PlayerObject::DEFAULT_MAX_HP`와 손으로 맞추는 값이다.** 최대치를 보내는 패킷이 없어 클라 쪽 유일한 출처이며, 어긋나도 컴파일·통신은 정상이고 **체력바 비율만 조용히 틀어진다.** 전투 수치는 전부 1/100 고정소수점이라 HP 로직에 숫자 리터럴을 쓰면 출처가 갈린다
- **스폰 시 실드는 최대치다(서버 규칙).** 최대치 출처가 방어구 스펙뿐이라 필드 초기값으로 둘 수 없어 `SyncHealthBarMax()` 직후 `TryInitShield()`가 일회성으로 적용한다. **`_itemLoaded` 가드를 빼지 말 것** — 인벤토리 도착 전에는 방어구가 null이라 최대치가 0이고, 그대로 플래그가 소진되면 **실드가 끝까지 0이 된다**
- 방어구는 착용·해제·교체 어느 경로든 실드가 0에서 다시 찬다(서버 규칙). `ApplyEquipItem`의 `equipmentSlotType == 2`에서 `ResetShieldPrediction()` — **최초 스폰과 갈리는 지점이고 `TryInitShield()`의 일회성 플래그가 둘을 가른다**

## 스태미나 (`UpdateStamina`)

달리기·점프의 공용 자원. **서버에 필드가 없는 완전한 클라 로컬 상태다** — 서버 절대값으로 리셋되는 경로가 아예 없어서 상수 여섯이 유일한 출처다(최대 60 / 달리기 진입 20 / 소모 10·s / 회복 10·s / 회복 지연 1초 / 점프 12). 서버가 아는 것은 `MovementState`의 RUN·WALK뿐이라 **고갈의 유일한 대외 효과는 RUN이 WALK로 바뀌어 나가는 것**이다.

- **값과 판정은 여기 있고 전이는 `PlayerController`가 한다.** 컨트롤러는 `CanStartRunning`(20 이상) / `HasStamina`(0 초과) / `CanJump`(12 이상)로 묻고 `ConsumeJumpStamina()`로 알리기만 한다 — **수치를 컨트롤러에 복사하지 말 것**
- **소비자가 둘인데 형태가 다르다** — 달리기는 매 프레임 지속 소모(`UpdateStamina`가 스스로 깎는다), 점프는 한 번에 깎는 소모(컨트롤러가 부른다). **점프를 `UpdateStamina` 안으로 옮기려 들지 말 것** — '이번 프레임에 뛰었는가' 플래그를 씬으로 넘겨야 해서 입력을 컨트롤러에 두는 구도가 깨진다
- **회복 잠금(1초)을 달리기 '중단 사유'마다 걸지 않는다.** 중단 경로가 넷이라(Shift 해제 · 이동 정지 · 스태미나 0 · 이탈로 입력 차단) 지점마다 흩으면 반드시 하나가 빠진다. `_wasRunning`의 **true→false 엣지 한 곳**에서만 세우므로 어떤 이유로 멈췄든 지나간다 — **사유별 분기를 추가하지 말 것**
  - **점프는 이 금지에 걸리지 않는다** — 달리기의 중단 사유가 아니라 별개 소비자다. 대신 잠금을 세우는 형태는 `BlockStaminaRegen()` 하나로 모은다
  - **두 호출부가 서로의 잠금을 줄이지 않는 것은 지연이 같은 상수이기 때문이다.** 점프에만 다른 지연을 주려면 `BlockStaminaRegen()`을 `Mathf.Max` 형태로 바꿔야 한다
- **소모와 회복이 같은 10/s인데 실제 회복이 더 느리다** — 1초 잠금 때문에 6초 달리면 회복에 7초가 든다
- `PlayerController.Update()`와 `OnUpdate()`의 실행 순서가 보장되지 않아 **소모가 최대 1프레임 늦을 수 있다**(60fps에서 0.17). 맞추려고 갱신을 한쪽으로 몰지 말 것 — 입력은 컨트롤러, 자원은 씬이 이 설계다
- **매치 이탈 중에는 `OnUpdate()`가 조기 반환해 소모·회복이 멈춘다.** 입력이 잠겨 달릴 수 없으므로 문제되지 않지만 게이지 표시도 함께 얼어붙는다(다른 HUD도 같다)
- **게이지는 `달리는 중 || 스태미나 ≤ STAMINA_SHOW_THRESHOLD`일 때만 보인다.** 판단이 씬에 있는 것은 문턱이 씬 상수라서다 — UI가 정하게 하면 상수가 두 벌이 된다
  - **`STAMINA_SHOW_THRESHOLD`는 `RUN_ENTRY_STAMINA`와 값이 같지만 상수를 따로 둔다.** 하나로 합치면 **진입 문턱을 조정하는 순간 표시 규칙이 조용히 따라 바뀐다**
  - 숨김은 `gameObject.SetActive()`다. **씬에는 활성 상태로 저장하고 `Init()`이 바인딩 직후 스스로 끈다** — `GameObject.Find`는 비활성을 못 찾는다
  - **점프 요구치(12)가 표시 문턱(20)보다 낮아 플레이어는 점프 소모를 눈으로 확인할 수 없다.** 피드백이 필요해지면 표시 규칙이 아니라 **점프 시점에 게이지를 잠깐 띄우는 쪽**으로 붙일 것

## 킬 피드 (`HandlePlayerKilled`)

`D2CNotifyPlayerKilled`는 **피해자를 포함한** 룸 전체에 온다. 남의 캐릭터 제거는 `D2CDespawnPlayerObject`가 맡고 이 패킷은 표기를 다룬다.

- **이 패킷이 피해자 본인의 '사망 확정' 신호를 겸한다.** `victim_object_id`가 내 objectId면 자기 사망이고 그때부터 5초 유예가 시작된다
- **사망 기점을 `D2CNotifyHealthChange`(HP 0)로 잡지 말 것** — 두 패킷이 모두 오므로 기점이 이중화되고 순서에 따라 흔들린다
- **자기 캐릭터의 디스폰 통보는 오지 않는다.** 유예 동안 화면에 남겨두고 스스로 치워야 한다

## 매치 이탈 (`BeginMatchExit` / `IsInputLocked`)

사망·귀환 성공·연결 끊김 셋이 **하나의 출구**를 공유한다. `BeginMatchExit(reason)` → `MATCH_EXIT_DELAY`(4초) → `CompleteMatchExit()`(UDP 종료). 새 이탈 사유는 여기에 붙인다.

- **4초는 서버 계약(5초)보다 짧게 잡은 값이다.** 서버가 세션을 정리하기 전에 클라가 먼저 정리하고 통보 ACK가 나갈 시간을 번다 — **5초로 늘리지 말 것**
- **이탈 시작 시 하트비트를 강제로 한 번 보낸다**(`SendHeartbeatNow`). ACK가 다음 송신에 piggyback되는데 유예 중 송신이 하트비트뿐이라 주기(3초)만큼 늦는다
- **입력 잠금은 `IsInputLocked` 하나로 본다** — 사격·시점·이동·조준 판정과 `RequestXXX` 전부. 서버가 하트비트를 뺀 모든 요청을 버리므로 클라가 막지 않으면 반응 없는 조작이 된다. 이동은 입력만 끊고 **중력은 유지**한다
- **수신은 막지 않는다** — 브로드캐스트가 계속 오는 것이 관전 유지의 근거다. `OnUpdate`는 상태 전송·상호작용 갱신만 건너뛴다
- **연결 끊김 통보는 `UDPManager`의 수신 워치독 지점에 둔다.** `Disconnect()` 안에 넣지 말 것 — 재연결 직전 정리에서도 불려 접속할 때마다 이탈 처리가 돈다
- 사망 사유일 때 `DeathCameraController.Play()`로 탑뷰 연출을 시작한다. 자기 캐릭터는 디스폰 통보가 없어 유예 동안 그 자리에 남으므로 그대로 연출 대상이 된다
- **귀환 성공에는 대응하는 연출을 두지 않는다**(확정) — 승인 카운트다운과 결과 씬이 필요한 정보를 전달하므로 사망 쪽과의 비대칭은 의도다. **빠뜨린 배선으로 보고 대칭을 맞추려 들지 말 것**
  - 남는 관찰: 유예 4초 동안 입력이 잠기고 HUD가 얼어붙어 **화면이 정지한 것처럼 보인다**(카운트다운도 `BeginMatchExit`에서 꺼진다). 거슬린다는 판단이 서면 카메라 연출이 아니라 페이드아웃 같은 최소 신호부터 검토할 것
- 씬 정리(커서·UDP)는 `IngameScene.Clear()` 오버라이드가 맡는다. `Managers.Clear()`가 씬 전환 때 부르므로 유예를 다 쓰지 않는 경로에서도 보장된다
- **`CompleteMatchExit()`이 `GameResult` 스냅샷을 뜬 뒤 연결을 끊는다.** `BeginMatchExit`으로 당기지 말 것 — 킬 통보가 사격보다 한 틱 뒤라 죽기 직전에 쏜 탄의 킬이 유예 중에 도착한다. 반대로 **결과 씬 전환은 반드시 `CompleteMatchExit()`을 거칠 것**(우회하는 강제 전환은 결과가 저장되지 않는다)
- `killer_object_id == 0xFFFFFFFF`는 가해자 없는 죽음이다(`NO_ATTACKER_OBJECT_ID`와 같은 의미). `0`은 실재 objectId
- **표시는 이름으로, 로직은 objectId로 가른다.** `victim_object_name`/`killer_object_name`(플레이어는 userId)은 표기 전용이고 스폰 요청·킬 기록·사망 판정은 전부 objectId로 한다. **`killer_object_name`이 비면 가해자를 표시하지 않는다** — 가해자 없음과 서버가 못 찾은 경우를 함께 덮는다
- **킬러 무기는 실려 오지 않는다** — 통보가 사격보다 한 틱 뒤라 그 사이 교체되면 틀린 값이 된다. `EquippedWeaponId`로 추적해둔 값을 쓴다
- 모르는 **킬러**는 `RequestSpawnIfUnknown()`으로 채우고 **피해자는 요청하지 않는다**(같은 타이밍에 디스폰이 온다)

## 킬 카운트 (`RecordPlayerKill` / `RecordObjectKill`)

내가 죽인 대상의 objectId를 담는 `HashSet<uint>` 2개. 킬수는 `Count`로 읽는다.

- **카운터가 아니라 Set인 이유** — 수신 reliable에 중복 제거가 없어 재전송된 킬 통보가 두 번 디스패치될 수 있다. objectId는 재사용되지 않으므로 Set이면 멱등이다
- 플레이어 킬 가드는 `_spawnCompleted` → `killer == _myObjectId` → `victim != _myObjectId` 순이다(`_myObjectId` 초기값 0은 실재 objectId)
- 오브젝트 킬(`D2CNotifyObjectKilled`, 41)은 피해자가 내가 될 수 없어 `victim` 가드가 없는 것 외에는 같다
- **`ObjectKillCount`는 `ICombatTarget` 보급에 종속된다** — 전투 오브젝트가 구현하지 않으면 `hit_object_id`에 `0xFFFFFFFF`가 실려 서버가 데미지를 넣지 않는다. 배선이 끝나도 그때까지는 항상 0이다
- **`HandleObjectKilled`는 제거까지 책임진다** — 처치에는 `D2CNotifyDespawnObject`가 오지 않으므로 여기서 `DespawnObject()`를 부른다. 빠뜨리면 파괴된 오브젝트가 맵에 영원히 남는다
- **오브젝트 킬은 킬 피드에 올리지 않는다**(확정). 그래서 `killer_object_name`은 **의도적으로 소비하지 않는다 — 빠뜨린 배선이 아니다.** 지금 받는 이름은 킬러 식별 확인용 `TEMP:`이며 로그에만 쓰이고, 짝이 되는 추출부가 `Handle_D2CNotifyObjectKilled`에 있다(**원복은 한 쌍**)
- 오브젝트 킬에서는 **미스폰 킬러를 요청하지 않는다** — 표시부가 없어 소비처가 없고 근처 플레이어는 상태 스트림이 채운다

## 다른 플레이어 관리

- `_oppoPlayers`: objectId → `OppoPlayerController`
- `UpdatePlayerStates()`: 미등록 objectId 수신 시 `RequestSpawnIfUnknown()`
- `PlayerSpawnData`/`PlayerStateData`(`SceneManagerEx.cs`): Protobuf 타입 격리용 중간 타입

### 지연 스폰 요청 (`RequestSpawnIfUnknown` / `_pendingSpawnRequests`)

`C2DRequestSpawnByObjectId`를 보내는 유일한 경로. **새 통보 패킷을 붙일 때도 직접 전송하지 말고 이 함수를 쓸 것.**

- **같은 objectId로 두 번 보내지 않는다.** reliable이라 ACK될 때까지 알아서 재전송된다. 매 틱 다시 만들면 같은 내용이 서로 다른 시퀀스로 쌓여 **in-flight 32슬롯을 채우고, 넘치는 순간 아직 ACK되지 않은 다른 패킷이 덮어써진다.** 상태 스트림이 10Hz라 이 경로는 특히 위험하다
- `_pendingSpawnRequests`에 만료를 두지 않는다 — 응답도 디스폰도 오지 않는 objectId는 서버가 모르는 것이라 재요청해도 결과가 같다
- 해제는 4곳(`SpawnPlayerObject`/`SpawnObject`/`DespawnPlayerObject`/`DespawnObject`). **앞의 둘은 중복·차단 가드보다 앞에서 제거한다** — 응답이 왔으면 스폰 여부와 무관하게 요청은 끝난 것이다
- **내 objectId를 걸러낸다** — 나는 `_oppoPlayers`에 없으므로 가드가 없으면 나 자신의 스폰을 요청한다
- 디스폰 목록·`_oppoPlayers`·`_sceneObjects`를 모두 확인한다. objectId 공간과 이 요청이 플레이어·비플레이어 공용이라 한쪽만 보면 **이미 아는 오브젝트를 다시 요청한다**

## 행동 잠금 (`TryBeginAction` / `UpdateAction` / `ClearAction`)

재장전·무기 교체처럼 **유예를 거쳐 서버로 나가는 행동**을 잠금 하나로 묶는다. 잠금은 **사유**(`PlayerActionKind`)와 **단계**(`PlayerActionPhase`)를 함께 든다 — 사유만으로는 "R 중 R은 무시, R 중 교체는 우선"이 갈리지 않고, 단계가 없으면 **이미 나간 요청을 취소할 수 있다고 착각**하게 된다.

| 단계 | 뜻 | 취소 |
|---|---|---|
| `Local` | 유예 중(모션 자리). 아직 **전송 전** | 가능 |
| `Pending` | 전송 후 응답 대기 | **불가** — 서버가 이미 처리했다 |

진입 판정은 `TryBeginAction()` 한 곳뿐이다.

| 현재 | 새 요청 | 결과 |
|---|---|---|
| 없음 | 무엇이든 | 진입 |
| 같은 사유 + 같은 대상 | R 중 R, 1 중 1 | **무시** — 재요청이 자기 행동을 무효화하지 않게 한다 |
| 같은 사유 + 다른 대상 | | **재타게팅**(호출부가 `ClearAction()` 후 재진입) |
| 다른 사유 + `Local` | 재장전 중 교체 | **취소 후 진입** |
| 다른 사유 + `Pending` | | **무시** — 확정 전에는 새 행동의 전제가 미정이다 |

- **`IsActionBusy`가 발사 차단의 단일 근거다**(`PlayerController.IsFireInput`)
- **in-flight 요청은 항상 1개다.** 재타게팅이 `Local`에서만 일어나기 때문이며, 이것이 `rSeqNum` 순서 방어를 끌어오지 않아도 되는 근거다 — **`Pending` 중 재전송을 허용하는 변경은 그 방어를 함께 가져와야 한다**
- **요청에 실을 값은 전송 시점에 읽는다.** 유예 중 인벤토리를 조작하면 버전이 오르는데, 시작 시점 값을 캐시하면 자기 조작 때문에 `DENY_VERSION_MISMATCH`로 거부된다
- **유예 시간은 클라가 주도하는 확정값이다**(`RELOAD_LOCAL_SEC` 2초 / `SWITCH_LOCAL_SEC` 0.5초). 무기별 차등 계획이 없어(2026-08-27 서버 확인) DB가 아니라 이 두 상수가 유일한 출처다. **애니메이션이 붙으면 클립을 이 길이에 맞출 것 — 반대로 하지 말 것**(클립에 상수를 맞추면 확정된 조작 감각이 조용히 바뀐다)
- **달리기는 재장전의 진입 조건이자 유지 조건이다.** `RequestReload()`가 `IsRunning`이면 시작하지 않고 `UpdateAction()`이 `Local` 중 `IsRunning`이 되면 취소한다. **재장전이 달리기를 해제하는 방향은 쓰지 않는다** — R을 눌렀다고 `_shift`를 뒤집으면 Shift를 누른 채인데 안 달리는 상태가 남는다
  - **반대로 스태미나 고갈로 달리기가 풀리는 것은 정상이다.** 입력이 아니라 자원이 원인이라 위 금지에 걸리지 않는다. 부수 효과로 **고갈로 달리기가 끊긴 순간부터 재장전이 가능해지는데** 그대로 둔다
- **`Pending` 워치독은 `ACTION_PENDING_TIMEOUT`(3초) 하나로 통합돼 있다** — 행동을 추가할 때 워치독을 따로 만들지 말 것
  - **제거하지 말 것.** 결과를 추측하지 않고 로컬 잠금만 풀어 늦게 온 응답도 정상 처리되므로 **헛발동 비용이 없고**, 없으면 응답 유실 시 **그 판 내내 발사가 막힌다.** `[Action] … 응답 미수신 (3초)`가 반복되면 워치독이 아니라 **서버가 43번을 안 보내는 것**이다
- 매치 이탈은 잠금을 지우고, 이탈 중에는 `IsInputLocked`가 진입을 막는다
- **`TODO:` 귀환·상호작용의 편입 범위는 미결이다.** 귀환(`_recallRequested`)은 구조가 같아 흡수 가능하지만, **상호작용은 컨테이너가 열려 있는 동안 지속되는 상태라 `Pending`으로 잡으면 여는 내내 모든 행동이 잠긴다**

## 재장전 (`RequestReload` / `HandleReloadResponse`)

`R` → 진입 문턱(`CanReload`) → 2초 유예 → `C2DRequestReload`(42, reliable) → `D2CResponseReload`(43). 대상은 '손에 든 무기'이며 서버가 정하므로 슬롯을 싣지 않는다.

**진입 문턱** — ① 탄창이 이미 `WeaponSpec.MaxAmmo`만큼 차 있거나 ② 예비탄이 0이면 시작하지 않는다. 헛도는 2초 유예와 불필요한 reliable을 줄이는 장치이고 **판정 권한은 여전히 서버에 있다**(발사 차단과 같은 성격).

- **알림음·경고는 두지 않는다**(확정). 잔탄 표기의 `0`이 그 역할을 한다
- **값의 출처를 `SyncWeaponUI()`와 같은 것으로 맞춘다**(`CurrentWeapon` → `WeaponSpec` → `CurrentMagazine` / `CountAmmo(spec.AmmoType)`). 갈리면 **"화면엔 예비탄이 있는데 R이 안 먹는다"** 가 된다
- **'가득'이라는 판단은 이 방향으로 오판이 없다** — 로컬 탄창 수치는 발사마다 차감되는 예측값이라 서버 값보다 크지 않으므로 로컬이 가득이면 서버도 가득이다. 반대 경우는 요청이 나가고 `DENY_SLOT_EMPTY`로 돌아오는 기존 경로 그대로다
- **스펙을 못 찾으면 막지 않는다**(맨손 포함) — 판단 근거가 없을 때는 보내고 서버에 맡긴다. **문턱이 '확실할 때만 막는' 형태를 유지할 것**

**응답 처리**

- **성공·거부 모두 인벤토리 전체 스냅샷이다**(델타가 아니다). 서버가 여러 칸에서 탄약을 나눠 빼는 스캔 순서를 클라가 재현할 수 없어서이며, proto가 명시한 의도다 — `D2CResponseEquipItem`(단일 스왑)과 **형태를 통일하려 들지 말 것**
- **`D2CFullInventorySync`의 수신 경로를 재사용하지 않는다** — ① 전체 동기화에 딸린 최초 1회용 초기화가 재장전마다 돌고 ② 그쪽에는 낡은 스냅샷을 버릴 버전 비교가 없다
- **버전 역전 방어**: `inventory_version`이 로컬보다 낮으면 스냅샷을 통째로 버린다. reliable은 전달과 중복 제거만 보장하고 순서는 보장하지 않는다
- **`fire_sequence`는 버전 가드보다 먼저, 스냅샷을 버리는 경우에도 반영한다.** 거부 응답에도 채워져 오며 **되돌아간 시퀀스를 복구하는 유일한 경로**다. 반영은 `RaiseFireSequenceTo()`의 `max()` 단조 상승만 — 대입 setter를 만들지 말 것
- **`DENY_VERSION_MISMATCH`는 실린 스냅샷이 곧 재동기화다.** 반영한 뒤 새 버전으로 **1회만** 재요청한다(`_reloadRetried`) — 무한 재시도는 버전이 계속 바뀌면 루프가 된다
- **`DENY_SLOT_EMPTY`는 재요청하지 않는다** — 결과가 같다

### 재장전 연출 단계 (`AdvanceReloadSequence` / `HandleReloadSequence`)

`C2DNotifyReloadSequence`(44) / `D2CNotifyReloadSequence`(45). **재장전 요청과 완전히 별개 흐름이다** — 서버는 값을 해석하지도 보관하지도 않고 `object_id`만 채워 중계하며, 보내지 않아도 재장전은 되고 보낸 뒤 취소해도 정리할 상태가 없다.

| 단계 | 시점 | 발행 | 클립 |
|---|---|---|---|
| 0 | 재장전 시작 | 클라 | `m4_reload_start` |
| 1 | 시작 +1초 | 클라 | `m4_reload_sequence1` |
| **15 = 완료** | `C2DRequestReload` 성공 | **서버** | `m4_reload_complete` |

- **15는 서버 전용이라 클라가 실어 보내면 통보 전체가 버려진다.** `RELOAD_SEQUENCE_TIMES`에 없어 구조적으로 막혀 있고 `UDPManager`에 가드가 한 겹 더 있다 — 조용히 버려지면 증상이 "그 단계만 남에게 안 들림"이라 원인을 짚기 어렵다
- **내 완료음은 45번으로 돌아오지 않는다**(통보가 당사자를 제외하고 나간다). 그래서 `HandleReloadResponse`의 `result == true`에서 직접 낸다. **2초(전송 시점)로 당기지 말 것** — 서버가 거부해도 울린다. 지금은 내 귀와 남의 귀가 같은 근거(요청 성공)를 쓴다
- **단계 진행은 `_actionTimer`에 얹는다.** 별도 타이머를 두면 "소리는 났는데 전송은 아직"이 생긴다. **한 프레임에 한 단계씩만** 올린다 — `while`로 몰아 올리면 같은 프레임에 소리 둘이 겹친다
- **취소는 `ClearAction()`이 `_reloadSequence`를 되돌리는 것으로 끝난다.** 달리기 시작·무기 교체·매치 이탈·워치독 만료가 전부 그 한 곳을 지나가므로 **사유별 분기를 만들지 말 것.** 서버에 알릴 것도 없다
- **수신 측은 상태를 두지 않는다 — 계약 요구다.** unreliable이라 단계가 통째로 빠질 수 있어 **'직전 단계가 도착했는가'를 전제하면 안 되고** 중복 제거·순서 검사도 넣지 않는다
- **취소 통보가 없어 수신 측 연출은 스스로 끝나야 한다.** 지금은 원샷 소리뿐이라 자동으로 충족되지만, **오포 재장전 애니메이션을 붙이면 "다음 단계가 오지 않으면 종료" 조건이 필요해진다**(빠뜨리면 취소된 모션이 영원히 돈다)
- 모르는 `object_id`에는 `RequestSpawnIfUnknown()`을 부르지 않는다 — 발사 브로드캐스트와 같은 판단이다

## 손에 든 무기

**'장착한 무기'와 '손에 든 무기'는 다른 개념이다.** 무기 슬롯 2개는 장착이고 손에 든 것은 하나뿐이다(`IsPrimaryWeaponApplyed`). `C2DRequestWeaponFire.weapon_dbid`는 **손에 든** 쪽이어야 하며 어긋나면 **서버가 발사를 조용히 버린다.** 추적 출처가 셋이므로 한 묶음으로 볼 것.

| 상황 | 반영 경로 |
|------|-----------|
| **내 초기 무기** | 통보 없음. `InitWeapon()`이 **주무기 우선, 없으면 보조무기**(양쪽 비면 맨손) — 서버와 같은 규칙이라 한쪽만 바꾸면 매치 시작부터 어긋난다 |
| 남의 초기 무기 | `D2CSpawnPlayerObject.weapon_id` |
| 남의 변경 | `D2CNotifyWeaponChanged`(장착·해제·전환 전부) |
| 내 전환 | `C2DRequestSwitchWeapon` → `D2CNotifyWeaponChanged`(성공·거부 모두 본인에게) |
| **내 장착·해제** | **통보 없음. 클라가 서버 규칙대로 직접 반영**(`SyncHeldWeapon`) |

### 내 장착·해제 규칙 (`SyncHeldWeapon`)

**들고 있던 슬롯이 비었을 때만 반대쪽으로 옮긴다**(양쪽 다 비면 맨손). 그 외에는 손에 든 슬롯을 유지하고 그 슬롯의 무기가 바뀌었으면 갱신한다. 서버 규칙이며 임의로 바꾸면 `weapon_dbid`가 어긋나 **사격이 통째로 무시된다.**

### 탄창 언로드 (`IngameInventory.UnloadMagazineToInventory`)

무기 슬롯 조작(`equipmentSlotType <= 1`)은 **장착·해제 양쪽 모두** 그 슬롯의 탄창을 인벤토리로 쏟는다. 배치는 ① 같은 `item_id` 스택 중 최소 인덱스에 합산(수량 상한 없음) ② 없으면 최소 인덱스 빈 칸 ③ 그것도 없으면 폐기이며 **조작 자체는 성공한다.** 서버 규칙을 클라가 재현하는 구조라 한 줄만 달라도 인벤토리 구성이 조용히 갈린다.

- **호출은 스왑을 마친 뒤다.** 앞으로 옮기면 목적지가 최소 인덱스 빈 칸일 때 규칙 ②가 그 칸을 골라 **무기와 탄약이 자리를 다툰다** — 스왑 뒤에는 무기가 그 칸을 차지해 후보에서 빠진다
- **목적지는 언제나 플레이어 인벤토리다.** 컨테이너 대상 조작이어도 탄약은 컨테이너로 가지 않으므로 `SetSlotByObjectId`가 아니라 `_inventorySlots`를 직접 쓴다
- **탄창 슬롯은 네 갈래(빈 탄창·합산·이동·폐기) 어디로 가든 비운다.** 배치 성공에 묶으면 폐기된 경우 **무기 없는 탄창**이 남는다 — 이 상태가 없다는 것이 순서를 논하지 않아도 되는 근거이기도 하다
- **잔탄 0은 '옮길 것 없음'이다.** 서버는 0을 빈 슬롯으로 만들지만 클라는 발사마다 로컬 차감해 `quantity == 0`인 탄창이 실재한다(느슨한 동기화). 안 막으면 0발을 옮기려 들어 아래 검산이 헛돈다
- **스왑으로 들어온 무기의 탄창은 비어 있는 것이 정상이다** — 인벤토리에 놓인 무기는 탄창 상태를 들고 다니지 않고 자동 장전도 없다. 재장전 전까지 못 쏘는 것이 서버와 같은 동작이다

**검산 (`VerifyUnloadedAmmo`)** — 계산 결과를 `D2CResponseEquipItem.unloaded_ammo_slot`과 대조하고 어긋나면 `RequestRecentInventoryInfo()`로 스냅샷을 다시 받는다.

- **서버값을 그대로 대입하지 말 것**(계약) — 대입하면 재현 규칙이 갈린 것을 영영 모른다. 이 필드는 배치 하나만 담고 스왑 결과는 검증하지 않는다
- **`quantity`는 비교 대상이 아니다.** 발사 탄약이 느슨한 동기화라 로컬 수치가 서버보다 작은 것이 정상이고, 넣으면 **쏘고 나서 무기를 바꿀 때마다 헛 재동기화**가 나간다. 배치 규칙이 수량에 의존하지 않으므로 `slot_index`와 `item_id`면 충분하다

### 무기 전환 (`RequestSwitchWeapon` / `HandleWeaponChanged`)

**로컬 예측을 쓰지 않는다.** 키 입력 시점에는 행동 잠금만 잡고 손의 무기는 서버 통보 후에만 바꾼다.

- **즉시 전송이 아니라 `SWITCH_LOCAL_SEC`(0.5초) 유예 뒤에 보낸다** — 유예가 모션 자리이자 취소 창이다
- **전송 시점 값으로 다시 검증한다.** 유예 중 대상 슬롯이 비거나 이미 손에 들린 슬롯이 되었으면 보내지 않고 행동을 소멸시킨다. `my_inventory_version`도 전송 시점 값이어야 한다
- **확정(`Pending`) 중에는 재요청을 막는다** — in-flight가 1개로 유지되어 통보 순서 역전이 정상 경로에서 발생하지 않는다. 이 잠금을 푸는 변경(로컬 예측 등)은 `rSeqNum` 순서 방어를 함께 가져와야 한다. **유예 중 재타게팅은 이 잠금을 푸는 것이 아니다**(전송 전이라 in-flight가 늘지 않는다)
- **확정 전에는 발사도 막는다** — reliable(교체)과 unreliable(사격) 사이에 순서 보장이 없다. **`_fireBlocked`는 마우스 재클릭으로 풀리므로 재사용 금지**
- 통보의 `slot`/`weapon_id`가 **성공·거부 구분 없이 항상 권위값**이다. 반영은 한 경로(`ApplyServerWeaponState`)로 하고 갈리는 것은 재동기화 여부뿐이다
- 판정은 `slot == 보낸 target_slot`이다. **`object_id`가 본인이라고 거부가 아니다**(구 스펙). 거부 중 **버전 불일치일 때만** 재동기화하고 **자동 재요청은 하지 않는다**
- **통보의 `inventory_version`으로 로컬 버전을 갱신하지 않는다** — 버전만 맞추고 슬롯 내용이 낡으면 다음 요청이 '버전은 맞는데 내용은 틀린' 상태로 통과한다
- `weapon_id = 0`은 맨손이므로 `EquipWeapon`에 그대로 넘긴다 — **`weaponId != 0` 가드를 붙이면 맨손 전환이 반영되지 않는다**
- 남의 통보에는 `inventory_version`이 `0xFFFFFFFF`로 온다. **`0`은 실재하는 버전**(세션 시작값)이라 미설정으로 읽으면 안 된다
- 남의 방어구·실드·HP는 어떤 패킷으로도 오지 않는다

## 발사 브로드캐스트 (`HandleWeaponFireBroadcast`)

`D2CBroadcastWeaponFire`(발사자 objectId + 탄착 좌표)로 **상대의 총알 궤적**을 그리고 **발사음**을 낸다. 내 궤적·총성은 `PlayerController.Fire()`가 직접 처리하며 이 경로를 타지 않는다.

- **미등록 발사자에서 반환하는 것이 이중 재생을 막는 가드를 겸한다.** 룸 전체 브로드캐스트라 내 발사도 돌아오는데 나는 `_oppoPlayers`에 없어 걸러진다 — **이 가드를 풀면 내 궤적이 두 겹으로 그려지고 내 총성이 두 번 울린다**
- **모르는 발사자에게 `RequestSpawnIfUnknown()`을 부르지 않는다** — 발사는 빈도가 높아 reliable이 폭주하고, 근처 플레이어라면 상태 스트림이 곧 채운다
- **`hit_point`가 없으면(빗나감) 궤적을 그리지 않는다.** 좌표가 안 오면 방향 자체를 모른다. 내 궤적은 레이를 직접 갖고 있어 빗나가도 그리는데 **정보량 차이에서 오는 의도된 비대칭**이므로 맞추려 들지 말 것
- **발사음은 그 가드 밖에 있다 — 빗나간 총도 소리는 난다.** 좌표가 없어 못 하는 것은 그리기뿐이므로 **소리와 궤적을 같은 `if`에 묶지 말 것.** 재생은 `shooter.PlayFireSound()`가 하고 무기별 분기는 `GameObjectController.GetGunShotSound()`에 있다
- **탄착 이펙트는 넣지 않기로 확정됐다**(로컬·수신 양쪽) — `hasHitPoint` 블록에 궤적만 있는 것은 빠뜨린 배선이 아니다. 총구 화염과 같은 판단이며 근거는 `Controller/CLAUDE.md`의 '미구현이 남은 메서드'에 있다

## 비플레이어 오브젝트 관리

- `_sceneObjects`: objectId → `GameObjectController`
- **`SpawnObject(ObjectData)`가 유일한 스폰 경로다.** 정적·동적 초기 스폰, 지연 스폰 응답, 런타임 스폰 통보가 전부 여기로 모인다. **`Managers.Resource.InstantiateFromObjectDataStruct()`를 직접 부르지 말 것** — 레지스트리 등록과 차단 검사를 건너뛴다
- **`DespawnObject(objectId)`가 유일한 제거 경로다.** 차단 목록 등록 → 레지스트리 제거 → 파괴. 파괴된 컨테이너를 열어둔 상태였다면 `CloseContainerLocal()`로 UI만 닫고 **`C2DCloseContainer`는 보내지 않는다**(서버가 이미 없앤 오브젝트)
- **제거 통보가 둘로 갈린다** — 처치는 `D2CNotifyObjectKilled`(41), 그 외는 `D2CNotifyDespawnObject`(39). **후자는 현재 서버가 보내지 않지만**(컨테이너가 게임 종료까지 유지된다) 사유가 생길 때를 위한 예약이므로 핸들러를 유지하고, 새 제거 경로도 둘 다 `DespawnObject()`로 모을 것
- 컨테이너 닫기 가드는 위 이유로 지금 도달하지 않지만 **제거하지 말 것** — 컨테이너가 파괴 대상이 되는 순간 되살아나는 방어 코드다
- `Define.ObjectPaths`에 매핑이 없는 `object_type`은 에러 로그로 드러낸다. `Undefined`는 키가 있어도 경로가 null이라 함께 걸러낸다

### 디스폰과 유령 재스폰 차단 (`_despawnedObjectIds`)

디스폰된 objectId를 만료 없이 씬 수명 내내 보관한다. **플레이어·비플레이어 공용**이며 **새 스폰 경로를 추가할 때마다 가드를 얹어야 한다.**

- **차단 지점은 "요청"과 "응답" 두 곳이다.** 요청 억제만으로는 부족하다 — 이미 보낸 요청의 응답이 디스폰보다 늦게 오면 `SpawnPlayerObject()`가 그대로 되살린다. **스폰을 실제로 수행하는 함수에도 반드시 가드를 둘 것**
- **전제: objectId는 한 게임 안에서 단조 증가하며 재사용되지 않고, 죽은 오브젝트가 살아나지 않는다**(2026-08-20 서버 확인). proto에 문서화된 보장이 아니므로 계약이 바뀌면 설계를 다시 봐야 한다(증상은 "특정 오브젝트가 끝까지 안 보임")
- **만료 창을 쓰지 않는다** — 창보다 늦게 오는 패킷이 뚫으며, 재전송 한도가 없어 reliable이 아주 늦게 도착할 수 있다
- 씬 인스턴스 필드라 매치가 바뀌면 자연히 비워진다

## UI 열림 상태 관리 (`_uiOpenCount`)

레퍼런스 카운팅이다. 열 때 `OnUIOpened()`, 닫을 때 `OnUIClosed()`를 부르고, `IsAnyUIOpen`이면 마우스룩·발사가 막히고 커서 잠금이 풀린다.

**세는 대상은 컨테이너·내 인벤토리·설정 창·ESC 창 넷이고 각자 자기 플래그로 짝을 맞춘다** — **카운트가 0으로 안 떨어지면 그 판 내내 커서가 풀린 채 총이 안 나간다.** 반대로 새 UI를 세면서 빠뜨리면 **커서가 잠긴 채라 그 UI의 버튼을 클릭할 수 없다.**

- **`OnUIOpened()`/`OnUIClosed()`를 부르는 자리는 전부 멱등이어야 한다 — 상태 플래그의 '전이'에서만 부를 것.** 특히 **수신 핸들러가 직접 부르는 자리**가 위험하다: 수신 reliable에 중복 제거가 없어 재전송된 응답이 두 번 디스패치되고, 요청 쪽 in-flight 가드가 없으면 응답 전 재입력으로 요청 자체가 두 번 나간다. **`+1`이 두 번 되면 닫기는 플래그 가드에 걸려 한 번뿐이라 카운트가 1로 굳는다** — 증상이 "커서가 보이는데 마우스가 안 먹고, 인벤토리는 열리는데 닫아도 그대로"라 UI 버그로 보이지 실제로는 수신 중복이다
- **컨테이너와 내 인벤토리는 같은 오브젝트(`IngameInventoryUI`)를 레이아웃만 바꿔 쓴다.** 그래서 `ShowOpenedContainer()`는 인벤토리가 열려 있었다면 **그 열림을 반납**해야 한다(안 하면 컨테이너를 닫아도 카운트가 1로 남는다). 반납을 `OnUIOpened()` **뒤**에 두는 것은 카운트가 0을 찍어 커서가 한 번 잠기는 것을 피하려는 것 — **위 멱등 가드보다는 뒤여야 한다**(중복 응답에서 반납이 다시 돌면 이번엔 카운트가 과소 계상된다)
- `BeginMatchExit()`이 넷을 모두 정리한다. 설정 창은 `CancelAndHide()`가 아니라 **`Hide()`** — 이미 반영된 값을 되돌릴 이유가 없다. **새 UI를 여기 빠뜨리면 사망 연출 위에 남고 카운트도 0으로 안 떨어진다**

### 인게임 키 라우팅

| 키 | 동작 |
|---|---|
| `Tab` / `I` | `ToggleMyInventory()` — **컨테이너 상호작용 중이면 그 종료가 우선**(확정), 아니면 인벤토리 토글. 설정 창이 떠 있으면 무시 |
| `Esc` | `OnEscapeInput()` — **가장 위에 있는 것부터 닫는다**(설정 → ESC 창 → 컨테이너 → 인벤토리). 닫을 것이 없을 때만 **ESC 창**을 연다 |
| `E` | `TryInteract()` |
| `1` / `2` | 무기 전환 (0.5초 유예 뒤 전송) |
| `R` | `RequestReload()` — 진입 문턱 통과 시 2초 유예 뒤 전송 |
| `Q` | `PlayerController.ChangeCamAngle()` — 좌각/우각 토글. 등록·해제가 `IngameScene`이 아니라 **`PlayerController`**에 있다(`E`와 같은 자리) |

- **`Esc`는 `IsInputLocked`에도 막지 않는다** — 설정은 서버로 나가는 요청이 없고 **게임 종료는 이탈 중에도 허용해야 하는 조작**이다. 반대로 `Tab`/`I`는 잠근다(인벤토리 조작이 서버 요청으로 이어진다)

### ESC 창과 게임 종료 (`IngameEscUI` / `QuitFromMatch`)

옵션 / 게임 종료 두 갈래를 제시하는 창. **인게임에서 판을 뜨는 유일한 자발적 경로**다(그 외 이탈은 사망·귀환·연결 끊김).

- **옵션을 누르면 설정을 연 뒤 ESC 창을 닫는다** — 둘은 동시에 열리지 않는다(확정). 순서를 뒤집으면 `_uiOpenCount`가 0을 찍어 커서가 한 번 잠겼다 풀린다
- **ESC는 확인 패널에서 창을 닫지 않고 선택 패널로 돌아간다**(`CancelByEscape`) — 모든 층에서 '취소'라는 규칙 그대로다
- **`QuitFromMatch()`는 `CompleteMatchExit()`을 쓰지 않는다** — 그 안이 결과 씬을 로드하는데 앱을 끌 것이라 볼 사람이 없고, 같은 이유로 `BeginMatchExit()`의 4초 유예도 건너뛴다
- **`Disconnect()`를 직접 부른다** — `Application.Quit()`으로는 `Managers.Clear()`가 돌지 않아 `IngameScene.Clear()`의 정리가 보장되지 않는다
- **종료 요청 패킷을 보내지 않는 것은 의도다**(서버 확인) — 서버가 강제 종료한 플레이어를 찾아 정상 이탈 처리하며, 자발적 종료도 같은 맥락으로 인식한다. **탈출을 제외한 모든 이탈은 인벤토리 소실이 전제**이므로 포기를 따로 알릴 이유가 없다
- **로비의 `Esc`와 층 구조는 같고 내용은 다르다** — 양쪽 다 '가장 위에 있는 것 하나만 소비'하지만 로비 쪽 아래층은 상태별 뒤로가기·로그아웃 확인이다(`UI/CLAUDE.md`의 '로비 ESC 우선순위'). **동작을 통일 대상으로 보지 말 것**

## 상호작용 상태 관리

`_canInteract`/`_interactTarget`을 씬이 중앙 보유하고 `PlayerController.CheckInteractable()`이 매 프레임 갱신한다.

- `TryInteract()`: 컨테이너가 열려 있으면 닫고 아니면 대상의 `Interact()`를 부른다. **컨테이너 외의 UI가 떠 있으면 막는다** — 커서가 풀리면 시점이 멈춘 채 직전 조준 대상이 남아, 창을 띄운 상태로 컨테이너·귀환이 눌린다
- `_isContainerOpen`: objectId가 0일 수 있으므로 별도 bool 플래그를 쓴다
- Deny 처리는 아래 '거부 사유' 참조

### 귀환(Recall) 상태 관리

`_recallRequested`는 **스팟별이 아닌 씬 단위 플래그**여야 다른 스팟으로 이동해 재요청하는 경로가 막힌다.

- 2단계: `RequestRecall()` → `HandleRecallResponse()`(승인/거부) → `HandleRecallResult()`(서버 5초 검사 후 최종 성공/취소)
- 승인 시점에는 잠금을 유지한다. 해제는 거부·취소·워치독 만료 시에만
- **워치독(`RECALL_TIMEOUT` 10초)은 제거하지 말 것** — 응답 유실 시 잠금만 해제하고 결과를 추측하지 않아 판정 권한이 서버에 남는다. 늦게 온 통지도 정상 처리되므로 **헛발동 비용이 없고**, 없으면 통지가 유실될 때 **그 판 탈출이 영영 불가능해진다**

#### 귀환 카운트다운 (`_isInEscapeSequence` / `SetEscapeSequence`)

**승인(`HandleRecallResponse` 성공)에서 켜고 최종 결과에서 끈다 — 요청 시점이 아니다.** 5라는 숫자의 출처가 서버의 1초 간격 5회 검사이고 그 시계가 승인에서 시작하므로, 요청부터 세면 승인이 늦은 만큼 숫자가 실제보다 빨리 흐른다.

- **`_isInEscapeSequence`에 직접 대입하지 말 것.** `SetEscapeSequence()`가 플래그와 UI 활성화를 같은 자리에서 바꾼다 — 대입 지점이 늘면 카운트다운이 화면에 남거나 반대로 안 뜬다
- **끄는 자리는 셋이고 `BeginMatchExit()`이 캐치올이다** — 귀환 성공·사망·연결 끊김이 전부 그곳을 지나므로, **카운트다운 중 사망처럼 통보 순서가 뒤집혀도** UI가 연출 위에 남지 않는다. 나머지 둘은 재시도 가능한 취소(`HandleRecallResult`의 `default`)와 워치독 만료다
- **카운트다운은 표시일 뿐 종료를 판정하지 않는다 — 1에서 멈춰 결과를 기다린다.** 0까지 내려가거나 스스로 꺼지면 서버 지터로 결과가 5초를 넘길 때 화면과 실제가 갈린다
- **`_recallTimer`를 재사용하지 않는다** — 기점(요청 vs 승인)도 길이(10 vs 5)도 목적도 다르다. 합치면 워치독과 표시가 함께 틀어진다
- **`ESCAPE_COUNTDOWN_SEC`(5)는 서버 검사 횟수와 손으로 맞추는 값이다.** 패킷으로 오지 않아 서버가 바꾸면 조용히 어긋난다(증상: 1에서 한참 멈추거나, 숫자가 남았는데 결과가 온다)
- 숫자는 바뀔 때만 UI에 민다(`_shownCountdown`) — `SetCountdown`이 `int.ToString()`을 해서 매 프레임 부르면 5초에 300번 문자열을 만든다
- **입력을 잠그지 않는다** — 카운트다운 중에도 움직일 수 있어야 하고, 존을 벗어나면 서버가 `OUT_OF_ZONE`으로 취소한다(그때 숫자는 안내 없이 사라진다)

### 드래그 + 서버 요청/응답

- 드래그: `BeginDrag`/`UpdateDragPosition`/`EndDrag` → 고스트 제어
- 아이템 조작: `RequestInteractContainerObject()` (get=0, swap=1, merge=2)
- 장비 장착: `RequestEquipItem()` (장착=0, 해제=1)

### 거부 사유 (`HandleContainerDeny`)

`D2CResponseInteractContainerObjectDeny`와 `D2CResponseEquipItemDeny`가 **같은 비트를 쓰므로 처리도 한 함수로 모은다** — 나누면 한쪽만 고쳐져 갈린다. 기본 처리는 `RequestRecentInventoryInfo()` 재동기화이고 아래 둘만 예외다.

| 비트 | 처리 | 근거 |
|---|---|---|
| `0x0400` CONTAINER_NOT_OPEN | `CloseContainerLocal()` 후 재동기화 | 그 컨테이너가 **나에게** 열려 있지 않다 |
| `0x0800` OUT_OF_RANGE | **아무것도 하지 않는다** | 가까이 가면 성공하는 일시적 조건 |

- **UI가 열려 있다고 내 것이라는 보장이 없다** — 가장 흔한 `0x0400`은 소유권 이전이다. 내가 상호작용 거리 밖에 나가 있는 동안 남이 그 컨테이너를 열면 점유가 넘어간다
- **`0x0400`에서 컨테이너 재동기화 요청은 성립하지 않는다.** 먼저 닫아 `IsContainerOpen`을 내려야 뒤따르는 `RequestRecentInventoryInfo()`가 내 인벤토리만 요청한다. **순서를 뒤집지 말 것**
- **`CloseContainerLocal()` 앞의 `_isContainerOpen` 가드를 빼지 말 것** — 닫힌 상태에서 부르면 `OnUIClosed()`가 `_uiOpenCount`를 잘못 깎는다. 내가 닫은 뒤 앞선 조작의 거부가 도착하는 경로가 실재한다(서버가 close를 먼저 처리한 경우)
- **이 통보에는 `container_object_id`가 없다** — 앞 컨테이너의 거부가 늦게 오면 방금 연 컨테이너를 닫는다. **감수하기로 한 오작동**이고 복구는 다시 여는 것이다(서버 점유는 거리로 풀린다)
- **둘 다 서버 오류가 아니라 정상 플레이에서 나오므로 `LogWarning`이다.** `LogError`로 올리면 진짜 내부 오류(`0x0200`)가 묻힌다
- **`0x0040`(타입 불일치)의 등급은 경로로 가른다** — 장비 쪽은 탄약 칸에 무기를 내리는 흔한 오조작이라 경고, 컨테이너 merge 쪽은 클라가 `item_id` 일치를 확인하고 보내므로 이상 신호라 에러다. 합치면 한쪽이 반드시 묻힌다
- **`ApplyEquipItem`의 무조건 스왑은 결함이 아니다** — 서버가 **장비 슬롯으로 들어가는 쪽**(장착이면 소스 칸, 해제면 목적지 칸)의 타입을 모든 변경보다 앞에서 검사해 `0x0040`으로 막으므로, 무기 슬롯에 탄약이 들어앉는 응답이 오지 않는다. 클라에 타입 가드를 덧대지 않아도 된다
- **장비 경로의 거부에는 되돌릴 로컬 상태가 없다.** 서버가 모든 변경보다 앞에서 검사하고 클라는 응답에서만 반영하므로(로컬 예측 없음) **롤백 코드를 만들지 말 것**
- **자동 재시도를 붙이지 말 것**(서버 계약) — 재전송해도 결과가 같다. 다시 쓰려면 열기부터 한다
