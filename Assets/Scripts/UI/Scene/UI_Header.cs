using UnityEngine;
using UnityEngine.UI;

public class UI_Header : UI_Scene {
    LobbyScene _scene;

    public enum HeaderState {
        BeforeAuth,
        Guest,
        Logined,
        Matching,
    }

    GameObject _leftMenuGroup;
    Button _lobbyButton;
    Button _inventoryButton;
    Button _shopButton;
    Button _optionButton;
    Button _logoutButton;

    Image _lobbyBtnAccBar;
    Image _inventoryBtnAccBar;
    Image _shopBtnAccBar;
    Image _optionBtnAccBar;
    Image _logoutBtnAccBar;

    Color _lobbyBtnAccBarColor = new Color(51 / 255f, 153 / 255f, 255 / 255f, 1f);
    Color _inventoryBtnAccBarColor = new Color(102 / 255f, 217 / 255f, 128 / 255f, 1f);
    Color _shopBtnAccBarColor = new Color(255 / 255f, 178 / 255f, 51 / 255f, 1f);
    Color _optionBtnAccBarColor = new Color(178 / 255f, 102 / 255f, 230 / 255f, 1f);
    Color _logoutBtnAccBarColor = new Color(230 / 255f, 80 / 255f, 80 / 255f, 1f);
    Color _invalidBtnAccBarColor = new Color(50 / 255f, 50 / 255f, 50 / 255f, 1f);

    HeaderState _headerState;

    public override void Init() {
        base.Init();
        if (isInit) return;
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is LobbyScene lobbyScene)
            _scene = lobbyScene;

        _lobbyButton = BindComponent<Button>("HeaderPanel/LeftMenuGroup/Btn_LOBBY");
        _inventoryButton = BindComponent<Button>("HeaderPanel/LeftMenuGroup/Btn_INVENTORY");
        _shopButton = BindComponent<Button>("HeaderPanel/LeftMenuGroup/Btn_SHOP");
        _optionButton = BindComponent<Button>("HeaderPanel/RightMenuGroup/Btn_OPTION");
        _leftMenuGroup = GameObject.Find("HeaderPanel/LeftMenuGroup");
        _lobbyBtnAccBar = BindComponent<Image>("HeaderPanel/LeftMenuGroup/Btn_LOBBY/AccentBar");
        _inventoryBtnAccBar = BindComponent<Image>("HeaderPanel/LeftMenuGroup/Btn_INVENTORY/AccentBar");
        _shopBtnAccBar = BindComponent<Image>("HeaderPanel/LeftMenuGroup/Btn_SHOP/AccentBar");
        _optionBtnAccBar = BindComponent<Image>("HeaderPanel/RightMenuGroup/Btn_OPTION/AccentBar");
        _logoutButton = BindComponent<Button>("HeaderPanel/RightMenuGroup/Btn_LOGOUT");
        _logoutBtnAccBar = BindComponent<Image>("HeaderPanel/RightMenuGroup/Btn_LOGOUT/AccentBar");
        _lobbyBtnAccBar.color = new Color(_lobbyBtnAccBar.color.r, _lobbyBtnAccBar.color.g, _lobbyBtnAccBar.color.b, 0);

        _lobbyButton.onClick.AddListener(OnClickLobbyBtn);
        _inventoryButton.onClick.AddListener(OnClickInventoryBtn);
        _shopButton.onClick.AddListener(OnClickShopBtn);
        _optionButton.onClick.AddListener(OnClickOptionBtn);
        _logoutButton.onClick.AddListener(OnClickLogoutBtn);

        ApplyHeaderState(HeaderState.BeforeAuth);

        base.OnInitComplete();
    }

    public void ApplyHeaderState(HeaderState state) {
        switch(state) {
            case HeaderState.BeforeAuth:
                ApplyBeforeAuthMode();
                break;
            case HeaderState.Guest:
                ApplyGuestMode();
                break;
            case HeaderState.Logined:
                ApplyLoginedMode();
                break;
            case HeaderState.Matching:
                ApplyMatchingMode();
                break;
        }
        _headerState = state;
    }

    private void ApplyBeforeAuthMode() {
        _leftMenuGroup.SetActive(false);

        _lobbyButton.interactable = false;
        _lobbyBtnAccBar.color = _invalidBtnAccBarColor;

        _inventoryButton.interactable = false;
        _inventoryBtnAccBar.color = _invalidBtnAccBarColor;

        _shopButton.interactable = false;
        _shopBtnAccBar.color = _invalidBtnAccBarColor;

        _optionButton.interactable = true;
        _optionBtnAccBar.color = _optionBtnAccBarColor;

        _logoutButton.interactable = false;
        _logoutBtnAccBar.color = _invalidBtnAccBarColor;
    }

    private void ApplyGuestMode() {
        _leftMenuGroup.SetActive(true);

        _lobbyButton.interactable = true;
        _lobbyBtnAccBar.color = _lobbyBtnAccBarColor;

        _inventoryButton.interactable = false;
        _inventoryBtnAccBar.color = _invalidBtnAccBarColor;

        _shopButton.interactable = false;
        _shopBtnAccBar.color = _invalidBtnAccBarColor;

        _optionButton.interactable = true;
        _optionBtnAccBar.color = _optionBtnAccBarColor;

        _logoutButton.interactable = true;
        _logoutBtnAccBar.color = _logoutBtnAccBarColor;
    }

    private void ApplyLoginedMode() {
        _leftMenuGroup.SetActive(true);

        _lobbyButton.interactable = true;
        _lobbyBtnAccBar.color = _lobbyBtnAccBarColor;

        _inventoryButton.interactable = true;
        _inventoryBtnAccBar.color = _inventoryBtnAccBarColor;

        _shopButton.interactable = true;
        _shopBtnAccBar.color = _shopBtnAccBarColor;

        _optionButton.interactable = true;
        _optionBtnAccBar.color = _optionBtnAccBarColor;

        _logoutButton.interactable = true;
        _logoutBtnAccBar.color = _logoutBtnAccBarColor;
    }

    private void ApplyMatchingMode() {
        _leftMenuGroup.SetActive(true);

        _lobbyButton.interactable = false;
        _lobbyBtnAccBar.color = _invalidBtnAccBarColor;

        _inventoryButton.interactable = false;
        _inventoryBtnAccBar.color = _invalidBtnAccBarColor;

        _shopButton.interactable = false;
        _shopBtnAccBar.color = _invalidBtnAccBarColor;

        _optionButton.interactable = false;
        _optionBtnAccBar.color = _invalidBtnAccBarColor;

        _logoutButton.interactable = false;
        _logoutBtnAccBar.color = _invalidBtnAccBarColor;
    }

    private void OnClickLobbyBtn() {
        _scene.BackToLobbyMain();
    }

    private void OnClickInventoryBtn() {
        _scene.ShowInventory();
    }

    private void OnClickShopBtn() {
        _scene.ShowShop();
    }

    private void OnClickLogoutBtn() {
        _scene.LogoutPopup();
    }

    private void OnClickOptionBtn() {
        _scene.ShowSettingUI();
    }

    private void OnDestroy() {
        _lobbyButton.onClick.RemoveAllListeners();
        _inventoryButton.onClick.RemoveAllListeners();
        _shopButton.onClick.RemoveAllListeners();
        _optionButton.onClick.RemoveAllListeners();
        _logoutButton.onClick.RemoveAllListeners();
    }
}
