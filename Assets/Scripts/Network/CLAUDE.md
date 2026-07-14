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
- 재전송 7회 초과 시 `Disconnect()`. 3초마다 `C2DHeartBeat` 자동 전송

## 패킷 형식 (`PacketHandler`)
- **헤더**: `UDPHeader` 35바이트, `LayoutKind.Sequential Pack=1`
- **서명/검증**: `xxHash64(패킷 전체 + securityKey)` — securityKey는 핸들러 내부 상태
- **플래그**: `FLAG_HAS_ACK=0x01` / `FLAG_RELIABLE=0x02` / `FLAG_FRAGMENTED=0x04`(예약)
- ACK: 모든 송신 패킷 헤더에 piggybacked
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
