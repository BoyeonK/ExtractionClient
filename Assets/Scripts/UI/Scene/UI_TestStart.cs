using TMPro;
using UnityEngine;

public class UI_TestStart : UI_Scene {
    TestLobbyScene _scene;

    enum Texts {
        PressToStart,
        TestSpinner,
    }

    public override void Init() {
        base.Init();
        Bind<TextMeshProUGUI>(typeof(Texts));

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;
    }

    void Start() {
        Init();
    }

    void Update() {
        
    }
}
