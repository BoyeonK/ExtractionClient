using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Register : UI_Scene {
    TMP_InputField _idField;
    TMP_InputField _passwordField;
    TMP_InputField _passwordField2;

    Button _registerButton;
    Button _closeButton;

    public override void Init() {
        base.Init();

        _idField = BindComponent<TMP_InputField>("RegisterPanel/IdInputField");
        _passwordField = BindComponent<TMP_InputField>("RegisterPanel/PasswordInputField");
        _passwordField2 = BindComponent<TMP_InputField>("RegisterPanel/ConfirmPasswordInputField");
        _registerButton = BindComponent<Button>("RegisterPanel/RegisterButton");
        _closeButton = BindComponent<Button>("RegisterPanel/CloseButton");
    }

    void Start() {
        Init();
    }

    void Update() {
        
    }
}
