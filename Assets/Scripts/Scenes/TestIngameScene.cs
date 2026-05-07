using UnityEngine;

public class TestIngameScene : BaseScene {
    bool operationFlag = false;

    protected override void Init() {
        base.Init();
        SceneType = Define.Scene.TestIngame;

        foreach (ObjectData data in Managers.Scene.NextSceneContext.StaticObjects)
            Managers.Resource.InstantiateFromObjectDataStruct(data);
        
        Managers.Scene.ResetLoadSceneOp();
    }

    void Start() {
        Managers.Network.udpManager.SendC2DRequestSpawnMe();
    }

    void Update() {
        if (operationFlag == false)
            return;
    }
}
