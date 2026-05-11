using UnityEngine;

public class IngameScene : BaseScene {
    protected bool operationFlag = false;

    protected override void Init() {
        base.Init();
        foreach (ObjectData data in Managers.Scene.NextSceneContext.StaticObjects)
            Managers.Resource.InstantiateFromObjectDataStruct(data);
        
        Managers.Scene.ResetLoadSceneOp();
    }

    protected void RequestSpawnMe() {
        Managers.Network.udpManager.SendC2DRequestSpawnMe();
    }

    protected void OnUpdate() {
        if (operationFlag == false)
            return;
    }
}
