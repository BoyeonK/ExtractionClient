using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Register : UI_Scene {
    TestLobbyScene _scene;

    TMP_InputField _idField;
    TMP_InputField _passwordField;
    TMP_InputField _passwordField2;

    Button _registerButton;
    Button _closeButton;
    Button _backToLoginBtn;

    bool _isTryingRegister = false;

    public override void Init() {
        base.Init();
        if (isInit) return;
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        _idField = BindComponent<TMP_InputField>("RegisterPanel/IdInputField");
        _passwordField = BindComponent<TMP_InputField>("RegisterPanel/PasswordInputField");
        _passwordField2 = BindComponent<TMP_InputField>("RegisterPanel/ConfirmPasswordInputField");
        _registerButton = BindComponent<Button>("RegisterPanel/RegisterButton");
        _backToLoginBtn = BindComponent<Button>("RegisterPanel/BackToLoginButton");
        _closeButton = BindComponent<Button>("RegisterPanel/CloseButton");

        _registerButton.onClick.AddListener(OnClickRegisterBtn);
        _closeButton.onClick.AddListener(OnClickCloseBtn);
        _backToLoginBtn.onClick.AddListener(OnClickCloseBtn);

        base.OnInitComplete();
    }

    void Start() {
        Init();
    }

    private void OnClickRegisterBtn() {
        string id = _idField.text;
        string password = _passwordField.text;
        string password2 = _passwordField2.text;
        //TODO : 입력값 유효성 검사
        if(string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(password2)) {
            // TODO : 입력값이 유효하지 않다는 메시지 띄우기
            return;
        }

        _isTryingRegister = true;
        _scene.TryRegister(id, password);
    }

    public void Reload() {
        _isTryingRegister = false;
        _idField.text = "";
        _passwordField.text = "";
        _passwordField2.text = "";
    }

    public void OnTabBtnPressOn() {
        if (_idField.isFocused) {
            _passwordField.Select();
            _passwordField.ActivateInputField();
        } else if (_passwordField.isFocused) {
            _passwordField2.Select();
            _passwordField2.ActivateInputField();
        } else {
            _idField.Select();
            _idField.ActivateInputField();
        }
    }

    private void OnClickCloseBtn() {
        _scene.BackToAuthNoneSelected();
    }

    private void OnDestroy() {
        _registerButton.onClick.RemoveAllListeners();
        _closeButton.onClick.RemoveAllListeners();
    }
}
