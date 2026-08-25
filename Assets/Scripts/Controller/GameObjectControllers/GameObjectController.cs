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
