using UnityEngine;
using UnityEngine.UI;

public class UI_Header : UI_Scene {
    TestLobbyScene _scene;

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

    HeaderState _headerState;

    public override void Init() {
        base.Init();
        if (isInit) return;
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        _lobbyButton = BindComponent<Button>("HeaderPanel/LeftMenuGroup/Btn_LOBBY");
        _inventoryButton = BindComponent<Button>("HeaderPanel/LeftMenuGroup/Btn_INVENTORY");
        _shopButton = BindComponent<Button>("HeaderPanel/LeftMenuGroup/Btn_SHOP");
        _optionButton = BindComponent<Button>("HeaderPanel/RightMenuGroup/Btn_OPTION");
        _leftMenuGroup = GameObject.Find("HeaderPanel/LeftMenuGroup");

        _lobbyButton.onClick.AddListener(OnClickLobbyBtn);
        _inventoryButton.onClick.AddListener(OnClickInventoryBtn);
        _shopButton.onClick.AddListener(OnClickShopBtn);
        _optionButton.onClick.AddListener(OnClickOptionBtn);

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
        _inventoryButton.interactable = false;
        _shopButton.interactable = false;
        _optionButton.interactable = true;
    }

    private void ApplyGuestMode() {
        _leftMenuGroup.SetActive(true);
        _lobbyButton.interactable = true;
        _inventoryButton.interactable = false;
        _shopButton.interactable = false;
        _optionButton.interactable = true;
    }

    private void ApplyLoginedMode() {
        _leftMenuGroup.SetActive(true);
        _lobbyButton.interactable = true;
        _inventoryButton.interactable = true;
        _shopButton.interactable = true;
        _optionButton.interactable = false;
    }

    private void ApplyMatchingMode() {
        _leftMenuGroup.SetActive(true);
        _lobbyButton.interactable = false;
        _inventoryButton.interactable = false;
        _shopButton.interactable = false;
        _optionButton.interactable = false;
    }

    private void OnClickLobbyBtn() {

    }

    private void OnClickInventoryBtn() {

    }

    private void OnClickShopBtn() {

    }

    private void OnClickOptionBtn() {

    }

    private void OnDestroy() {
        _lobbyButton.onClick.RemoveAllListeners();
        _inventoryButton.onClick.RemoveAllListeners();
        _shopButton.onClick.RemoveAllListeners();
        _optionButton.onClick.RemoveAllListeners();
    }
}
