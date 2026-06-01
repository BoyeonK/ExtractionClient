using UnityEngine;

public class ContainerCollider : MonoBehaviour {
    ContainerController _ownerController;
    public ContainerController OwnerController => _ownerController;

    public void RegisterOwner(ContainerController ownerController) {
        _ownerController = ownerController;
    }
}
