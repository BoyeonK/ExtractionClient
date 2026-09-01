using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class OppoPlayerController : GameObjectController, ICombatTarget {
    // 히트박스 정의. 캡슐 길이는 본과 자식 본 사이 거리에서 그대로 나오고, 반지름만
    // 여기서 정한다. 반지름은 절대값이 아니라 **골반→머리 거리에 대한 비율**이라
    // 모델 스케일이 바뀌어도 따라간다 (HB0/1/2가 서로 달라도 된다)
    private struct HitboxDef {
        public readonly string Bone;
        public readonly string ChildBone;
        public readonly float RadiusFactor;

        public HitboxDef(string bone, string childBone, float radiusFactor) {
            Bone = bone;
            ChildBone = childBone;
            RadiusFactor = radiusFactor;
        }
    }

    // 인접 캡슐이 관절에서 맞물리므로 부위 사이에 틈이 생기지 않는다.
    // 손·발은 전완·정강이 캡슐이 관절 너머까지 덮어 별도로 두지 않았다
    private static readonly HitboxDef[] HITBOX_DEFS = {
        new HitboxDef("mixamorig:Head",         "mixamorig:HeadTop_End",  0.14f),
        new HitboxDef("mixamorig:Spine",        "mixamorig:Spine2",       0.24f),
        new HitboxDef("mixamorig:Hips",         "mixamorig:Spine",        0.20f),
        new HitboxDef("mixamorig:LeftArm",      "mixamorig:LeftForeArm",  0.08f),
        new HitboxDef("mixamorig:RightArm",     "mixamorig:RightForeArm", 0.08f),
        new HitboxDef("mixamorig:LeftForeArm",  "mixamorig:LeftHand",     0.06f),
        new HitboxDef("mixamorig:RightForeArm", "mixamorig:RightHand",    0.06f),
        new HitboxDef("mixamorig:LeftUpLeg",    "mixamorig:LeftLeg",      0.12f),
        new HitboxDef("mixamorig:RightUpLeg",   "mixamorig:RightLeg",     0.12f),
        new HitboxDef("mixamorig:LeftLeg",      "mixamorig:LeftFoot",     0.09f),
        new HitboxDef("mixamorig:RightLeg",     "mixamorig:RightFoot",    0.09f),
    };

    // 사옹할 모델 및 애니메이션 및 Rig
    RigBuilder _rigBuilder;
    MultiAimConstraint _constraint;
    Animator _anim;
    Transform _aimTarget;

    //장착 무기 정보
    Transform _weaponSocketTr;
    GameObject _equippedWeaponGo;

    // D2CSpawnPlayerObject.weapon_id + D2CNotifyWeaponChanged로 추적한 현재 무기.
    // 킬 피드는 킬러의 무기를 싣지 않으므로 표기가 필요하면 이 값을 쓴다
    int _equippedWeaponId;
    public int EquippedWeaponId => _equippedWeaponId;

    // 궤적 시각화 전용 원점. 맨손이면 null
    Transform _muzzlePointTr;
    public Transform MuzzlePoint => _muzzlePointTr;

    // 월드 소리를 내보내는 3D 소스 둘. 둘 다 가슴팍이고 가청 거리만 다르다(근거리 30m / 총성 120m).
    // 로컬 PlayerController에는 GunSoundPoint가 없다 — 리스너가 자기 가슴팍 바로 위라
    // 두 소스 모두 Min Distance 안쪽이고, 감쇠 구간에 들어가질 않아 결과가 같다
    AudioSource _soundAudio;
    AudioSource _gunSoundAudio;

    Vector3 _velocity;
    float _yaw;
    float _pitch;
    uint _movementState;
    uint _actionState;

    // MovementState(서버 계약). 0=IDLE, 3은 쓰이지 않는다
    private const uint MOVEMENT_WALK = 1;
    private const uint MOVEMENT_RUN = 2;
    private const uint MOVEMENT_JUMP = 4;

    // 점프 중 걷기/달리기를 가르는 속도 문턱. PlayerController의 walkSpeed 1.5 / runSpeed 7 사이여야
    // 하므로 이동 속도를 조정하면 이 둘도 함께 볼 것 — 벗어나면 남의 발소리만 조용히 어긋난다
    private const float FOOTSTEP_MIN_SPEED = 0.5f;
    private const float FOOTSTEP_RUN_SPEED = 2f;

    float yOffset = 0.58f;

    // 보간용 필드
    Vector3 _targetPosition;
    bool _hasReceivedState = false;

    // 점프 중에는 MovementState가 JUMP로 덮여 걷기/달리기 구분이 사라진다. 그래서 속도로 가른다 —
    // 접지 여부로 발소리를 막으면 점프를 연달아 뛰는 것만으로 소리 없이 이동할 수 있게 된다.
    // 제자리 점프는 수평 속도가 0에 가까워 여기서 자연히 걸러진다
    float HorizontalSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
    bool IsAirborneMoving => _movementState == MOVEMENT_JUMP && HorizontalSpeed > FOOTSTEP_MIN_SPEED;

    // 이름을 ProcessAnimation의 isMoving/isRunning과 갈라 둔다 — 애니메이션은 점프 중을
    // 이동으로 보지 않고(공중에서는 idle 블렌드), 발소리만 위 이유로 이동으로 본다
    bool IsStepping => _movementState == MOVEMENT_WALK || _movementState == MOVEMENT_RUN
                       || IsAirborneMoving;
    bool IsRunningStep => _movementState == MOVEMENT_RUN
                          || (IsAirborneMoving && HorizontalSpeed >= FOOTSTEP_RUN_SPEED);

    public override void Init() {
        base.Init();
    }

    public void Setup(int characterType) {
        _aimTarget = transform.Find("Aim");

        Transform soundTransform = transform.Find("SoundPoint");
        if (soundTransform != null) _soundAudio = soundTransform.GetComponent<AudioSource>();
        if (_soundAudio == null) Util.LogError($"SoundPoint의 AudioSource가 없어 이 적의 소리가 나지 않는다 (objectId={_objectId}) — OppoPlayerObject 프리팹에 SoundPoint + AudioSource 필요");

        // 총성 전용 원거리 소스. 없으면 근거리 소스로 떨어뜨린다 — 30m에서 잘리더라도
        // 총성이 통째로 무음인 것보다 낫고, 로그가 원인을 남긴다
        Transform gunSoundTransform = transform.Find("GunSoundPoint");
        if (gunSoundTransform != null) _gunSoundAudio = gunSoundTransform.GetComponent<AudioSource>();
        if (_gunSoundAudio == null) {
            _gunSoundAudio = _soundAudio;
            Util.LogError($"GunSoundPoint의 AudioSource가 없어 총성이 발소리와 같은 거리에서 끊긴다 (objectId={_objectId}) — OppoPlayerObject 프리팹에 GunSoundPoint + AudioSource 필요");
        }

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

        _weaponSocketTr = transform.Find($"{modelName}/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand/WeaponSocket");

        BuildHitboxes(transform.Find(modelName));
    }

    // 히트스캔이 걸릴 유일한 대상이다. **로컬 PlayerController에는 만들지 않는다** —
    // 내 클라이언트가 나를 대상으로 레이캐스트할 일이 없고(내가 맞았다는 판정은 상대
    // 클라이언트가 자기 쪽 OppoPlayer에 대고 한다), 발사 원점이 가슴팍이라 내 팔
    // 히트박스가 내 총알을 막는다.
    // 루트 캡슐 하나로는 애니메이션을 따라가지 못해 뻗은 팔·손이 판정에서 빠진다
    private void BuildHitboxes(Transform modelRoot) {
        if (modelRoot == null) {
            Util.LogError($"모델을 찾지 못해 히트박스를 만들지 못했다 (objectId={_objectId}) — 이 적은 총에 맞지 않는다");
            return;
        }

        Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
            bones[t.name] = t;

        if (!bones.TryGetValue("mixamorig:Hips", out Transform hips)
            || !bones.TryGetValue("mixamorig:Head", out Transform head)) {
            Util.LogError($"기준 본(Hips/Head)이 없어 히트박스를 만들지 못했다 (objectId={_objectId}) — 이 적은 총에 맞지 않는다");
            return;
        }

        float refLength = Vector3.Distance(hips.position, head.position);

        int built = 0;
        foreach (HitboxDef def in HITBOX_DEFS) {
            if (!bones.TryGetValue(def.Bone, out Transform bone)) continue;
            if (!bones.TryGetValue(def.ChildBone, out Transform childBone)) continue;

            Vector3 axis = childBone.position - bone.position;
            if (axis.sqrMagnitude < 0.000001f) continue;

            // 척추·다리 본은 축이 거의 수직이라 기본 up 힌트(Vector3.up)와 평행해진다.
            // 캡슐은 축 대칭이라 roll은 아무래도 좋고, 축만 제대로 서면 된다
            Vector3 upHint = Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up;

            GameObject go = new GameObject($"Hitbox_{def.Bone}");
            go.transform.SetParent(bone, false);
            go.transform.rotation = Quaternion.LookRotation(axis, upHint);

            // 콜라이더 치수는 로컬 단위다. 본에 스케일이 걸려 있으면 나눠줘야 월드 크기가 맞는다
            float scale = go.transform.lossyScale.x;   // 균등 스케일 가정
            if (Mathf.Approximately(scale, 0f)) {
                Object.Destroy(go);
                continue;
            }

            float length = axis.magnitude / scale;

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 2;   // Z = 본이 뻗은 방향
            capsule.radius = refLength * def.RadiusFactor / scale;
            capsule.height = length;
            capsule.center = new Vector3(0f, 0f, length * 0.5f);
            // 물리 충돌은 시키지 않는다. 오포는 Rigidbody 없이 매 프레임 Lerp로 위치가
            // 강제되므로, 일반 콜라이더면 로컬 플레이어 CharacterController와 밀어내기가
            // 싸워 떨린다. 트리거는 레이캐스트에만 걸린다
            capsule.isTrigger = true;
            built++;
        }

        // OPTION: 히트박스가 매 프레임 움직이는 static 콜라이더라 물리 트리가 재구축된다.
        //         부담이 보이면 루트에 isKinematic Rigidbody를 붙여 moving body로 만들 것
        if (built == 0)
            Util.LogError($"히트박스가 하나도 만들어지지 않았다 (objectId={_objectId}) — 이 적은 총에 맞지 않는다");
    }

    public void EquipWeapon(int weaponId) {
        // 무기 GO가 사라지는 경로마다 총구 캐시를 함께 비운다.
        // 빠뜨리면 파괴된 트랜스폼을 가리킨 채 남는다
        if (_equippedWeaponGo != null) {
            Managers.Resource.Destroy(_equippedWeaponGo);
            _equippedWeaponGo = null;
        }
        _muzzlePointTr = null;

        _equippedWeaponId = weaponId;
        if (weaponId == 0) return;   // 맨손 — 총이 없으니 총구도 없다

        IngameScene scene = Managers.Scene.CurrentScene as IngameScene;
        if (!scene.WeaponPrefabCache.TryGetValue(weaponId, out GameObject weaponPrefab)) {
            Util.LogError($"무기 프리팹이 없어 맨손으로 표시된다 weaponId={weaponId} (objectId={_objectId}) — Resources/Prefabs/Weapons에 Weapon_{weaponId}_* 필요");
            return;
        }

        _equippedWeaponGo = Object.Instantiate(weaponPrefab, _weaponSocketTr);
        _equippedWeaponGo.transform.localPosition = Vector3.zero;
        _equippedWeaponGo.transform.localRotation = Quaternion.identity;
        DisableWeaponColliders(_equippedWeaponGo);
        _muzzlePointTr = FindMuzzlePoint(_equippedWeaponGo);
    }

    // 발사음. 무기별 분기는 공용 헬퍼(GetGunShotSound)에 있고 여기서는 자기 무기 id만 넘긴다 —
    // 로컬 PlayerController.Fire()와 같은 자리를 쓰므로 무기별 소리를 넣을 때 두 곳이 갈리지 않는다.
    //
    // 무기 id의 출처는 D2CSpawnPlayerObject.weapon_id + D2CNotifyWeaponChanged로 추적한
    // _equippedWeaponId다. 발사 브로드캐스트에는 무기가 실려 오지 않는다
    public void PlayFireSound() {
        Managers.Sound.PlayOneShotAt(GetGunShotSound(_equippedWeaponId), _gunSoundAudio);
    }

    // 재장전 연출 단계음. D2CNotifyReloadSequence를 받을 때마다 그 단계만 재생한다 —
    // 직전 단계가 도착했는지 따지지 않는다(unreliable이라 통째로 빠질 수 있다는 것이 계약이다)
    public void PlayReloadSound(uint sequenceNum) {
        Managers.Sound.PlayOneShotAt(GetReloadSound(_equippedWeaponId, sequenceNum), _soundAudio);
    }

    public void ApplyState(PlayerStateData data) {
        _targetPosition = data.Position;
        _yaw = data.Yaw;
        _pitch = data.Pitch;
        _velocity = data.Velocity;
        _movementState = data.MovementState;
        _actionState = data.ActionState;

        // 첫 수신 또는 대규모 이동(리스폰 등)시 즉시 텔레포트
        if (!_hasReceivedState || (transform.position - _targetPosition).sqrMagnitude > 100f) {
            SetPosition(_targetPosition);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _hasReceivedState = true;
        }
    }

    void Update() {
        ProcessMovement();
        // 위치가 갱신된 뒤에 울려야 한다 — 소스가 자식이라 이 순서면 갱신된 자리에서 난다
        ProcessFootstep();
        ProcessAnimation();
        ProcessAim();
    }

    // 타이머·간격·클립 선택은 공용 베이스에 있다(GameObjectController.UpdateFootstep) —
    // 로컬 플레이어와 같은 것을 쓰므로 내가 듣는 내 발소리와 남의 발소리가 같은 리듬이다
    private void ProcessFootstep() {
        if (!_hasReceivedState) return;

        UpdateFootstep(_soundAudio, IsStepping, IsRunningStep);
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

        bool isMoving = _movementState == MOVEMENT_WALK || _movementState == MOVEMENT_RUN;
        bool isRunning = _movementState == MOVEMENT_RUN;

        Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 localVel = Quaternion.Inverse(yawRot) * _velocity;

        float refSpeed = isRunning ? 3.5f : 1f;
        float moveX = Mathf.Clamp(localVel.x / refSpeed, -1f, 1f);
        float moveY = Mathf.Clamp(localVel.z / refSpeed, -1f, 1f);

        float speedMult = isRunning ? 2f : 1f;

        _anim.SetFloat("MoveX", moveX * speedMult, 0.1f, Time.deltaTime);
        _anim.SetFloat("MoveY", moveY * speedMult, 0.1f, Time.deltaTime);
        _anim.SetFloat("MovingSpeed", isMoving ? 1f : 0f, 0.1f, Time.deltaTime);
        _anim.SetBool("IsShooting", _actionState == 1);
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