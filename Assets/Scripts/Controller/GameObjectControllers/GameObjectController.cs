using UnityEngine;
using static Define;

public class GameObjectController : MonoBehaviour {
	protected int _objectId;
	protected ObjectType _objectType { get; set; }

	public void SetDataFromStruct(ObjectData data) {
		SetObjectId((int)data.ObjectId);
		SetPosition(data.Position);
		SetRotation(data.Rotation);
	}

	public int GetObjectId() => _objectId;

	// 무기 프리팹에 콜라이더가 딸려 오지만 쓰이는 용도가 없다. 손에 든 채로 켜져 있으면
	// 몸 대신 총에 히트스캔이 걸리고(총은 ICombatTarget 하위라 그대로 피격 판정이 된다),
	// 발사 원점이 가슴팍이라 자기 무기에 자탄까지 걸린다.
	// 프리팹에서 지우지 않고 장착 시점에만 끄는 것은, 바닥에 놓인 무기를 집는 용도가
	// 나중에 생기면 그때 되살릴 수 있게 두는 것이다
	protected static void DisableWeaponColliders(GameObject weaponGo) {
		foreach (Collider col in weaponGo.GetComponentsInChildren<Collider>(true))
			col.enabled = false;
	}

	private const string MUZZLE_POINT_NAME = "MuzzlePoint";

	// 궤적 시각화의 시작점. 모델 하위에 중첩돼 있을 수 있어 재귀로 찾는다
	// (transform.Find는 직계 자식만 본다).
	// 못 찾으면 그 무기만 궤적이 안 보이는데 원인이 남지 않으므로 에러로 드러낸다
	protected static Transform FindMuzzlePoint(GameObject weaponGo) {
		Transform muzzle = Util.FindChild<Transform>(weaponGo, MUZZLE_POINT_NAME, recursive: true);
		if (muzzle == null)
			Util.LogError($"{weaponGo.name}에 {MUZZLE_POINT_NAME}가 없어 총알 궤적이 그려지지 않는다");

		return muzzle;
	}

	private const string DEFAULT_GUN_SHOT_SOUND = "gun_shot_1";

	// 손에 든 무기의 blueprint_id로 발사음을 고른다. 지금은 무기 3종이 모두 같은 소리라
	// 분기가 default 하나뿐이지만, **갈라낼 자리를 여기 한 곳으로 정해 두는 것이 목적이다** —
	// 호출부(로컬·오포)가 각자 클립 이름을 만들면 무기별 소리를 넣을 때 두 곳이 갈린다.
	// 모르는 id도 기본음으로 떨어진다. 새 무기가 소리 없이 조용해지는 것보다 낫다
	protected static string GetGunShotSound(int weaponId) {
		switch (weaponId) {
			default: return DEFAULT_GUN_SHOT_SOUND;
		}
	}

	// 재장전 단계별 소리. 번호가 0·1·15로 띄엄띄엄하므로 배열 인덱스가 아니라 switch다.
	// 발사음과 같은 이유로 여기 한 곳에 둔다 — 호출부(로컬·오포)가 각자 이름을 만들면
	// 무기별 소리를 넣을 때 두 곳이 갈린다.
	//
	// 모르는 번호에 null을 돌려주는 것이 중요하다 — sequenceNum은 네트워크에서 오는 값이라
	// 서버가 나중에 단계를 늘리면 여기 없는 번호가 들어온다. 조용히 넘기는 것이 맞다
	protected static string GetReloadSound(int weaponId, uint sequenceNum) {
		switch (sequenceNum) {                  // 무기별 분기가 생기면 weaponId로 한 겹 더 감싼다
			case 0:  return "m4_reload_start";
			case 1:  return "m4_reload_sequence1";
			case Define.RELOAD_SEQUENCE_COMPLETE: return "m4_reload_complete";
			default: return null;
		}
	}

	// 발소리. 상수와 타이머를 여기 두는 것은 로컬과 오포가 같은 값을 써야 하기 때문이다 —
	// 양쪽에 따로 두면 같은 동작의 발소리 간격이 조용히 갈린다.
	//
	// 타이머가 곧 연타 가드다(발사 타이머와 같은 방식: 상한 고정 + 차감). 상한에 걸려 멈춰 있으므로
	// 호출부가 아무리 자주 걷기 상태를 오가도 직전 재생으로부터 현재 간격이 지나기 전에는
	// 두 번째 소리가 나가지 않는다.
	//
	// 타이머 갱신이 재생 조건보다 먼저 와야 한다 — 멈춰 있는 동안에도 상한까지 차오르므로
	// 첫 걸음이 즉발이 된다. 순서를 뒤집으면 첫 걸음이 한 간격만큼 밀린다
	private const float WALK_FOOTSTEP_INTERVAL = 0.6f;
	private const float RUN_FOOTSTEP_INTERVAL = 0.25f;   // 초당 4번
	private float _footstepTimer = 0f;

	protected void UpdateFootstep(AudioSource source, bool isStepping, bool isRunning) {
		float interval = isRunning ? RUN_FOOTSTEP_INTERVAL : WALK_FOOTSTEP_INTERVAL;
		_footstepTimer = Mathf.Min(_footstepTimer + Time.deltaTime, interval);

		if (!isStepping || _footstepTimer < interval) return;

		_footstepTimer -= interval;
		Managers.Sound.PlayOneShotAt(
			isRunning ? "run_foot_step" : $"foot_step{Random.Range(1, 4)}", source);
	}

	public void SetObjectId(int objectId) {
		_objectId = objectId;
	}

	public void SetPosition(Vector3 position) { 
		transform.position = position;
	}

	public void SetRotation(Quaternion rotation) {
		transform.rotation = rotation;
	}

	void Start() {
		Init();
	}

	public virtual void Init() {

	}
}
