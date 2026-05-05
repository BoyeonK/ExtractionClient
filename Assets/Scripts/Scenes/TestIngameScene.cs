using UnityEngine;

public class TestIngameScene : BaseScene {
    protected override void Init() {
        base.Init();
        SceneType = Define.Scene.TestIngame;
        Managers.Scene.ResetLoadSceneOp();
        
        
    }
}
