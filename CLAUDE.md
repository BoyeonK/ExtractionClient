# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

- **엔진**: Unity 6000.4.0f1, URP 17.4.0
- **장르**: 멀티플레이어 Extraction 게임 (알파 단계)
- **서버 통신**: HTTP REST API (로비/인증) + UDP (실시간 인게임)
- **플랫폼**: Windows Standalone x64

---

## 매니저 시스템 (`Managers.cs`)

싱글톤 허브. `@Managers` GameObject에 부착되며 `DontDestroyOnLoad`. 모든 서브 매니저는 정적 프로퍼티로 접근:

| 접근자 | 클래스 | 역할 |
|--------|--------|------|
| `Managers.Input` | `InputManager` | 키 입력 이벤트 등록/해제 |
| `Managers.Network` | `NetworkManager` | HTTP + UDP 네트워크 오케스트레이션 |
| `Managers.Pool` | `PoolManager` | 오브젝트 풀링 |
| `Managers.Resource` | `ResourceManager` | 프리팹 로드/인스턴스화/파괴 |
| `Managers.UI` | `UIManager` | UI 생성, 표시, 캐싱 |
| `Managers.Scene` | `SceneManagerEx` | 씬 전환 |
| `Managers.Sound` | `SoundManager` | BGM / 효과음 |
| `Managers.Setting` | `SettingManager` | 게임 설정 저장 |

**백그라운드 → 메인 스레드 브릿지**: 비동기 콜백이나 UDP 수신 스레드에서 Unity API를 호출할 때는 반드시 `Managers.ExecuteAtMainThread(() => { ... })`로 감싼다. 매니저의 `Update()`가 매 프레임 lock으로 보호된 `_jobQueue`를 소진한다.

---

## 네트워크 구조

### HTTP (`HTTPManager`)
- 정적 `HttpClient`, 타임아웃 5초, 베이스 URL은 `Gitignores.baseUrl`에서 가져옴
- 인증이 필요한 요청은 `x-session-id` 헤더 포함
- `Gitignores.cs`는 git에서 제외된 파일 — 서버 URL 등 민감 설정 관리. **베이스 URL 하드코딩 금지**

주요 엔드포인트: `GET api/version`, `POST api/signup`, `POST api/login`, `POST api/guest`, `POST api/logout`, `POST api/game/match/start`, `GET api/game/match/status`, `POST api/game/match/cancel`, `POST api/game/match/connect`

매칭 흐름: `StartMatchCall` → `CheckMatchStatusCall` 폴링 (WAITING → SUCCESS) → `TryConnectCall` → UDP 연결 시작

### UDP (`UDPManager`)
- 전용 백그라운드 수신 스레드(`UDP_Receive_Thread`) 사용
- `UdpClient.Connect()`로 목적지 고정. `Disconnect()` 시 `_udpClient.Close()` → 수신 루프의 `SocketException`으로 안전하게 종료
- 수신 데이터는 모두 `Managers.ExecuteAtMainThread`를 통해 메인 스레드에서 처리

### 패킷 형식 (`PacketHandler`)
- **헤더** (`UDPHeader`, 13바이트, `LayoutKind.Sequential Pack=1`): `packetId(2) | sessionId(2) | sequenceNum(4) | securityKey(4) | flags(1)`
- 직렬화: Google.Protobuf (`GameProtocol` 네임스페이스, `ExternalProtocol.cs`에 정의)
- 핸들러 등록: `PacketHandler` 생성자에서 `_handlers.Add((ushort)PktId.XXX, Handle_XXX)`
- Zero-Allocation 패킷 조립: `MemoryMarshal.Write/Read` + `Span<byte>` 사용

---

## UI 시스템

```
UI_Base (abstract)
├── UI_Scene    → Managers.UI.ShowSceneUI<T>() 로 표시
│   ├── UI_TestStart, UI_Auth, UI_Login, UI_Register
│   ├── UI_Header, UI_Inventory, UI_Warehouse
└── UI_Popup    → Managers.UI.ShowPopupUI<T>() 로 표시
    ├── UI_OnlyConfirm, UI_ConfirmOrCancel
```

- 프리팹 경로: `Resources/Prefabs/UI/Scene/` 또는 `Resources/Prefabs/UI/Popup/`
- 모든 UI는 `@UI_Root` 하위에 배치됨 (`DontDestroyOnLoad`)
- **UI는 Destroy 금지 — SetActive 사용**. UIManager가 인스턴스를 캐시하므로 `DisableUI` / `EnableUI` 활용
- 정렬 순서: SceneUI는 0부터 증가, PopupUI는 20부터 증가, 긴급 팝업은 +1000
- `UI_Base.BindComponent<T>(path)`: 경로로 자식 컴포넌트를 찾아 반환 (없으면 예외 발생)

---

## 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`TestLobbyScene`**: 로비 씬 진입점 — `LobbyState` enum 기반 상태 머신 (BeforeConnect → BeforeAuth → Lobby → Matching). 모든 인증/매칭 흐름 및 ESC 키 동작을 담당
- 씬 전환: 반드시 `Managers.Scene` 사용

---

## 입력 시스템

- `com.unity.inputsystem` 1.19.0 패키지 사용 (구형 `Input` 클래스 사용 금지)
- 등록: `Managers.Input.AddKeyListener(Key, Action, KeyState)`
- `OnDestroy`에서 반드시 `RemoveKeyListener`로 해제
- `KeyState`: `Down` / `Up`

---

## 핵심 규칙 / 컨벤션

1. **Unity API는 반드시 메인 스레드**에서 호출 — 비동기/UDP 콜백에서는 `Managers.ExecuteAtMainThread` 사용
2. **프리팹은 `Resources/Prefabs/` 하위에 위치** — `ResourceManager` 경로는 이 폴더 기준 상대경로
3. **서버 URL은 `Gitignores.baseUrl`에서** — 하드코딩 절대 금지
4. **새 UDP 패킷 타입 추가 시**: `ExternalProtocol.cs`에 `PktId` 항목 추가 → Protobuf 메시지 정의 → `PacketHandler` 생성자에 핸들러 등록 → `Handle_XXX` 메서드 구현

---

## 주요 패키지

| 패키지 | 버전 | 용도 |
|--------|------|------|
| `com.unity.inputsystem` | 1.19.0 | 입력 처리 |
| `com.unity.render-pipelines.universal` | 17.4.0 | URP 렌더링 |
| `com.unity.ai.navigation` | 2.0.11 | AI 내비게이션 |
| `com.unity.ugui` | 2.0.0 | UGUI |
| Google.Protobuf | — | UDP 패킷 직렬화 |


## 절대 규칙
- Library/ 폴더는 절대 읽지 말 것. 참조가 필요하면 나에게 먼저 물어볼 것
- .gitignore 파일은 읽기만 가능, 절대 수정하지 말 것
- Asset/Scripts/Utils/Gitignores.cs는 절대 읽거나 문서화하지 말 것
