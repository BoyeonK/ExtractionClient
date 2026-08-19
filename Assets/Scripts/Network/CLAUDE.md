# 네트워크 구조

## HTTP (`HTTPManager`)
- 정적 `HttpClient`, 타임아웃 5초, 베이스 URL은 `Gitignores.baseUrl`에서 가져옴
- 인증이 필요한 요청은 `x-session-id` 헤더 포함
- `Gitignores.cs`는 git에서 제외된 파일 — **베이스 URL 하드코딩 금지**

엔드포인트 전체 목록 및 요청/응답 스키마: `Assets/Scripts/Network/http-api-spec.yaml` (OpenAPI 3.0) 참고

매칭 흐름: `StartMatchCall` → `CheckMatchStatusCall` 폴링 (WAITING → SUCCESS) → `TryConnectCall` → UDP 연결 시작

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

### 새 UDP 패킷 타입 추가 절차
1. `External_Protocol.proto`에 메시지 정의 + `PktId` 항목 추가
2. `PacketHandler` 생성자에 핸들러 등록
3. `Handle_XXX` 구현 — Unity API는 `ExecuteAtMainThread`로, 데이터는 기본 타입으로 변환
