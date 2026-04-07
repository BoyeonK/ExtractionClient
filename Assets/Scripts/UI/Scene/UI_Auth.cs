using UnityEngine;
using UnityEngine.UI;

public class UI_Auth : UI_Scene {
    TestLobbyScene _scene;

    Button _loginButton;
    Button _registerButton;
    Button _guestLoginButton;

    private bool _isRequestSending = false;

    public override void Init() {
        base.Init();
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        _loginButton = BindComponent<Button>("Card/LoginButton");
        _registerButton = BindComponent<Button>("Card/RegisterButton");
        _guestLoginButton = BindComponent<Button>("Card/GuestLoginButton");
    }
}
