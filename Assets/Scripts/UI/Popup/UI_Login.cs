using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Login : UI_Popup {
    TMP_InputField _idField;
    TMP_InputField _passwordField;

    Button _loginButton;
    Button _closeButton;

    public override void Init() {
        base.Init();

        Transform idTransform = transform.Find("LoginPanel/IdInputField");
        if (idTransform != null) {
            _idField = idTransform.GetComponent<TMP_InputField>();
        } else {
            Util.LogError("[UI_Login] IdInputField를 찾을 수 없습니다!");
        }

        Transform pwTransform = transform.Find("LoginPanel/PasswordInputField");
        if (pwTransform != null) {
            _passwordField = pwTransform.GetComponent<TMP_InputField>();
        } else {
            Util.LogError("[UI_Login] PasswordInputField를 찾을 수 없습니다!");
        }

        Transform loginBtnTransform = transform.Find("LoginPanel/LoginButton");
        if (loginBtnTransform != null) {
            _loginButton = loginBtnTransform.GetComponent<Button>();
        } else {
            Util.LogError("[UI_Login] LoginButton를 찾을 수 없습니다!");
        }

        Transform closeBtnTransform = transform.Find("LoginPanel/CloseButton");
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
