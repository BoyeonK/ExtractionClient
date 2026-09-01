using UnityEngine;

public class TenerifeBlueCarController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.TenerifeBlueCar;
    }
}
