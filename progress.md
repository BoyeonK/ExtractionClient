# 프로젝트 진행 상황

> 최종 수정: 2026-04-22
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-21 00) `LobbyReconfirmUI` 리팩토링 — `isActive` 플래그로 중복 팝업 방지, `LobbyConfirmOrCancel` + `lobbyOnlyConfirm` 하위 컴포넌트 관리, 프리팹 이름 단수화
- [x] (2026-04-21 00) 로그인/회원가입 실패 팝업 연결 — `TestLobbyScene`에서 `_lobbyReconfirmUI.ActiveOnlyConfirm()` 호출
- [x] (2026-04-21 00) `UI_Login` / `UI_Register` 기본 구조 완성 — 닫기 버튼 / Reload 구현, `UI_Register` 이메일 InputField 제거 및 `ConfirmPasswordInputField` 추가
- [x] (2026-04-21 00) Tab 키 포커스 순환 구현 — `UI_Login`: id ↔ password, `UI_Register`: id → password → confirmPassword → id
- [x] (2026-04-21 00) Enter 키로 로그인 시도 구현 — `OnEnterInput()` → `BeforeAuthEnter()` → `_loginUI.OnEnterBtnPressOn()` 연결
- [x] (2026-04-21 00) `UI_Register` 로그인 화면 이동 버튼 연결 — `BackToLoginButton` 바인딩 → `OnClickCloseBtn()` 호출로 Auth 화면으로 복귀
- [x] (2026-04-22 06) `UI_MapSelect` 상태 연결 — Lobby/Main 진입(OnLoginComplete·BackToLobbyMain)에서 ShowSceneUI, 이탈(ShowInventory·ShowShop·OnLogoutComplete)에서 SetNormalState() 후 DisableUI, ShowMapSelect() 삭제

### 네트워크
- [x] (2026-04-21 00) HTTP 요청 전역 중복 차단 — `_tryingVersionCall` / `_tryingAuthCall` 제거, `_isRequesting` 단일 플래그로 통합. 미보호였던 `GetInventoryCall`, `PostPurchaseCall`, `StartMatchCall`, `CancelMatchCall`, `CheckMatchStatusCall` 5개 메서드에 try-finally 가드 추가

### 버그 수정
- [x] (2026-04-21 00) Enter 키 오등록 버그 수정 — `AddKeyListener(Key.Escape, OnEnterInput, ...)` → `Key.Enter`로 수정
- [x] (2026-04-21 00) `BackToBeforeConnectPopup` 오복붙 로그 및 플레이스홀더 수정 — `TestLobbyScene.cs:108` 로그 문자열 정정, `"asdfasdf"` 팝업 문구를 실제 안내 문구로 교체

---

## 진행 중 / 미완성

### 로비 (BeforeAute상태)
- [ ] UI의 버튼 위에 마우스가 올려질 경우 색 변화 적용

### 로비 (Lobby 상태)
- [ ] UI_MapSelect - 맵 브라우징 기능 (스프라이트·이름 표시, 좌우 버튼 순환 로직).
- [ ] UI_MapSelect - 매칭 시작 버튼 클릭 시 로드아웃 타입 결정 UI 활성화 및 매치메이킹 연결.
- [ ] UI_Shop의 DetailPanel에 채워넣을 item_description과 item_name맵 작성하기.
- [ ] UI_Shop의 상단 탭의 버튼에 마우스가 올려질 경우 색 변화 적용 (현재 아예 버튼 컴포넌트가 아니라 Image임)
- [ ] 창고 상단의 탭 가려놓기 - 남겨놓되, y를 0으로 하기. 추후에 소팅이 구현될 일이 있으면 연결 할 수 있도록.

### 기타
- [ ] 로그아웃 안내 팝업 내용 한글로 적기.
- [ ] 로그아웃 성공, 버전 체크 실패의 경우, LobbyReconfirmUI 활용하여 안내 팝업 띄우기
- [ ] LobbyReconfirmUI의 버튼에 마우스가 올려질 경우의 색 변화 적용
- [ ] 설정UI 만들기

---

## 다음 작업 우선순위 (제안)

1. **맵 선택 및 매치메이킹 진행** — UI부터 처음부터 만들어야 함.
2. **창고 탭 숨기기** — 상단 탭 y=0 처리.
3. **버전 체크 실패 팝업** — `resData.data` 실패 시 팝업 띄우기.
