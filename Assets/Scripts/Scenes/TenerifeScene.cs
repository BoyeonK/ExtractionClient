using UnityEngine;

public class TenerifeScene : IngameScene {
    protected override void Init() {
        SceneType = Define.Scene.TenerifeScene;
        base.Init();
    }

    void Start() {
        RequestSpawnMe();
    }

    void Update() {
        OnUpdate();
    }
}
