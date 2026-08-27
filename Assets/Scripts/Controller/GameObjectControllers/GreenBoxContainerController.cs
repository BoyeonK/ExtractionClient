using UnityEngine;

public class GreenBoxContainerController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.GreenBoxContainer;
    }
}
