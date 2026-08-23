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

public enum MatchExitReason { Dead, Recalled, ConnectionLost }

// 매치 종료 결과 스냅샷. IngameScene.CompleteMatchExit()이 채우고 결과 씬이 소비한다.
// 인벤토리는 클라이언트 로컬 상태 기준이라 서버와 어긋날 수 있으나 표시용으로 감수한다.
// 같은 아이템 목록이라도 사유별 의미가 다르다 — Recalled=반출 확정, Dead·ConnectionLost=잃은 것
public struct GameResult {
    public MatchExitReason ExitReason;
    public InventoryItem[] InventorySlots;   // 빈 슬롯은 null. 슬롯 배치를 유지한다 (탄창은 제외)
    public InventoryItem PrimaryWeapon;
    public InventoryItem SecondaryWeapon;
    public InventoryItem Armor;
    public int PlayerKillCount;
    public int ObjectKillCount;
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

    // null이면 결과 없음 — default(GameResult)가 유효한 값처럼 보이는 것을 막는다.
    // ResetLoadSceneOp()에서 지우지 않는다. 그 함수는 결과 씬 진입 초기화에서도 불리므로
    // 소비 전에 날아간다. 제거는 소비자의 ClearGameResult() 또는 다음 매치 종료의 덮어쓰기로 한다
    public GameResult? LastGameResult { get; private set; }

    public void SetGameResult(GameResult result) {
        LastGameResult = result;
    }

    public void ClearGameResult() {
        LastGameResult = null;
    }

    // LobbyScene 진입이 GameResultScene 경유(true)인지 게임 최초 시작(false)인지 구분한다.
    // ResetLoadSceneOp()에서 지우지 않는다 — 로비 초기화에서 소비 전에 날아간다.
    // 소비자인 LobbyScene.Init()이 읽은 뒤 직접 false로 리셋한다
    public bool IsReturnFromGameResult { get; set; } = false;

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
