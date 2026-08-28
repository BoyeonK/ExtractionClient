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
- 설정 영속화 · 입력 시스템: `Assets/Scripts/Managers/CLAUDE.md`
- 리소스 (프리팹/이미지/사운드 경로·명명 규약): `Assets/Resources/CLAUDE.md`

---

## 매니저 시스템 (`Managers.cs`)

싱글톤 허브. `@Managers` GameObject에 부착되며 `DontDestroyOnLoad`. 모든 서브 매니저는 정적 프로퍼티로 접근:

| 접근자 | 클래스 | 역할 |
|--------|--------|------|
| `Managers.Input` | `InputManager` | 키 입력 이벤트 등록/해제 |
| `Managers.Network` | `NetworkManager` | HTTP + UDP 네트워크 오케스트레이션 |
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
5. **미완성 코드 주석 마커** — 아래 세 가지만 사용한다. 새로 남길 때도 이 중 하나를 고를 것

| 마커 | 의미 | 해소 조건 |
|------|------|-----------|
| `TODO:` | 아직 구현되지 않은 것 | 구현하면 제거 |
| `TEMP:` | 테스트를 위해 의도적으로 값을 제한하거나 코드를 막아둔 것 | 테스트가 끝나면 원복 |
| `OPTION:` | 없어도 동작에 문제가 없고 여유가 생기면 구현할 개선 항목 | 상시 보류 가능 |

6. **모듈 `CLAUDE.md`에는 '코드를 고칠 때 지켜야 할 것'만 적는다** — 경위·서사는 `progress.md`(시간축) 소관이고, 모듈 문서는 결과만 든다
   - **판단 기준 하나: 이 문장이 없으면 다음 세션이 코드를 잘못 고치는가?** 아니면 적지 않는다. **애매하면 자른다** — 잘못 자른 규칙은 다시 밟히면 다시 적히지만, 비대해진 문서는 아무도 안 읽어 있으나 마나가 된다
   - 한 항목은 **규칙 + 어겼을 때의 증상**으로 2줄 이내
   - **빼는 것**: 세션 번호·날짜 참조(`(2026-08-27 #5)에서 …`), 누가 무엇을 뒤집었는지, 과거의 오진 사례
   - **남기는 것**: 왜 그래야 하는지(근거). 이유 없는 금지는 "내가 더 잘 안다"로 덮인다. **사실의 출처인 날짜는 예외**(`부위별 차등 없음(2026-08-27 서버 확인)`)

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
- `Assets/Scripts/Utils/ItemDBHelper.cs`는 **DB 자료를 긁어 생성되는 스크립트다. 읽는 것은 자유롭게 하되 절대 수정하지 말 것** — 아이템·무기·방어구 수치는 DB 쪽이 주도하며, 이 세션에서 값을 고치면 다음 생성 때 덮여 사라진다. 수치 조정이 필요해 보이면 코드를 고치지 말고 나에게 말할 것
- 탐색·수정 범위는 `Assets/Scripts/` 와 `Assets/Resources/` 두 폴더로 제한할 것. 그 밖(씬 `.unity`, 그 외 에셋 등)을 읽어야 할 필요가 생기면 먼저 나에게 물어볼 것
  - `Assets/Resources/`를 다룰 때는 **`Assets/Resources/CLAUDE.md`를 먼저 볼 것** — 조사 비용 규칙(프리팹 전체 Read는 마지막 수단, Glob → Grep 순)과 경로·명명 규약이 거기 있다
  - 프리팹은 내가 에디터로 만들고 관리한다. **읽어서 확인하는 것이 목적이며, 수정이 필요해 보이면 고치지 말고 나에게 말할 것**
