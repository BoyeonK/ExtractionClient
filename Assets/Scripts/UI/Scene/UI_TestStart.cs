using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class UI_TestStart : UI_Scene {
    TestLobbyScene _scene;

    //TextMeshProUGUI _pressToStartText;
    TextMeshProUGUI _testSpinnerText;
    UI_EventHandler _overlay;

    private bool _isConnecting = false;

    public override void Init() {
        base.Init();
        //_pressToStartText = BindComponent<TextMeshProUGUI>("PressToStart");
        _testSpinnerText = BindComponent<TextMeshProUGUI>("TestSpinner");

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;
        _overlay = BindComponent<UI_EventHandler>("DarkOverlay");
        _overlay.OnClickHandler += (eventData) => { TryConnectToServer(); };
    }

    void TryConnectToServer() {
        if (_isConnecting == true) return;
        _isConnecting = true;
        _testSpinnerText.gameObject.SetActive(true);
        _scene.TryConnectToServer();
    }

    public void Reload() {
        _isConnecting = false;
        _testSpinnerText.gameObject.SetActive(false);
    }

    void Update() {
        
    }
}
