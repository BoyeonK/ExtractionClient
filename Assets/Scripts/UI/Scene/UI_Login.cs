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

        // 검사 항목을 PostLoginCall의 가드와 맞춘다 — 로그인은 이미 만들어진 계정이 대상이라
        // 비밀번호는 형식이 아니라 비어 있는지만 본다(규칙이 바뀌기 전에 만든 계정을 막지 않는다)
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password)) {
            _scene.ActiveReconfirmOnlyConfirm("아이디와 비밀번호를 모두 입력해주세요.");
            return;
        }
        if (!HTTPManager.IsValidId(id)) {
            _scene.ActiveReconfirmOnlyConfirm(HTTPManager.ID_RULE_MESSAGE);
            return;
        }

        // 유효성 가드 뒤에서 낸다 — 앞에 두면 빈 칸으로 눌러도 성공한 것처럼 들린다.
        // 엔터 입력도 이 함수를 거치므로 키·버튼이 같은 소리를 낸다
        Managers.Sound.PlayUISubmit();
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
        Managers.Sound.PlayUIReturn();
        _scene.BackToAuthNoneSelected();
    }

    private void OnDestroy() {
        _loginButton.onClick.RemoveAllListeners();
        _closeButton.onClick.RemoveAllListeners();
    }
}
