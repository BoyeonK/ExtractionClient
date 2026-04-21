using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MapSelect : UI_Scene {
    TestLobbyScene _scene;

    bool _finalDecision = true;
    GameObject _overlay;
    GameObject _loadoutSelectWindow;

    int _selectedMapId = 0;
    Image _mapSprite;
    TextMeshProUGUI _mapName;
    Button _leftBtn;
    Button _rightBtn;
    Button _matchStartBtn;

    Button _customLoadoutBtn;
    Button _freeLoadoutBtn;
    Button _cancelBtn;

    public override void Init() {
        base.Init();
        if (isInit) return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        Transform overlayTrans = Util.BindComponent<Transform>("DarkOverlay", this.gameObject);
        _overlay = overlayTrans.gameObject;
        Transform loadoutSelectWindow = Util.BindComponent<Transform>("LoadoutSelectWindow", this.gameObject);
        _loadoutSelectWindow = loadoutSelectWindow.gameObject;

        _mapSprite = Util.BindComponent<Image>("MapSelectWindow/Image_Map", this.gameObject);
        _mapName = Util.BindComponent<TextMeshProUGUI>("MapSelectWindow/Text_MapName", this.gameObject);
        _leftBtn = Util.BindComponent<Button>("MapSelectWindow/Button_Left", this.gameObject);
        _rightBtn = Util.BindComponent<Button>("MapSelectWindow/Button_Right", this.gameObject);
        _matchStartBtn = Util.BindComponent<Button>("MapSelectWindow/Button_StartMatch", this.gameObject);

        _customLoadoutBtn = Util.BindComponent<Button>("LoadoutSelectWindow/ButtonRow/CustomLoadoutButton", this.gameObject);
        _freeLoadoutBtn = Util.BindComponent<Button>("LoadoutSelectWindow/ButtonRow/FreeLoadoutButton", this.gameObject);
        _cancelBtn = Util.BindComponent<Button>("LoadoutSelectWindow/ButtonRow/CancelButton", this.gameObject);

        _leftBtn.onClick.AddListener(OnLeftBtnClick);
        _rightBtn.onClick.AddListener(OnRightBtnClick);
        _matchStartBtn.onClick.AddListener(OnMatchStartBtnClick);
        _customLoadoutBtn.onClick.AddListener(OnCustomLoadoutBtnClick);
        _freeLoadoutBtn.onClick.AddListener(OnFreeLoadoutBtnClick);
        _cancelBtn.onClick.AddListener(OnCancelBtnClick);

        SetNormalState();

        base.OnInitComplete();
    }

    private void OnLeftBtnClick() {
        int max = (int)Define.Map.MaxCount;
        _selectedMapId = (_selectedMapId + max - 1) % max;
    }

    private void OnRightBtnClick() {
        int max = (int)Define.Map.MaxCount;
        _selectedMapId = (_selectedMapId + 1) % max;
    }

    private void OnMatchStartBtnClick() { 
        SetFinalDecisionState();
    }

    private void OnCustomLoadoutBtnClick() { 
        
    }

    private void OnFreeLoadoutBtnClick() { 
    
    }

    private void OnCancelBtnClick() {
        SetNormalState();
    }

    private void SetFinalDecisionState() {
        if (_finalDecision == true) return;
        _finalDecision = true;
        _overlay.SetActive(true);
        _loadoutSelectWindow.SetActive(true);
    }

    public void SetNormalState() {
        if (_finalDecision == false) return;
        _finalDecision = false;
        _overlay.SetActive(false);
        _loadoutSelectWindow.SetActive(false);
    }

    private void RefreshMapSelectWindow() {
        // TODO :
        // 1. 스프라이트 추가하기
        // 2. _selectedMapId에 해당하는 map sprite적용
        // 3. _selectedMapId에 해당하는 map name적용
    }

    private void OnDestroy() {
        if (_leftBtn != null) _leftBtn.onClick.RemoveAllListeners();
        if (_rightBtn != null) _rightBtn.onClick.RemoveAllListeners();
        if (_matchStartBtn != null) _matchStartBtn.onClick.RemoveAllListeners();
        if (_customLoadoutBtn != null) _customLoadoutBtn.onClick.RemoveAllListeners();
        if (_freeLoadoutBtn != null) _freeLoadoutBtn.onClick.RemoveAllListeners();
        if (_cancelBtn != null) _cancelBtn.onClick.RemoveAllListeners();
    }
}
