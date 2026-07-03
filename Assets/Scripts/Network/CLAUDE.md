# 네트워크 구조

## HTTP (`HTTPManager`)
- 정적 `HttpClient`, 타임아웃 5초, 베이스 URL은 `Gitignores.baseUrl`에서 가져옴
- 인증이 필요한 요청은 `x-session-id` 헤더 포함
- `Gitignores.cs`는 git에서 제외된 파일 — 서버 URL 등 민감 설정 관리. **베이스 URL 하드코딩 금지**
- 주요 프로퍼티: `AuthState`, `SessionId`, `Uid`, `Inventory`, `Money`, `ShopItems`, `MapId`

엔드포인트 전체 목록 및 요청/응답 스키마: `Assets/Scripts/Network/http-api-spec.yaml` (OpenAPI 3.0) 참고

매칭 흐름: `StartMatchCall` → `CheckMatchStatusCall` 폴링 (WAITING → SUCCESS) → `TryConnectCall` → UDP 연결 시작
구매 흐름: `LobbyScene.TryPurchase()` → 빈 슬롯 탐색(창고 우선) → 스냅샷 조립 → `PostPurchaseCall()` → `OnPurchaseComplete()`

## UDP (`UDPManager`)
- 전용 백그라운드 워커 스레드(`UDP_Network_Thread`) — 수신 + 송신 큐 소진 담당
- `Socket.Connect()`로 목적지 고정. `Poll(1ms)` 기반 루프로 수신 확인, 데이터 없어도 최대 1ms마다 송신 큐(`ConcurrentQueue`) 소진
- `Disconnect()` 시 `_isRunning = false` → 스레드 Join(최대 2초, Poll 루프 자연 종료) → `_socket.Close()` → `Handler.Reset()` 순서
- 수신 데이터는 모두 `Managers.ExecuteAtMainThread`를 통해 메인 스레드에서 처리
- 송신: `SendReliable(packetId, IMessage)` / `SendUnreliable(packetId, IMessage)` — 큐에 삽입, 워커 스레드가 실제 전송
- 인벤토리 조작 송신: `SendC2DRequestInteractContainerObject(interactType, startObjectId, startVersion, startSlotIdx, quantity, endObjectId, endVersion, endSlotIdx)` (Reliable) / `SendC2DRequestEquipItem(actionType, equipmentSlotType, objectId, objectVersion, objectSlotIdx, myInventoryVersion)` (Reliable) — `myInventoryVersion`은 objectId가 컨테이너일 때 플레이어 인벤토리 버전, PLAYER_OBJECT_ID(0xFFFFFFFF)일 때는 0
- 인벤토리 재동기화 요청: `SendC2DRequestRecentInventoryInfo(uint objectId)` (Reliable) — `objectId=0xFFFFFFFF`이면 플레이어 인벤토리, 그 외는 컨테이너. Deny 수신 시 호출. 서버는 기존 `D2CFullInventorySync` / `D2CResponseOpenContainer`로 응답
- `OnUpdate()`가 매 프레임 `PacketHandler.CollectRetransmits()`를 호출해 RTO 초과 패킷을 큐에 재삽입. 재전송 7회 초과 시 `Disconnect()`. 3초마다 `C2DHeartBeat`(Unreliable) 자동 전송
- Reliable 재전송 상수: `MIN_RTO_MS=50ms` (RTO 하한), `MAX_RTO_MS=1000ms` (RTO 상한), `MIN_RTT_MS=20ms` (RTT 하한), `MAX_RETRY=7` (최대 재전송). RTO는 `Mathf.Clamp(SRTT + 4×RTTVAR, 50, 1000)`으로 계산

## 패킷 형식 (`PacketHandler`)
- **헤더** (`UDPHeader`, 35바이트, `LayoutKind.Sequential Pack=1`):
  `signature(8) | packetId(2) | sessionId(2) | rSeqNum(4) | uSeqNum(2) | flags(1) | ackRSeqNum(4) | ackBitfield(4) | timestamp(4) | timestampEcho(4)`
- **서명/검증**: 송신 시 `signature=0`으로 조립 후 `xxHash64(패킷 전체 + securityKey)`를 `signature` 필드에 기록. 수신 시 동일 방식으로 재계산해 검증 실패 패킷 드롭. `securityKey`는 헤더 필드가 아닌 `PacketHandler` 내부 상태로 관리
- **플래그** (`UDPFlags`): `FLAG_HAS_ACK=0x01` (ack 필드 유효) / `FLAG_RELIABLE=0x02` (재전송 대상) / `FLAG_FRAGMENTED=0x04` (예약)
- Reliable 채널: `rSeqNum` 사용, pending 큐에 등록, ACK 수신 시 제거
- Unreliable 채널: `uSeqNum` 사용, 재전송 없음
- ACK 전송: 모든 송신 패킷 헤더에 piggybacked (`FLAG_HAS_ACK` + `ackRSeqNum` + `ackBitfield`)
- 직렬화: Google.Protobuf (`GameProtocol` 네임스페이스)
  - **`External_Protocol.proto`**: `PktId` enum 및 모든 C2D/D2C 메시지 타입 정의. 새 패킷 추가 시 이 파일에 먼저 메시지를 정의한다
  - **`External_Unity_Object.proto`**: 공유 오브젝트 타입 정의 (`UnityGameObject`, `GameObjectMovementInfo`, `Vector3`). `External_Protocol.proto`가 이 파일을 import
- 핸들러 등록: `PacketHandler` 생성자에서 `_handlers.Add((ushort)PktId.XXX, Handle_XXX)`
- Zero-Allocation 패킷 조립: `MemoryMarshal.Write/Read` + `Span<byte>` 사용

### 핸들러 함수 실행 컨텍스트 (중요)
- `Handle_XXX` 함수는 **UDP 워커 스레드**(`UDP_Network_Thread`)에서 직접 호출된다
- 핸들러 내부에서 `GameObject`, `Transform`, `UnityEngine.*` 등 **Unity 전용 API를 직접 호출하면 안 된다**
- Unity API가 필요한 작업은 반드시 `Managers.ExecuteAtMainThread(() => { ... })`로 감싸서 메인 스레드에 위임해야 한다

### Protobuf 타입 격리 규칙
- **Protobuf 타입(`GameProtocol.*`)은 `Handle_XXX` 함수 밖으로 노출하지 않는다**
- 핸들러 내부에서 Unity/C# 기본 타입으로 변환 후 전달할 것
  - `GameProtocol.Vector3` → `UnityEngine.Vector3`
  - Protobuf 메시지 → 전용 데이터 클래스 (예: `StaticObjectData`)

### 새 UDP 패킷 타입 추가 절차
1. `External_Protocol.proto`에 메시지 정의 + `PktId` 항목 추가
2. `PacketHandler` 생성자에 핸들러 등록
3. `Handle_XXX` 메서드 구현 — Unity API 호출이 필요하면 `Managers.ExecuteAtMainThread`로 감쌀 것 (핸들러는 워커 스레드에서 실행됨)
4. 전달할 데이터는 핸들러 내부에서 Unity/C# 기본 타입으로 변환 후 전달 (Protobuf 타입 격리 규칙 준수)
