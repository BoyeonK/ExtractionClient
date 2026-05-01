# 프로젝트 진행 상황

> 최종 수정: 2026-04-30
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI

### 네트워크
- [x] (2026-04-28 #2) 적응형 RTO 적용 — 고정 100ms → `max(100ms, SRTT + 4×RTTVAR)` RFC 6298 EWMA, `timestampEcho` 기반 RTT 샘플링, `UpdateRtt()` 메인 스레드 전용
- [x] (2026-04-28 #3) `UdpClient` → `Socket` 전환 — 수신 버퍼 사전 할당(`_recvBuf[1500]`), per-packet GC 할당 제거, `ProcessReceivedPacket(byte[], int)` 시그니처 변경
- [x] (2026-04-28 #4) 송신 큐 + `Poll(1ms)` 루프 도입 — `ConcurrentQueue` 기반 송신 큐, 워커 스레드가 수신·송신 모두 담당, `Disconnect()` 순서 Join→Close로 변경
- [x] (2026-04-29 #0) xxHash64 서명 도입 — `UDPHeader`에 `signature(8B)` 추가, `securityKey` 필드 제거, 헤더 35B, `BuildPacketInto`에서 `xxHash64(패킷 + securityKey)` 서명 생성
- [x] (2026-04-29 #1) 수신 패킷 signature 검증 — `VerifySignature()` 추가, `ProcessReceivedPacket`에서 검증 실패 시 드롭

### 기타
- [x] (2026-04-29 #2) .proto 파일로 전환 및 캐시 정리 — `External_Protocol.proto` / `External_Unity_Object.proto` 추가, 기존 `ExternalProtocol.cs` / `ExternalUnityObject.cs` 삭제
- [x] (2026-04-30 #0) `LoadingScene1` 씬 전환 코드 완성 — 비동기 로딩 90% 도달 시 `C2DRequestBlueprint` 전송, `staticObjectsLoadFlag` 세팅 후 `CompleteLoadSceneAsync` 호출
- [x] (2026-04-30 #1) TestIngame 전환 코드 작성 및 작동 확인 — D2CResponseBlueprint 수신 후 TestIngame 씬으로 전환하는 흐름 구현 및 검증

### 버그 수정
- [x] (2026-04-30 #2) DragGhost null 참조 버그 수정 — LobbyScene 탈출 시 DragGhost가 null 참조되던 문제 해결
- [x] (2026-04-30 #3) allowSceneActivation 버그 수정 — 비동기 씬 전환 플래그가 변경되지 않던 문제 해결

---

## 진행 중 / 미완성

### 매칭 성공시 씬 전환
/connect요청을 통해서 ip와 port를 받았을 경우
1. ~~workerThread를 살려내고 루프 작동. (ping 작동)~~ ← **완료**
    - workerThread내에서 ReliableFlag로 C2DHeartBeat전송, D2CHeartBeat로 응답 받음.
2. ~~Scene을 LoadingScene으로 변경하고, GameScene의 비동기 로딩 시작.~~ ← **완료**
3. ~~비동기 로딩 완료되었을 경우, C2DRequestBluePrint 전송~~ ← **완료**
4. ~~3의 패킷의 응답을 받았을 경우, 해당 내용을 역직렬화해서 보관하고 Scene교체 진행.~~ ← **완료**
    - D2CResponseBlueprint, 여기서 Spawn위치 결정됨. `staticObjectsLoadFlag = true` 세팅
5. 교체된 Scene의 Init() 함수에서 C2DRequestBluePrint에서 받아온 친구들 까지 포함해서 그려냄 ← **다음**
6. Init함수가 실행된 이후, 서버에 Scene 로딩 완료됬음을 알려줌과 동시에 동적인 정보를 다시 요청.
    - C2DRequestSpawnMe

---

## 다음 작업 우선순위 (제안)

1. **GameScene Init()** — Blueprint 데이터 기반 정적 오브젝트 렌더링
2. **C2DRequestSpawnMe 전송** — Init() 완료 후 서버에 로딩 완료 알림 및 동적 정보 요청
3. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
