using UnityEngine;
using UnityEngine.InputSystem; // 1. 네임스페이스 추가

[RequireComponent(typeof(CharacterController))]
public class PlayerController : GameObjectController
{
    Camera _pcam;
    CharacterController _controller;

    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Look Settings")]
    public float mouseSensitivity = 1f;
    float xRotation = 0f;

    bool _w = false, _a = false, _s = false, _d = false, _jump = false;
    Vector3 _velocity;

    public override void Init() {
        Cursor.lockState = CursorLockMode.Locked;

        _controller = GetComponent<CharacterController>();
        Transform camTransform = transform.Find("Pcam");
        if (camTransform != null) _pcam = camTransform.GetComponent<Camera>();

        // 2. KeyCode -> Key 로 전부 변경
        Managers.Input.AddKeyListener(Key.W, WDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.A, ADown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.S, SDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.D, DDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Space, TryJump, InputManager.KeyState.Down);

        Managers.Input.AddKeyListener(Key.W, WUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.A, AUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.S, SUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.D, DUp, InputManager.KeyState.Up);
    }

    void Update() {
        ProcessMovement();
        ProcessMouseLook();
    }

    private void ProcessMouseLook() {
        if (_pcam == null) return;

        // Mouse Delta는 순수 픽셀 이동량
        float mouseX = 0f;
        float mouseY = 0f;

        if (Mouse.current != null) {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            mouseX = mouseDelta.x * mouseSensitivity;
            mouseY = mouseDelta.y * mouseSensitivity;
        }

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);
        _pcam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void ProcessMovement() {
        if (_controller.isGrounded && _velocity.y < 0) {
            _velocity.y = -2f;
        }

        Vector3 move = Vector3.zero;
        if (_w) move += transform.forward;
        if (_s) move -= transform.forward;
        if (_d) move += transform.right;
        if (_a) move -= transform.right;

        if (move.sqrMagnitude > 1f) move.Normalize();

        _controller.Move(move * moveSpeed * Time.deltaTime);

        if (_jump) {
            if (_controller.isGrounded) {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _jump = false;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    // 입력 콜백
    private void WDown() { _w = true; }
    private void ADown() { _a = true; }
    private void SDown() { _s = true; }
    private void DDown() { _d = true; }
    private void WUp() { _w = false; }
    private void AUp() { _a = false; }
    private void SUp() { _s = false; }
    private void DUp() { _d = false; }
    private void TryJump() { _jump = true; }

    private void OnDestroy() {
        if (Managers.Instance == null) return;
        Managers.Input.RemoveKeyListener(Key.W, WDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.A, ADown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.S, SDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.D, DDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.Space, TryJump, InputManager.KeyState.Down);

        Managers.Input.RemoveKeyListener(Key.W, WUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.A, AUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.S, SUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.D, DUp, InputManager.KeyState.Up);
    }
}