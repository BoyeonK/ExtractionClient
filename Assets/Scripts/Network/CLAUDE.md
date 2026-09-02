# 네트워크 구조

## HTTP (`HTTPManager`)
- 정적 `HttpClient`, 타임아웃 5초, 베이스 URL은 `Gitignores.baseUrl`에서 가져옴
- 인증이 필요한 요청은 `x-session-id` 헤더 포함
- `Gitignores.cs`는 git에서 제외된 파일 — **베이스 URL 하드코딩 금지**

엔드포인트 전체 목록 및 요청/응답 스키마: `Assets/Scripts/Network/http-api-spec.yaml` (OpenAPI 3.0) 참고. **이 파일은 서버 사본과 동기화된다 — 한쪽만 수정하지 말 것**

매칭 흐름: `StartMatchCall` → `CheckMatchStatusCall` 폴링 (WAITING → SUCCESS) → `TryConnectCall` → UDP 연결 시작

### 상태 코드가 필요한 호출 (`SendRequestWithStatusAsync`)

`SendRequestAsync`는 본문 문자열만 돌려주므로 상태 코드로 분기할 수 없다. 필요한 호출부는 `SendRequestWithStatusAsync`(본문 + 상태 코드 + `HasResponse`)를 쓴다. `SendRequestAsync`는 이 함수의 본문만 꺼내는 래퍼이므로 **둘 중 하나만 고치지 말 것**.

`HasResponse == false`는 서버 응답 자체가 없었다는 뜻(전송 실패·취소)이며 이때 상태 코드는 의미가 없다.

### 아이디·비밀번호 형식 규칙 (`IsValidId` / `IsValidPassword`)

**규칙과 안내 문구의 유일한 출처는 이 둘과 `ID_RULE_MESSAGE`/`PASSWORD_RULE_MESSAGE`다.** `public static`인 것은 UI가 선검사에 같은 것을 쓰기 위해서이고, **정규식이나 문구를 UI로 복제하면 "클라는 통과인데 서버가 거부"가 되며 어느 쪽이 낡았는지 알 수 없다.**

- **비밀번호 정규식은 특수문자를 요구하지 않는다** — 필수는 영문+숫자이고 특수문자는 허용 문자에 불과하다. 문구를 '특수문자 필수'로 되돌리지 말 것(검사와 안내가 갈린다). 서버가 실제로 특수문자를 요구한다면 문구가 아니라 **정규식에 lookahead를 더하는 쪽**이다
- **HTTPManager는 여전히 팝업을 띄우지 않는다**(로그인·버전 확인과 같은 구도). 문구 상수를 내주기만 하고 무엇을 보여줄지는 호출자가 고른다

### 로그인 실패 구분 (`LoginResult`)

`PostLoginCall`의 반환은 `bool`이 아니라 3상태다. 409는 아이디·비밀번호와 무관한 거부라 뭉뚱그리면 **매치가 끝날 때까지 "아이디와 비밀번호를 확인해주세요"만 반복해서 보게 된다.**

- **409의 두 사유(`ERR_ALREADY_IN_GAME` / `ERR_MATCH_ALREADY_SUCCESS`)는 안내를 하나로 묶는다** — 둘 다 매치가 끝나기를 기다리는 것 외에 사용자가 할 수 있는 일이 없다. 사유를 가르려면 상태 코드가 아니라 본문 `error.code`를 봐야 한다
- **409 판정은 본문 파싱보다 앞에 둘 것** — 409 본문 형식이 명세에 없어 비어 올 수 있고, 빈 문자열을 `JsonUtility.FromJson`에 넣으면 예외가 `async void` 호출부로 빠져나가 **팝업조차 뜨지 않고 로그인 버튼이 굳는다**
- 본문의 `code == 409`도 함께 본다 — 실패를 200으로 감싸 보내는 응답이 있다(세션 유지의 401과 같은 이유)

### 버전 확인 (`GetVersionCall` / `VersionResult`)

