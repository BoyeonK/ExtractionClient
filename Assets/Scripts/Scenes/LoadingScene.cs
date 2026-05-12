using UnityEngine;
using UnityEngine.InputSystem;

public class LoadingScene : BaseScene {
    LoadingUI1 _ui;

    bool sceneLoadFlag = false;
    bool staticObjectsLoadFlag = false;
    float progress = 0.0f;

    // SpawnPoint가 수신되었고, StaticObjects가 0~lastIndex 모두 수신된 경우 씬 전환 준비 완료
    public void TryCompleteBlueprint() {
        if (Managers.Scene.NextSceneStaticContext.IsComplete())
            staticObjectsLoadFlag = true;
    }

    protected override void Init() {
        SceneType = Define.Scene.LoadingScene;
        _ui = FindAnyObjectByType<LoadingUI1>();
    }

    // Init()은 Awake단에서 실행된다. Awake단에서는 Scene의 구분이 불명확하므로
    // SceneManager를 다루는 작업은 Start단에서 하는 것을 권장한다.
    void Start() {
        Managers.Scene.LoadSceneAsync();
    }

    void Update() {
        if (sceneLoadFlag == false) {
            float currentRate = Managers.Scene.GetLoadingProgressRate();

            progress = Mathf.MoveTowards(progress, currentRate, Time.deltaTime);

            if (currentRate >= 0.9f) {
                sceneLoadFlag = true;
                Util.Log("blueprint 요청 전송");
                Managers.Network.udpManager.SendC2DRequestBlueprint();
            }
        }
        else if (staticObjectsLoadFlag == true) {
            Managers.Scene.CompleteLoadSceneAsync();
        }
    }
}