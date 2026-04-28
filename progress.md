# 프로젝트 진행 상황

> 최종 수정: 2026-04-28
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-25 #0) Lobby 화면에 더미 캐릭터 세워놓기 (애니메이션 적용)
- [x] (2026-04-25 #1) 로그아웃 성공 시 LobbyReconfirmUI 확인 팝업 — `TryLogout` 성공 시 `ActiveOnlyConfirm` → 확인 클릭 후 `OnLogoutComplete` 실행
- [x] (2026-04-25 #2) 버전 확인 실패 시 LobbyReconfirmUI 알림 팝업 — `GetVersionCall` 실패 시 `OnConnectedFailed` 즉시 실행 후 `ActiveOnlyConfirm` 알림 표시

### 네트워크
- [x] (2026-04-28 #0) RUDP 전환 — 헤더 31B, reliable/unreliable 채널 분리, ACK bitfield 재전송 로직, 고정 RTO 100ms
- [x] (2026-04-28 #1) `_pendingReliable` 최적화 — Dictionary → 32슬롯 링 버퍼, byte[1400] 사전 할당, `BuildPacketInto(Span<byte>)` 직접 쓰기, `CollectRetransmits` 반환 List 재사용
- [x] (2026-04-28 #2) 적응형 RTO 적용 — 고정 100ms → `max(100ms, SRTT + 4×RTTVAR)` RFC 6298 EWMA, `timestampEcho` 기반 RTT 샘플링, `UpdateRtt()` 메인 스레드 전용
- [x] (2026-04-28 #3) `UdpClient` → `Socket` 전환 — 수신 버퍼 사전 할당(`_recvBuf[1500]`), per-packet GC 할당 제거, `ProcessReceivedPacket(byte[], int)` 시그니처 변경
- [x] (2026-04-28 #4) 송신 큐 + `Poll(1ms)` 루프 도입 — `ConcurrentQueue` 기반 송신 큐, 워커 스레드가 수신·송신 모두 담당, `Disconnect()` 순서 Join→Close로 변경

### 기타
- [x] (2026-04-26 #0) TestLobbyScene을 LobbyScene으로 변경
- [x] (2026-04-26 #1) `LobbySettingUI` 완성 — 탭 클릭·hover 색상 전환, Apply/Cancel → `LobbyReconfirmUI` 재확인, `Show()`/`Hide()`, `LobbyScene` wrapper(`ShowSettingUI`, `ActiveReconfirmConfirmOrCancel`), `UI_Header` OPTION 버튼 연결

### 버그 수정

---

## 진행 중 / 미완성

### 매칭 성공시 씬 전환
/connect요청을 통해서 ip와 port를 받았을 경우
1. workerThread를 살려내고 루프 작동. (ping 작동)
    - workerThread내에서 ReliableFlag로 C2DHeartBeat전송, D2CHeartBeat로 응답 받음.
2. Scene을 LoadingScene으로 변경하고, GameScene의 비동기 로딩 시작.
3. 비동기 로딩 완료되었을 경우, GameScene의 현재 정적인 내용을 요구하는 패킷 전송.
    - C2DRequestBluePrint 전송
4. 3의 패킷의 응답을 받았을 경우, 해당 내용을 역직렬화해서 보관하고 Scene교체 진행.
    - D2CResponseBlueprint, 여기서 Spawn위치 결정됨.
5. 교체된 Scene의 Init() 함수에서 C2DRequestBluePrint에서 받아온 친구들 까지 포함해서 그려냄
6. Init함수가 실행된 이후, 서버에 Scene 로딩 완료됬음을 알려줌과 동시에 동적인 정보를 다시 요청.
    - C2DRequestSpawnMe

---

## 다음 작업 우선순위 (제안)

1. **매칭 성공 씬 전환** — `OnMatchingSuccess()` → LoadingScene 로드
2. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
