using UnityEngine;
using UnityEngine.UI;

public class UI_Auth : UI_Scene {
    LobbyScene _scene;

    Button _loginButton;
    Button _registerButton;
    Button _guestLoginButton;

    private bool _isRequestSending = false;

    public override void Init() {
        base.Init();
        if (isInit) return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is LobbyScene lobbyScene)
            _scene = lobbyScene;

        _loginButton = BindComponent<Button>("Card/LoginButton");
        _registerButton = BindComponent<Button>("Card/RegisterButton");
        _guestLoginButton = BindComponent<Button>("Card/GuestLoginButton");

        _loginButton.onClick.AddListener(() => { _scene.OnClickSelectLogin(); });
        _registerButton.onClick.AddListener(() => { _scene.OnClickSelectRegister(); });
        _guestLoginButton.onClick.AddListener(() => { _scene.OnClickGuestLogin(); });

        base.OnInitComplete();
    }

    private void OnDestroy() {
        _loginButton.onClick.RemoveAllListeners();
        _registerButton.onClick.RemoveAllListeners();
        _guestLoginButton.onClick.RemoveAllListeners();
    }
}
