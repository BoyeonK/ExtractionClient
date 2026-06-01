using System;

public class InteractableGameObjectController : GameObjectController {
    protected IngameScene _ingameScene;
    protected string _interactText;
    public string InteractText => _interactText;
    protected Action _onInteract;

    public override void Init() {
        _ingameScene = Managers.Scene.CurrentScene as IngameScene;
    }

    public void Interact() {
        _onInteract?.Invoke();
    }
}
