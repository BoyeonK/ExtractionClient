using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager {
    public enum KeyState {
        Down = 0,
        Up = 1,
        Press = 2,
    }

    // 에러 발생시 쌓일 스택. 0이 아니면 Input을 Invoke하지 않는다 (아직 호출자 없음)
    private int errStack = 0;

    public void IncrementCounter() {
        Interlocked.Increment(ref errStack);
    }

    public void DecrementCounter() {
        // 0 아래로 내려가지 않는다 — 짝이 안 맞는 호출은 입력이 영구히 잠기는 대신 여기서 무시된다
        int current;
        do {
            current = Volatile.Read(ref errStack);
            if (current <= 0) return;
        } while (Interlocked.CompareExchange(ref errStack, current - 1, current) != current);
    }

    private class ActionState {
        public KeyState State;
        public Action Action;
    }

    public Action<Define.MouseEvent> MouseAction = null;
    private Dictionary<Key, List<ActionState>> _keyActions = new Dictionary<Key, List<ActionState>>();
    bool _mousePressed = false;

    // 디스패치용 스냅샷 버퍼. 리스너가 씬 전환을 일으키면 그 콜백 안에서 Clear()가 돌아
    // 순회 중인 컬렉션이 바뀐다. 그래서 원본이 아니라 여기에 떠서 돌리며,
    // **Clear()는 _keyBuffer를 건드리면 안 된다** — 비우는 순간 열거 중인 리스트가 바뀌어
    // 이걸 막으려고 만든 InvalidOperationException이 되살아난다.
    // 키 집합이 바뀌는 지점은 AddKeyListener의 새 키 추가와 Clear() 둘뿐이라
    // (RemoveKeyListener는 안쪽 리스트만 비우고 항목은 남긴다) 그때만 다시 뜬다
    private readonly List<Key> _keyBuffer = new List<Key>();
    private readonly List<ActionState> _actionBuffer = new List<ActionState>();
    private bool _keyBufferDirty = true;

    public void AddKeyListener(Key key, Action action, KeyState state = KeyState.Down) {
        if (!_keyActions.ContainsKey(key)) {
            _keyActions.Add(key, new List<ActionState>());
            _keyBufferDirty = true;
        }

        _keyActions[key].Add(new ActionState { Action = action, State = state });
    }

    public void RemoveKeyListener(Key key, Action action, KeyState state) {
        if (!_keyActions.TryGetValue(key, out List<ActionState> list))
            return;

        // 람다(Find)는 action·state를 캡처해 호출마다 델리게이트를 할당한다
        for (int i = list.Count - 1; i >= 0; i--) {
            if (list[i].Action == action && list[i].State == state) {
                list.RemoveAt(i);
                break;
            }
        }
    }

    public void OnUpdate() {
        if (Volatile.Read(ref errStack) > 0)
            return;

        if (Keyboard.current != null) {
            if (_keyBufferDirty) {
                _keyBuffer.Clear();
                _keyBuffer.AddRange(_keyActions.Keys);
                _keyBufferDirty = false;
            }

            foreach (Key key in _keyBuffer) {
                // 앞선 리스너가 이 키의 구독을 통째로 해제했을 수 있다
                if (!_keyActions.TryGetValue(key, out List<ActionState> actionList))
                    continue;

                KeyState firedState;
                if (Keyboard.current[key].wasPressedThisFrame)
                    firedState = KeyState.Down;
                else if (Keyboard.current[key].wasReleasedThisFrame)
                    firedState = KeyState.Up;
                else if (Keyboard.current[key].isPressed)
                    firedState = KeyState.Press;
                else
                    continue;

                _actionBuffer.Clear();
                _actionBuffer.AddRange(actionList);
                foreach (var actionState in _actionBuffer) {
                    if (actionState.State == firedState) actionState.Action.Invoke();
                }
            }
        }

        if (MouseAction != null && Mouse.current != null) {
            // Input.GetMouseButton(0) -> Mouse.current.leftButton.isPressed
            if (Mouse.current.leftButton.isPressed) {
                MouseAction.Invoke(Define.MouseEvent.Press);
                _mousePressed = true;
            }
            else {
                if (_mousePressed)
                    MouseAction.Invoke(Define.MouseEvent.Click);
                _mousePressed = false;
            }
        }
    }

    public void Clear() {
        _keyActions.Clear();
        _keyBufferDirty = true;
        MouseAction = null;
    }
}