`GET /api/version`. 반환은 `VersionResult` 4상태이고 **안내 문구는 호출자가 고른다** — HTTPManager는 팝업을 띄우지 않는다(로그인·세션 유지와 같은 구도).

- **점검(`isMaintenance`)을 버전 비교보다 먼저 본다** — 둘 다 참일 수 있는데 점검 중에는 최신 클라를 받아도 못 들어가므로, 버전 안내를 먼저 하면 사용자를 헛수고시킨다
- **`HTTPManager.version`은 서버 `latestVersion`과 손으로 맞추는 값이다** — 어긋나면 표시가 틀어지는 정도가 아니라 **아무도 로그인 화면에 못 간다.** 서버 빌드가 값을 올리면 이 상수도 함께 고칠 것
- **`latestVersion`이 비어 오면 불일치로 본다**(확정). **'비었으면 통과'로 완화하지 말 것** — 검사를 켜 둔 의미가 사라진다
- **비-JSON 본문을 파싱보다 앞에서 걸러낸다** — 예외가 `async void` 호출부로 빠져나가면 팝업 없이 시작 버튼이 굳는다
- 점검 중에도 서버는 **200 + `isMaintenance: true`**로 응답한다(2026-08-31 서버 확인). 상태 코드로 가르는 경로를 만들지 말 것

### 세션 유지 (`PostResumeSessionCall` / `ClearAuthStateLocal`)

매치 종료 후 로비 복귀 시 `POST /api/session/resume`으로 세션 유효성을 확인하고 결과가 반영된 재화·인벤토리를 재조회한다. 호출 맥락은 `Scenes/CLAUDE.md`의 '로비 복귀와 세션 유지' 참조.

- **반환은 `bool`이 아니라 `ResumeResult` 3상태다.** 실패를 뭉뚱그리면 막다른 길이 생긴다 — `PostLoginCall`이 `AuthState != None`이면 거부하므로, 네트워크 오류에서 인증 상태를 남기면 로그인이 아예 막히고 지워버리면 살아 있는 세션을 클라가 스스로 버린다
  - `Expired`: HTTP 401 **또는** 200으로 감싸 온 본문의 `code == 401`. 재로그인 외에 방법이 없으므로 `ClearAuthStateLocal()` + 폴백 확정
  - `Unreachable`: 응답 없음 또는 그 외 오류. 세션이 아직 살아 있을 수 있어 재시도 여지가 있다. **자동 재시도는 하지 않는다** — 타임아웃 5초가 그대로 검은 화면이 되므로 호출자가 사용자에게 묻는다
- `ShopItems`는 응답에 실리지 않는다(세션 중 불변이라 재전송하지 않는 스펙). 로그인 때 받은 캐시를 유지할 것
- `ClearAuthStateLocal()`은 **서버 호출 없는 로컬 인증 리셋**이다. 세션이 서버에서 이미 사라진 경우 로그아웃 API를 부를 수 없어 분리했으며 로그아웃 성공 블록과 공유한다
- **`ClearAuthStateLocal()`은 `IsMatching`도 지운다** — `TicketId`와 한 쌍이라(`StartMatchCall`이 함께 세우고 `CancelMatchCall`이 함께 지운다) 남겨두면 `PostLoginCall`의 첫 가드에 걸려 **재로그인이 통째로 막힌다**

### 세션 만료 통보 (`OnSessionExpired`)

`requireAuth` 요청이 **401**(또는 200으로 감싸 온 본문의 `code == 401`)을 받으면 `SendRequestWithStatusAsync`가 감지해 `OnSessionExpired`를 발화한다. 401은 어느 요청에서 왔든 결론이 '세션이 죽었으니 재로그인' 하나라 호출자가 분기할 여지가 없으므로, **호출부마다 반환 타입을 3상태로 늘리지 말고 이 경로를 쓸 것** — 새 인증 API를 추가해도 배선이 필요 없고, 빠뜨려서 조용히 옛 동작으로 돌아가는 일이 없다.

