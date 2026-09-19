using UnityEngine;

// 첫 HostileNPC 파생. 이동하지 않고 조준·사격만 하는 전투 오브젝트다 —
// MinAggro/MaxAggro는 기본값(4/8)을 쓰며, 다른 값이 필요해지면 여기서 override한다
public class TurretNPC : HostileNPC {
    // 대상의 루트 피벗이 발밑이고 PlayerObject 스케일이 2라 가슴 높이가 이 부근이다 —
    // 발밑을 겨누면 포신이 지면을 향한다
    private const float AIM_TARGET_Y_OFFSET = 1f;

    // 각도를 바꿀 대상. 프리팹에 이 경로가 없으면 BindComponent가 에러를 남기고 조준만 죽는다
    private const string POINTER_PATH = "Top/pointer";

    // 월드 소리를 내보내는 3D 소스. 가청 거리·Rolloff는 프리팹이 정하므로 코드에 값이 없다
    private const string SOUND_SOURCE_PATH = "Top";

    // 대상이 포신 위치와 겹쳤을 때 방향을 만들 수 없다
    private const float MIN_AIM_SQR_DIST = 0.0001f;

    // up 힌트를 갈아탈 문턱. 조준 방향이 이보다 수직에 가까우면 Vector3.up과 평행해진다
    private const float VERTICAL_DOT_LIMIT = 0.99f;

    [SerializeField] private float _aimSpeed = 90f;   // 초당 도

    private Transform _pointer;
    private AudioSource _soundAudio;

    // 조준 대상. _targetId에서 유도한 캐시이고 실체의 출처는 씬 레지스트리다.
    // 주도권 통보가 대상의 스폰보다 먼저 올 수 있어 null이 정상 상태이며, 그때는 AimTarget이 다시 푼다.
    // ICombatTarget 같은 인터페이스로 들지 말 것 — Unity의 == 오버로드가 인터페이스 참조에는
    // 적용되지 않아 파괴된 대상이 non-null로 읽힌다
    private Transform _targetObj;

    private Vector3 AimPoint => _targetObj.position + Vector3.up * AIM_TARGET_Y_OFFSET;

    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.Turret;

        _pointer = Util.BindComponent<Transform>(POINTER_PATH, gameObject);
        _soundAudio = Util.BindComponent<AudioSource>(SOUND_SOURCE_PATH, gameObject);
    }

    // 대상이 있는 동안 20Hz로 불린다(기반 클래스가 NO_TARGET을 걸러내고 주기를 잰다).
    // 조준은 주도권과 무관한 표현이라 남이 주도하는 포탑도 여기서 같이 돌아간다
    protected override void OnTargetedUpdate(float deltaTime) => AimTarget(deltaTime);

    // _pointer의 +z가 AimPoint를 향하게 돌린다. 즉시 대입이 아니라 _aimSpeed로 각속도를 제한하는 것이
    // 이 함수의 요점이다 — 순간 조준하는 포탑은 피할 수 없고 회전음을 붙일 근거도 사라진다.
    //
    // **루트를 돌리지 말 것** — 비주체 클라에서는 ProcessRemoteMovement가 매 프레임 루트 회전을
    // 상태 스트림 yaw로 덮어 둘이 싸운다. 그래서 대입 대상이 자식(_pointer)의 월드 회전이다.
    //
    // **deltaTime은 Time.deltaTime이 아니라 tick 간격이다**(기반 클래스가 넘긴다)
    protected void AimTarget(float deltaTime) {
        if (_targetObj == null) {
            ResolveTargetObj();      // 스폰이 늦게 도착한 대상을 여기서 줍는다
            if (_targetObj == null) return;
        }

        if (_pointer == null) return;

        Vector3 toTarget = AimPoint - _pointer.position;
        if (toTarget.sqrMagnitude < MIN_AIM_SQR_DIST) return;

        // 대상이 거의 수직에 있으면 forward와 up 힌트가 평행해져 회전이 튄다
        Vector3 dir = toTarget.normalized;
        Vector3 up = Mathf.Abs(dir.y) > VERTICAL_DOT_LIMIT ? _pointer.up : Vector3.up;

        _pointer.rotation = Quaternion.RotateTowards(
            _pointer.rotation, Quaternion.LookRotation(dir, up), _aimSpeed * deltaTime);
    }

    // 서버가 대상을 바꿨을 때의 훅. 값이 실제로 바뀔 때만 통과시키므로 부수효과를 걸 수 있다 —
    // 주도권 통보는 중복 수신될 수 있어 가드가 없으면 같은 대상에도 소리가 난다.
    // TODO: 조준 전환 사운드 — 룸 전체가 받는 통보라 2D가 아니라 3D 소스로 낼 것
    public void AimTargetChange() {
        Transform next = FindTargetObj();
        if (_targetObj == next) return;

        _targetObj = next;
    }

    // 주도권 통보가 곧 대상 변경 통보다(_targetId 대입 직후 불린다)
    protected override void OnAuthorityChanged() => AimTargetChange();

    private void ResolveTargetObj() => _targetObj = FindTargetObj();

    // 대상은 나·다른 플레이어·비플레이어 전투 오브젝트 어느 쪽도 될 수 있어 씬 조회에 맡긴다
    private Transform FindTargetObj() {
        if (TargetId == NO_TARGET || _ingameScene == null) return null;

        return _ingameScene.FindCombatObjectTransform((uint)TargetId);
    }

    // TODO: 사거리·시야 판정 — 대상은 언제나 주도권자이므로 후보 탐색이 아니라 가시성 확인이다
    protected override void FindTarget() { }

    // TODO: 연사 간격과 피격 보고 — 보고 경로는 프로토콜 설계 후에 붙는다
    protected override void TryAttack() { }
}
