using UnityEngine;

public class TestIngame : BaseScene {
    protected override void Init() {
        base.Init();
        SceneType = Define.Scene.TestIngame;
        Managers.Scene.ResetLoadSceneOp();
    }
}
