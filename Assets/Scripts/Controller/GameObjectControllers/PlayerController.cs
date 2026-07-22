using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : GameObjectController, ICombatTarget {
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

    // 상호작용
    IngameScene _ingameScene;

    // 장착 무기 정보 연동
    Transform _weaponSocketTr;
    GameObject _equippedWeaponGo;

    // 발사 타이머
    float _fireTimer = 0f;
    float _fireInterval = 0f; // 60f / RPM

    // WeaponSpec 캐시 (EquipWeapon 시 갱신)
    float _vRecoilMin, _vRecoilMax, _hRecoilMax;
    float _spreadBase, _spreadMax, _spreadIncreasePerShot, _spreadRecoveryRate;

    // 스프레드 상태
    float _currentSpread = 0f;

    // 반동 보간 상태
    Vector2 _recoilTarget = Vector2.zero;   // 목표 반동 (x=수직, y=수평)
    Vector2 _recoilCurrent = Vector2.zero;  // 현재까지 적용된 반동
    float _recoilApplySpeed = 15f;          // 반동 올라가는 속도

    // 시점 분리: 마우스 에임 + 반동 오프셋
    float _aimPitch = 0f;                   // 마우스로 제어하는 순수 피치
    float _aimYaw = 0f;                     // 마우스로 제어하는 순수 요
    float _recoilPitch = 0f;               // 반동에 의한 피치 오프셋
    float _recoilYaw = 0f;                 // 반동에 의한 요 오프셋

    // 발사 차단
    bool _fireBlocked = false;
    bool _wasMousePressed = false;
    bool _wasUIOpen = false;

    // Character Controller 설정값
    float walkSpeed = 1f;
    float runSpeed = 3.5f;
    float jumpHeight = 0.6f;
    float gravity = -9.81f;

    float mouseSensitivity = 1f;

    bool _w = false, _a = false, _s = false, _d = false, _shift = false, _jump = false;
    Vector3 _velocity;

    bool IsMoving => _w || _s || _a || _d;
    bool IsRunning => IsMoving && _shift;
    bool IsShooting => Mouse.current != null && Mouse.current.leftButton.isPressed
                       && !_ingameScene.IsAnyUIOpen && !IsRunning;

    public override void Init() {
        base.Init();

        Managers.Input.AddKeyListener(Key.W, WDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.A, ADown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.S, SDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.D, DDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.LeftShift, ShiftDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Space, TryJump, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.E, TryInteract, InputManager.KeyState.Down);

        Managers.Input.AddKeyListener(Key.W, WUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.A, AUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.S, SUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.D, DUp, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.LeftShift, ShiftUp, InputManager.KeyState.Up);
    }

    public void Setup(int characterType) {
        _ingameScene = Managers.Scene.CurrentScene as IngameScene;
        _controller = GetComponent<CharacterController>();
        Transform camTransform = transform.Find("ViewPoint");
        if (camTransform != null) _viewPoint = camTransform.gameObject;

        _aimTarget = transform.Find("Aim");
        _camera = _viewPoint.GetComponentInChildren<Camera>();

        string modelName = $"HB{characterType}Player";
        Managers.Resource.Instantiate($"GameObject/PlayerObject_ingredient/{modelName}", this.transform);
        _rigBuilder = Util.BindComponent<RigBuilder>($"{modelName}", this.gameObject);
        _constraint = Util.BindComponent<MultiAimConstraint>($"{modelName}/WeaponRig/SpineAim", this.gameObject);

        WeightedTransformArray sourceObjects = new WeightedTransformArray();
        sourceObjects.Add(new WeightedTransform(_aimTarget, 1f));

        var constraintData = _constraint.data;
        constraintData.sourceObjects = sourceObjects;
        _constraint.data = constraintData;

        if (_rigBuilder != null) {
            _rigBuilder.Build();
        }

        _anim = Util.BindComponent<Animator>(modelName, this.gameObject);

        _weaponSocketTr = transform.Find($"{modelName}/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand/WeaponSocket");
    }

    public void EquipWeapon(int weaponId) {
        if (_equippedWeaponGo != null) {
            Managers.Resource.Destroy(_equippedWeaponGo);
            _equippedWeaponGo = null;
        }

        IngameScene scene = Managers.Scene.CurrentScene as IngameScene;
        if (!scene.WeaponPrefabCache.TryGetValue(weaponId, out GameObject weaponPrefab))
            return;

        _equippedWeaponGo = Object.Instantiate(weaponPrefab, _weaponSocketTr);
        _equippedWeaponGo.transform.localPosition = Vector3.zero;
        _equippedWeaponGo.transform.localRotation = Quaternion.identity;

        if (ItemDBHelper.TryGetWeaponSpec(weaponId, out WeaponSpec spec)) {
            _fireInterval = 60f / spec.Rpm;
            _vRecoilMin = spec.VRecoilMin / 100f;
            _vRecoilMax = spec.VRecoilMax / 100f;
            _hRecoilMax = spec.HRecoilMax / 100f;
            _spreadBase = spec.SpreadBase / 100f;
            _spreadMax = spec.SpreadMax / 100f;
            _spreadIncreasePerShot = spec.SpreadIncreasePerShot / 100f;
            _spreadRecoveryRate = spec.SpreadRecoveryRate / 100f;
            _currentSpread = _spreadBase;
        }
    }

    void Update() {
        ProcessMovement();
        ProcessMouseLook();
        ProcessRecoil();
        ApplyViewRotation();
        ProcessAnimation();
        ProcessFire();
        ProcessAim();
    }

    private void ProcessMouseLook() {
        if (_viewPoint == null) return;
        if (_ingameScene.IsAnyUIOpen) return;

        if (Mouse.current != null) {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            _aimPitch -= mouseDelta.y * mouseSensitivity;
            _aimPitch = Mathf.Clamp(_aimPitch, -80f + _recoilPitch, 90f + _recoilPitch);
            _aimYaw += mouseDelta.x * mouseSensitivity;
        }
    }

    private void ProcessRecoil() {
        if (_recoilTarget == _recoilCurrent) return;

        // 목표까지 Lerp로 보간
        _recoilCurrent = Vector2.Lerp(_recoilCurrent, _recoilTarget, _recoilApplySpeed * Time.deltaTime);

        // 반동 오프셋 갱신 (실제 회전 적용은 ApplyViewRotation에서)
        _recoilPitch = _recoilCurrent.x;
        _recoilYaw = _recoilCurrent.y;

        // 목표에 충분히 도달하면 스냅
        if ((_recoilTarget - _recoilCurrent).sqrMagnitude < 0.0001f) {
            _recoilCurrent = _recoilTarget;
        }
    }

    private void ApplyViewRotation() {
        if (_viewPoint == null) return;

        // 피치: 마우스 에임 + 반동 오프셋 합산 후 클램프
        float finalPitch = Mathf.Clamp(_aimPitch - _recoilPitch, -80f, 90f);
        _viewPoint.transform.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);

        // 요: 마우스 에임 + 반동 오프셋 합산
        transform.rotation = Quaternion.Euler(0f, _aimYaw + _recoilYaw, 0f);
    }

    private void ProcessMovement() {
        if (_controller == null) return;
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

        _anim.SetBool("IsShooting", IsShooting);

        float speedMult = IsRunning ? 2f : 1f;
        _anim.SetFloat("MoveX", animDir.x * speedMult, 0.1f, Time.deltaTime);
        _anim.SetFloat("MoveY", animDir.y * speedMult, 0.1f, Time.deltaTime);

        float moveSpeed = IsMoving ? 1f : 0f;
        _anim.SetFloat("MovingSpeed", moveSpeed, 0.1f, Time.deltaTime);
    }

    private void ProcessFire() {
        if (_fireInterval <= 0f) return;

        // UI 열림 전환 감지 → block
        if (!_wasUIOpen && _ingameScene.IsAnyUIOpen)
            _fireBlocked = true;
        _wasUIOpen = _ingameScene.IsAnyUIOpen;

        // 마우스 재클릭 감지 (release→press) → block 해제
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (!_wasMousePressed && mousePressed)
            _fireBlocked = false;
        _wasMousePressed = mousePressed;

        _fireTimer = Mathf.Min(_fireTimer + Time.deltaTime, _fireInterval);
        _currentSpread = Mathf.Max(_currentSpread - _spreadRecoveryRate * Time.deltaTime, _spreadBase);

        if (!_fireBlocked && IsShooting && _fireTimer >= _fireInterval) {
            _fireTimer -= _fireInterval;
            Fire();
        }
    }

    private void Fire() {
        // 1. 탄약 확인
        InventoryItem magazine = _ingameScene.Inventory.IsPrimaryWeaponApplyed
            ? _ingameScene.Inventory.PrimaryWeaponMagazine
            : _ingameScene.Inventory.SecondaryWeaponMagazine;

        // 테스트용 주석처리
        // if (magazine == null || magazine.quantity <= 0) {
        //     EmptyAmmoFire();
        //     return;
        // }

        // magazine.quantity--; // 테스트용 주석처리

        // 2. 스프레드 적용 히트스캔
        Ray spreadRay = CalculateSpreadRay();
        bool hasHit = Physics.Raycast(spreadRay, out RaycastHit hit, 1000f);
        ProcessHit(hit, hasHit);

        // 3. 반동 목표값 누적 (실제 적용은 ProcessRecoil에서 보간)
        float vRecoil = Random.Range(_vRecoilMin, _vRecoilMax);
        float hRecoil = Random.Range(0f, _hRecoilMax);
        float hDirection = Random.value > 0.5f ? 1f : -1f;
        _recoilTarget += new Vector2(vRecoil, hRecoil * hDirection);

        // 5. 스프레드 증가
        _currentSpread = Mathf.Min(_currentSpread + _spreadIncreasePerShot, _spreadMax);
    }

    private Ray CalculateSpreadRay() {
        Ray baseRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (_currentSpread <= 0f)
            return baseRay;

        float angle = Random.Range(0f, _currentSpread) * Mathf.Deg2Rad;
        float rotation = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        Vector3 right = Vector3.Cross(baseRay.direction, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(right, baseRay.direction).normalized;

        Vector3 offset = (right * Mathf.Cos(rotation) + up * Mathf.Sin(rotation)) * Mathf.Sin(angle);
        Vector3 spreadDir = (baseRay.direction + offset).normalized;

        return new Ray(baseRay.origin, spreadDir);
    }

    private void EmptyAmmoFire() {
        _fireBlocked = true;
        // TODO: 빈 탄창 사운드, 재장전 유도 UI 등
    }

    private void ProcessHit(RaycastHit hit, bool hasHit) {
        // 현재 장착 총기의 blueprintId
        InventoryItem currentWeapon = _ingameScene.Inventory.IsPrimaryWeaponApplyed
            ? _ingameScene.Inventory.PrimaryWeapon
            : _ingameScene.Inventory.SecondaryWeapon;
        uint weaponDbid = currentWeapon != null ? (uint)currentWeapon.item_id : 0;

        // 피격 대상 object_id 추출
        uint hitObjectId = 0xFFFFFFFF;
        if (hasHit) {
            var combatTarget = hit.collider.GetComponentInParent<ICombatTarget>();
            if (combatTarget != null)
                hitObjectId = (uint)combatTarget.GetObjectId();
        }

        // 서버에 발사 패킷 전송
        Managers.Network.udpManager.SendC2DRequestWeaponFire(
            weaponDbid,
            hasHit,
            hasHit ? hit.point : UnityEngine.Vector3.zero,
            hitObjectId
        );

        // TODO: 로컬 히트 이펙트 (탄착 파티클 등)
    }

    private void ProcessAim() {
        if (_aimTarget == null || _camera == null) return;

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f)) {
            _aimTarget.position = hit.point;
            CheckInteractable(hit);
        } else {
            _aimTarget.position = ray.GetPoint(1000f);
            _ingameScene.SetInteractState(false, null);
        }
    }

    private void CheckInteractable(RaycastHit hit) {
        InteractableGameObjectController interactable = hit.collider.GetComponentInParent<InteractableGameObjectController>();
        if (interactable != null && Vector3.Distance(transform.position, hit.collider.transform.position) <= 2f) {
            _ingameScene.SetInteractState(true, interactable);
        } else {
            _ingameScene.SetInteractState(false, null);
        }
    }

    // 네트워크 상태 프로퍼티
    public uint ObjectId => (uint)_objectId;
    public float Yaw => transform.eulerAngles.y;
    // 최종 피치(마우스+반동 합산)를 네트워크에 전송, -5도 보정
    public float Pitch => (Mathf.Clamp(_aimPitch - _recoilPitch, -80f, 90f) - 5f);
    public Vector3 Velocity => _controller != null ? _controller.velocity : Vector3.zero;
    public uint ActionState => IsShooting ? 1u : 0u;
    public uint MovementState {
        get {
            if (_controller == null) return 0;
            if (!_controller.isGrounded) return 4; // JUMP/FALL
            if (!IsMoving) return 0;              // IDLE
            return _shift ? 2u : 1u;              // RUN : WALK
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
    private void TryInteract() { _ingameScene.TryInteract(); }

    private void OnDestroy() {
        if (Managers.Instance == null) return;
        Managers.Input.RemoveKeyListener(Key.W, WDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.A, ADown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.S, SDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.D, DDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.LeftShift, ShiftDown, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.Space, TryJump, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.E, TryInteract, InputManager.KeyState.Down);

        Managers.Input.RemoveKeyListener(Key.W, WUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.A, AUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.S, SUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.D, DUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.LeftShift, ShiftUp, InputManager.KeyState.Up);
    }
}