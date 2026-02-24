using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

//이 객체가 사용하는 모든 함수는, 유니티의 메서드를 사용해야 하므로 메인스레드에서 실행만을 전제로 만들어졌음.
//동기화 기법을 사용하지 않을 예정.
public class SceneManagerEx {
    private enum LoadingState {
        None,
        Loading,
        Ready
    }
    
    private LoadingState _loadingState = LoadingState.None;
    private Define.Scene _nextScene = Define.Scene.Undefined;
    //private bool _sceneActiveImmidiately = false;
    private AsyncOperation _asyncLoadSceneOp;
    private float _progress = 0.0f;

    public bool _isWinner = false;
    public bool _isScoreResult = true;
    public int _winnerIdx = -1;
    public List<string> _playerIds = new List<string>();
    public List<int> _scores = new List<int>();

    private readonly object _loadingProgressLock = new object();
    List<int> _loadingProgress = Enumerable.Repeat(0, 4).ToList();

    public BaseScene CurrentScene { get { return GameObject.FindFirstObjectByType<BaseScene>(); } }

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

    //LoadingScene이 아닌 Scene의 초기화 과정에서 호출해야함.
    public void ResetLoadSceneOp() {
        _asyncLoadSceneOp = null;
        _loadingState = LoadingState.None;
        _nextScene = Define.Scene.Undefined;
        _progress = 0;
    }

    public BaseScene GetCurrentSceneComponent() {
        GameObject go = GameObject.Find("GameScene");
        if (go == null) return null;
        return go.GetComponent<BaseScene>();
    }

    public void RenewLoadingProgress(int playerIdx, int loadingProgress) {
        lock (_loadingProgressLock) {
            _loadingProgress[playerIdx] = loadingProgress;
        }
    }

    public void Clear() {
        CurrentScene.Clear();
    }
}
