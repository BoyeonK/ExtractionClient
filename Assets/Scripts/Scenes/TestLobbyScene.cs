using UnityEngine;
using UnityEngine.InputSystem;

public class TestLobbyScene : BaseScene {
    UI_TestStart _startUI;
    UI_Login _loginUI;
    UI_Register _registerUI;
    UI_TestLobbyMain _mainUI;
    UI_Setting _settingUI;
    LobbyState _lobbyState = LobbyState.BeforeConnect;
    bool optionsOpen = false;

    enum LobbyState {
        BeforeConnect,
        BeforeAuth,
        Lobby,
        Matching,
    }

    protected override void Init() {
        SceneType = Define.Scene.TestLobby;

        _startUI = Managers.UI.ShowSceneUI<UI_TestStart>();
        _loginUI = Managers.UI.CachePopupUI<UI_Login>();
        _registerUI = Managers.UI.CachePopupUI<UI_Register>();
        _mainUI = Managers.UI.CacheSceneUI<UI_TestLobbyMain>();
        _settingUI = Managers.UI.CachePopupUI<UI_Setting>();
        Managers.Input.AddKeyListener(Key.Escape, BackToPrevious, InputManager.KeyState.Up);
    }

    // ---------- BeforeConnect 상태에서의 UI 이벤트 핸들러 ----------
    public void TryConnectToServer() {
        // TODO: 서버에 버전 체크 요청 보내기
    }

    public void OnConnectedComplete() {
        _lobbyState = LobbyState.BeforeAuth;
        Managers.UI.DisableUI("UI_TestStart");
        //Auth용 UI만들고 띄우기
        //Managers.UI.ShowSceneUI<UI_Auth>();
    }

    // ---------- BeforeAuth 상태에서의 UI 이벤트 핸들러 ----------
    public void TryQuit() {

    }

    public void TryGuestLogin() {

    }

    public void TryLogin(string id, string password) {

    }

    public void TryRegister(string id, string password) {

    }

    public void OnLoginComplete() {
        _lobbyState = LobbyState.Lobby;
        Managers.UI.DisableUI("UI_Login");
        Managers.UI.DisableUI("UI_Register");
        Managers.UI.EnableUI("UI_TestLobbyMain");
    }

    // ---------- Lobby 상태에서의 UI 이벤트 핸들러 ----------
    public void TryLogout() {

    }

    public void ShowLobby() {

    }

    public void ShowInventory() {

    }

    public void ShowShop() {

    }

    public void ShowMapSelect() {

    }

    public void TryMatchMake() {

    }

    // ---------- Matching 상태에서의 UI 이벤트 핸들러 ----------


    public void BackToPrevious() {
        if (optionsOpen) {
            HideSettingUI();
        }
        else {

        }
    }

    public void ShowSettingUI() {
        Managers.UI.ShowPopupUI<UI_Setting>();
        optionsOpen = true;
    }

    public void HideSettingUI() {
        Managers.UI.ClosePopupUI(_settingUI);
        optionsOpen = false;
    }

    private void OnDestroy() {
        Managers.Input.RemoveKeyListener(Key.Escape, BackToPrevious, InputManager.KeyState.Up);
    }
}
