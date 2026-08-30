using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class UI_TestStart : UI_Scene {
    LobbyScene _scene;

    //TextMeshProUGUI _pressToStartText;
    TextMeshProUGUI _testSpinnerText;
    UI_EventHandler _overlay;

    private bool _isRequestSending = false;

    public override void Init() {
        base.Init();
        if (isInit) return;
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is LobbyScene lobbyScene)
            _scene = lobbyScene;

        //_pressToStartText = BindComponent<TextMeshProUGUI>("PressToStart");
        _testSpinnerText = BindComponent<TextMeshProUGUI>("TestSpinner");

        _overlay = BindComponent<UI_EventHandler>("DarkOverlay");
        _overlay.OnClickHandler += (eventData) => { TryConnectToServer(); };
        base.OnInitComplete();
    }

    void TryConnectToServer() {
        // 중복 클릭 가드 뒤에 둔다 — 앞에 두면 접속 중에 연타할 때마다 소리만 난다
        if (_isRequestSending == true) return;
        Managers.Sound.PlayUISubmit();
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
