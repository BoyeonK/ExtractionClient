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
