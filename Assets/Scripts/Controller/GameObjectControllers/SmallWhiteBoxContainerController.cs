using UnityEngine;

public class SmallWhiteBoxContainerController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.SmallWhiteBoxContainer;
    }
}
