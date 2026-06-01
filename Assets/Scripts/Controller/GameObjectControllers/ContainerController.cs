using UnityEngine;

public class ContainerController : GameObjectController {
    ContainerCollider _containerCollider;

    public void RequestOpenContainer() {
        Managers.Network.udpManager.SendC2DRequestOpenContainer((uint)_objectId);
    }

    protected void RegisterCollider(ContainerCollider collider) {
        if (collider == null)
            return;

        _containerCollider = collider;
        collider.RegisterOwner(this);
    }
}
