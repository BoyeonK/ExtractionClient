# 프로젝트 진행 상황

> 최종 수정: 2026-04-20
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### UI
- [x] (2026-04-16) 인벤토리/창고 데이터 소유권 TestLobbyScene으로 이전 — UI는 뷰 역할만, SyncSlot 제거
- [x] (2026-04-16) `FirstEmptySlot` / `HasEmptySlot` — UI_Inventory, UI_Warehouse에 구현
- [x] (2026-04-16) Shift+클릭 수량 분할 — 인벤토리/창고 모두 지원
- [x] (2026-04-17) `UI_Shop.cs` 최소 구현 — `UI_Scene` 상속, `RequestPurchase(itemId, quantity)` 진입점 제공

### 네트워크
- [x] (2026-04-17) `http-api-spec.yaml` 변경사항 C# 동기화 — `ErrorDetail` 클래스 추가, `BaseResponse.message` → `error`, `InventoryItem`에 `slot_index` 추가, `AuthData`에 `money` 추가
- [x] (2026-04-17) `TestLobbyScene` 인벤토리 로딩 로직 — `slot_index` 전역 범위 기반 배치 (0~79=창고, 80~104=인벤토리, 105~107=로드아웃)
- [x] (2026-04-17) `PurchaseRequest` / `PurchaseData` / `PurchaseResponse` 데이터 클래스 추가
- [x] (2026-04-17) `HTTPManager.PostPurchaseCall()` 구현 — 인벤토리 스냅샷 전송, Money/Inventory 갱신. `Money` 프로퍼티 추가 및 로그인·로그아웃 동기화
- [x] (2026-04-17) `TestLobbyScene` 상점 상태 처리 완성 — `ShowShop()`, `TryPurchase()`, `OnPurchaseComplete()`, 창고 우선 빈 슬롯 탐색(`FindEmptyPurchaseSlotIndex`), 스냅샷 조립(`BuildInventorySnapshot`)
- [x] (2026-04-20) `http-api-spec.yaml` `AuthData.shopItems` 동기화 — `AuthData`에 `int[] shopItems` 추가, `HTTPManager.ShopItems` 프로퍼티 추가, 로그인·회원가입 수신 시 저장, 로그아웃 시 초기화

---

## 진행 중 / 미완성

### 로비 (Lobby 상태)
- [ ] 상점 UI 본격 구현 — `Managers.Network.http.ShopItems`로 판매 목록 수신 완료, 아이템 목록 표시·구매 수량 입력·잔액 표시 구현 필요 (UI_Shop 스크립트는 진입점만 존재)

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
