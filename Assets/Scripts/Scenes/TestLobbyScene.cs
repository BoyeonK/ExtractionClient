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
        _loginUI = Managers.UI.CacheSceneUI<UI_Login>();
        _registerUI = Managers.UI.CacheSceneUI<UI_Register>();
        //_mainUI = Managers.UI.CacheSceneUI<UI_TestLobbyMain>();
        _settingUI = Managers.UI.CachePopupUI<UI_Setting>();
        Managers.Input.AddKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Up);

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
        _startUI.Reload();
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

    public void BackToBeforeConnectPopup() {
        Util.Log("TryConnectToServer 실행");
        Managers.UI.ShowUIConfirmOrCancel("asdfasdf", BackToBeforeConnectState);
    }

    private void BackToBeforeConnectState() {
        if (_lobbyState != LobbyState.BeforeAuth) {
            return;
        }
            
        Managers.UI.DisableUI("UI_Auth");
        Managers.UI.ShowSceneUI<UI_TestStart>();
        _lobbyState = LobbyState.BeforeConnect;
    }

    public void BackToAuthNoneSelected() {
        if (_lobbyState != LobbyState.BeforeAuth || _authState == BeforeAuthState.NoneSelected)
            return;

        Managers.UI.DisableUI("UI_Login");
        Managers.UI.DisableUI("UI_Register");
        Managers.UI.ShowSceneUI<UI_Auth>();
        _authState = BeforeAuthState.NoneSelected;
    }

    public void OnClickSelectLogin() {
        if (_lobbyState != LobbyState.BeforeAuth || _authState != BeforeAuthState.NoneSelected)
            return;

        Managers.UI.DisableUI("UI_Auth");
        Managers.UI.ShowSceneUI<UI_Login>();
        _authState = BeforeAuthState.Login;
    }

    public async void TryLogin(string id, string password) {
        Util.Log("TryLogin 실행");
        bool isSuccess = await Managers.Network.httpManager.PostLoginCall(id, password, _cts.Token);
        if (isSuccess == true) {
            OnLoginComplete();
        } else {
            OnAuthRequestFailed();
        }
    }

    private void OnLoginComplete() {
        _isLoggined = true;
        _lobbyState = LobbyState.Lobby;
        Managers.UI.DisableUI("UI_Login");
        Managers.UI.DisableUI("UI_Register");
        
    }

    public void OnClickSelectRegister() { 
        if (_lobbyState != LobbyState.BeforeAuth || _authState != BeforeAuthState.NoneSelected)
            return;

        Managers.UI.DisableUI("UI_Auth");
        Managers.UI.ShowSceneUI<UI_Register>();
        _authState = BeforeAuthState.Register;
    }

    public async void TryRegister(string id, string password) {
        Util.Log("TryRegister 실행");
        bool isSuccess = await Managers.Network.httpManager.PostCreateAccountCall(id, password, _cts.Token);
        if (isSuccess == true) {
            OnRegisterComplete();
        } else {
            OnAuthRequestFailed();
        }
    }

    // TODO : 회원가입 완료 후 로그인 진행 되는지 확인, 즉시 진행되는 경우 로그인과 같은 프로세스로 진행
    private void OnRegisterComplete() {
        Managers.UI.DisableUI("UI_Register");
        Managers.UI.ShowSceneUI<UI_Login>();
    }

    public async void OnClickGuestLogin() {
        Util.Log("TryGuestLogin 실행");
        bool isSuccess = await Managers.Network.httpManager.PostGuestLoginCall(_cts.Token);
    }

    private void OnGuestLoginComplete() {

    }

    private void OnAuthRequestFailed() {
        _loginUI.Reload();
        _registerUI.Reload();
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

    // ------------------------------------------------
    // ---------- Matching 상태에서의 메서드 ----------
    // ------------------------------------------------


    // ------------------------------------------------
    // ---------- 공통적으로 사용되는 메서드 ----------
    // ------------------------------------------------
    public void OnEscapeInput() {
        switch (_lobbyState) {
            case LobbyState.BeforeConnect:
                QuitPopup();
                break;
            case LobbyState.BeforeAuth:
                if (_authState == BeforeAuthState.NoneSelected) {
                    BackToBeforeConnectPopup();
                } else {
                    BackToAuthNoneSelected();
                }
                break;
            case LobbyState.Lobby:
                break;
            case LobbyState.Matching:
                break;
        }
    }

    private void QuitPopup() {
        Managers.UI.ShowUIConfirmOrCancel("Do you want to exit the game?", QuitGameApplication);
    }

    private void QuitGameApplication() {
        Managers.ExecuteAtMainThread(() => {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
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
        Managers.Input.RemoveKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Up);
        _cts.Cancel();
        _cts.Dispose();
    }
}
