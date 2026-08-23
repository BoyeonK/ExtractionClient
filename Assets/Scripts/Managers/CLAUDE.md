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
