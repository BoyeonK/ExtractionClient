# Extraction

> 멀티플레이어 Extraction 슈터 — Unity 클라이언트 + 전용 게임 서버 (개인 프로젝트)

---

## 1. 프로젝트 개요

플레이어가 맵에 진입해 아이템을 확보하고, **살아서 귀환해야만 전리품을 지킬 수 있는** Extraction 장르의 멀티플레이어 슈터입니다.

죽으면 소지품을 사망 지점에 떨어뜨리고, 해당 아이템은 다른 플레이어가 획득할 수 있는 컨테이너로 남습니다.

| | |
|---|---|
| **장르** | 멀티플레이어 Extraction / FPS |
| **플랫폼** | Windows Standalone x64 |
| **개발 기간** | 2026.02 ~ 진행 중 |
| **개발 인원** | 1인 |
| **담당 범위** | Unity 클라이언트 전체 + 전용 게임 서버 |
| **현재 단계** | 알파 — 매칭·전투·귀환·정산의 전체 루프 동작 |

클라이언트와 서버를 모두 직접 설계·구현했으며, 이 저장소는 Unity 클라이언트입니다.

서버에서의 상세 구현, 배포에 대한 내용은 [ExtractionServer](https://github.com/BoyeonK/ExtractionServer) 저장소에서 확인하실 수 있습니다.

클라이언트와 서버의 통신 계약은 다음 두 파일을 기준으로 관리합니다.

- `Assets/Scripts/Network/http-api-spec.yaml` — OpenAPI 3.0 스펙
- `Assets/Scripts/Network/External_Protocol.proto` — Protobuf 메시지 + 패킷 ID

### 핵심 특징

- **이중 통신 구조** — 로비/인증은 HTTP REST, 인게임은 Custom UDP/RUDP
- **직접 구현한 신뢰성 계층** — Reliable / Unreliable 채널, ACK Bitfield, RTT 기반 재전송
- **서버 중심 상태 관리** — Inventory, Match Result 등 영속성과 일관성이 필요한 상태는 서버에서 최종 결정
- **Unity Main Thread 경계 분리** — Network Worker에서 수신한 이벤트를 Main Thread Job Queue를 통해 Scene에 반영

---

## 2. 데모

<!-- TODO: 플레이 영상 링크 -->

| 상점 | 옵션 창 | 캐릭터선택 |
|---|---|---|
| ![로비](docs/images/lobby_shop.png) | ![옵션 창](docs/images/lobby_option.png) | ![캐릭터선택](docs/images/lobby_character.png) |

| 인게임 HUD | 인게임 파밍중 | 정산 화면 |
|---|---|---|
| ![인게임](docs/images/ingame.png) | ![황금고블린](docs/images/ingame_farming.png) | ![결과](docs/images/result.png) |

### 게임 루프

```text
로그인
  ↓
로비
  ├── 인벤토리
  ├── 상점
  └── 로드아웃 구성
  ↓
맵 선택 / 매칭
  ↓
로딩
  ↓
인게임
  ├── 전투
  ├── 파밍
  ├── 귀환 성공
  └── 사망
  ↓
정산
  ↓
로비 복귀
```

---

## 3. 기술 스택

| 분류 | 사용 기술 |
|---|---|
| **엔진** | Unity 6000.4.0f1 |
| **렌더링** | Universal Render Pipeline 17.4.0 |
| **언어** | C# |
| **입력** | Unity Input System 1.19.0 |
| **직렬화** | Google.Protobuf (UDP) / `JsonUtility` (HTTP) |
| **해싱** | K4os.Hash.xxHash |
| **내비게이션** | Unity AI Navigation 2.0.11 |
| **UI** | uGUI 2.0.0 + TextMeshPro |
| **API 스펙** | OpenAPI 3.0 |

---

## 4. Client Architecture

### Manager Hub

`Managers`가 여러 서브 매니저를 소유하는 싱글톤 허브 역할을 합니다.

```text
Managers
  ├── Input
  ├── Network
  ├── Resource
  ├── UI
  ├── Scene
  ├── Sound
  └── Setting
```

`@Managers` GameObject는 `DontDestroyOnLoad`로 유지되며, Scene 전환 시 필요한 상태만 정리합니다.

특히 Network와 Setting 상태는 Scene 전환 이후에도 유지되므로, Match 종료 후 Lobby로 돌아와도 기존 Session과 설정을 이어갈 수 있습니다.

### Scene Flow

```text
BaseScene
  ├── LobbyScene
  ├── LoadingScene
  ├── IngameScene
  │   ├── TenerifeScene
  │   └── TestIngameScene (개발용 테스트 씬)
  └── GameResultScene
```

### Main Thread Dispatch

UDP 수신은 전용 Worker Thread에서 처리하지만 Unity API는 Main Thread에서만 호출할 수 있습니다.

따라서 Network 수신 결과를 즉시 Scene에 적용하지 않고 Main Thread Job Queue로 전달합니다.

```csharp
Managers.ExecuteAtMainThread(() => {
    // Unity API 호출이 필요할 경우 사용.
    // Worker Thread에서 Unity API호출의 동작을 보장할 수 없음.
});
```

`Managers.Update()`가 매 Frame Queue를 소진하며, 실행 대상은 Lock 내부에서 별도 목록으로 옮긴 뒤 Lock 밖에서 실행합니다.

이를 통해 Network Thread와 Unity Scene Update의 실행 경계를 분리했습니다.

---

## 5. Networking

Lobby와 Match에서는 요구사항이 다르기 때문에 통신 방식을 분리했습니다.

| | HTTP REST | UDP / Custom RUDP |
|---|---|---|
| 담당 | 인증 · 인벤토리 · 상점 · 매치메이킹 | 이동 · 전투 · 상호작용 |
| 신뢰성 | TCP | 직접 구현 |
| 직렬화 | JSON | Protobuf |
| 형태 | 요청-응답 | 양방향 실시간 통신 |

### HTTP Layer

`HTTPManager`는 인증, Inventory, Shop, Matchmaking 등의 요청을 담당합니다.

Matchmaking 이후에는 다음 흐름으로 Dedicated Game Server에 연결합니다.

```text
Start Match
   ↓
Match Status Polling
   ↓
SUCCESS
   ↓
Connection Info Request
   ↓
UDP / Custom RUDP Connection
```

HTTP 실패는 단순 성공/실패 Boolean으로 처리하지 않고, **사용자가 취할 수 있는 행동이 달라지는 경우를 기준으로 상태를 분리**합니다.

예를 들어 Inventory나 Shop 요청에서 다음과 같은 결과를 구분합니다.

- 재화 부족
- Client / Server State 불일치
- 서버의 요청 거부
- 서버 연결 실패
- 이미 요청 처리 중

또한 인증이 필요한 API에서 `401`을 받으면 개별 호출부가 각각 처리하지 않고 Session Expired Event를 통해 재로그인 흐름으로 통합합니다.

실패 응답은 Body가 비어 있거나 예상한 JSON 형식이 아닐 수 있으므로, **HTTP Status Code를 먼저 판정한 뒤 필요한 경우에만 Body를 Parsing**하도록 구성했습니다.

### Custom RUDP

인게임에서는 Unity Netcode 같은 별도 Networking Framework 대신 서버와 공유하는 Custom UDP/RUDP Protocol을 사용합니다.

주요 특징은 다음과 같습니다.

- Reliable / Unreliable Channel
- Selective ACK 기반 Reliable 전송
- RTT 기반 Retransmission
- Heartbeat 및 Lightweight Packet Signature
- MTU를 고려한 Packet Size 제한

이동처럼 새로운 상태가 계속 생성되는 데이터는 Unreliable Channel을 사용하고, 재장전이나 상호작용처럼 반드시 전달되어야 하는 요청은 Reliable Channel을 사용합니다.

Custom RUDP의 상세 설계는 Server Repository의 Networking 문서에서 설명합니다.

[ExtractionServer Networking](https://github.com/BoyeonK/ExtractionServer/blob/main/docs/networking.md)

---

## 6. Engineering Highlights

### AI-assisted Asset Production

게임에 필요한 Character Asset은 AI 기반 도구와 기존 Animation Tool을 조합하여 직접 제작 파이프라인을 구성했습니다.

Character Asset은 다음 과정을 통해 제작했습니다.
1. 컨셉 아트 생성 (Gemini + ChatGPT)
2. 3D 모델 생성 (Tripo3D)
3. 리깅 및 애니메이션 적용 (Adobe Mixamo)
4. Unity Editor에서 게임 Asset으로 구성 

| Glint | Prometheus |
|---|---|
| ![로비](docs/images/glint_concept.png) | ![옵션 창](docs/images/prometheus_concept.png) |
| ![로비](docs/images/glint_3d_model.png) | ![옵션 창](docs/images/prometheus_3d_model.png) |
| ![로비](docs/images/glint_mixamo.png) | ![옵션 창](docs/images/prometheus_mixamo.png) |
| ![로비](docs/images/lobby_character_glint.png) | ![옵션 창](docs/images/lobby_character.png) |

이렇게 만들어진 Character Asset은 Skeleton Bone 정보를 포함하기 때문에 아래의 Runtime Hitbox Generation에 활용할 수 있습니다.

### Runtime Hitbox Generation

Remote Character Prefab에 Hit Collider를 직접 배치하지 않고, Character가 생성될 때 Skeleton Bone을 기준으로 Capsule Hitbox를 생성합니다.

```text
Character Skeleton
      ↓
BuildHitboxes()
      ↓
Head / Torso / Pelvis
Arms / Legs
      ↓
Capsule Colliders
```

Hitbox 길이는 Bone 간 거리를 이용하고, Radius는 Character 전체 크기에 대한 비율로 계산합니다.

이를 통해 체형이 다른 Character Model에서도 동일한 생성 로직을 사용할 수 있도록 했습니다.

### Combat and View Separation

ARC Raiders, VEILED EXPERTS 등의 TPS 플레이 경험을 바탕으로 발사 판정에 사용하는 Line과 화면에 표시되는 Tracer의 시작점을 분리했습니다. 또한 Q 키를 통해 좌·우 Camera Anchor를 전환할 수 있도록 구현했습니다.

```text
Hit Detection
Player Shot Point ─────► Hit Point

Visual Tracer
Weapon Muzzle ─────────► Hit Point
```

Camera가 엄폐물 밖을 바라보더라도 Player의 실제 발사 원점이 엄폐물 뒤에 있다면 총알은 벽에 막힙니다.

따라서 Camera 이동은 시야 확보 수단으로만 사용하고, 실제 Player 위치나 Hitbox가 함께 이동하지 않도록 했습니다.

Aim과 Recoil 역시 별도 상태로 관리합니다.

- `_aimPitch` / `_aimYaw` — Player Input으로 만들어지는 View
- `_recoilPitch` / `_recoilYaw` — 발사에 의해 추가되는 Offset (총기반동)
- `ApplyViewRotation()` — 두 값을 합산하여 최종 View 적용

### Server-authoritative Inventory Snapshot

Inventory는 Server가 최종 상태를 소유하며 Client는 변경 결과를 전체 Snapshot으로 전달받습니다.

```text
Client Request
     ↓
Server Validation
     ↓
Inventory Snapshot
     ↓
Client UI Update
```

재장전처럼 하나의 요청이 여러 Inventory Slot에 영향을 줄 수 있기 때문에 Client가 동일한 변경 과정을 재현하기보다 Server가 계산한 최종 결과를 사용합니다.

Snapshot에는 `inventory_version`을 포함하여 Client보다 오래된 Version의 상태가 뒤늦게 도착하면 적용하지 않습니다.

### Action State Machine

재장전이나 무기 교체처럼 일정한 Local Delay 이후 Server Request가 발생하는 행동은 하나의 Action Lock으로 관리합니다.

| 상태 | 의미 | 취소 |
|---|---|---|
| `Local` | 아직 Server로 전송하지 않은 준비 단계 | 가능 |
| `Pending` | Server Request 전송 후 응답 대기 | 불가 |

```text
Idle
 ↓
Local
 ↓
Request Send
 ↓
Pending
 ↓
Server Response
 ↓
Idle
```

이미 Server로 전송된 행동을 Client가 취소한 것처럼 처리하지 않도록 `Local`과 `Pending`을 구분합니다.

동시에 진행되는 In-flight Action Request를 하나로 제한하여 서로 다른 행동 요청이 같은 상태를 동시에 변경하는 상황을 줄였습니다.

### RenderTexture Map

전체 지도를 UI 좌표로 직접 변환하는 대신 별도의 Top-view Camera와 `RenderTexture`를 사용합니다.

```text
World
  ↓
Top-view Camera
  ↓
RenderTexture
  ↓
RawImage
```

Player 위치 역시 UI 좌표로 변환하지 않고 Map 전용 Layer의 World Marker를 통해 표시합니다.

Main Camera에서는 해당 Layer를 제외하고 Map Camera에서만 렌더링합니다.

Map Camera는 지도를 열 때 한 Frame만 렌더링하고 이후 비활성화합니다. `RenderTexture`가 마지막 Frame을 유지하므로, Map을 닫아둔 동안 추가적인 Scene Rendering을 피할 수 있습니다.

---

## 7. Troubleshooting

### Unreliable Fire Sequence

**문제**

특정 Packet 유실 이후 같은 Weapon의 발사 요청이 Server에서 계속 거부되는 문제가 있었습니다.

**원인**

발사 요청은 Unreliable Channel을 사용하기 때문에 Sequence가 중간에 빠질 수 있습니다.

하지만 초기 Server 검증 로직은 다음 Sequence가 예상값과 정확히 일치해야만 요청을 허용했습니다.

```text
Expected: 10

Receive 10 → Accept
Receive 12 → Reject
Receive 13 → Reject
...
```

Packet Loss가 정상적으로 발생할 수 있는 Transport 특성과 Server의 검증 규칙이 충돌한 것입니다.

**해결**

Sequence 검증을 완전 일치가 아니라 **단조 증가 여부**로 변경했습니다.

```text
Receive Sequence < Expected
    → Reject

Receive Sequence >= Expected
    → Accept
    → Expected 갱신
```

Client 역시 발사 Sequence를 되돌리거나 재사용하지 않고 항상 증가시키도록 제한했습니다.

> Unreliable Channel에서는 Packet Loss를 예외 상황이 아니라 정상적인 동작 범위로 고려해야 한다는 점을 확인했습니다.

### Empty HTTP Response and `async void`

**문제**

특정 HTTP 실패 상황에서 Popup이 표시되지 않고 UI Button이 계속 비활성 상태로 남는 문제가 있었습니다.

**원인**

실패 Response Body가 비어 있는데도 먼저 `JsonUtility.FromJson()`을 호출했고, Parsing Exception이 `async void` UI Event Handler 밖으로 전달되면서 이후 Cleanup Logic이 실행되지 않았습니다.

**해결**

모든 HTTP API 처리 순서를 다음과 같이 통일했습니다.

```text
HTTP Response
     ↓
Status Code 판단
     ↓
필요한 경우에만 Body Parsing
     ↓
Caller Result
```

Response Body 형식과 관계없이 Status Code만으로 처리할 수 있는 실패는 Parsing 단계에 진입하지 않도록 했습니다.

---

## 8. Controls

| 키 | 동작 |
|---|---|
| `WASD` / `Space` | 이동 / 점프 |
| `Shift` | 달리기 |
| `좌클릭` / `우클릭` | 발사 / 조준 |
| `R` | 재장전 |
| `1` `2` | 주무기 / 보조무기 전환 |
| `Q` | 좌우 Camera Anchor 전환 |
| `E` | 상호작용 |
| `Tab` | 인벤토리 |
| `M` | 지도 |
| `ESC` | 설정 / 종료 |

---

## 9. Repository

- Client: [ExtractionClient](https://github.com/BoyeonK/ExtractionClient)
- Server: [ExtractionServer](https://github.com/BoyeonK/ExtractionServer)

