using UnityEngine;

public class YellowBoxContainerController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.YellowBoxContainer;
    }
}
