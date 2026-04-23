# 프로젝트 진행 상황

> 최종 수정: 2026-04-24
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-24 #1) 창고 상단 탭 가리기 — y=0 처리, 소팅 구현 시 연결할 수 있도록 오브젝트 유지
- [x] (2026-04-24 #2) 로그아웃 안내 팝업 내용 한글화
- [x] (2026-04-24 #3) `LobbyReconfirmUI` 버튼 호버 색 변화 적용
- [x] (2026-04-24 #4) `UI_MapSelect` 게스트 계정 즉시 FreeLoadout 매치메이킹 — Button_StartMatch 클릭 시 게스트이면 오버레이 생략하고 바로 `TryMatchMake` 호출
- [x] (2026-04-24 #8) `UI_MatchProgress` 표시/숨김 연결 — Matching 진입 시 ShowSceneUI, 로그아웃 완료 시 DisableUI
- [x] (2026-04-24 #9) `UI_MatchProgress` 타이머·취소 버튼 기능 완성 — OnEnable 초기화, Update MM:SS 표시, 취소 클릭 시 `LobbyReconfirmUI` 재확인 후 `TryCancelMatch` 호출

### 네트워크
- [x] (2026-04-24 #5) `HTTPManager` `IsMatching` 플래그 추가 — 매칭 중 `CancelMatchCall` / `CheckMatchStatusCall` 이외 모든 요청 차단. StartMatchCall 성공/실패, 취소, 티켓 만료, TryConnectCall 종료 시 해제
- [x] (2026-04-24 #6) 매치 성공 시 Matching 상태 진입 및 Lobby UI 비활성화 — `EnterMatchingState()` 추가, MapSelect / Inventory / Warehouse / Shop 비활성화
- [x] (2026-04-24 #7) Matching 상태에서 로그아웃 버튼 클릭 시 매치 취소 유도 — `LogoutPopup()` Matching 분기 추가, `TryCancelMatchThenLogout()` 구현 (취소 성공 시 TryLogout 연계)
- [x] (2026-04-24 #10) 매칭 상태 폴링 — `HTTPManager.StartMatchPolling()` 구현, `IsMatching`을 source of truth로 루프 제어, 성공 시 메인 스레드 콜백 실행
- [x] (2026-04-24 #11) Matching 상태 중 옵션·로그아웃 버튼 외 모든 UI 접근 차단 — `EnterMatchingState()`에서 전체 비활성화, 각 Show 메서드에 Lobby 상태 가드 존재, ESC 무동작

### 버그 수정

---

## 진행 중 / 미완성

### 로비 (Matching 상태)
- [ ] 매칭 성공 시 게임 씬 로드 — `OnMatchingSuccess()`에서 씬 전환 로직 구현 (`TryConnectCall`은 `CheckMatchStatusCall` 내부에서 이미 호출됨)

### 기타
- [ ] 로그아웃 성공, 버전 체크 실패의 경우, LobbyReconfirmUI 활용하여 안내 팝업 띄우기
- [ ] 설정 UI 만들기

---

## 다음 작업 우선순위 (제안)

1. **매칭 성공 씬 전환** — `OnMatchingSuccess()` → 게임 씬 로드
2. **버전 체크 실패 팝업** — 로그아웃 성공/버전 체크 실패 시 LobbyReconfirmUI 안내 팝업 띄우기