- **`notifySessionExpiry: false`는 세션 수명을 스스로 다루는 둘뿐이다.** `PostResumeSessionCall`은 401을 `Expired`로 직접 돌려주고 호출자가 자기 폴백 UI를 돌리므로 통보까지 나가면 **팝업과 전이가 두 번씩 돈다.** `PostLogoutCall`의 401은 세션이 이미 없다는 뜻이라 **성공으로 처리한다**(스스로 로그아웃을 누른 사용자에게 "만료" 안내가 뜨는 꼴이 된다)
- **통보는 세션당 1회다.** 래치 해제는 `AuthState`가 `None`을 벗어나는 setter 한 곳뿐 — 대입 지점마다 풀게 하면 새 인증 경로에서 반드시 하나가 빠진다
- **`IsUnauthorized()`의 파싱 실패는 '만료 아님'으로 흘린다** — 여기서 예외가 새면 모든 인증 요청의 응답 처리가 함께 죽는다

### 구매 실패 구분 (`PurchaseResult`)

`PostPurchaseCall`의 반환은 6상태다. **서버 400이 5종이라고 상태를 5개로 늘리지 말 것** — 가르는 기준은 에러 코드 개수가 아니라 **사용자가 할 수 있는 일이 다른가**이며(`LoginResult`와 같은 원칙), 개발자가 알아야 할 구분은 enum이 아니라 `error.code` 로그가 진다.

| 상태 | 출처 | 호출자 |
|---|---|---|
| `NotEnoughMoney` | 402 | 안내만 — **재조회하지 않는다**(서버가 아무것도 바꾸지 않아 같은 내용이 다시 온다) |
| `OutOfSync` | 409, 400/`ERR_SLOT_OCCUPIED` | 재조회 후 안내 |
| `Rejected` | 그 외 400, 403, 404 | 재조회 후 안내 + `LogError` |
| `Unreachable` | 응답 없음 | 안내만 — **재조회하지 않는다**(서버에 닿지도 못해 5초 타임아웃만 한 번 더 돈다) |
| `Busy` | `_isRequesting`·`IsMatching`·미로그인 | **무통보** — 요청이 나가지도 않았다 |

- **상태 코드 판정이 본문 파싱보다 앞이어야 한다** — 실패 응답의 본문 스키마가 명세에 없어 비어 오거나 JSON이 아닐 수 있고, 그대로 `JsonUtility.FromJson`에 넣으면 예외가 `async void` 호출부로 빠져나가 **팝업 없이 굳는다**(로그인 409와 같은 함정)
- **재조회 후에 안내한다** — 순서를 뒤집으면 "갱신되었습니다"가 갱신 전에 뜬다. 재조회가 실패해도 안내는 예정대로 낸다(구매가 실패했다는 사실은 달라지지 않는다)
- `error.code`가 실려 오지 않으면 `Rejected`로 흘린다. **가정하고 분기를 늘리지 말 것** — 본문 스키마가 아직 서버에 확인되지 않았다

### 판매 실패 구분 (`SellResult`)

`PostSellCall`의 반환은 5상태다. **`PurchaseResult`를 재사용하지 말 것** — 대금을 받는 쪽이라 402가 없어 도달 불가능한 `NotEnoughMoney`가 남고, 판매 전용 사유가 생길 때 구매 쪽이 오염된다.

| 상태 | 출처 |
|---|---|
| `OutOfSync` | 409, 400/`ERR_SLOT_EMPTY`, 400/`ERR_ITEM_MISMATCH` |
| `Rejected` | 400/`ERR_BAD_REQUEST`, 400/`ERR_DUPLICATE_SLOT`, 그 외 |

호출자 처리·판정 순서·`error.code` 없을 때의 처리는 전부 위 `PurchaseResult`와 같다.

- **`item_id`·`quantity`는 판매 지시가 아니라 서버가 스냅샷의 해당 슬롯과 대조하는 검사값이다.** `quantity`는 판매 수량이 아니라 **그 슬롯 스택 전체 수량에 대한 주장**이라 정확히 일치해야 하고, 어긋나면 `ERR_ITEM_MISMATCH`다 — **수량 선택 UI를 만들면 안 된다**(부분 판매는 클라가 스택을 먼저 나눈 스냅샷으로 표현한다)
- **호출자는 스냅샷을 만든 뒤 그것이 확정한 값을 넘길 것** — 따로 읽으면 어긋난다

