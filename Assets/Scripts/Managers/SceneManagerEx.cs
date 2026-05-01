using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StaticObjectData {
    public uint ObjectId;
    public uint ObjectType;
    public Vector3 Position;
    public Vector3 Front;
}

public class GameSceneContext {
    public bool SpawnPointReceived { get; private set; } = false;
    public Vector3 SpawnPoint { get; private set; }

    public List<StaticObjectData> StaticObjects { get; } = new List<StaticObjectData>();

    private int _staticLastIndex = -1;
    private HashSet<uint> _receivedStaticIndices = new HashSet<uint>();

    public void SetSpawnPoint(Vector3 position) {
        if (SpawnPointReceived) return;
        SpawnPoint = position;
        SpawnPointReceived = true;
    }

    public void AddStaticObjects(uint index, bool isLast, List<StaticObjectData> objects) {
        if (_receivedStaticIndices.Contains(index)) return;
        StaticObjects.AddRange(objects);
        _receivedStaticIndices.Add(index);
        if (isLast)
            _staticLastIndex = (int)index;
    }

    public bool IsComplete() {
        if (!SpawnPointReceived) return false;
        if (_staticLastIndex < 0) return false;
        for (uint i = 0; i <= (uint)_staticLastIndex; i++) {
            if (!_receivedStaticIndices.Contains(i)) return false;
        }
        return true;
    }

    public void OnStaticObjectsSpawned() {
        SpawnPoint = Vector3.zero;
        SpawnPointReceived = false;
        StaticObjects.Clear();
        _receivedStaticIndices.Clear();
        _staticLastIndex = -1;
    }
}

public class SceneManagerEx {
    private enum LoadingState {
        None,
        Loading,
        Ready
    }
    
    public GameSceneContext NextSceneContext { get; private set; } = new GameSceneContext();

    private LoadingState _loadingState = LoadingState.None;
    private Define.Scene _nextScene = Define.Scene.Undefined;
    //private bool _sceneActiveImmidiately = false;
    private AsyncOperation _asyncLoadSceneOp;
    private float _progress = 0.0f;

    public BaseScene CurrentScene { get { return GameObject.FindAnyObjectByType<BaseScene>(); } }

    public void OnUpdate() {
        if (_loadingState != LoadingState.Loading || _asyncLoadSceneOp == null)
            return;
        _progress = _asyncLoadSceneOp.progress;
    }
    
    public void LoadScene(Define.Scene type) {
        if (_loadingState != LoadingState.None)
            return;

        Managers.Clear();
        SceneManager.LoadScene(GetSceneName(type));
    }

    string GetSceneName(Define.Scene type) {
        string name = System.Enum.GetName(typeof(Define.Scene), type);
        return name;
    }

    public void LoadSceneWithLoadingScene(Define.Scene targetScene, Define.Scene loadingScene) {
        if (_loadingState != LoadingState.None)
            return;

        _loadingState = LoadingState.Loading;
        _nextScene = targetScene;
        Managers.Clear();
        SceneManager.LoadScene(GetSceneName(loadingScene));
    }

    public void LoadSceneAsync() {
        if (_loadingState != LoadingState.Loading || _nextScene == Define.Scene.Undefined)
            return;

        _asyncLoadSceneOp = SceneManager.LoadSceneAsync(GetSceneName(_nextScene));
        _asyncLoadSceneOp.allowSceneActivation = false;
    }

    public float GetLoadingProgressRate() {
        return _progress;
    }

    public void CompleteLoadSceneAsync() {
        Managers.Clear();
        _asyncLoadSceneOp.allowSceneActivation = true;
    }

    //LoadingScene이 아닌 Scene의 초기화 완료에서 호출해야한다.
    public void ResetLoadSceneOp() {
        _asyncLoadSceneOp = null;
        _loadingState = LoadingState.None;
        _nextScene = Define.Scene.Undefined;
        _progress = 0;
        NextSceneContext = new GameSceneContext();
    }

    public void Clear() {
        CurrentScene.Clear();
    }
}
