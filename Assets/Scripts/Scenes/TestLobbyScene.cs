using UnityEngine;
using UnityEngine.InputSystem;

public class TestLobbyScene : BaseScene {
    UI_TestLobbyMain _mainUI;
    UI_Setting _settingUI;
    //LobbyState _lobbyState = LobbyState.BeforeLogin;
    bool optionsOpen = false;

    enum LobbyState {
        BeforeLogin,
        Lobby,
        Inventory,
    }

    protected override void Init() {
        SceneType = Define.Scene.TestLobby;

        _mainUI = Managers.UI.ShowSceneUI<UI_TestLobbyMain>();
        _settingUI = Managers.UI.CachePopupUI<UI_Setting>();
        Managers.Input.AddKeyListener(Key.Escape, BackToPrevious, InputManager.KeyState.Up);
    }

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
