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
            if (Managers.Scene.GetLoadingProgressRate() - epsilon >= progress + progressThreshold) {
                //progress를 progressThreshold의 배수로서 표현
            }

            if (progress >= 0.9f - epsilon) {
                sceneLoadFlag = true;
                //C2DRequestBlueprint 전송
                //Managers.Network.udpManager.SendC2DRequestBlueprint();
            }
        }
        else if (staticObjectsLoadFlag == true) {
            // C2DRequestBlueprint에 대한 응답이 왔는지 확인
            // 왔으면 다음 씬으로 이동
        }
    }
}