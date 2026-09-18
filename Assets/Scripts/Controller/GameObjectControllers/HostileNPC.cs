using UnityEngine;

// 서버가 스폰하는 전투 NPC. targetId가 가리키는 플레이어의 클라이언트가 구동하고,
// NO_TARGET이면 서버가 구동한다. 주도권과 aggro의 판정 권한은 서버에 있으며
// 여기 값은 캐시다 — 클라의 비교는 헛 요청을 줄이는 문턱일 뿐이다.
//
// 공격 대상은 언제나 주도권자다(확정). 멤버 함수가 주체일 때만 도는 구조와 합쳐지면
// 내 클라에서 이 NPC가 노리는 것은 언제나 내 로컬 플레이어이며, 남을 후보로 볼 경로가 없다
public class HostileNPC : GameObjectController, ICombatTarget {
    // -1의 비트값이 곧 0xFFFFFFFF다 — 프로젝트의 '대상 없음' 표기와 같은 값이라
    // int로 들고 있어도 그대로 실어 보낼 수 있다. 리터럴 -1을 흩지 말 것
    public const int NO_TARGET = -1;

    // 요청 잠금의 워치독. 주도권을 추측하지 않고 잠금만 푼다(귀환·행동 워치독과 같은 형태) —
    // 없으면 통보가 유실될 때 그 NPC를 그 판 내내 못 뺏는다
    private const float AGGRO_REQUEST_TIMEOUT = 3f;

    // 비주체 보간. 오포와 같은 값·같은 규칙이라 NPC만 다른 방식으로 떨리지 않는다
    private const float REMOTE_LERP_SPEED = 15f;
    private const float TELEPORT_SQR_DIST = 100f;

    protected IngameScene _ingameScene;

    private int _targetId = NO_TARGET;
    private int _aggro;

    private bool _aggroRequested;
    private float _aggroRequestTimer;

    private Vector3 _remotePosition;
    private float _remoteYaw;
    private bool _hasReceivedState;

    // 하위 객체마다 다르므로 const가 아니라 virtual이다(const는 override되지 않는다)
    protected virtual int MinAggro => 4;
    protected virtual int MaxAggro => 8;

    public int TargetId => _targetId;
    public int Aggro => _aggro;

    // 주체 판정의 유일한 출처. IsMyObjectId가 스폰 완료 여부까지 접고 있다 —
    // 0이 실재하는 objectId라 스폰 전에는 비교 자체가 성립하지 않는다
    public bool IsMine => _targetId != NO_TARGET
                       && _ingameScene != null
                       && _ingameScene.IsMyObjectId((uint)_targetId);

    // 파생 클래스는 반드시 base.Init()을 부를 것 — 씬 참조와 aggro 초기화가 여기 있고,
    // 빠뜨리면 에러 없이 그 NPC만 통째로 죽는다(ContainerController와 같은 함정)
    public override void Init() {
        _ingameScene = Managers.Scene.CurrentScene as IngameScene;
        _aggro = MinAggro;   // virtual이라 필드 초기화로는 파생값이 잡히지 않는다
    }

    public void IncreaseAggro(int amount) => SetAggro(_aggro + amount);
    public void DecreaseAggro(int amount) => SetAggro(_aggro - amount);

    // 주체 가드와 클램프가 각각 한 자리여야 갈래가 늘어도 빠지지 않는다 —
    // Increase/Decrease가 여기로 수렴하는 이유다.
    // MAX에 닿으면 주도권이 굳는 것은 의도이고, DecreaseAggro는 주도권을 내려놓는 함수가
    // 아니라 다른 플레이어의 탈취 가능성을 여는 함수다
    public void SetAggro(int value) {
        if (!IsMine) return;
        _aggro = Mathf.Clamp(value, MinAggro, MaxAggro);
    }

    // 현재 aggro를 넘는 적대 행동을 했을 때 서버에 주도권을 요청한다.
    // 이름이 '획득'이지 접근자가 아니다 — 값 읽기는 Aggro 프로퍼티.
    // 주체가 아닐 때 부르는 것이 존재 이유라 이것만 주체 가드 밖이다
    public void GetAggro(int amount) {
        if (IsMine) return;
        if (amount <= _aggro) return;
        if (_aggroRequested) return;
        if (_ingameScene == null) return;

        _aggroRequested = true;
        _aggroRequestTimer = 0f;
        _ingameScene.RequestNpcAggro((uint)_objectId, amount);
    }

    // 서버 통보 반영. 주체 가드 밖이며 _targetId를 대입하는 유일한 지점이다 —
    // 대입 지점이 늘면 '주체가 아닌데 움직이는' 상태가 남는다
    public void ApplyServerAuthority(int targetId, int aggro) {
        _targetId = targetId;
        _aggro = Mathf.Clamp(aggro, MinAggro, MaxAggro);
        _aggroRequested = false;

        OnAuthorityChanged();
    }

    // 구동 컴포넌트(내비게이션·애니메이터 등)를 껐다 켜는 자리.
    // 주도권 전환과 같은 프레임에 함께 뒤집혀야 한다
    protected virtual void OnAuthorityChanged() { }

    // 원격 상태 입력. 프로토콜 배선이 붙기 전까지 호출부가 없다
    public void ApplyRemoteState(Vector3 position, float yaw) {
        _remotePosition = position;
        _remoteYaw = yaw;

        // 첫 수신 또는 대규모 이동에서는 즉시 텔레포트(오포와 같은 규칙)
        if (!_hasReceivedState || (transform.position - _remotePosition).sqrMagnitude > TELEPORT_SQR_DIST) {
            SetPosition(_remotePosition);
            transform.rotation = Quaternion.Euler(0f, _remoteYaw, 0f);
            _hasReceivedState = true;
        }
    }

    // 비주체 표현 전용. 주체 구동은 IngameScene이 OnOwnedUpdate()로 부르며
    // 두 경로는 IsMine으로 상호 배타다 — 한쪽에 다른 쪽 일을 넣으면 주도권 전환 직후
    // NPC가 두 방향으로 움직이고, 재현이 어려운 증상으로 남는다
    void Update() {
        UpdateAggroRequestWatchdog();

        if (IsMine) return;
        ProcessRemoteMovement();
    }

    // 씬 전용 진입점
    public void OnOwnedUpdate() {
        FindTarget();
        TryAttack();
    }

    // 후보 탐색이 아니다 — 공격 대상은 언제나 주도권자(= 이 클라의 로컬 플레이어)라
    // 남는 일은 '지금 쏠 수 있는 상태인가' 확인뿐이다.
    // 사거리·시야·재탐색 주기가 하위 객체마다 달라 virtual로 둔다
    protected virtual void FindTarget() { }

    protected virtual void TryAttack() { }

    private void UpdateAggroRequestWatchdog() {
        if (!_aggroRequested) return;

        _aggroRequestTimer += Time.deltaTime;
        if (_aggroRequestTimer < AGGRO_REQUEST_TIMEOUT) return;

        _aggroRequested = false;
        Util.LogWarning($"[NPC] objectId={_objectId} 주도권 응답 미수신 ({AGGRO_REQUEST_TIMEOUT}초) — 요청 잠금 해제");
    }

    private void ProcessRemoteMovement() {
        if (!_hasReceivedState) return;

        transform.position = Vector3.Lerp(transform.position, _remotePosition, Time.deltaTime * REMOTE_LERP_SPEED);

        float smoothYaw = Mathf.LerpAngle(transform.eulerAngles.y, _remoteYaw, Time.deltaTime * REMOTE_LERP_SPEED);
        transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
    }
}
