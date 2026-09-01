using UnityEngine;

public class TenerifeBusController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.TenerifeBus;
    }
}
