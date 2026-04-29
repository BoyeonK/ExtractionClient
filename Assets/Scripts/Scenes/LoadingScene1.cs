using UnityEngine;
using UnityEngine.InputSystem;

public class LoadingScene1 : BaseScene {
    LoadingUI1 _ui;

    bool sceneLoadFlag = false;
    bool staticObjectsLoadFlag = false;
    float progress = 0.0f;

    protected override void Init() {
        SceneType = Define.Scene.LoadingScene1;
        _ui = FindAnyObjectByType<LoadingUI1>();

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