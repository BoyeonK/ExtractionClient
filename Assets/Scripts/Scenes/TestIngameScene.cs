using UnityEngine;

public class TestIngameScene : IngameScene {
    protected override void Init() {
        SceneType = Define.Scene.TestIngame;
        base.Init();
    }

    void Start() {
        RequestSpawnMe();
    }

    void Update() {
        OnUpdate();
    }
}
