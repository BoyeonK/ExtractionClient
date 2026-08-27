using UnityEngine;

public class SmallYellowBoxContainerController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.SmallYellowBoxContainer;
    }
}
