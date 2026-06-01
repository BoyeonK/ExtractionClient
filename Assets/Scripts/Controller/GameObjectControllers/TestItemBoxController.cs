using UnityEngine;

public class TestItemBoxController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.TestItemBox;
    }
}
