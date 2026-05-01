using UnityEngine;
using UnityEngine.InputSystem;

public class LoadingScene : BaseScene {
    LoadingUI1 _ui;

    bool sceneLoadFlag = false;
    bool staticObjectsLoadFlag = false;
    float progress = 0.0f;

    protected override void Init() {
        SceneType = Define.Scene.LoadingScene1;
        _ui = FindAnyObjectByType<LoadingUI1>();
    }

    // Init()은 Awake단에서 실행된다. Awake단에서는 Scene의 구분이 불명확하므로
    // SceneManager를 다루는 작업은 Start단에서 하는 것을 권장한다.
    void Start() {
        Managers.Scene.LoadSceneAsync();
    }

    void Update() {
        const float epsilon = 0.01f;
        const float progressThreshold = 0.05f;
        if (sceneLoadFlag == false) {
            float currentRate = Managers.Scene.GetLoadingProgressRate();

            while (currentRate - epsilon >= progress + progressThreshold) {
                progress += progressThreshold;
            }

            if (progress >= 0.9f - epsilon) {
                sceneLoadFlag = true;
                Managers.Network.udpManager.SendC2DRequestBlueprint();
            }

            //로딩 UI 업데이트
        }
        else if (staticObjectsLoadFlag == true) {
            Managers.Scene.CompleteLoadSceneAsync();
        }
    }
}