# 프로젝트 진행 상황

> 최종 수정: 2026-04-26
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-24 #8) `UI_MatchProgress` 표시/숨김 연결 — Matching 진입 시 ShowSceneUI, 로그아웃 완료 시 DisableUI
- [x] (2026-04-24 #9) `UI_MatchProgress` 타이머·취소 버튼 기능 완성 — OnEnable 초기화, Update MM:SS 표시, 취소 클릭 시 `LobbyReconfirmUI` 재확인 후 `TryCancelMatch` 호출
- [x] (2026-04-25 #0) Lobby 화면에 더미 캐릭터 세워놓기 (애니메이션 적용)
- [x] (2026-04-25 #1) 로그아웃 성공 시 LobbyReconfirmUI 확인 팝업 — `TryLogout` 성공 시 `ActiveOnlyConfirm` → 확인 클릭 후 `OnLogoutComplete` 실행
- [x] (2026-04-25 #2) 버전 확인 실패 시 LobbyReconfirmUI 알림 팝업 — `GetVersionCall` 실패 시 `OnConnectedFailed` 즉시 실행 후 `ActiveOnlyConfirm` 알림 표시

### 네트워크
- [x] (2026-04-24 #7) Matching 상태에서 로그아웃 버튼 클릭 시 매치 취소 유도 — `LogoutPopup()` Matching 분기 추가, `TryCancelMatchThenLogout()` 구현 (취소 성공 시 TryLogout 연계)
- [x] (2026-04-24 #10) 매칭 상태 폴링 — `HTTPManager.StartMatchPolling()` 구현, `IsMatching`을 source of truth로 루프 제어, 성공 시 메인 스레드 콜백 실행
- [x] (2026-04-24 #11) Matching 상태 중 옵션·로그아웃 버튼 외 모든 UI 접근 차단 — `EnterMatchingState()`에서 전체 비활성화, 각 Show 메서드에 Lobby 상태 가드 존재, ESC 무동작

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
- [ ] RUDP 패킷 헤더 완성 및 ACK bitfield를 이용한 재전송 로직 완성하기

---

## 다음 작업 우선순위 (제안)

1. **매칭 성공 씬 전환** — `OnMatchingSuccess()` → LoadingScene 로드
2. **설정 UI 콘텐츠 채우기** — General / Graphic / Audio 탭 실제 항목 구현
