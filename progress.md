# 프로젝트 진행 상황

> 최종 수정: 2026-04-16
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] `UI_Inventory`, `UI_Warehouse` — 슬롯 할당, 드래그 앤 드롭, 수량 표시, 데이터 바인딩
- [x] `ISlot` / `LSlot` — 슬롯 컴포넌트 계층. LSlot은 로드아웃 전용(타입 제약). Weapon/Equipment 수량 합산 전역 차단
- [x] `ItemTypeHelper` — item_id 범위 기반 아이템 타입 판별
- [x] 로드아웃 슬롯 (무기2 + 장비1) — UI_Inventory 내 별도 관리
- [x] 인벤토리/창고 데이터 소유권 TestLobbyScene으로 이전 — UI는 뷰 역할만, SyncSlot 제거
- [x] `FirstEmptySlot` / `HasEmptySlot` — UI_Inventory, UI_Warehouse에 구현
- [x] Shift+클릭 수량 분할 — 인벤토리/창고 모두 지원

### 씬 / 게임 흐름
- [x] `TestLobbyScene` — `LobbyState` 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching)
- [x] ESC 키 흐름 전 단계 처리 (종료 팝업, 뒤로가기, 로그인 화면 복귀)
- [x] 로그인 성공 / 실패 / 로그아웃 후 상태 전환

---

## 진행 중 / 미완성

### 로비 (Lobby 상태)
- [ ] `ShowLobby()`, `ShowInventory()`, `ShowShop()` 내부 로직 비어 있음
- [ ] `UserState` (Main / Inventory / Shop) 전환 UI 연결 안 됨
- [ ] 인벤토리 / 창고 탭 전환 버튼 연결 (Header → ShowInventory 호출 경로)
- [ ] 상점 UI 미구현

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
- [ ] 버전 체크 응답(`resData.data`)에 따른 분기 처리 미구현 (TODO 주석 있음)
- [ ] 인증 실패 시 Util.Log 대신 팝업 UI로 메시지 표시 (TODO 주석 있음)
- [ ] 사운드 / 설정 매니저 실제 기능 연결 여부 확인 필요

---

## 다음 작업 우선순위 (제안)

1. **상점 기능** — Header 버튼으로 상점 탭으로 전환, 임의의 아이템 구매 API 구현.
2. **매칭 흐름 완성** — 맵 선택 → 매칭 시작 → 폴링 → 성공/취소 전환
3. **인게임 씬 기초** — 씬 전환, 플레이어 오브젝트 스폰, 실제 게임 패킷 정의
4. **에러 UX 개선** — 인증/네트워크 실패 시 팝업 메시지 표시
