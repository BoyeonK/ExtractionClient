using UnityEngine;

public class IngameScene : BaseScene {
    private bool _operationFlag = false;

    private bool _isGetResponseSpawnMe = false;
    private Vector3 _spawnPoint;
    private int _characterType = -1;

    protected override void Init() {
        base.Init();
        foreach (ObjectData data in Managers.Scene.NextSceneStaticContext.ObjectDatas)
            Managers.Resource.InstantiateFromObjectDataStruct(data);
        
        Managers.Scene.ResetLoadSceneOp();
    }

    public void TryCompleteSpawnMe() {
        if (_operationFlag) return;
        if (_isGetResponseSpawnMe && Managers.Scene.SceneDynamicContext.IsComplete())
            SpawnMeAndStartGame();
    }

    public void HandleSpawnSpot(Vector3 spawnPoint, int characterType) {
        _spawnPoint = spawnPoint;
        _characterType = characterType;
        _isGetResponseSpawnMe = true;
        TryCompleteSpawnMe();
    }

    private void SpawnMeAndStartGame() {
        // 충분한 컨텍스트가 모였으므로 플레이어 캐릭터와 동적 object들을 생성
        _operationFlag = true;
        Managers.Scene.SceneDynamicContext.Clear();
        /*
        foreach (ObjectData data in Managers.Scene.SceneDynamicContext.ObjectDatas)
            Managers.Resource.InstantiateFromObjectDataStruct(data);
        */
    }

    protected void RequestSpawnMe() {
        Managers.Network.udpManager.SendC2DRequestSpawnMe();
    }

    protected void OnUpdate() {
        if (_operationFlag == false)
            return;
    }
}