### 매치 시작 실패 구분 (`MatchStartResult`)

`StartMatchCall`의 반환은 6상태다. **409를 하나로 묶지 말 것** — `ERR_ALREADY_IN_MATCH`는 서버에만 큐가 있고 클라에 `TicketId`가 없어 폴링도 취소도 못 하는 상태라(`CheckMatchStatusCall`·`CancelMatchCall`이 둘 다 `TicketId`를 요구한다) 재조회가 아무것도 바꾸지 않는다.

| 상태 | 출처 | 호출자 |
|---|---|---|
| `OutOfSync` | 409/`ERR_SNAPSHOT_MISMATCH`, code 없는 409 | 재조회 후 안내 |
| `AlreadyInMatch` | 409/`ERR_ALREADY_IN_MATCH` | 안내만 — **재조회하지 않는다** |
| `Rejected` | 400 전체, 그 외 | 재조회 후 안내 + `LogError` |
| `Unreachable` | 응답 없음 | 안내만 |
| `Busy` | `_isRequesting`·`IsMatching`·미로그인·스냅샷 없음 | **무통보** |

판정 순서와 200으로 감싼 실패 처리는 위 `PurchaseResult`와 같다.

- **400 여섯 종을 상태로 늘리지 말 것** — `ERR_LOADOUT_NOT_EMPTY`·`ERR_NO_WEAPON_EQUIPPED`는 `LobbyScene`이 선제로 막으므로 도달하면 그 검사가 새는 클라 버그이고, 나머지도 사용자가 할 수 있는 일이 같다
- **code 없는 409는 `OutOfSync`로 흘린다**(`PurchaseResult`가 `Rejected`로 흘리는 것과 갈리는 지점) — 헛도는 재조회는 해가 없지만, 반대로 흘리면 다시 시도하면 되는 경우를 막다른 길로 안내한다
- **`inventory`는 FREE·CUSTOM 모두 필수다.** 빈 배열은 '가진 것이 없다'는 유효한 스냅샷이라 길이 검사는 CUSTOM에만 건다 — 모드 무관으로 올리면 게스트와 창고까지 빈 계정이 매치를 시작하지 못한다

## UDP (`UDPManager`)
- 전용 백그라운드 워커 스레드(`UDP_Network_Thread`) — 수신 + 송신 큐 소진
- `Poll(1ms)` 기반 루프, 수신 데이터는 모두 `Managers.ExecuteAtMainThread`로 메인 스레드 전달
- 송신: `SendReliable(packetId, IMessage)` / `SendUnreliable(packetId, IMessage)`
- `Disconnect()` 순서: `_isRunning = false` → 스레드 Join → `_socket.Close()` → `Handler.Reset()`
- 3초마다 `C2DHeartBeat` 자동 전송
- **연결 유지 판정은 수신 여부 단독** — 서명 검증을 통과한 패킷을 10초(`RECV_TIMEOUT_SEC`) 이상 못 받으면 `Disconnect()`. 서버가 하트비트에 응답하므로 정상 수신 간격은 3초 이하이고, 10초는 3회 연속 무응답에 해당한다
  - 재전송 횟수 한도는 없다. 연결이 살아있다고 보는 동안에는 계속 재시도한다 — ACK 실패는 "그 패킷이 도착했는가"이지 "상대가 살아있는가"가 아니다
  - 워치독 시작 시각은 `SetSessionVariable`에서 시드한다. 0으로 두면 `Time.realtimeSinceStartup`과 비교되어 접속 즉시 오탐이 난다
  - 재전송 한도가 사라지면서 in-flight 32개 초과를 막던 장치도 없어졌다. 초과 시 `MakeReliablePacket`이 미ACK 슬롯을 덮어쓰고 그 패킷은 유실되므로 에러 로그로 남긴다

