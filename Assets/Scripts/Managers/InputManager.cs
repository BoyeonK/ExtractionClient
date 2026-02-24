using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager {
    //키를 누른 시점에 1번, 키를 뗀 시점에 1번, 꾹누르기 가능 여부를 구별하기 위한 enum
    // 1 = 딸, 2 = 깍, 3 = 꾹
    public enum KeyState {
        Down = 0,
        Up = 1,
        Press = 2,
    }

    //에러 발생시, 쌓일 스택. 1이라도 있으면 Input을 Invoke하지않는다.
    int errStack = 0;
    public void IncrementCounter() {
        Interlocked.Increment(ref errStack);
    }

    public void DecrementCounter() {
        Interlocked.Decrement(ref errStack);
    }

    private class ActionState {
        public KeyState State;
        public Action Action;
    }

    public Action<Define.MouseEvent> MouseAction = null;
    private Dictionary<Key, List<ActionState>> _keyActions = new Dictionary<Key, List<ActionState>>();
    bool _mousePressed = false;

    //구독
    public void AddKeyListener(Key key, Action action, KeyState state = KeyState.Down) {
        if (!_keyActions.ContainsKey(key))
            _keyActions.Add(key, new List<ActionState>());

        _keyActions[key].Add(new ActionState { Action = action, State = state });
    }

    //구독 취소
    public void RemoveKeyListener(Key key, Action action, KeyState state) {
        if (_keyActions.ContainsKey(key)) {
            var list = _keyActions[key];
            var itemToRemove = list.Find(x => x.Action == action && x.State == state);
            if (itemToRemove != null) {
                list.Remove(itemToRemove);
            }
        }
    }

    public void OnUpdate() {
        if (errStack != 0)
            return;

        // 키보드가 연결되어 있는지 확인
        if (Keyboard.current != null) {
            foreach (var pair in _keyActions) {
                Key key = pair.Key;
                var actionList = pair.Value;

                // 딸
                if (Keyboard.current[key].wasPressedThisFrame) {
                    foreach (var actionState in actionList) {
                        if (actionState.State == KeyState.Down) actionState.Action.Invoke();
                    }
                }
                // 깍
                else if (Keyboard.current[key].wasReleasedThisFrame) {
                    foreach (var actionState in actionList) {
                        if (actionState.State == KeyState.Up) actionState.Action.Invoke();
                    }
                }
                // 꾹
                else if (Keyboard.current[key].isPressed) {
                    foreach (var actionState in actionList) {
                        if (actionState.State == KeyState.Press) actionState.Action.Invoke();
                    }
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
        MouseAction = null;
    }
}
