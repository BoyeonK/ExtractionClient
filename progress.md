# 프로젝트 진행 상황

> 최종 수정: 2026-04-21
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-21) `UI_Shop` Detail Panel 빈 상태 처리 — `ResetDetailPanel()`에 `else` 분기 추가, `_selectedItemId == -1`일 때 이미지 숨김·텍스트 빈칸·+/- 버튼 비활성. `FilterBy()` 말미 `Refresh()` 호출 제거로 무한 재귀 방지
- [x] (2026-04-21) `LobbyReconfirmUI` 리팩토링 — `isActive` 플래그로 중복 팝업 방지, `LobbyConfirmOrCancel` + `lobbyOnlyConfirm` 하위 컴포넌트 관리, 프리팹 이름 단수화
- [x] (2026-04-21) 로그인/회원가입 실패 팝업 연결 — `TestLobbyScene`에서 `_lobbyReconfirmUI.ActiveOnlyConfirm()` 호출
- [x] (2026-04-21) `UI_Login` / `UI_Register` 기본 구조 완성 — 닫기 버튼 / Reload 구현, `UI_Register` 이메일 InputField 제거 및 `ConfirmPasswordInputField` 추가
- [x] (2026-04-21) Tab 키 포커스 순환 구현 — `UI_Login`: id ↔ password, `UI_Register`: id → password → confirmPassword → id
- [x] (2026-04-21) Enter 키로 로그인 시도 구현 — `OnEnterInput()` → `BeforeAuthEnter()` → `_loginUI.OnEnterBtnPressOn()` 연결
- [x] (2026-04-21) `UI_Register` 로그인 화면 이동 버튼 연결 — `BackToLoginButton` 바인딩 → `OnClickCloseBtn()` 호출로 Auth 화면으로 복귀

### 네트워크
- [x] (2026-04-21) HTTP 요청 전역 중복 차단 — `_tryingVersionCall` / `_tryingAuthCall` 제거, `_isRequesting` 단일 플래그로 통합. 미보호였던 `GetInventoryCall`, `PostPurchaseCall`, `StartMatchCall`, `CancelMatchCall`, `CheckMatchStatusCall` 5개 메서드에 try-finally 가드 추가

### 버그 수정
- [x] (2026-04-21) Enter 키 오등록 버그 수정 — `AddKeyListener(Key.Escape, OnEnterInput, ...)` → `Key.Enter`로 수정
- [x] (2026-04-21) `BackToBeforeConnectPopup` 오복붙 로그 및 플레이스홀더 수정 — `TestLobbyScene.cs:108` 로그 문자열 정정, `"asdfasdf"` 팝업 문구를 실제 안내 문구로 교체

---

## 진행 중 / 미완성

### 로비 (Lobby 상태)
- [ ] 창고 상단의 탭 가려놓기 - 남겨놓되, y를 0으로 하기. 추후에 소팅이 구현될 일이 있으면 연결 할 수 있도록.

### 매칭 (Matching 상태)
- [ ] `TryMatchMake()`, `ShowMapSelect()` 내부 로직 비어 있음
- [ ] 매칭 폴링 루프 (`CheckMatchStatusCall`) TestLobbyScene에 연결 안 됨
- [ ] 매칭 취소 버튼 / 흐름 연결 안 됨
- [ ] 매칭 성공 후 씬 전환 로직 없음

### UDP / 인게임
- [ ] 에코 패킷 외 실제 게임 패킷 없음 — `PktId`에 정의된 것은 테스트 패킷 2개뿐
- [ ] `PlayerController`, `GameObjectController` 구현 미완성 (파일만 존재)
- [ ] 인게임 씬 없음

### 기타
- [ ] 버전 체크 응답(`resData.data`)에 실패한 경우 팝업창 띄우기

---

## 다음 작업 우선순위 (제안)

1. **맵 선택 및 매치메이킹 진행** — UI부터 처음부터 만들어야 함.
2. **창고 탭 숨기기** — 상단 탭 y=0 처리.
3. **버전 체크 실패 팝업** — `resData.data` 실패 시 팝업 띄우기.
