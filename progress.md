# 프로젝트 진행 상황

> 최종 수정: 2026-04-23
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-23 00) `UI_MapSelect` 외형 디자인
- [x] (2026-04-23 00) `UI_MapSelect` 컴포넌트 추가 및 하위 컴포넌트 연결
- [x] (2026-04-23 00) 상점탭 필터 기능 추가
- [x] (2026-04-23 05) 버튼 마우스 호버 색 변화 적용 — UI_Auth, UI_Login, UI_MapSelect, UI_Register
- [x] (2026-04-23 05) `UI_MapSelect` 맵 브라우징 기능 추가 — 임시 스프라이트 추가, 좌우 버튼 순환 로직
- [x] (2026-04-23 23) `ItemTypeHelper` `_nameMap` / `_descriptionMap` 실제 데이터로 채우기 — 아이템 7종(무기 3, 장비 1, 탄약 2, 기타 1) 이름·설명 등록, range 기반 판별 → Dictionary 방식으로 전환

### 네트워크
- [x] (2026-04-21 00) HTTP 요청 전역 중복 차단 — `_tryingVersionCall` / `_tryingAuthCall` 제거, `_isRequesting` 단일 플래그로 통합. 미보호였던 `GetInventoryCall`, `PostPurchaseCall`, `StartMatchCall`, `CancelMatchCall`, `CheckMatchStatusCall` 5개 메서드에 try-finally 가드 추가
- [x] (2026-04-23 00) `/api/game/match/start` 전송 로직 수정 — `EquippedItem` 방식 제거, `InventoryItem[]` 전체 스냅샷 방식으로 변경. `PackedItems` 시스템 삭제, `TryMatchMake(int mapId, string loadoutType)` 구현 및 UI 연결

### 버그 수정
- [x] (2026-04-21 00) Enter 키 오등록 버그 수정 — `AddKeyListener(Key.Escape, OnEnterInput, ...)` → `Key.Enter`로 수정
- [x] (2026-04-23 00) BeforeAuth 상태에서 ESC 누를 시 혼란스런 로그 수정

---

## 진행 중 / 미완성

### 로비 (BeforeAuth 상태)

### 로비 (Lobby 상태)
- [ ] UI_MapSelect - 게스트 계정의 경우, 로드아웃 타입 결정하지 않음. 무료 로드아웃으로 즉시 매치메이킹 시도
- [ ] UI_Shop의 상단 탭의 버튼에 마우스가 올려질 경우 색 변화 적용 (현재 아예 버튼 컴포넌트가 아니라 Image임)
- [ ] 창고 상단의 탭 가려놓기 - 남겨놓되, y를 0으로 하기. 추후에 소팅이 구현될 일이 있으면 연결 할 수 있도록.

### 로비 (Matching 상태)
- [ ] 매칭 상태 폴링 로직 구현 (CheckMatchStatusCall 주기 호출, WAITING → SUCCESS 감지)
- [ ] 매칭 성공 시 TryConnectCall → UDP 연결 흐름 구현
- [ ] 우측 하단에 UI를 띄움. 매칭이 진행 중이며, 취소할 수 있는 수단을 제공
- [ ] 옵션과 로그아웃 버튼을 제외한 다른 모든 UI의 접근을 차단
- [ ] 로그아웃 버튼을 이 상태에서 클릭 시, 매치 취소를 먼저 유도
- [ ] 해당 상태에서 매치 취소 및 매칭 상태 확인 요청을 제외한 다른 요청을 차단

### 기타
- [ ] 로그아웃 안내 팝업 내용 한글로 적기
- [ ] 로그아웃 성공, 버전 체크 실패의 경우, LobbyReconfirmUI 활용하여 안내 팝업 띄우기
- [ ] LobbyReconfirmUI의 버튼에 마우스가 올려질 경우의 색 변화 적용
- [ ] 설정 UI 만들기

---

## 다음 작업 우선순위 (제안)

1. **매칭 폴링 및 연결 흐름** — CheckMatchStatusCall 루프 → TryConnectCall → UDP 연결
2. **Matching 상태 UI** — 우측 하단 매칭 중 표시 및 취소 버튼
3. **창고 탭 숨기기** — 상단 탭 y=0 처리
4. **버전 체크 실패 팝업** — `resData.data` 실패 시 팝업 띄우기
