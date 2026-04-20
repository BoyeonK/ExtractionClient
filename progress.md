# 프로젝트 진행 상황

> 최종 수정: 2026-04-21
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-21) 한글 폰트 Atlas 3종 생성 및 전체 UI 적용 — 모든 프리팹(UI_Header, UI_Shop, UI_Login, UI_Register, UI_Auth, UI_Inventory, UI_Warehouse, UI_TestStart, Popup 2종, 슬롯 프리팹 등)에 한글 폰트 적용 완료
- [x] (2026-04-20) Weapon/Equipment 구매 수량 고정 — 선택 시 수량 1 고정, +/- 버튼 `SetActive(false)` (`UI_Shop.ResetDetailPanel()`)
- [x] (2026-04-20) 구매 후 UI 즉시 갱신 — `OnPurchaseComplete()`에 `_shopUI.Refresh()` / `_warehouseUI.Refresh()` / `_inventoryUI.Refresh()` 추가
- [x] (2026-04-21) `LobbyReconfirmUI` 리팩토링 — `isActive` 플래그로 중복 팝업 방지, `LobbyConfirmOrCancel` + `lobbyOnlyConfirm` 하위 컴포넌트 관리, 프리팹 이름 단수화
- [x] (2026-04-21) 로그인/회원가입 실패 팝업 연결 — `TestLobbyScene`에서 `_lobbyReconfirmUI.ActiveOnlyConfirm()` 호출
- [x] (2026-04-21) `UI_Login` / `UI_Register` 기본 구조 완성 — 닫기 버튼 / Reload 구현, `UI_Register` 이메일 InputField 제거 및 `ConfirmPasswordInputField` 추가
- [x] (2026-04-21) Tab 키 포커스 순환 구현 — `UI_Login`: id ↔ password, `UI_Register`: id → password → confirmPassword → id
- [x] (2026-04-21) Enter 키로 로그인 시도 구현 — `OnEnterInput()` → `BeforeAuthEnter()` → `_loginUI.OnEnterBtnPressOn()` 연결
- [x] (2026-04-21) `UI_Register` 로그인 화면 이동 버튼 연결 — `BackToLoginButton` 바인딩 → `OnClickCloseBtn()` 호출로 Auth 화면으로 복귀

### 버그 수정
- [x] (2026-04-20) `BuildInventorySnapshot()` slot_index 불일치 버그 수정 — 스냅샷 조립 시 배열 인덱스로 `slot_index`를 직접 계산해 덮어씀
- [x] (2026-04-21) Enter 키 오등록 버그 수정 — `AddKeyListener(Key.Escape, OnEnterInput, ...)` → `Key.Enter`로 수정

---

## 진행 중 / 미완성

### 로비 (Lobby 상태)
- [ ] 상점 상단의 탭 필터링 미구현 — `SelectedTab` enum 및 필드 존재하나 탭 버튼 이벤트 미연결, 슬롯 필터링 로직 없음
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

1. **상점 내용 필터링** — 서버로부터 가져온 상점 아이템 리스트와 별개로 scene에서 표시할 리스트를 따로 관리.
2. **맵 선택 및 매치메이킹 진행** — UI부터 처음부터 만들어야 함.
