using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : GameObjectController {
    CharacterController _controller;
    
    // 카메라 및 Raycast
    Camera _camera;
    GameObject _viewPoint;
    Vector3 _raycastDir = new Vector3(0.05f, 0.15f, -0.4f);
    Transform _aimTarget;

    // 사옹할 모델 및 애니메이션 및 Rig
    RigBuilder _rigBuilder;
    MultiAimConstraint _constraint;
    Animator _anim;
    
    // Character Controller 설정값
    float walkSpeed = 1f;
    float runSpeed = 3.5f;
    float jumpHeight = 0.6f;
    float gravity = -9.81f;

    float mouseSensitivity = 1f;
    float xRotation = 0f;

    bool _w = false, _a = false, _s = false, _d = false, _shift = false, _jump = false;
    Vector3 _velocity;


    public override void Init() {
        Cursor.lockState = CursorLockMode.Locked;

        _controller = GetComponent<CharacterController>();
        Transform camTransform = transform.Find("ViewPoint");
        if (camTransform != null) _viewPoint = camTransform.gameObject;

        _aimTarget = transform.Find("Aim");
        _camera = _viewPoint.GetComponentInChildren<Camera>();

        string type = "HB1Player";
        GameObject modelGo = Managers.Resource.Instantiate($"GameObject/PlayerObject_ingredient/{type}", this.transform);
        _rigBuilder = Util.BindComponent<RigBuilder>($"{type}", this.gameObject);
        _constraint = Util.BindComponent<MultiAimConstraint>($"{type}/WeaponRig/SpineAim", this.gameObject);
        
        WeightedTransformArray sourceObjects = new WeightedTransformArray();
        sourceObjects.Add(new WeightedTransform(_aimTarget, 1f));
        _constraint.data.sourceObjects = sourceObjects;

        if (_rigBuilder != null) {
            _rigBuilder.Build();
        }

        _anim = Util.BindComponent<Animator>(type, this.gameObject);

        Managers.Input.AddKeyListener(Key.W, WDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.A, ADown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.S, SDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.D, DDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.LeftShift, ShiftDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Space, TryJump, InputManager.KeyState.Down);

        Managers.Input.AddKeyListener(Key.W, WUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.A, AUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.S, SUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.D, DUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.LeftShift, ShiftUp, InputManager.KeyState.Up);
    }

    void Update() {
        ProcessMovement();
        ProcessMouseLook();
        ProcessAnimation(); 
        ProcessAim();
    }

    private void ProcessMouseLook() {
        if (_viewPoint == null) return;

        // Mouse Delta는 순수 픽셀 이동량
        float mouseX = 0f;
        float mouseY = 0f;

        if (Mouse.current != null) {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            mouseX = mouseDelta.x * mouseSensitivity;
            mouseY = mouseDelta.y * mouseSensitivity;
        }

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 90f);
        _viewPoint.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

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

        float moveSpeed = _shift ? runSpeed : walkSpeed;

        if (_jump) {
            if (_controller.isGrounded) {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            _jump = false;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(move * moveSpeed * Time.deltaTime + _velocity * Time.deltaTime);
    }

    private void ProcessAnimation() {
        if (_anim == null) return;

        float inputX = (_d ? 1f : 0f) - (_a ? 1f : 0f);
        float inputY = (_w ? 1f : 0f) - (_s ? 1f : 0f);
        Vector2 animDir = new Vector2(inputX, inputY).normalized;

        bool isMoving = animDir.sqrMagnitude > 0;
        bool isRunning = isMoving && _shift;
        bool isShootingInput = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool actualShooting = isShootingInput && !isRunning;

        _anim.SetBool("IsShooting", actualShooting);

        float speedMult = isRunning ? 2f : 1f;
        _anim.SetFloat("MoveX", animDir.x * speedMult, 0.1f, Time.deltaTime);
        _anim.SetFloat("MoveY", animDir.y * speedMult, 0.1f, Time.deltaTime);

        float moveSpeed = isMoving ? 1f : 0f;
        _anim.SetFloat("MovingSpeed", moveSpeed, 0.1f, Time.deltaTime);
    }

    private void ProcessAim() {
        if (_aimTarget == null || _camera == null) return;

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f)) {
            _aimTarget.position = hit.point; 
        } else {
            _aimTarget.position = ray.GetPoint(1000f); 
        }
    }

    // 입력 콜백
    private void WDown() { _w = true; }
    private void ADown() { _a = true; }
    private void SDown() { _s = true; }
    private void DDown() { _d = true; }
    private void ShiftDown() { _shift = true; }
    private void WUp() { _w = false; }
    private void AUp() { _a = false; }
    private void SUp() { _s = false; }
    private void DUp() { _d = false; }
    private void ShiftUp() { _shift = false; }
    private void TryJump() { _jump = true; }

    private void OnDestroy() {
        if (Managers.Instance == null) return;
        Managers.Input.RemoveKeyListener(Key.W, WDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.A, ADown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.S, SDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.D, DDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.LeftShift, ShiftDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.Space, TryJump, InputManager.KeyState.Down);

        Managers.Input.RemoveKeyListener(Key.W, WUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.A, AUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.S, SUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.D, DUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.LeftShift, ShiftUp, InputManager.KeyState.Up);
    }
}