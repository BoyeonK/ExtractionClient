using UnityEngine;

public class ContainerController : InteractableGameObjectController {

    public override void Init() {
        base.Init();
        _onInteract += RequestOpenContainer;
    }

    public void RequestOpenContainer() {
        _ingameScene.RequestOpenContainer((uint)_objectId);
    }
}
