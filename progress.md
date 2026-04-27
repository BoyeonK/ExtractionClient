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
- [x] (2026-04-24 #10) 매칭 상태 폴링 — `HTTPManager.StartMatchPolling()` 구현, `IsMatching`을 source of truth로 루프 제어, 성공 시 메인 스레드 콜백 실행
- [x] (2026-04-24 #11) Matching 상태 중 옵션·로그아웃 버튼 외 모든 UI 접근 차단 — `EnterMatchingState()`에서 전체 비활성화, 각 Show 메서드에 Lobby 상태 가드 존재, ESC 무동작
- [x] (2026-04-28 #0) RUDP 전환 — 헤더 31B, reliable/unreliable 채널 분리, ACK bitfield 재전송 로직, 고정 RTO 100ms
- [x] (2026-04-28 #1) `_pendingReliable` 최적화 — Dictionary → 32슬롯 링 버퍼, byte[1400] 사전 할당, `BuildPacketInto(Span<byte>)` 직접 쓰기, `CollectRetransmits` 반환 List 재사용
- [x] (2026-04-28 #2) 적응형 RTO 적용 — 고정 100ms → `max(100ms, SRTT + 4×RTTVAR)` RFC 6298 EWMA, `timestampEcho` 기반 RTT 샘플링, `UpdateRtt()` 메인 스레드 전용

### 기타
- [x] (2026-04-26 #0) TestLobbyScene을 LobbyScene으로 변경
- [x] (2026-04-26 #1) `LobbySettingUI` 완성 — 탭 클릭·hover 색상 전환, Apply/Cancel → `LobbyReconfirmUI` 재확인, `Show()`/`Hide()`, `LobbyScene` wrapper(`ShowSettingUI`, `ActiveReconfirmConfirmOrCancel`), `UI_Header` OPTION 버튼 연결

### 버그 수정

---

## 진행 중 / 미완성

### 로비 (Matching 상태)
- [ ] 매칭 성공 시 게임 씬 로드 — `OnMatchingSuccess()`에서 씬 전환 로직 구현 (`TryConnectCall`은 `CheckMatchStatusCall` 내부에서 이미 호출됨)

### 매칭 성공시 씬 전환
- [ ] LoadingScene 만들고 SceneManagerEx와 연동하기
- [ ] 테스트용 GameScene 만들기
- [ ] /status가 SUCCESS 반환 시 LoadingScene으로 전환하고, 동시에 비동기적으로 해당 Map의 Scene을 로딩

---

## 다음 작업 우선순위 (제안)

1. **매칭 성공 씬 전환** — `OnMatchingSuccess()` → LoadingScene 로드
2. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
