using UnityEngine;
using static Define;

public class GameObjectController : MonoBehaviour {
	protected int _objectId;
	protected ObjectType _objectType { get; set; }

	public void SetObjectId(int objectId) {
		_objectId = objectId;
	}

	public void SetPositionVector(Vector3 position) { 
		transform.position = position;
	}

	void Start() {
		Init();
	}

	public virtual void Init() {

	}
}
