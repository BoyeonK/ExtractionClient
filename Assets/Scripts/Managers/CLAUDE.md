# 설정 영속화 (`SettingManager`)

필드 8개(볼륨 3 · 마우스 감도 · 창모드 · 해상도 · 프레임레이트 · FOV)를 `PlayerPrefs`에 저장한다. **로비·인게임 설정 UI가 같은 매니저를 쓰므로 여기만 고치면 둘 다 따라온다.**

- **저장 키는 클래스 상단 상수에 모아 둔다.** 흩어지면 오타 하나로 그 항목만 조용히 기본값으로 돌아간다. 키 이름을 바꾸면 이미 저장된 값은 읽히지 않고 버려진다
- **enum은 정수가 아니라 이름으로 저장한다.** `Define.Resolution`이 오름차순 정렬이라 새 해상도가 목록 **중간에 삽입되기 쉬운데**, 정수로 저장하면 그 순간 저장된 값이 다른 항목을 가리킨다. 증상이 "설정이 멋대로 바뀐다"라 원인을 짚기 어렵다. `MaxCount`는 실재하는 설정이 아니므로 로드에서 함께 걸러낸다
- **로드는 필드 직접 대입이 아니라 setter를 통과시킨다.** 범위 검증(`Mathf.Clamp`)이 setter에 있고, `PlayerPrefs`는 사용자가 편집할 수 있는 영역이라 손상된 값이 그대로 들어올 수 있다
- **로드 순서: 마스터 볼륨이 먼저다.** `SetVolume()`이 `(볼륨 × 마스터)`로 실제 음량을 계산하므로 순서가 뒤집히면 효과음·BGM이 기본 마스터 기준으로 한 번 잘못 적용된다. **`IngameSettingUI.CancelAndHide()`의 되돌리기에도 같은 규칙이 걸려 있다**
- **`Load()`는 `ApplyFrameRate()`보다 먼저 불러야 한다.** 순서가 뒤집히면 저장된 프레임레이트가 필드에만 들어가고 실제 적용은 기본값으로 이뤄진다
- `SettingManager`는 `Managers.Clear()` 대상이 아니라 **씬 전환에서 살아남는다.** `Init()`은 프로세스당 1회이므로 재로드를 걱정할 필요가 없다

## 저장 시점 (`Save()` = `PlayerPrefs.Save()`)

setter는 **메모리에만** 쓴다. 디스크로 내리는 것은 아래 셋뿐이다.

| 지점 | 이유 |
|---|---|
| `LobbySettingUI.ApplySetting()` | 이 창의 **확정 지점**('적용'에서 setter를 한 번에 호출한다) |
| `IngameSettingUI.Hide()` | 이 창은 **슬라이더 즉시 반영**이라 '적용'이 확정이 아니다. 조작이 끝나는 시점은 '닫힘' 하나뿐이고 `CancelAndHide()`도 되돌린 뒤 여기로 오므로 **취소까지 한 자리로 덮인다** |
| `Managers.OnApplicationQuit()` | backstop. **`s_isQuitting`을 세우기 전에** 부른다 — 세운 뒤에는 `Instance` 게터가 null을 반환한다 |

- **setter에는 걸지 않는다.** 인게임 설정 창이 슬라이더를 드래그하는 매 프레임 setter를 부르므로, 거기서 플러시하면 드래그 내내 동기 I/O가 나간다
- **종료 시점만으로는 부족하다.** `OnApplicationQuit`은 정상 종료에서만 불려서, 크래시·강제 종료에서는 방금 확정한 설정이 통째로 날아간다. 설정 창을 닫는 시점에 저장하는 것이 본선이고 종료 쪽이 예비다

## 마우스 감도 범위

`MIN_MOUSE_SENSITIVITY`(0.1) / `MAX_MOUSE_SENSITIVITY`(3.0)는 **설정 창 슬라이더의 Min/Max와 손으로 맞추는 값이다.** 여기만 넓히면 슬라이더로는 못 넣는 값이 저장 파일 편집으로 들어오고, 슬라이더만 넓히면 조작이 이 범위에서 잘린다. 실제 도/픽셀은 이 값 × `PlayerController.MOUSE_SENSITIVITY_DEG_PER_PIXEL`(0.1)이다.

---

# 입력 시스템

- `com.unity.inputsystem` 1.19.0 패키지 사용 (구형 `Input` 클래스 사용 금지)
- 등록: `Managers.Input.AddKeyListener(Key, Action, KeyState)`
- `OnDestroy`에서 반드시 `RemoveKeyListener`로 해제
- `KeyState`: `Down` / `Up`

## 리스너 작성 규칙

- **`OnDestroy`에서 `Managers.*`를 건드리기 전에 `Managers.Instance` null 가드를 둘 것.** 종료 시 `OnApplicationQuit`이 먼저 돌아 `s_isQuitting`이 서면 `Instance` 게터가 null을 반환한다. 가드가 없으면 게임을 끌 때마다 NRE가 난다
- **리스너 안에서 씬을 직접 전환하지 말고 `Managers.ExecuteAtMainThread`로 미룰 것.** `LoadScene()`이 부르는 `Managers.Clear()`가 입력 등록을 통째로 비우기 때문이다. 잡큐는 같은 `Managers.Update()` 안에서 `_input.OnUpdate()` 뒤에 소진되므로 프레임 지연은 없다

## 디스패치 동작 (`InputManager.OnUpdate`)

키 목록·액션 목록을 **재사용 버퍼로 스냅샷해 순회한다.** 리스너가 콜백 안에서 구독을 바꿔도 예외가 나지 않고, 변경은 다음 프레임부터 반영된다.

- **`Clear()`는 `_keyBuffer`를 건드리면 안 된다.** 비우는 순간 열거 중인 리스트가 바뀌어 이 설계가 막으려던 `InvalidOperationException`이 되살아난다
- `Clear()` 이후 순회에 남은 키들은 `TryGetValue` 실패로 건너뛴다 — 죽은 씬의 리스너가 이어서 불리지 않는다
- 키 버퍼는 **더티 플래그로 씬 로드당 1회만** 재구축한다. 키 집합이 바뀌는 지점이 `AddKeyListener`의 새 키 추가와 `Clear()` 둘뿐이라 성립하는 것이며, `RemoveKeyListener`가 딕셔너리 항목까지 지우도록 바꾸면 이 전제를 다시 봐야 한다
- `errStack`이 0보다 크면 디스패치 전체를 건너뛴다. `IncrementCounter`/`DecrementCounter`는 **아직 호출자가 없고**, 짝이 안 맞는 `Decrement`는 무시된다(음수로 떨어지면 입력이 영구히 잠기므로)
