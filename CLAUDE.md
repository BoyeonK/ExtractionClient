# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

- **엔진**: Unity 6000.4.0f1, URP 17.4.0
- **장르**: 멀티플레이어 Extraction 게임 (알파 단계)
- **서버 통신**: HTTP REST API (로비/인증) + UDP (실시간 인게임)
- **플랫폼**: Windows Standalone x64

---

## 모듈별 상세 문서

각 폴더의 CLAUDE.md에서 해당 모듈의 상세 규칙을 확인할 것:
- 네트워크 (HTTP/UDP/패킷): `Assets/Scripts/Network/CLAUDE.md`
- UI 시스템: `Assets/Scripts/UI/CLAUDE.md`
- 씬 구성: `Assets/Scripts/Scenes/CLAUDE.md`
- 컨트롤러 (Player/Oppo/Interactable): `Assets/Scripts/Controller/CLAUDE.md`
- 입력 시스템: `Assets/Scripts/Managers/CLAUDE.md`

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

## 핵심 규칙 / 컨벤션

1. **Unity API는 반드시 메인 스레드**에서 호출 — 비동기/UDP 콜백에서는 `Managers.ExecuteAtMainThread` 사용
2. **프리팹은 `Resources/Prefabs/` 하위에 위치** — `ResourceManager` 경로는 이 폴더 기준 상대경로
3. **서버 URL은 `Gitignores.baseUrl`에서** — 하드코딩 절대 금지
4. **새 UDP 패킷 타입 추가 시**: `Assets/Scripts/Network/CLAUDE.md`의 절차를 따를 것

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
- Asset/LowPolyAssetBundle 폴더를 절대 읽지 말 것. 참조가 필요하면 나에게 먼저 물어볼 것
- .gitignore 파일은 읽기만 가능, 절대 수정하지 말 것
- Asset/Scripts/Utils/Gitignores.cs는 절대 읽거나, 수정하거나, 문서화하지 말 것
- 스크립트 작업 시 탐색·수정 범위는 `Assets/Scripts/` 이내로 제한할 것. 다른 폴더(씬, 프리팹, 에셋 등)를 읽어야 할 필요가 생기면 먼저 나에게 물어볼 것
