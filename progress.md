# 프로젝트 진행 상황

> 최종 수정: 2026-04-16
> 장르: 멀티플레이어 Extraction 게임 (알파 단계)
> 엔진: Unity 6000.4.0f1 / URP 17.4.0

---

## 완료된 것들

### 인프라 / 매니저
- [x] 싱글톤 매니저 허브 (`Managers.cs`) — Input, Network, Pool, Resource, UI, Scene, Sound, Setting
- [x] 메인 스레드 브릿지 (`ExecuteAtMainThread` + `_jobQueue`)
- [x] `ResourceManager`, `PoolManager`, `UIManager`, `SceneManagerEx`, `InputManager`

### 네트워크
- [x] HTTP 통신 기반 구조 (`HTTPManager`) — 정적 `HttpClient`, 5초 타임아웃, 세션 헤더
- [x] HTTP API 명세서 작성 (`http-api-spec.yaml`, OpenAPI 3.0)
- [x] 인증 전체 흐름 — 회원가입, 로그인, 게스트 로그인, 로그아웃 (서버 동작 확인)
- [x] 로그인/회원가입 응답에서 인벤토리 데이터 수신 및 저장 (`HTTPManager.Inventory`)
- [x] 인벤토리 조회 API 구현 — `GetInventoryCall` (`GET api/inventory`)
- [x] 매칭 API 구현 — `StartMatchCall`, `CheckMatchStatusCall` (폴링), `CancelMatchCall`, `TryConnectCall`
- [x] UDP 수신 스레드 + 안전한 종료 (`SocketException` 방식)
- [x] 패킷 헤더 구조체 (`UDPHeader`, 13바이트) + Protobuf 직렬화 (`ExternalProtocol.cs`)
- [x] UDP 에코 패킷 송수신 테스트 성공 (`C2DTestPkt` / `D2CTestPkt`)
- [x] 세션/보안키 변수를 `HTTPManager`에서 단일 관리

### UI
- [x] `UI_Base` → `UI_Scene` / `UI_Popup` 계층 구조
- [x] `UIManager` — ShowSceneUI, ShowPopupUI, CacheSceneUI, DisableUI, EnableUI (SetActive 방식, Destroy 없음)
- [x] `UI_TestStart` — 서버 연결 시도 화면
- [x] `UI_Auth` — 로그인 / 회원가입 / 게스트 선택 화면
- [x] `UI_Login` — 로그인 입력 화면
- [x] `UI_Register` — 회원가입 입력 화면
- [x] `UI_Header` — 로그인 상태 표시 헤더 (BeforeAuth / Logined / Guest)
- [x] `UI_OnlyConfirm`, `UI_ConfirmOrCancel` — 범용 팝업
- [x] `UI_Inventory`, `UI_Warehouse` — 슬롯 할당, 드래그 앤 드롭, 수량 표시, 데이터 바인딩
- [x] `ISlot` / `LSlot` — 슬롯 컴포넌트 계층. LSlot은 로드아웃 전용(타입 제약)
- [x] `ItemTypeHelper` — item_id 범위 기반 아이템 타입 판별
- [x] 로드아웃 슬롯 (무기2 + 장비1) — UI_Inventory 내 별도 관리, TestLobbyScene에서 배열로 동기화

### 씬 / 게임 흐름
- [x] `BaseScene` — EventSystem 자동 생성
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

1. **로비 UI 연결** — Header 버튼으로 인벤토리/창고 탭 전환, `UserState` 전환 구현
2. **인벤토리/창고 UI 연동** — 슬롯에 `HTTPManager.Inventory` 데이터 바인딩
3. **매칭 흐름 완성** — 맵 선택 → 매칭 시작 → 폴링 → 성공/취소 전환
4. **인게임 씬 기초** — 씬 전환, 플레이어 오브젝트 스폰, 실제 게임 패킷 정의
5. **에러 UX 개선** — 인증/네트워크 실패 시 팝업 메시지 표시
