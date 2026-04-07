using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestLobbyScene : BaseScene {
    UI_TestStart _startUI;
    UI_Auth _authUI;
    UI_Login _loginUI;
    UI_Register _registerUI;
    UI_TestLobbyMain _mainUI;
    UI_Setting _settingUI;

    private CancellationTokenSource _cts = new CancellationTokenSource();

    LobbyState _lobbyState = LobbyState.BeforeConnect;
    bool optionsOpen = false;
    bool _isLoggined = false;

    enum LobbyState {
        BeforeConnect,
        BeforeAuth,
        Lobby,
        Matching,
    }

    protected override void Init() {
        SceneType = Define.Scene.TestLobby;

        _startUI = Managers.UI.CacheSceneUI<UI_TestStart>();
        _authUI = Managers.UI.CacheSceneUI<UI_Auth>();
        _loginUI = Managers.UI.CachePopupUI<UI_Login>();
        _registerUI = Managers.UI.CachePopupUI<UI_Register>();
        //_mainUI = Managers.UI.CacheSceneUI<UI_TestLobbyMain>();
        _settingUI = Managers.UI.CachePopupUI<UI_Setting>();
        Managers.Input.AddKeyListener(Key.Escape, BackToPrevious, InputManager.KeyState.Up);

        // TODO : 최초 실행인지, 한 게임 종료 후 재실행인지에 따라 분기 처리
        _lobbyState = LobbyState.BeforeConnect;
        Managers.UI.ShowSceneUI<UI_TestStart>();
    }

    // -----------------------------------------------------
    // ---------- BeforeConnect 상태에서의 메서드 ----------
    // -----------------------------------------------------
    public async void TryConnectToServer() {
        Util.Log("TryConnectToServer 실행");
        bool isSuccess = await Managers.Network.httpManager.GetVersionCall(_cts.Token);
        if (isSuccess == true) {
            OnConnectedComplete();
        } else {
            OnConnectedFailed();
        }
    }

    private void OnConnectedFailed() {
        _startUI.Reload();
    }

    private void OnConnectedComplete() {
        _lobbyState = LobbyState.BeforeAuth;
        Managers.UI.DisableUI("UI_TestStart");
        Managers.UI.ShowSceneUI<UI_Auth>();
    }

    // --------------------------------------------------
    // ---------- BeforeAuth 상태에서의 메서드 ----------
    // --------------------------------------------------
    enum BeforeAuthState {
        NoneSelected,
        Login,
        Register,
    }

    BeforeAuthState _authState = BeforeAuthState.NoneSelected;

    public void BackToBeforeConnect() {
        Util.Log("TryConnectToServer 실행");
        // TODO: 팝업 띄우고, 확인 시 이전 단계로 돌아가기
    }

    public async void TryGuestLogin() {
        Util.Log("TryGuestLogin 실행");
        bool isSuccess = await Managers.Network.httpManager.PostGuestLoginCall(_cts.Token);
    }

    private void OnGuestLoginComplete() {
        
    }

    public async void TryLogin(string id, string password) {
        Util.Log("TryLogin 실행");
        bool isSuccess = await Managers.Network.httpManager.PostLoginCall(id, password, _cts.Token);
    }

    private void OnLoginComplete() {
        _lobbyState = LobbyState.Lobby;
        Managers.UI.DisableUI("UI_Login");
        Managers.UI.DisableUI("UI_Register");
        Managers.UI.EnableUI("UI_TestLobbyMain");
    }

    public async void TryRegister(string id, string password) {
        Util.Log("TryRegister 실행");
        bool isSuccess = await Managers.Network.httpManager.PostCreateAccountCall(id, password, _cts.Token);
    }

    private void OnRegisterComplete() {
        Managers.UI.DisableUI("UI_Register");
        Managers.UI.ShowPopupUI<UI_Login>();
    }

    private void OnAuthRequestFailed() {

    }

    // ---------------------------------------------
    // ---------- Lobby 상태에서의 메서드 ----------
    // ---------------------------------------------
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

    // ---------- Matching 상태에서의 메서드 ----------


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
        _cts.Cancel();
        _cts.Dispose();
    }
}
