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