## 패킷 형식 (`PacketHandler`)
- **헤더**: `UDPHeader` 35바이트, `LayoutKind.Sequential Pack=1`
- **서명/검증**: `xxHash64(패킷 전체 + securityKey)` — securityKey는 핸들러 내부 상태
- **플래그**: `FLAG_HAS_ACK=0x01` / `FLAG_RELIABLE=0x02` / `FLAG_FRAGMENTED=0x04`(예약)
- ACK: 모든 송신 패킷 헤더에 piggybacked
- **`timestampEcho`는 서버의 끊김 판정 유일 근거** — 클라가 마지막으로 되돌려준 서버 timestamp가 6초 이상 갱신되지 않으면 서버가 세션을 `DISCONNECTED`로 강제 이탈시킨다(인벤토리 소실 포함). 따라서 수신 timestamp 보관은 **채널을 가리지 않는다**. reliable 수신 시에만 갱신하도록 되돌리지 말 것 — 인게임 정상 구간에서 서버가 보내는 것 대부분이 unreliable이라 그 즉시 끊긴다
  - 서버 시계 도메인 값이므로 가공 금지. 역행 방지를 위해 더 큰 값일 때만 갱신한다(세션 최대 15분이라 랩어라운드는 고려 대상 아님)
  - `timestampEcho == 0`인 세션을 판정에서 제외하는 것은 서버의 과도기 임시 조치다. 0을 계속 보내면 RTT 측정도 무력화되므로 의존 금지
- 직렬화: Google.Protobuf (`GameProtocol` 네임스페이스)
  - `External_Protocol.proto`: `PktId` enum + 모든 C2D/D2C 메시지
  - `External_Unity_Object.proto`: 공유 오브젝트 타입
- Zero-Allocation 패킷 조립: `MemoryMarshal.Write/Read` + `Span<byte>`

### 핸들러 함수 실행 컨텍스트 (중요)
- `Handle_XXX`는 **UDP 워커 스레드**에서 호출된다 — Unity API 직접 호출 금지
- Unity API 필요 시 반드시 `Managers.ExecuteAtMainThread`로 위임

### Protobuf 타입 격리 규칙
- `GameProtocol.*` 타입은 `Handle_XXX` 밖으로 노출하지 않는다
- 핸들러 내부에서 Unity/C# 기본 타입으로 변환 후 전달

### `fire_sequence` (`C2DRequestWeaponFire`)

`PacketHandler._fireSequence`. **서버 판정은 '기대값보다 작으면 거부, 같거나 크면 수락 후 기대값 이동'** 이다(완전 일치 요구는 옛 규칙). 발사는 unreliable이라 재전송이 없고 `uSeqNum`을 이동 패킷과 공유해 재정렬만으로 한 장이 드롭되는데, 예전에는 그 한 장 때문에 이후 발사가 영구히 거부됐다. **번호가 건너뛰는 것은 정상이며 서버가 따라붙는다.**

- 클라 계약은 **매 발사 무조건 증가 / 되돌리거나 재사용 금지** 둘뿐이다. 유실을 감지했다고 되돌리지 말 것 — 되돌린 번호는 '낡은 시퀀스'로 거부되고, 복구 경로는 `D2CResponseReload.fire_sequence` 하나뿐이라 그때까지 그 무기의 발사가 계속 거부된다
- 반영은 `RaiseFireSequenceTo()`의 `max()` 단조 상승만. **대입 setter를 만들지 말 것** — 요청을 보낸 뒤 응답 전에 쏜 발사가 이미 번호를 올려놨을 수 있다
- 접근자는 `NextFireSequence()`(송신, 메인 스레드)와 `RaiseFireSequenceTo()`(수신 반영) 둘이며, 후자를 **`ExecuteAtMainThread` 안에서만** 부른다. 단일 스레드 소유라 lock이 없으므로 워커 스레드에서 직접 부르면 그 전제가 깨진다

### 재장전 연출 통보 (44 / 45) — 유실이 정상 범위다

