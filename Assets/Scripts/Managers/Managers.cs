using System;
using System.Collections.Generic;
using UnityEngine;

public class Managers : MonoBehaviour {
    private static Managers s_instance;
    private static bool s_isQuitting = false;

    public static Managers Instance {
        get {
            if (s_isQuitting) return null;

            Init();
            return s_instance;
        }
    }

    InputManager _input = new InputManager();
    public static InputManager Input { get { return Instance._input; } }

    NetworkManager _network = new NetworkManager();
    public static NetworkManager Network { get { return Instance._network; } }

    PoolManager _pool = new PoolManager();
    public static PoolManager Pool { get { return Instance._pool; } }

    ResourceManager _resource = new ResourceManager();
    public static ResourceManager Resource { get { return Instance._resource; } }

    UIManager _ui = new UIManager();
    public static UIManager UI { get { return Instance._ui; } }

    SceneManagerEx _scene = new SceneManagerEx();
    public static SceneManagerEx Scene { get { return Instance._scene; } }

    SoundManager _sound = new SoundManager();
    public static SoundManager Sound { get { return Instance._sound; } }

    SettingManager _setting = new SettingManager();
    public static SettingManager Setting { get { return Instance._setting; } }

    private static Queue<Action> _jobQueue = new Queue<Action>();
    private static List<Action> _actions = new List<Action>();
    private static readonly object _jobQueuelock = new object();
    public static void ExecuteAtMainThread(Action job) {
        lock (_jobQueuelock) {
            _jobQueue.Enqueue(job);
        }
    }

    void Update() {
        _input.OnUpdate();
        //_network.OnUpdate();
        _scene.OnUpdate();

        lock (_jobQueuelock) {
            while (_jobQueue.Count > 0) {
                _actions.Add(_jobQueue.Dequeue());
            }
        }

        foreach (Action act in _actions) {
            act?.Invoke();
        }
        _actions.Clear();
    }

    void Awake() {
        if (s_instance == null) {
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            InitSubManagers();
        }
        else if (s_instance != this) {
            Destroy(gameObject);
        }
    }

    static void Init() {
        if (s_instance == null) {
            GameObject go = GameObject.Find("@Managers");
            if (go == null) {
                go = new GameObject { name = "@Managers" };
                go.AddComponent<Managers>();
            }
            DontDestroyOnLoad(go);

            if (s_instance == null) {
                s_instance = go.GetComponent<Managers>();
                s_instance.InitSubManagers();
            }
        }
    }

    void InitSubManagers() {
        _pool.Init();
        _sound.Init();
        _ui.Init();
        _setting.Init();
    }

    public static void Clear() {
        Sound.Clear();
        Input.Clear();
        Scene.Clear();
        UI.Clear();
        Pool.Clear();
    }

    // 애플리케이션이 종료될 때 싱글톤 인스턴스가 파괴되는 것을 방지하기 위해 플래그를 설정
    private void OnApplicationQuit() {
        s_isQuitting = true;
    }
}
