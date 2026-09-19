using UnityEngine;

// 조준 동작 실험용 컴포넌트. **HostileNPC 계층이 아니고 IngameScene도 참조하지 않는다** —
// 서버 없이 도는 테스트 씬에서 포신 회전만 보기 위한 것이라 objectId·aggro·주도권이 전부 없다.
// 여기서 맞춘 회전 코드는 TurretNPC.AimTarget()으로 옮겨야 하므로 실제 포탑과 같은 형태를 유지할 것
public class TestTurretNPC : MonoBehaviour {
    // TurretNPC와 같은 값이어야 한다 — 여기서 조정했으면 그쪽도 함께 고칠 것.
    // 대상의 루트 피벗이 발밑이고 PlayerObject 스케일이 2라 가슴 높이가 이 부근이다
    private const float AIM_TARGET_Y_OFFSET = 1f;

    // 각도를 바꿀 대상. 실제 포탑도 프리팹 구조가 정해지면 같은 자리를 잡는다
    private const string POINTER_PATH = "Top/pointer";

    // 서버 통보를 대신하는 실험용 대상. 씬에 놓인 이름으로 찾는다
    private const string AIM_TARGET_NAME = "TestAimTarget";

    // 조준 갱신 주기(20Hz). 144Hz에서 매 프레임 돌리면 NPC 대수만큼 헛 계산이 쌓인다.
    // **FixedUpdate로 대신하지 말 것** — Time.fixedDeltaTime은 물리 전체의 주기라
    // 여기에 맞춰 늘리면 CharacterController 이동과 히트스캔이 먼저 망가진다
    private const float AIM_TICK_INTERVAL = 0.05f;

    // 대상이 포신 위치와 겹쳤을 때 방향을 만들 수 없다
    private const float MIN_AIM_SQR_DIST = 0.0001f;

    // up 힌트를 갈아탈 문턱. 조준 방향이 이보다 수직에 가까우면 Vector3.up과 평행해진다
    private const float VERTICAL_DOT_LIMIT = 0.99f;

    [SerializeField] private float _aimSpeed = 90f;   // 초당 도

    private Transform _pointer;
    private float _aimTickTimer;

    // TurretNPC._targetObj와 같은 역할(조준 대상 캐시).
    // Transform으로 드는 것이 중요하다 — 인터페이스로 들면 Unity의 == 오버로드가 적용되지 않아
    // 파괴된 대상이 non-null로 읽힌다
    private Transform _targetObj;

    private Vector3 AimPoint => _targetObj.position + Vector3.up * AIM_TARGET_Y_OFFSET;

    // 실제 포탑은 주도권 통보로 대상을 받지만 여기는 통보가 없어 씬에서 이름으로 집는다.
    // 대입이 아니라 AimTargetChange를 거치는 것은 실제와 같은 경로를 타게 하려는 것이다
    void Start() {
        _pointer = Util.BindComponent<Transform>(POINTER_PATH, gameObject);

        // 같은 프레임에 스폰된 NPC들이 같은 프레임에 몰려 tick하지 않도록 위상을 흩는다 —
        // 정적 오브젝트는 매치 시작에 한꺼번에 스폰되므로 초기값을 0으로 두면 그대로 겹친다
        _aimTickTimer = Random.Range(0f, AIM_TICK_INTERVAL);

        GameObject targetGo = GameObject.Find(AIM_TARGET_NAME);
        if (targetGo == null) {
            // 비활성 오브젝트는 GameObject.Find로 찾히지 않는다
            Util.LogError($"씬에 {AIM_TARGET_NAME}가 없어 조준할 대상이 없다");
            return;
        }

        AimTargetChange(targetGo.transform);
    }

    // 실제 포탑의 게이트는 targetId != NO_TARGET이고(기반 클래스가 검사한다) 여기서는 대상 참조가 그 역할을 한다.
    //
    // 타이머는 발소리·발사 타이머와 같은 형태다(Mathf.Min 상한 + -= 차감) — 차감이라 남은 시간이
    // 다음 tick으로 넘어가 평균 주기가 정확히 20Hz로 유지되고, 상한이 프레임 저하 시 빚이
    // 무한히 쌓이는 것을 막는다(그 구간에서는 tick이 늦어지는 만큼 조준이 느려진다)
    void Update() {
        if (_targetObj == null || _pointer == null) return;

        _aimTickTimer = Mathf.Min(_aimTickTimer + Time.deltaTime, AIM_TICK_INTERVAL * 2f);
        if (_aimTickTimer < AIM_TICK_INTERVAL) return;

        _aimTickTimer -= AIM_TICK_INTERVAL;
        AimTarget(AIM_TICK_INTERVAL);
    }

    // _pointer의 +z가 AimPoint를 향하게 돌린다. 즉시 대입이 아니라 _aimSpeed로 각속도를 제한하는 것이
    // 이 함수의 요점이다 — 순간 조준하는 포탑은 피할 수 없고 회전음을 붙일 근거도 사라진다.
    // **루트를 돌리지 말 것** — 실제 포탑에서는 비주체 클라의 HostileNPC.ProcessRemoteMovement가
    // 매 프레임 루트 회전을 상태 스트림 yaw로 덮어 둘이 싸운다
    //
    // **Time.deltaTime이 아니라 tick 간격을 받는다** — 프레임마다 돌지 않으므로 Time.deltaTime을 쓰면
    // 프레임률이 높을수록 조준이 느려진다(144Hz에서 약 1/7 속도)
    private void AimTarget(float deltaTime) {
        Vector3 toTarget = AimPoint - _pointer.position;
        if (toTarget.sqrMagnitude < MIN_AIM_SQR_DIST) return;

        // 대상이 거의 수직에 있으면 forward와 up 힌트가 평행해져 회전이 튄다
        Vector3 dir = toTarget.normalized;
        Vector3 up = Mathf.Abs(dir.y) > VERTICAL_DOT_LIMIT ? _pointer.up : Vector3.up;

        _pointer.rotation = Quaternion.RotateTowards(
            _pointer.rotation, Quaternion.LookRotation(dir, up), _aimSpeed * deltaTime);
    }

    // TurretNPC.AimTargetChange와 같은 형태. 값이 실제로 바뀔 때만 통과시키므로 부수효과를 걸 수 있다
    // (실제 포탑은 인자 없이 _targetId에서 대상을 푼다 — 여기는 씬 레지스트리가 없어 참조를 직접 받는다)
    // TODO: 조준 전환 사운드
    public void AimTargetChange(Transform targetObj) {
        if (_targetObj == targetObj) return;

        _targetObj = targetObj;
    }
}
