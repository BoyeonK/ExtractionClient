using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class OppoPlayerController : GameObjectController {
    // 사옹할 모델 및 애니메이션 및 Rig
    RigBuilder _rigBuilder;
    MultiAimConstraint _constraint;
    Animator _anim;
    Transform _aimTarget;

    Vector3 _velocity;
    float _yaw;
    float _pitch;
    uint _movementState;

    float yOffset = 0.58f;

    // 보간용 필드
    Vector3 _targetPosition;
    bool _hasReceivedState = false;

    public override void Init() {
        base.Init();
    }

    public void Setup(int characterType) {
        _aimTarget = transform.Find("Aim");

        string modelName = $"HB{characterType}OppoPlayer";
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
    }

    public void ApplyState(PlayerStateData data) {
        _targetPosition = data.Position;
        _yaw = data.Yaw;
        _pitch = data.Pitch;
        _velocity = data.Velocity;
        _movementState = data.MovementState;

        // 첫 수신 또는 대규모 이동(리스폰 등)시 즉시 텔레포트
        if (!_hasReceivedState || (transform.position - _targetPosition).sqrMagnitude > 100f) {
            SetPosition(_targetPosition);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _hasReceivedState = true;
        }
    }

    void Update() {
        ProcessMovement();
        ProcessAnimation(); 
        ProcessAim();
    }

    private void ProcessMovement() {
        if (!_hasReceivedState) return;

        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * 15f);

        float currentYaw = transform.eulerAngles.y;
        float smoothYaw = Mathf.LerpAngle(currentYaw, _yaw, Time.deltaTime * 15f);
        transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
    }

    private void ProcessAnimation() {
        if (_anim == null) return;

        bool isMoving = _movementState == 1 || _movementState == 2;
        bool isRunning = _movementState == 2;

        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 localVel = Quaternion.Inverse(yawRot) * _velocity;

        float refSpeed = isRunning ? 3.5f : 1f;
        float moveX = Mathf.Clamp(localVel.x / refSpeed, -1f, 1f);
        float moveY = Mathf.Clamp(localVel.z / refSpeed, -1f, 1f);

        float speedMult = isRunning ? 2f : 1f;

        _anim.SetFloat("MoveX", moveX * speedMult, 0.1f, Time.deltaTime);
        _anim.SetFloat("MoveY", moveY * speedMult, 0.1f, Time.deltaTime);
        _anim.SetFloat("MovingSpeed", isMoving ? 1f : 0f, 0.1f, Time.deltaTime);
        _anim.SetBool("IsShooting", false);
    }

    private void ProcessAim() {
        if (_aimTarget == null) return;

        float pitchRad = _pitch * Mathf.Deg2Rad;

        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 forward = yawRot * Vector3.forward;

        Vector3 aimDir = forward * Mathf.Cos(pitchRad) + Vector3.up * Mathf.Sin(-pitchRad);

        _aimTarget.position = transform.position + Vector3.up * yOffset + aimDir * 100f;
    }


    private void OnDestroy() {

    }
}