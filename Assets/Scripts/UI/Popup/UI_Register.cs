using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Register : UI_Popup {
    TMP_InputField _idField;
    TMP_InputField _passwordField;
    TMP_InputField _passwordField2;

    Button _registerButton;
    Button _closeButton;

    public override void Init() {
        base.Init();

        Transform idTransform = transform.Find("RegisterPanel/IdInputField");
        if (idTransform != null) {
            _idField = idTransform.GetComponent<TMP_InputField>();
        } else {
            Util.LogError("[UI_Login] IdInputField를 찾을 수 없습니다!");
        }

        Transform pwTransform = transform.Find("RegisterPanel/PasswordInputField");
        if (pwTransform != null) {
            _passwordField = pwTransform.GetComponent<TMP_InputField>();
        } else {
            Util.LogError("[UI_Login] PasswordInputField를 찾을 수 없습니다!");
        }

        Transform pw2Transform = transform.Find("RegisterPanel/ConfirmPasswordInputField");
        if (pw2Transform != null) {
            _passwordField2 = pw2Transform.GetComponent<TMP_InputField>();
        } else {
            Util.LogError("[UI_Login] ConfirmPasswordInputField를 찾을 수 없습니다!");
        }

        Transform registerBtnTransform = transform.Find("RegisterPanel/RegisterButton");
        if (registerBtnTransform != null) {
            _registerButton = registerBtnTransform.GetComponent<Button>();
        } else {
            Util.LogError("[UI_Login] RegisterButton를 찾을 수 없습니다!");
        }

        Transform closeBtnTransform = transform.Find("RegisterPanel/CloseButton");
        if (closeBtnTransform != null) {
            _closeButton = closeBtnTransform.GetComponent<Button>();
        } else {
            Util.LogError("[UI_Login] CloseButton를 찾을 수 없습니다!");
        }
    }

    void Start() {
        Init();
    }

    void Update() {
        
    }
}
