using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class OppoPlayerController : GameObjectController {
    CharacterController _controller;
    
    // 사옹할 모델 및 애니메이션 및 Rig
    RigBuilder _rigBuilder;
    MultiAimConstraint _constraint;
    Animator _anim;
    
    Vector3 _velocity;
    float _yaw;
    float _pitch;
    uint _movementState;

    public override void Init() {
        base.Init();
    }

    public void Setup(int characterType) {
        /*
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
        */
    }

    public void ApplyState(PlayerStateData data) {
        SetPosition(data.Position);
        _yaw = data.Yaw;
        _pitch = data.Pitch;
        _velocity = data.Velocity;
        _movementState = data.MovementState;
    }

    void Update() {
        ProcessMovement();
        ProcessAnimation(); 
        ProcessAim();
    }

    private void ProcessMovement() {

    }

    private void ProcessAnimation() {

    }

    private void ProcessAim() {

    }


    private void OnDestroy() {

    }
}