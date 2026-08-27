using UnityEngine;

public class PlayerLootController : ContainerController {
    public override void Init() {
        base.Init();
        _objectType = Define.ObjectType.PlayerLoot;
    }
}
