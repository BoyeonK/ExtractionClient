using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Login : UI_Scene {
    LobbyScene _scene;

    TMP_InputField _idField;
    TMP_InputField _passwordField;

    Button _loginButton;
    Button _closeButton;

    bool _isTryingLogin = false;

    public override void Init() {
        base.Init();
        if (isInit) return;
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is LobbyScene lobbyScene)
            _scene = lobbyScene;

        _idField = BindComponent<TMP_InputField>("LoginPanel/IdInputField");
        _passwordField = BindComponent<TMP_InputField>("LoginPanel/PasswordInputField");
        _loginButton = BindComponent<Button>("LoginPanel/LoginButton");
        _closeButton = BindComponent<Button>("LoginPanel/CloseButton");

        _loginButton.onClick.AddListener(TryLogin);
        _closeButton.onClick.AddListener(OnClickCloseBtn);

        base.OnInitComplete();
    }

    void Start() {
        Init();
    }

    private void TryLogin() { 
        string id = _idField.text;
        string password = _passwordField.text;

        //TODO : 입력값 유효성 검사
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password)) {
            // TODO : 입력값이 유효하지 않다는 메시지 띄우기
            return;
        }

        // TODO : 로그인 시도
        _isTryingLogin = true;
        _scene.TryLogin(id, password);
    }

    public void Reload() { 
        _isTryingLogin = false;
        _idField.text = "";
        _passwordField.text = "";
    }

    public void OnTabBtnPressOn() {
        if (_passwordField.isFocused) {
            _idField.Select();
            _idField.ActivateInputField();
        } else {
            _passwordField.Select();
            _passwordField.ActivateInputField();
        }
    }

    public void OnEnterBtnPressOn() {
        TryLogin();
    }

    private void OnClickCloseBtn() {
        _scene.BackToAuthNoneSelected();
    }

    private void OnDestroy() {
        _loginButton.onClick.RemoveAllListeners();
        _closeButton.onClick.RemoveAllListeners();
    }
}
