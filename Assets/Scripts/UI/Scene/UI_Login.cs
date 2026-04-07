using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Login : UI_Scene {
    TMP_InputField _idField;
    TMP_InputField _passwordField;

    Button _loginButton;
    Button _closeButton;

    public override void Init() {
        base.Init();

        _idField = BindComponent<TMP_InputField>("LoginPanel/IdInputField");
        _passwordField = BindComponent<TMP_InputField>("LoginPanel/PasswordInputField");
        _loginButton = BindComponent<Button>("LoginPanel/LoginButton");
        _closeButton = BindComponent<Button>("LoginPanel/CloseButton");
    }



    void Start() {
        Init();
    }

    void Update() {
        
    }
}
