using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class UI_TestStart : UI_Scene {
    TestLobbyScene _scene;

    //TextMeshProUGUI _pressToStartText;
    TextMeshProUGUI _testSpinnerText;
    UI_EventHandler _overlay;

    private bool _isRequestSending = false;

    public override void Init() {
        base.Init();
        if (isInit) return;
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        //_pressToStartText = BindComponent<TextMeshProUGUI>("PressToStart");
        _testSpinnerText = BindComponent<TextMeshProUGUI>("TestSpinner");

        _overlay = BindComponent<UI_EventHandler>("DarkOverlay");
        _overlay.OnClickHandler += (eventData) => { TryConnectToServer(); };
        base.OnInitComplete();
    }

    void TryConnectToServer() {
        if (_isRequestSending == true) return;
        _isRequestSending = true;
        _testSpinnerText.gameObject.SetActive(true);
        _scene.TryConnectToServer();
    }

    public void Reload() {
        _isRequestSending = false;
        _testSpinnerText.gameObject.SetActive(false);
    }

    void Update() {
        
    }
}
