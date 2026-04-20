# 프로젝트 진행 상황

> 최종 수정: 2026-04-20 (세션 3)
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-20) `UI_Shop.cs` 핵심 기능 완성 — `ResetDetailPanel()` 구현(아이콘 갱신), +/- 수량 조절(1~99), 버튼 이벤트 연결, `_myMoney` 보유금액 표시, 구매 버튼 guard 추가
- [x] (2026-04-20) Weapon/Equipment 구매 수량 고정 — 선택 시 수량 1 고정, +/- 버튼 `SetActive(false)` (`UI_Shop.ResetDetailPanel()`)
- [x] (2026-04-20) 구매 후 UI 즉시 갱신 — `OnPurchaseComplete()`에 `_shopUI.Refresh()` / `_warehouseUI.Refresh()` / `_inventoryUI.Refresh()` 추가

### 버그 수정
- [x] (2026-04-20) `BuildInventorySnapshot()` slot_index 불일치 버그 수정 — 스냅샷 조립 시 배열 인덱스로 `slot_index`를 직접 계산해 덮어씀. 구매 → Shift+클릭 분할 → 재구매 흐름에서 발생하던 서버 에러 해결

### 네트워크
- [x] (2026-04-17) `http-api-spec.yaml` 변경사항 C# 동기화 — `ErrorDetail` 클래스 추가, `BaseResponse.message` → `error`, `InventoryItem`에 `slot_index` 추가, `AuthData`에 `money` 추가
- [x] (2026-04-17) `TestLobbyScene` 인벤토리 로딩 로직 — `slot_index` 전역 범위 기반 배치 (0~79=창고, 80~104=인벤토리, 105~107=로드아웃)
- [x] (2026-04-17) `PurchaseRequest` / `PurchaseData` / `PurchaseResponse` 데이터 클래스 추가
- [x] (2026-04-17) `HTTPManager.PostPurchaseCall()` 구현 — 인벤토리 스냅샷 전송, Money/Inventory 갱신. `Money` 프로퍼티 추가 및 로그인·로그아웃 동기화
- [x] (2026-04-17) `TestLobbyScene` 상점 상태 처리 완성 — `ShowShop()`, `TryPurchase()`, `OnPurchaseComplete()`, 창고 우선 빈 슬롯 탐색, 스냅샷 조립
- [x] (2026-04-20) `AuthData.shopItems` 구조 변경 — `int[]` → `ShopItem[]` (`item_id`, `price`), `HTTPResponses.cs`에 `ShopItem` 클래스 추가, `HTTPManager.ShopItems` 타입 동기화

---

## 진행 중 / 미완성

### 로비 (Lobby 상태)
- [ ] 상점 탭 필터링 미구현 — `SelectedTab` enum 및 필드 존재하나 탭 버튼 이벤트 미연결, 슬롯 필터링 로직 없음

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

---

## 다음 작업 우선순위 (제안)

1. **상점 탭 필터링** — All/Weapon/Equipment/Ammo/Misc 탭 버튼 연결, 슬롯 필터링 로직
2. **매칭 흐름 완성** — 맵 선택 → 매칭 시작 → 폴링 → 성공/취소 전환
3. **인게임 씬 기초** — 씬 전환, 플레이어 오브젝트 스폰, 실제 게임 패킷 정의
4. **에러 UX 개선** — 인증/네트워크 실패 시 팝업 메시지 표시 (HTTPManager TODO 주석 다수)
