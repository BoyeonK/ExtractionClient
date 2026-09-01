using UnityEngine;

public class TenerifeRedCarController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.TenerifeRedCar;
    }
}
