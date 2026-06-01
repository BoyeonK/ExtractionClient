using UnityEngine;

public class ContainerCollider : MonoBehaviour {
    ContainerController _ownerController;
    public ContainerController OwnerController => _ownerController;

    public void RegisterOwner(ContainerController ownerController) {
        if (ownerController == null)
            return;

        _ownerController = ownerController;
    }

    public void OnPressInteractBtn() {
        if (_ownerController == null)
            return;

        _ownerController.RequestOpenContainer();
    }
}