`C2DNotifyReloadSequence`(44)와 `D2CNotifyReloadSequence`(45)는 **양방향 모두 unreliable이다(서버 계약).** 서버는 값을 해석하지도 보관하지도 않고 `object_id`만 채워 중계하며, `C2DRequestReload`(42)와 아무 연결도 없다.

- **특히 C2D 쪽은 매 틱 나가는 상태 패킷과 `uSeqNum`을 공유해 조금만 늦게 도착해도 버려진다.** 단계가 통째로 빠지는 것이 정상이므로 **재전송·순서 보정을 붙이지 말 것** — 연출용 패킷이 in-flight 32슬롯을 먹게 된다
- 수신 측이 상태를 두지 않는 이유가 이것이다(`Scenes/CLAUDE.md`). **"그 단계만 안 들린다"를 결함으로 보고 reliable로 올리지 말 것**
- `sequence_num`은 **0~15 범위 안에서 쓴다** — 벗어나면 varint가 2B가 된다. **15는 서버 전용**이라 클라가 보내면 통보 전체가 버려진다

### `D2CFullInventorySync`가 실리는 패킷은 둘이다

전체 동기화(30)와 재장전 응답(43, `D2CResponseReload.inventory`)이 같은 메시지를 쓴다. 변환은 `ToInventoryItem()`/`ToInventorySlotArray()`로 공유하되 **핸들러는 공유하지 않는다** — 전체 동기화 쪽에는 최초 1회용 초기화가 딸려 있고 버전 비교가 없다(`Scenes/CLAUDE.md`의 '재장전' 참조).

### `.proto` 주석 정책 (2026-08-27 확정)

`External_Protocol.proto`에 주석을 달거나 정리할 때의 규칙이다.

| 구분 | 내용 | 처리 |
|---|---|---|
| **항구 주석** | 그 패킷이 **어느 상황에 쓰이는지** — 한 줄(길어도 두 줄) | 남긴다 |
| **처리 과정** | 받아서 **어떻게 처리하는지**, `~할 것` 형태의 지시문 | **넣지 않는다** — 모듈 CLAUDE.md 소관 |
| **`[작업사항]` 블록** | 서버 → 클라 **세션 간 인계용 임시 주석** | 해당 작업이 끝나면 그 블록만 삭제 |

- 필드 단위 의미(`0xFFFFFFFF`=없음, 비트마스크 표 등)를 적는 **인라인 주석은 항구 주석에 포함**된다. 이건 처리 과정이 아니라 필드 계약이다
- **`[작업사항]`은 서버 팀이 쓰는 태그다.** 클라가 반영을 끝냈으면 지우되, **파일을 서버에 다시 전달하는 것은 사용자가 직접 한다** — 클라에서만 지우면 두 사본이 갈라진다
- 블록을 지울 때는 **그 안의 영속 정보가 모듈 CLAUDE.md에 있는지 먼저 대조할 것.** 설계 근거가 proto에만 있는 채로 지우면 그대로 소실된다
- **블록 사이의 상호 참조에 주의할 것** — 헤더의 `공통 사항 (N)`을 본문 블록이 가리키고 있어, 헤더만 지우면 참조가 끊긴다

### 새 UDP 패킷 타입 추가 절차
1. `External_Protocol.proto`에 메시지 정의 + `PktId` 항목 추가
2. `PacketHandler` 생성자에 핸들러 등록
3. `Handle_XXX` 구현 — Unity API는 `ExecuteAtMainThread`로, 데이터는 기본 타입으로 변환

**서버에서 새 패킷을 받을 때는 `PktId` 등재 여부를 먼저 확인할 것.** 메시지 주석에는 번호가 적혀 있는데 `enum PktId`에는 빠져 있는 누락이 두 번 있었다(36~40, 41). 또한 생성된 `ExternalProtocol.cs`가 **메시지 클래스는 있는데 `PktId` 멤버는 없는 반쪽 상태**로 넘어온 적도 있으므로, 배선 전에 양쪽을 모두 확인해야 한다. `ExternalProtocol.cs`는 `.gitignore` 대상이라 git diff로는 드러나지 않는다.
