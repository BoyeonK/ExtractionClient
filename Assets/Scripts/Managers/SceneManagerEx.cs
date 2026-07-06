using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public struct ObjectData {
    public uint ObjectId;
    public int ObjectType;
    public UnityEngine.Vector3 Position;
    public UnityEngine.Quaternion Rotation;
}

public struct PlayerSpawnData {
    public uint ObjectId;
    public int CharacterType;
    public int WeaponId;
    public UnityEngine.Vector3 Position;
    public UnityEngine.Quaternion Rotation;
}

public struct PlayerStateData {
    public uint ObjectId;
    public UnityEngine.Vector3 Position;
    public float Yaw;
    public float Pitch;
    public UnityEngine.Vector3 Velocity;
    public uint MovementState;
    public uint ActionState;
}

public class GameSceneContext {
    public List<ObjectData> ObjectDatas { get; } = new List<ObjectData>();

    private int _dataLastIndex = -1;
    private HashSet<uint> _receivedDataIndices = new HashSet<uint>();

    public void AddObjectDatas(uint index, bool isLast, List<ObjectData> objects) {
        if (_receivedDataIndices.Contains(index)) return;
        ObjectDatas.AddRange(objects);
        _receivedDataIndices.Add(index);
        if (isLast)
            _dataLastIndex = (int)index;
    }

    public bool IsComplete() {
        if (_dataLastIndex < 0) return false;
        for (uint i = 0; i <= (uint)_dataLastIndex; i++) {
            if (!_receivedDataIndices.Contains(i)) return false;
        }
        return true;
    }

    public void Clear() {
        ObjectDatas.Clear();
        _receivedDataIndices.Clear();
        _dataLastIndex = -1;
    }
}

public class SceneManagerEx {
    private enum LoadingState {
        None,
        Loading,
        Ready
    }
    
    public GameSceneContext NextSceneStaticContext { get; private set; } = new GameSceneContext();
    public GameSceneContext SceneDynamicContext { get; private set; } = new GameSceneContext();

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
        NextSceneStaticContext.Clear();
        SceneDynamicContext.Clear();
    }

    public void Clear() {
        CurrentScene.Clear();
    }
}
