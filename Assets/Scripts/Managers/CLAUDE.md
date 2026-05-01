# 입력 시스템

- `com.unity.inputsystem` 1.19.0 패키지 사용 (구형 `Input` 클래스 사용 금지)
- 등록: `Managers.Input.AddKeyListener(Key, Action, KeyState)`
- `OnDestroy`에서 반드시 `RemoveKeyListener`로 해제
- `KeyState`: `Down` / `Up`
