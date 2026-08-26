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

### 세션 유지 (`PostResumeSessionCall` / `ClearAuthStateLocal`)

매치 종료 후 로비 복귀 시 `POST /api/session/resume`으로 세션 유효성을 확인하고 결과가 반영된 재화·인벤토리를 재조회한다. 호출 맥락은 `Scenes/CLAUDE.md`의 '로비 복귀와 세션 유지' 참조.

- **반환은 `bool`이 아니라 `ResumeResult` 3상태다.** 실패를 뭉뚱그리면 막다른 길이 생긴다 — `PostLoginCall`이 `AuthState != None`이면 거부하므로, 네트워크 오류에서 인증 상태를 남기면 로그인이 아예 막히고 지워버리면 살아 있는 세션을 클라가 스스로 버린다
  - `Expired`: HTTP 401 **또는** 200으로 감싸 온 본문의 `code == 401`. 재로그인 외에 방법이 없으므로 `ClearAuthStateLocal()` + 폴백 확정
  - `Unreachable`: 응답 없음 또는 그 외 오류. 세션이 아직 살아 있을 수 있어 재시도 여지가 있다. **자동 재시도는 하지 않는다** — 타임아웃 5초가 그대로 검은 화면이 되므로 호출자가 사용자에게 묻는다
- `ShopItems`는 응답에 실리지 않는다(세션 중 불변이라 재전송하지 않는 스펙). 로그인 때 받은 캐시를 유지할 것
- `ClearAuthStateLocal()`은 **서버 호출 없는 로컬 인증 리셋**이다. 세션이 서버에서 이미 사라진 경우 로그아웃 API를 부를 수 없어 분리했으며 로그아웃 성공 블록과 공유한다

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
