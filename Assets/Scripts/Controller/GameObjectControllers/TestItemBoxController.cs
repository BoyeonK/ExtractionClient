using UnityEngine;

public class TestItemBoxController : ContainerController {
    private const string COLLIDER_PATH = "Shape";

    public override void Init() {
        _objectType = Define.ObjectType.TestItemBox;

        ContainerCollider containerCollider = Util.BindComponent<ContainerCollider>(COLLIDER_PATH, this.gameObject);
        RegisterCollider(containerCollider);
    }
}
