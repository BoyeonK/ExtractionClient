using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : GameObjectController, ICombatTarget {
    CharacterController _controller;
    
    // 카메라 및 Raycast
    Camera _camera;
    GameObject _viewPoint;
    GameObject _shotPoint;
    // 사망 연출이 같은 시점의 카메라를 새로 만들 때 화각·클리핑을 물려받는다
    public Camera ViewCamera => _camera;
    Transform _aimTarget;

    // 좌/우 어깨 시점 앵커. Pcam은 스스로 위치를 갖지 않고 매 프레임 이 중 하나를 따라가므로,
    // 카메라 위치 조정은 프리팹의 Pcam이 아니라 이 두 앵커에서 한다
    Transform _rightCamPoint;
    Transform _leftCamPoint;
    bool _isLeftAngleCam = false;
    const float CAM_ANGLE_MOVE_SPEED = 15f;   // 좌↔우 전환 보간 속도. 반동(_recoilApplySpeed)과 같은 형태·같은 값

    // 조준점이 총구보다 뒤에 있거나 붙어 있으면 수렴이 성립하지 않는다
    const float MIN_CONVERGE_DIST = 0.5f;

    // 사옹할 모델 및 애니메이션 및 Rig
    RigBuilder _rigBuilder;
    MultiAimConstraint _constraint;
    Animator _anim;

    // 상호작용
    IngameScene _ingameScene;

    // 장착 무기 정보 연동
    Transform _weaponSocketTr;
    GameObject _equippedWeaponGo;

    // 손에 든 무기 blueprint_id (0=맨손). 서버가 보는 값과 같아야 발사가 버려지지 않는다
    int _equippedWeaponId;
    public int EquippedWeaponId => _equippedWeaponId;

    // 궤적 시각화 전용 원점. 맨손이면 null이며 피격 판정에는 쓰이지 않는다(그쪽은 _shotPoint)
    Transform _muzzlePointTr;
    public Transform MuzzlePoint => _muzzlePointTr;

    // 발사 타이머
    float _fireTimer = 0f;
    float _fireInterval = 0f; // 60f / RPM

    // 월드 소리를 내보내는 3D 소스(가슴팍). 발사음·재장전음도 여기에 태울 자리다
    AudioSource _soundAudio;

    // WeaponSpec 캐시 (EquipWeapon 시 갱신)
    float _vRecoilMin, _vRecoilMax, _hRecoilMax;
    float _spreadBase, _spreadMax, _spreadIncreasePerShot, _spreadRecoveryRate;

    // 스프레드 상태
    float _currentSpread = 0f;
    // 발사 원뿔의 반각(도). 크로스헤어가 벌어진 정도를 여기서 유도한다
    public float CurrentSpread => _currentSpread;

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
    float walkSpeed = 1.5f;
    float runSpeed = 7f;
    float jumpHeight = 0.6f;
    float gravity = -9.81f;

    // 설정 슬라이더 1.0에서의 '도/픽셀'. 마우스 raw 델타(픽셀)에 곱해지는 최종 계수는
    // 슬라이더 값 × 이 상수다. 이 계수가 없던 시절엔 1픽셀 = 1도라 세로 가동 범위
    // 170도 전체가 마우스 170픽셀이었다 — 조준이 성립하지 않는 값이었다
    const float MOUSE_SENSITIVITY_DEG_PER_PIXEL = 0.1f;

    // 설정에서 매 프레임 읽는다. 캐시하면 설정 창에서 바꾼 값이 다음 매치까지 반영되지 않는다
    float MouseSensitivity => Managers.Setting.GetMouseSensitivity() * MOUSE_SENSITIVITY_DEG_PER_PIXEL;

    bool _w = false, _a = false, _s = false, _d = false, _shift = false, _jump = false;
    Vector3 _velocity;

    bool IsMoving => _w || _s || _a || _d;

    // 발소리가 나는 접지 이동 상태. 걷기·달리기 구분은 여기가 아니라 _isRunning이 한다.
    //
    // IsInputLocked를 빼면 안 된다 — 이탈·사망 중에는 ProcessMovement가 이동만 끊고 _w 등은
    // 눌린 채로 남아서, 제자리에 선 시신이 계속 걷는 소리를 낸다
    bool IsStepping => IsMoving
                       && _controller != null && _controller.isGrounded
                       && !_ingameScene.IsInputLocked;

    // 달리기는 파생값이 아니라 '상태'다 — 스태미나가 진입 조건(20 이상)과 강제 종료(0)를
    // 걸기 때문에 IsMoving && _shift로는 표현되지 않는다. 전이는 ProcessRun() 한 곳에서만 한다.
    //
    // 이동 속도·MovementState·애니메이션이 전부 이 값을 봐야 한다. _shift를 직접 보는 자리를
    // 남기면 스태미나가 0인데 달리는 속도로 움직이고, 남의 화면에도 RUN으로 나간다
    bool _isRunning = false;
    // IngameScene이 재장전의 진입·유지 조건으로 읽는다 (달리면 재장전이 성립하지 않는다)
    public bool IsRunning => _isRunning;
    // '쏘려는 의사'. ProcessFire의 발사 게이트가 이걸 본다.
    //
    // 행동(재장전·무기 교체) 중에는 쏘지 않는다 — 교체는 reliable(교체)과 unreliable(사격) 사이에
    // 순서 보장이 없어 사격이 먼저 처리되면 weapon_dbid 불일치로 조용히 버려지고,
    // 재장전은 유예 구간이 곧 모션 시간이다.
    // 매치 이탈(사망·탈출) 중에는 서버가 요청 자체를 버린다
    bool IsFireInput => Mouse.current != null && Mouse.current.leftButton.isPressed
                        && !_ingameScene.IsAnyUIOpen && !IsRunning
                        && !_ingameScene.IsActionBusy
                        && !_ingameScene.IsInputLocked;

    // '실제로 총알이 나가는 중'. 발사 모션과 ActionState가 이걸 본다.
    //
    // 탄약·무기 조건을 위의 IsFireInput에 합치지 말 것 — 그러면 Fire()에 도달하지 못해
    // EmptyAmmoFire()가 영영 불려지지 않고, 빈 탄창 딸깍 소리가 통째로 사라진다.
    // '쏘려는 의사'와 '실제로 나가는 중'은 별개이며 이 파일에서 갈리는 유일한 지점이다
    bool IsShooting => IsFireInput && !_fireBlocked
                       && _equippedWeaponId != 0
                       && CurrentMagazine != null && CurrentMagazine.quantity > 0;

    // 손에 든 슬롯의 탄창. 규칙은 IngameInventory.CurrentMagazine 한 곳에 있고 여기서는 그대로 읽는다 —
    // 무기 UI 표시도 같은 것을 쓰므로 규칙이 두 벌이 되면 쏘는 탄창과 보여주는 탄창이 갈린다.
    // 매번 다시 조회하는 형태를 유지할 것 — 재장전 응답이 InventoryItem 인스턴스를 통째로 갈아치운다
    InventoryItem CurrentMagazine => _ingameScene.Inventory.CurrentMagazine;

    public override void Init() {
        base.Init();

        Managers.Input.AddKeyListener(Key.W, WDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.A, ADown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.S, SDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.D, DDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.LeftShift, ShiftDown, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Space, TryJump, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.E, TryInteract, InputManager.KeyState.Down);
        // 키를 바꿀 때는 OnDestroy의 해제와 한 쌍으로 바꿀 것 — 한쪽만 고치면 해제가 빗나가 리스너가 남는다
        Managers.Input.AddKeyListener(Key.Q, ChangeCamAngle, InputManager.KeyState.Down);

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

        Transform shotTransform = transform.Find("ShotPoint");
        if (shotTransform != null) _shotPoint = shotTransform.gameObject;
        else Util.LogError("ShotPoint가 없어 발사 원점을 카메라로 대체한다 — PlayerObject 프리팹에 ShotPoint 필요");

        Transform soundTransform = transform.Find("SoundPoint");
        if (soundTransform != null) _soundAudio = soundTransform.GetComponent<AudioSource>();
        if (_soundAudio == null) Util.LogError("SoundPoint의 AudioSource가 없어 월드 소리가 나지 않는다 — PlayerObject 프리팹에 SoundPoint + AudioSource 필요");

        _aimTarget = transform.Find("Aim");
        _camera = _viewPoint.GetComponentInChildren<Camera>();

        // FOV는 여기서 1회만 읽는다. 매 프레임 읽는 마우스 감도와 갈리는데, 인게임 설정 창에
        // FOV 항목이 없어(매치 중 변경 금지) 진입 후 값이 바뀔 경로가 없기 때문이다
        if (_camera != null) _camera.fieldOfView = Managers.Setting.GetFov();

        // 앵커는 PlayerObject 직속이 아니라 ViewPoint의 자식이다 — 루트에서 찾으면 null이 온다.
        // 없으면 대체할 것이 없으므로 Pcam을 프리팹 저작 위치에 그대로 둔다(ShotPoint와 갈리는 지점)
        _rightCamPoint = _viewPoint.transform.Find("RightCamPoint");
        _leftCamPoint = _viewPoint.transform.Find("LeftCamPoint");
        if (_rightCamPoint == null || _leftCamPoint == null)
            Util.LogError("ViewPoint에 RightCamPoint/LeftCamPoint가 없어 카메라가 저작 위치에 고정된다 — PlayerObject 프리팹 확인 필요");

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

    // 재장전 연출 단계음. 0·1은 IngameScene의 유예 타이머가, 완료(15)는 재장전 응답이 부른다 —
    // 서버가 뿌리는 완료 통보는 당사자를 제외하고 나가므로 내 완료음은 돌아오지 않는다
    public void PlayReloadSound(uint sequenceNum) {
        Managers.Sound.PlayOneShotAt(GetReloadSound(_equippedWeaponId, sequenceNum), _soundAudio);
    }

    public void EquipWeapon(int weaponId) {
        // 인벤토리 조작마다 호출되므로 같은 무기면 파괴·재생성하지 않는다.
        // 이 경로는 _equippedWeaponGo가 살아 있으므로 _muzzlePointTr 캐시도 그대로 유효하다
        if (_equippedWeaponId == weaponId && _equippedWeaponGo != null) return;

        // 무기 GO가 사라지는 경로마다 총구 캐시를 함께 비운다.
        // 빠뜨리면 파괴된 트랜스폼을 가리킨 채 남는다
        if (_equippedWeaponGo != null) {
            Managers.Resource.Destroy(_equippedWeaponGo);
            _equippedWeaponGo = null;
        }
        _muzzlePointTr = null;

        // 아래 스펙 조회에 성공할 때만 다시 채워진다. 여기서 비우지 않으면 맨손·프리팹 미스로
        // 조기 return할 때 직전 무기의 값이 남아, 총도 없는데 ProcessFire가 발사까지 간다
        _fireInterval = 0f;

        _equippedWeaponId = weaponId;
        if (weaponId == 0) return;   // 맨손 — 총이 없으니 총구도 없다

        IngameScene scene = Managers.Scene.CurrentScene as IngameScene;
        if (!scene.WeaponPrefabCache.TryGetValue(weaponId, out GameObject weaponPrefab)) {
            Util.LogError($"무기 프리팹이 없어 맨손으로 표시된다 weaponId={weaponId} — Resources/Prefabs/Weapons에 Weapon_{weaponId}_* 필요");
            return;
        }

        _equippedWeaponGo = Object.Instantiate(weaponPrefab, _weaponSocketTr);
        _equippedWeaponGo.transform.localPosition = Vector3.zero;
        _equippedWeaponGo.transform.localRotation = Quaternion.identity;
        DisableWeaponColliders(_equippedWeaponGo);
        _muzzlePointTr = FindMuzzlePoint(_equippedWeaponGo);

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
        // ProcessMovement가 _isRunning으로 속도를 고르므로 그보다 먼저 와야 한다
        ProcessRun();
        ProcessMovement();
        // isGrounded를 갱신하는 것이 ProcessMovement의 Move()다 — 앞에 두면 한 프레임 묵은 접지를 본다
        ProcessFootstep();
        ProcessMouseLook();
        ProcessRecoil();
        ApplyViewRotation();
        // ProcessAim·ProcessFire가 카메라 위치에서 레이를 쏘므로 그보다 앞이어야 한다 —
        // 뒤에 두면 시점을 바꾼 프레임의 조준이 직전 위치 기준으로 나간다
        UpdateCameraAnchor();
        ProcessAnimation();
        // ProcessAim이 갱신하는 _aimTarget을 ProcessFire가 읽는다 — 순서가 뒤집히면
        // 발사가 직전 프레임의 조준점을 쓰게 되어 반동 중에 각도가 어긋난다
        ProcessAim();
        ProcessFire();
    }

    // 달리기 상태 전이의 유일한 지점. 스태미나 값은 IngameScene이 쥐고 여기서는 묻기만 한다.
    //
    // 진입에만 20을 요구하고 유지는 0 초과다 — 달리는 중에 20 아래로 떨어져도 0까지 계속 달린다.
    // Shift를 누른 채 스태미나가 20까지 회복되면 다시 달리기로 넘어간다(사용자 확정) —
    // 그래서 진입 조건에 '재입력' 항이 없다. 재입력을 요구하려면 _fireBlocked 같은 플래그가 따로 필요하다
    private void ProcessRun() {
        bool wantsToRun = _shift && IsMoving;

        if (_isRunning)
            _isRunning = wantsToRun && _ingameScene.HasStamina;
        else
            _isRunning = wantsToRun && _ingameScene.CanStartRunning;
    }

    private void ProcessMouseLook() {
        if (_viewPoint == null) return;
        if (_ingameScene.IsAnyUIOpen) return;
        // 사망 연출이 카메라를 가져가므로 시점 입력을 여기서 끊는다
        if (_ingameScene.IsInputLocked) return;

        if (Mouse.current != null) {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float sensitivity = MouseSensitivity;
            _aimPitch -= mouseDelta.y * sensitivity;
            _aimPitch = Mathf.Clamp(_aimPitch, -80f + _recoilPitch, 90f + _recoilPitch);
            _aimYaw += mouseDelta.x * sensitivity;
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

    // 좌각 ↔ 우각 토글. 중앙 상태는 두지 않는다(확정) — 기본값이 우각이고 두 상태만 오간다
    public void ChangeCamAngle() {
        if (_ingameScene.IsInputLocked) return;
        if (_ingameScene.IsAnyUIOpen) return;

        _isLeftAngleCam = !_isLeftAngleCam;
    }

    // 회전은 부모(ViewPoint)가, 위치는 여기가 쥔다 — 축이 갈려 있어 ApplyViewRotation과 순서를 다투지 않는다.
    // 앵커 둘 다 회전이 identity이고 기울이기는 구현 예정이 없어 rotation은 따라가지 않는다.
    // 보간은 반동과 같은 형태다(속도 × deltaTime) — 즉시 대입하면 스케일 2배에서 월드 0.8을 한 프레임에 건너뛴다
    private void UpdateCameraAnchor() {
        if (_camera == null) return;

        Transform target = _isLeftAngleCam ? _leftCamPoint : _rightCamPoint;
        if (target == null) return;

        _camera.transform.localPosition = Vector3.Lerp(
            _camera.transform.localPosition, target.localPosition, CAM_ANGLE_MOVE_SPEED * Time.deltaTime);
    }

    private void ProcessMovement() {
        if (_controller == null) return;
        if (_controller.isGrounded && _velocity.y < 0) {
            _velocity.y = -2f;
        }

        // 이탈 중에는 이동 입력만 끊고 중력은 그대로 둔다 — 공중에서 죽어도 시신이 떠 있지 않도록
        bool inputLocked = _ingameScene.IsInputLocked;

        Vector3 move = Vector3.zero;
        if (!inputLocked) {
            if (_w) move += transform.forward;
            if (_s) move -= transform.forward;
            if (_d) move += transform.right;
            if (_a) move -= transform.right;

            if (move.sqrMagnitude > 1f) move.Normalize();
        }

        // _shift가 아니라 _isRunning이다 — 스태미나가 바닥나면 Shift를 누른 채여도 걷는 속도여야 한다
        float moveSpeed = _isRunning ? runSpeed : walkSpeed;

        // 스태미나 검사·차감은 키 입력이 아니라 '실제로 뛰는' 이 자리에서 한다 —
        // TryJump()에서 깎으면 공중에서 누른 Space가 뛰지도 않고 스태미나만 가져간다.
        // 값과 판정은 IngameScene에 있고 여기서는 묻고 알리기만 한다(달리기와 같은 규칙)
        if (_jump) {
            if (_controller.isGrounded && !inputLocked && _ingameScene.CanJump) {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _ingameScene.ConsumeJumpStamina();
            }
            _jump = false;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(move * moveSpeed * Time.deltaTime + _velocity * Time.deltaTime);
    }

    // 타이머·간격·클립 선택은 공용 베이스에 있다(GameObjectController.UpdateFootstep) —
    // 여기서는 '지금 어떤 이동 상태인가'만 넘긴다. 오포와 값이 갈리지 않게 하기 위함이다
    private void ProcessFootstep() {
        UpdateFootstep(_soundAudio, IsStepping, _isRunning);
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
        // 맨손(_fireInterval == 0)이어도 여기서 빠져나가지 않는다 — _fireBlocked 갱신과
        // 스프레드 회복까지 함께 멈춰버린다. 발사만 아래 조건에서 막는다

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

        // _fireInterval은 무기를 들 때만 채워진다. 맨손이면 0이라 여기서 걸린다
        if (_fireInterval > 0f && !_fireBlocked && IsFireInput && _fireTimer >= _fireInterval) {
            _fireTimer -= _fireInterval;
            Fire();
        }
    }

    private void Fire() {
        // 1. 탄약 확인
        InventoryItem magazine = CurrentMagazine;

        if (magazine == null || magazine.quantity <= 0) {
            EmptyAmmoFire();
            return;
        }

        // 매 발사마다 서버와 동기화하지 않는다(느슨한 동기화). 클라 값은 예측이고 최종 판정은
        // 서버가 쥐므로, 장전 등 확실한 동기화 시점까지 양쪽 수치가 어긋나는 것은 정상 범위다
        magazine.quantity--;
        _ingameScene.SyncWeaponUI();

        // 2. 스프레드 적용 히트스캔
        // 자기 제외 layerMask가 없는 것은 의도다(2026-08-27 플레이 검증에서 자탄 0건 확인).
        // 자기 하위에 남아 있는 콜라이더가 CharacterController 하나뿐이고 — 무기 콜라이더는
        // 장착 시 꺼지며 로컬에는 히트박스를 만들지 않는다 — 레이가 그 캡슐 '안에서' 출발해
        // Unity가 보고하지 않는다. 세 전제 중 하나라도 깨지면(로컬 히트박스 추가, 무기 콜라이더
        // 유지, 발사 원점을 몸 밖으로 이동) 그때 전용 레이어 + layerMask가 필요해진다
        Ray fireRay = CalculateFireRay();
        bool hasHit = Physics.Raycast(fireRay, out RaycastHit hit, 1000f);
        ProcessHit(hit, hasHit);

        // 무기별 분기는 GetGunShotSound() 한 곳에 있다 — 여기서 클립 이름을 직접 만들지 말 것.
        // 빈 탄창 딸깍(EmptyAmmoFire)은 2D로 남아 있고 이쪽만 월드 소리다
        Managers.Sound.PlayOneShotAt(GetGunShotSound(_equippedWeaponId), _soundAudio);
        DrawTracer(fireRay, hit, hasHit);

        // 3. 반동 목표값 누적 (실제 적용은 ProcessRecoil에서 보간)
        float vRecoil = Random.Range(_vRecoilMin, _vRecoilMax);
        float hRecoil = Random.Range(0f, _hRecoilMax);
        float hDirection = Random.value > 0.5f ? 1f : -1f;
        _recoilTarget += new Vector2(vRecoil, hRecoil * hDirection);

        // 5. 스프레드 증가
        _currentSpread = Mathf.Min(_currentSpread + _spreadIncreasePerShot, _spreadMax);
    }

    // 총알은 카메라가 아니라 몸(_shotPoint, 가슴팍)에서 나가되, 카메라가 보고 있는 지점
    // (_aimTarget)으로 수렴한다. 카메라 축과 평행하지 않으므로 엄폐물 뒤에서는 총구 앞의
    // 벽에 맞는다 — 의도된 동작이다
    private Ray CalculateFireRay() {
        Vector3 origin = _shotPoint != null ? _shotPoint.transform.position : _camera.transform.position;
        Vector3 camForward = _camera.transform.forward;

        Vector3 toAim = _aimTarget.position - origin;
        Vector3 dir = (toAim.sqrMagnitude < MIN_CONVERGE_DIST * MIN_CONVERGE_DIST
                       || Vector3.Dot(toAim, camForward) <= 0f)
            ? camForward
            : toAim.normalized;

        if (_currentSpread <= 0f)
            return new Ray(origin, dir);

        // dir이 월드 up과 평행하면 Cross가 영벡터가 된다. 시점 피치가 ±90에 닿으므로 실제로 밟는다
        Vector3 axis = Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right;
        Vector3 right = Vector3.Cross(dir, axis).normalized;
        Vector3 up = Vector3.Cross(right, dir);

        float angle = Random.Range(0f, _currentSpread) * Mathf.Deg2Rad;
        float rotation = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        Vector3 spreadDir = dir * Mathf.Cos(angle)
                          + (right * Mathf.Cos(rotation) + up * Mathf.Sin(rotation)) * Mathf.Sin(angle);

        return new Ray(origin, spreadDir);
    }

    // 총알 궤적. 시작점이 판정선(fireRay.origin = _shotPoint, 가슴팍)과 다르게 총구다 —
    // 판정과 표현을 일부러 분리한 것이므로 맞추려 들지 말 것.
    // 빗나가도 그린다. 안 그리면 "왜 안 나갔지"가 되어 궤적을 넣은 목적이 반감된다
    // (상대 궤적은 좌표가 안 와서 생략하는데, 정보량 차이에서 오는 의도된 비대칭이다.
    //  IngameScene.HandleWeaponFireBroadcast 참조)
    private void DrawTracer(Ray fireRay, RaycastHit hit, bool hasHit) {
        if (_muzzlePointTr == null) return;   // 맨손

        BulletTracer.Play(_muzzlePointTr.position, hasHit ? hit.point : fireRay.GetPoint(1000f));
    }

    // 아래 _fireBlocked가 딸깍 소리의 중복 재생 가드를 겸한다 — 해제 조건이 마우스 재클릭뿐이라
    // 트리거 1회당 1번이 된다. 별도 쿨다운을 넣으면 같은 것을 두 곳에서 관리하게 된다
    private void EmptyAmmoFire() {
        _fireBlocked = true;
        Managers.Sound.Play("empty_gun_shot");
    }

    // 탄착 이펙트는 만들지 않기로 확정됐다(총구 화염과 같은 판단) — 빠뜨린 것으로 보고
    // 다시 추가하지 말 것. 남은 미구현은 데미지 표시뿐이다
    // TODO: 데미지 표시 미구현, 별도 작업 예정
    private void ProcessHit(RaycastHit hit, bool hasHit) {
        // '장착한' 무기가 아니라 '손에 든' 무기여야 한다. 인벤토리에서 다시 유도하면
        // 교체 확정 전 상태를 실을 수 있어, 서버가 확정해 장착시킨 값을 그대로 쓴다
        uint weaponDbid = (uint)_equippedWeaponId;

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
    }

    private void ProcessAim() {
        if (_aimTarget == null || _camera == null) return;
        // 이탈 중에는 조준·상호작용 판정을 멈춘다. 안 그러면 매 프레임 상호작용 대상이 다시 잡힌다
        if (_ingameScene.IsInputLocked) return;

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
            return _isRunning ? 2u : 1u;          // RUN : WALK
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
        Managers.Input.RemoveKeyListener(Key.Q, ChangeCamAngle, InputManager.KeyState.Down);

        Managers.Input.RemoveKeyListener(Key.W, WUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.A, AUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.S, SUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.D, DUp, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.LeftShift, ShiftUp, InputManager.KeyState.Up);
    }
}