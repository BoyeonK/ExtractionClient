using UnityEngine;
using UnityEngine.InputSystem;

public class LoadingScene1 : BaseScene {
    LoadingUI1 _ui;

    bool sceneLoadFlag = false;
    bool staticObjectsLoadFlag = false;
    float progress = 0.0f;
    float progressThreshold = 0.05f;

    protected override void Init() {
        SceneType = Define.Scene.LoadingScene1;
        _ui = FindAnyObjectByType<LoadingUI1>();

        Managers.Scene.LoadSceneAsync();
    }

    void Update() {
        const float epsilon = 0.01f;
        if (sceneLoadFlag == false) {
            // 1. 현재 진행상태가 progress + threshold보다 크거나 같으면 progress갱신.
            // loadingbar를 progress에 맞게 갱신


            // 2. Scene로드 완료됬는지 확인 (진행상태가 0.9f-epsilon보다 크거나 같으면 완료로 간주)
            // 완료됬으면 sceneLoadFlag = true; 하고 C2DRequestBlueprint전송
        }
        else if (staticObjectsLoadFlag == true) {
            // C2DRequestBlueprint에 대한 응답이 왔는지 확인
            // 왔으면 다음 씬으로 이동
        }
    }
}