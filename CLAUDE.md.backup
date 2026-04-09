# Extraction - Unity Project

## 프로젝트 개요

- **프로젝트명**: Extraction
- **엔진**: Unity 6000.4.0f1
- **렌더 파이프라인**: URP (Universal Render Pipeline) 17.4.0
- **장르**: 멀티플레이어 Extraction 게임 (알파 테스트 단계)
- **서버 통신**: HTTP REST API + UDP (실시간 인게임)

---

## 디렉토리 구조

```
Assets/
├── Scripts/
│   ├── Controller/
│   │   ├── GameObjectControllers/   # GameObject 제어 (PlayerController 등)
│   │   └── LoadingScene1/           # 로딩 씬 UI 컨트롤러
│   ├── Managers/                    # 싱글톤 매니저 시스템
│   ├── Network/                     # HTTP / UDP 네트워크 레이어
│   ├── Scenes/                      # 씬별 진입점
│   ├── UI/
│   │   ├── Popup/                   # 팝업 UI (UI_Popup 상속)
│   │   ├── Scene/                   # 씬 UI (UI_Scene 상속)
│   │   ├── UI_Base.cs               # 모든 UI의 베이스 클래스
│   │   └── UI_EventHandler.cs
│   └── Utils/                       # Define, Util, PriorityQueue 등 유틸
├── Scenes/
│   ├── TestIngame.unity
│   ├── TestLobby.unity
│   └── WinchesterAlpha.unity
└── Resources/
    └── Prefabs/                     # ResourceManager가 로드하는 프리팹 경로
        ├── UI/Popup/
        └── UI/Scene/
```

---

## 매니저 시스템 (`Managers.cs`)

싱글톤 패턴. `@Managers` GameObject에 붙으며 `DontDestroyOnLoad`.

| 접근자 | 클래스 | 역할 |
|--------|--------|------|
| `Managers.Input` | `InputManager` | 키 입력 이벤트 등록/해제 |
| `Managers.Network` | `NetworkManager` | HTTP + UDP 네트워크 |
| `Managers.Pool` | `PoolManager` | 오브젝트 풀링 |
| `Managers.Resource` | `ResourceManager` | 프리팹 로드/인스턴스화/파괴 |
| `Managers.UI` | `UIManager` | UI 생성/표시/캐싱 |
| `Managers.Scene` | `SceneManagerEx` | 씬 전환 |
| `Managers.Sound` | `SoundManager` | BGM/효과음 |
| `Managers.Setting` | `SettingManager` | 게임 설정 |

**비동기 → 메인스레드 실행**: `Managers.ExecuteAtMainThread(Action)` 사용.
네트워크 콜백 등 백그라운드 스레드에서 Unity API 호출 시 반드시 이 메서드로 감싼다.

---

## 네트워크 구조

### HTTP (`HTTPManager`)
- `BaseAddress`: `Gitignores.baseUrl`
- `Gitignores.cs`는 git에서 제외된 파일 — URL 등 민감 설정을 관리함
- `HttpClient` 싱글톤 사용, 타임아웃 5초
- 인증이 필요한 요청은 `x-session-id` 헤더에 세션 ID를 포함

**주요 API 엔드포인트**:
| 기능 | 메서드 | URL |
|------|--------|-----|
| 버전 확인 | GET | `api/version` |
| 회원가입 | POST | `api/signup` |
| 로그인 | POST | `api/login` |
| 게스트 로그인 | POST | `api/guest` |
| 로그아웃 | POST | `api/logout` |
| 매칭 시작 | POST | `api/game/match/start` |
| 매칭 상태 확인 | GET | `api/game/match/status` |
| 매칭 취소 | POST | `api/game/match/cancel` |
| 인게임 서버 연결 | POST | `api/game/match/connect` |

**매칭 흐름**: `StartMatchCall` → 폴링(`CheckMatchStatusCall`) → `WAITING` / `SUCCESS` → `TryConnectCall` → UDP 연결 시작

### UDP (`UDPManager`)
- 전용 수신 스레드(`UDP_Receive_Thread`) 사용
- `UdpClient.Connect()`로 목적지 고정 후 송수신
- `Disconnect()` 시 `_udpClient.Close()` → 수신 루프의 `SocketException`으로 안전하게 종료

### 패킷 (`PacketHandler`)
- **헤더 구조체** (`UDPHeader`, 13바이트, `LayoutKind.Sequential Pack=1`):
  `packetId(2) | sessionId(2) | sequenceNum(4) | securityKey(4) | flags(1)`
- **직렬화**: Google.Protobuf (`GameProtocol` 네임스페이스)
- 핸들러 등록: `_handlers.Add((ushort)PktId.XXX, Handle_XXX)`
- Zero-Allocation 패킷 조립: `MemoryMarshal.Write/Read` + `Span<byte>` 사용

---

## UI 시스템

- **`UI_Base`**: 모든 UI 베이스. `BindComponent<T>(path)`로 자식 컴포넌트 참조
- **`UI_Scene`**: 씬 단위 UI. `Managers.UI.ShowSceneUI<T>()` 로 표시
- **`UI_Popup`**: 팝업 UI. `Managers.UI.ShowPopupUI<T>()` 로 표시
- UI 프리팹 경로: `Resources/Prefabs/UI/Scene/` 또는 `Resources/Prefabs/UI/Popup/`
- `@UI_Root` GameObject 하위에 모든 UI가 배치됨 (`DontDestroyOnLoad`)
- **정렬 순서**: SceneUI는 0부터 증가, PopupUI는 20부터 증가, 긴급 팝업은 +1000

---

## 입력 시스템

- **Input System** 패키지 사용 (`com.unity.inputsystem 1.19.0`)
- `InputManager`에 `AddKeyListener(Key, Action, KeyState)` 로 등록
- `OnDestroy`에서 반드시 `RemoveKeyListener`로 해제
- `KeyState`: `Down` / `Up`

---

## 씬 구성

- **`BaseScene`**: 모든 씬의 베이스. `Awake → Init()`에서 EventSystem 자동 생성
- **`TestLobbyScene`**: 로비 씬 진입점
- 씬 전환: `Managers.Scene` 사용

---

## 핵심 규칙 / 컨벤션

1. **Unity API는 반드시 메인 스레드**에서 호출. 비동기 콜백에서는 `Managers.ExecuteAtMainThread(() => { ... })` 사용
2. **프리팹은 `Resources/Prefabs/` 하위에 위치** — `ResourceManager.Instantiate()`의 경로는 이 폴더 기준 상대경로
3. **UI 표시/숨김은 Destroy 대신 SetActive** — UIManager가 캐시하므로 `DisableUI` / `EnableUI` 활용
4. **패킷 추가 시**: `PacketHandler` 생성자에 `_handlers.Add` 등록 + `Handle_XXX` 메서드 구현
5. **서버 URL**: 절대 하드코딩 금지 — `Gitignores.baseUrl` 사용

---

## 주요 패키지

| 패키지 | 버전 | 용도 |
|--------|------|------|
| `com.unity.inputsystem` | 1.19.0 | 입력 처리 |
| `com.unity.render-pipelines.universal` | 17.4.0 | URP 렌더링 |
| `com.unity.ai.navigation` | 2.0.11 | AI 내비게이션 |
| `com.unity.ugui` | 2.0.0 | UI Toolkit / UGUI |
| Google.Protobuf | - | UDP 패킷 직렬화 |
