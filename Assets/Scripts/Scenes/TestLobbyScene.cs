using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TestLobbyScene : BaseScene {
    UI_TestStart _startUI;
    UI_Auth _authUI;
    UI_Login _loginUI;
    UI_Register _registerUI;
    UI_Header _headerUI;
    UI_Inventory _inventoryUI;
    UI_Warehouse _warehouseUI;
    UI_Shop _shopUI;
    UI_MapSelect _mapSelectUI;
    LobbyReconfirmUI _lobbyReconfirmUI;

    const int INVENTORY_SLOT_COUNT = 25;
    const int WAREHOUSE_SLOT_COUNT = 80;
    const int LOADOUT_SLOT_COUNT = 3;
    InventoryItem[] _inventorySlots = new InventoryItem[INVENTORY_SLOT_COUNT];
    InventoryItem[] _warehouseSlots = new InventoryItem[WAREHOUSE_SLOT_COUNT];
    InventoryItem[] _loadoutSlots   = new InventoryItem[LOADOUT_SLOT_COUNT];

    private CancellationTokenSource _cts = new CancellationTokenSource();

    LobbyState _lobbyState = LobbyState.BeforeConnect;

    HTTPManager.LoginState _loginState => Managers.Network.httpManager.AuthState;

    enum LobbyState {
        BeforeConnect,
        BeforeAuth,
        Lobby,
        Matching,
    }

    protected override void Init() {
        SceneType = Define.Scene.TestLobby;

        // UI 초기화, Header의 sortorder를 뒤로 두어야 함.
        _startUI = Managers.UI.CacheSceneUI<UI_TestStart>();
        _authUI = Managers.UI.CacheSceneUI<UI_Auth>();
        _loginUI = Managers.UI.CacheSceneUI<UI_Login>();
        _registerUI = Managers.UI.CacheSceneUI<UI_Register>();
        _headerUI = Managers.UI.ShowSceneUI<UI_Header>();
        _inventoryUI = Managers.UI.CacheSceneUI<UI_Inventory>();
        _warehouseUI = Managers.UI.CacheSceneUI<UI_Warehouse>();
        _shopUI = Managers.UI.CacheSceneUI<UI_Shop>();
        _mapSelectUI = Managers.UI.CacheSceneUI<UI_MapSelect>();

        GameObject reconfirmObj = GameObject.Find("LobbyReconfirmUI");
        if (reconfirmObj != null) {
            _lobbyReconfirmUI = reconfirmObj.GetComponent<LobbyReconfirmUI>();
            _lobbyReconfirmUI.Init();
        }

        Managers.Input.AddKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.Enter, OnEnterInput, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.Tab, OnTabInput, InputManager.KeyState.Up);
        InitDragGhost();

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

    BeforeAuthState _beforeAuthState = BeforeAuthState.NoneSelected;

    public void BackToBeforeConnectPopup() {
        if (_lobbyState != LobbyState.BeforeAuth)
            return;

        Util.Log("BackToBeforeConnectPopup 실행");
        _lobbyReconfirmUI.ActiveConfirmOrCancel("서버 연결 화면으로 돌아가시겠습니까?", BackToBeforeConnectState);
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
        if (_lobbyState != LobbyState.BeforeAuth || _beforeAuthState == BeforeAuthState.NoneSelected)
            return;

        Managers.UI.DisableUI("UI_Login");
        Managers.UI.DisableUI("UI_Register");
        Managers.UI.ShowSceneUI<UI_Auth>();
        _beforeAuthState = BeforeAuthState.NoneSelected;
    }

    public void OnClickSelectLogin() {
        if (_lobbyState != LobbyState.BeforeAuth || _beforeAuthState != BeforeAuthState.NoneSelected)
            return;

        Managers.UI.DisableUI("UI_Auth");
        Managers.UI.ShowSceneUI<UI_Login>();
        _beforeAuthState = BeforeAuthState.Login;
    }

    public async void TryLogin(string id, string password) {
        Util.Log("TryLogin 실행");
        bool isSuccess = await Managers.Network.httpManager.PostLoginCall(id, password, _cts.Token);
        if (isSuccess) {
            OnLoginComplete(UI_Header.HeaderState.Logined);
        } else {
            OnAuthRequestFinished();
            _lobbyReconfirmUI.ActiveOnlyConfirm("Login Failed");
        }
    }

    public void OnLoginComplete(UI_Header.HeaderState hState) {
        _lobbyState = LobbyState.Lobby;
        OnAuthRequestFinished();
        Managers.UI.DisableUI("UI_Auth");
        Managers.UI.DisableUI("UI_Login");
        Managers.UI.DisableUI("UI_Register");
        _headerUI.ApplyHeaderState(hState);
        Managers.UI.ShowSceneUI<UI_MapSelect>();

        Array.Clear(_inventorySlots, 0, _inventorySlots.Length);
        Array.Clear(_warehouseSlots, 0, _warehouseSlots.Length);
        InventoryItem[] items = Managers.Network.httpManager.Inventory;
        if (items != null) {
            foreach (var item in items) {
                if (item.slot_index < 0) continue;

                if (item.slot_index < WAREHOUSE_SLOT_COUNT) {
                    _warehouseSlots[item.slot_index] = item;
                } else {
                    int invIndex = item.slot_index - WAREHOUSE_SLOT_COUNT;
                    if (invIndex < INVENTORY_SLOT_COUNT) {
                        _inventorySlots[invIndex] = item;
                    } else {
                        int loadoutIndex = invIndex - INVENTORY_SLOT_COUNT;
                        if (loadoutIndex < LOADOUT_SLOT_COUNT)
                            _loadoutSlots[loadoutIndex] = item;
                    }
                }
            }
        }

        _shopUI.Refresh();
    }

    public void OnClickSelectRegister() { 
        if (_lobbyState != LobbyState.BeforeAuth || _beforeAuthState != BeforeAuthState.NoneSelected)
            return;

        Managers.UI.DisableUI("UI_Auth");
        Managers.UI.ShowSceneUI<UI_Register>();
        _beforeAuthState = BeforeAuthState.Register;
    }

    public async void TryRegister(string id, string password) {
        Util.Log("TryRegister 실행");
        bool isSuccess = await Managers.Network.httpManager.PostCreateAccountCall(id, password, _cts.Token);
        if (isSuccess) {
            OnLoginComplete(UI_Header.HeaderState.Logined);
        } else {
            OnAuthRequestFinished();
            _lobbyReconfirmUI.ActiveOnlyConfirm("Register Failed");
        }
    }

    public async void OnClickGuestLogin() {
        Util.Log("OnClickGuestLogin 실행");
        if (_lobbyState != LobbyState.BeforeAuth || _beforeAuthState != BeforeAuthState.NoneSelected)
            return;
        
        bool isSuccess = await Managers.Network.httpManager.PostGuestLoginCall(_cts.Token);
        if (isSuccess) {
            OnLoginComplete(UI_Header.HeaderState.Guest);
        } else {
            OnAuthRequestFinished();
            _lobbyReconfirmUI.ActiveOnlyConfirm("Guest Login Failed");
        }
    }

    private void BeforeAuthEnter() {
        switch (_beforeAuthState) { 
            case BeforeAuthState.NoneSelected:
                break;
            case BeforeAuthState.Login:
                _loginUI.OnEnterBtnPressOn();
                break;
            case BeforeAuthState.Register:
                break;
        }
    }

    private void BeforeAuthTab() { 
        switch (_beforeAuthState) { 
            case BeforeAuthState.NoneSelected:
                break;
            case BeforeAuthState.Login:
                _loginUI.OnTabBtnPressOn();
                break;
            case BeforeAuthState.Register:
                _registerUI.OnTabBtnPressOn();
                break;
        }
    }

    public void OnAuthRequestFinished() {
        _loginUI.Reload();
        _registerUI.Reload();
    }

    // ---------------------------------------------
    // ---------- Lobby 상태에서의 메서드 ----------
    // ---------------------------------------------
    enum UserState {
        Main,
        Inventory,
        Shop,
    }

    UserState _userState = UserState.Main;

    public void LogoutPopup() {
        if (_lobbyState != LobbyState.Lobby)
            return;

        _lobbyReconfirmUI.ActiveConfirmOrCancel("Do you want to logout?", TryLogout);
    }

    public async void TryLogout() {
        bool isSuccess = await Managers.Network.httpManager.PostLogoutCall(_cts.Token);
        if (isSuccess == true) { 
            OnLogoutComplete();
        } else { 
            
        }
    }

    private void OnLogoutComplete() {
        _lobbyState = LobbyState.BeforeAuth;
        _beforeAuthState = BeforeAuthState.NoneSelected;
        _userState = UserState.Main;
        _headerUI.ApplyHeaderState(UI_Header.HeaderState.BeforeAuth);
        Managers.UI.ShowSceneUI<UI_Auth>();
        _mapSelectUI.SetNormalState();
        Managers.UI.DisableUI("UI_MapSelect");
        Managers.UI.DisableUI("UI_Inventory");
        Managers.UI.DisableUI("UI_Warehouse");
        Managers.UI.DisableUI("UI_Shop");

        Array.Clear(_inventorySlots, 0, _inventorySlots.Length);
        Array.Clear(_warehouseSlots, 0, _warehouseSlots.Length);
        Array.Clear(_loadoutSlots,   0, _loadoutSlots.Length);
    }

    public void ShowLobby() {
        if (_lobbyState != LobbyState.Lobby || _userState != UserState.Main)
            return;


    }

    public void ShowInventory() {
        if (_lobbyState != LobbyState.Lobby || _userState == UserState.Inventory)
            return;

        _userState = UserState.Inventory;
        _mapSelectUI.SetNormalState();
        Managers.UI.DisableUI("UI_MapSelect");
        Managers.UI.ShowSceneUI<UI_Inventory>();
        Managers.UI.ShowSceneUI<UI_Warehouse>();
        Managers.UI.DisableUI("UI_Shop");
        _inventoryUI.Refresh();
        _warehouseUI.Refresh();
    }

    public InventoryItem[] InventorySlots => _inventorySlots;
    public InventoryItem[] LoadoutSlots   => _loadoutSlots;
    public InventoryItem[] WarehouseSlots => _warehouseSlots;

    public bool IsShiftPressed {
        get {
            if (Keyboard.current == null) return false;
            
            return Keyboard.current.shiftKey.isPressed; 
        }
    }

    public void SetInventorySlot(int index, InventoryItem item) {
        if (index >= 0 && index < INVENTORY_SLOT_COUNT)
            _inventorySlots[index] = item;
    }

    public void SetLoadoutSlot(int index, InventoryItem item) {
        if (index >= 0 && index < LOADOUT_SLOT_COUNT)
            _loadoutSlots[index] = item;
    }

    public void SetWarehouseSlot(int index, InventoryItem item) {
        if (index >= 0 && index < WAREHOUSE_SLOT_COUNT)
            _warehouseSlots[index] = item;
    }

    public void BackToLobbyMain() {
        if (_lobbyState != LobbyState.Lobby || _userState == UserState.Main)
            return;

        _userState = UserState.Main;
        Managers.UI.DisableUI("UI_Inventory");
        Managers.UI.DisableUI("UI_Warehouse");
        Managers.UI.DisableUI("UI_Shop");
        Managers.UI.ShowSceneUI<UI_MapSelect>();
    }

    public void ShowShop() {
        if (_lobbyState != LobbyState.Lobby || _userState == UserState.Shop)
            return;

        _userState = UserState.Shop;

        _mapSelectUI.SetNormalState();
        Managers.UI.DisableUI("UI_MapSelect");
        Managers.UI.DisableUI("UI_Inventory");
        Managers.UI.ShowSceneUI<UI_Warehouse>();
        Managers.UI.ShowSceneUI<UI_Shop>();
        _warehouseUI.Refresh();
        _shopUI.Refresh();
    }

    public async void TryPurchase(int itemId, int quantity) {
        int slotIndex = FindEmptyPurchaseSlotIndex();
        if (slotIndex < 0) {
            Util.LogWarning("창고와 인벤토리가 모두 가득 찼습니다.");
            return;
        }
        InventoryItem[] snapshot = BuildInventorySnapshot();
        bool isSuccess = await Managers.Network.httpManager.PostPurchaseCall(
            itemId, slotIndex, quantity, snapshot, _cts.Token);
        if (isSuccess)
            OnPurchaseComplete();
    }

    private void OnPurchaseComplete() {
        Array.Clear(_inventorySlots, 0, _inventorySlots.Length);
        Array.Clear(_warehouseSlots, 0, _warehouseSlots.Length);
        Array.Clear(_loadoutSlots,   0, _loadoutSlots.Length);
        InventoryItem[] items = Managers.Network.httpManager.Inventory;
        if (items != null) {
            foreach (var item in items) {
                if (item.slot_index < 0) continue;
                if (item.slot_index < WAREHOUSE_SLOT_COUNT)
                    _warehouseSlots[item.slot_index] = item;
                else {
                    int invIndex = item.slot_index - WAREHOUSE_SLOT_COUNT;
                    if (invIndex < INVENTORY_SLOT_COUNT)
                        _inventorySlots[invIndex] = item;
                    else {
                        int loadoutIndex = invIndex - INVENTORY_SLOT_COUNT;
                        if (loadoutIndex < LOADOUT_SLOT_COUNT)
                            _loadoutSlots[loadoutIndex] = item;
                    }
                }
            }
        }

        _shopUI.Refresh();
        _warehouseUI.Refresh();
        _inventoryUI.Refresh();
    }

    private int FindEmptyPurchaseSlotIndex() {
        for (int i = 0; i < WAREHOUSE_SLOT_COUNT; i++) {
            if (_warehouseSlots[i] == null)
                return i;
        }
        for (int i = 0; i < INVENTORY_SLOT_COUNT; i++) {
            if (_inventorySlots[i] == null)
                return WAREHOUSE_SLOT_COUNT + i;
        }
        return -1;
    }

    private InventoryItem[] BuildInventorySnapshot() {
        if (Managers.Network.httpManager.Inventory == null)
            return new InventoryItem[0];

        var list = new System.Collections.Generic.List<InventoryItem>();
        for (int i = 0; i < WAREHOUSE_SLOT_COUNT; i++) {
            if (_warehouseSlots[i] == null) continue;
            _warehouseSlots[i].slot_index = i;
            list.Add(_warehouseSlots[i]);
        }
        for (int i = 0; i < INVENTORY_SLOT_COUNT; i++) {
            if (_inventorySlots[i] == null) continue;
            _inventorySlots[i].slot_index = WAREHOUSE_SLOT_COUNT + i;
            list.Add(_inventorySlots[i]);
        }
        for (int i = 0; i < LOADOUT_SLOT_COUNT; i++) {
            if (_loadoutSlots[i] == null) continue;
            _loadoutSlots[i].slot_index = WAREHOUSE_SLOT_COUNT + INVENTORY_SLOT_COUNT + i;
            list.Add(_loadoutSlots[i]);
        }
        return list.ToArray();
    }

    public async void TryMatchMake(int mapId, string loadoutType) {
        if (_lobbyState != LobbyState.Lobby) return;

        InventoryItem[] snapshot = loadoutType == "CUSTOM" ? BuildInventorySnapshot() : null;
        bool isSuccess = await Managers.Network.httpManager.StartMatchCall(
            mapId, loadoutType, snapshot, _cts.Token);
        if (isSuccess)
            _lobbyState = LobbyState.Matching;
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
                if (_beforeAuthState == BeforeAuthState.NoneSelected) {
                    BackToBeforeConnectPopup();
                } else {
                    BackToAuthNoneSelected();
                }
                break;
            case LobbyState.Lobby:
                if (_userState == UserState.Inventory || _userState == UserState.Shop)
                    BackToLobbyMain();
                break;
            case LobbyState.Matching:
                break;
        }
    }

    public void OnTabInput() { 
        switch (_lobbyState) {
            case LobbyState.BeforeConnect:
                break;
            case LobbyState.BeforeAuth:
                BeforeAuthTab();
                break;
            case LobbyState.Lobby:
                break;
            case LobbyState.Matching:
                break;
        }
    }

    public void OnEnterInput() { 
        switch (_lobbyState) {
            case LobbyState.BeforeConnect:
                break;
            case LobbyState.BeforeAuth:
                BeforeAuthEnter();
                break;
            case LobbyState.Lobby:
                break;
            case LobbyState.Matching:
                break;
        }
    }

    private void QuitPopup() {
        _lobbyReconfirmUI.ActiveConfirmOrCancel("Do you want to exit the game?", QuitGameApplication);
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

    // ----------------------------------------------
    // ---------- 드래그 앤 드롭 상태 관리 ----------
    // ----------------------------------------------
    ISlot _dragSourceSlot;
    DragGhost _dragGhost;

    public ISlot DragSource => _dragSourceSlot;

    private void InitDragGhost() {
        _dragGhost = UnityEngine.Object.FindAnyObjectByType<DragGhost>();

        if (_dragGhost != null) {
            _dragGhost.Init();
        }
    }

    public void OnSlotClick(ISlot slot) {
        if (!IsShiftPressed) return;
        if (slot is LSlot) return;

        InventoryItem item = slot.GetItem();
        if (item == null || item.quantity < 2) return;

        UI_Scene ui = slot.GetComponentInParent<UI_Scene>();

        if (ui is UI_Inventory) {
            ISlot emptySlot = _inventoryUI.FirstEmptySlot;
            if (emptySlot == null) return;

            int smaller = item.quantity / 2;
            int larger  = item.quantity - smaller;

            item.quantity = larger;
            _inventoryUI.SetItemAtSlot(slot.SlotIndex, item);

            InventoryItem splitItem = new InventoryItem { item_id = item.item_id, quantity = smaller };
            _inventoryUI.SetItemAtSlot(emptySlot.SlotIndex, splitItem);
        }
        else if (ui is UI_Warehouse) {
            ISlot emptySlot = _warehouseUI.FirstEmptySlot;
            if (emptySlot == null) return;

            int smaller = item.quantity / 2;
            int larger  = item.quantity - smaller;

            item.quantity = larger;
            _warehouseUI.SetItemAtSlot(slot.SlotIndex, item);

            InventoryItem splitItem = new InventoryItem { item_id = item.item_id, quantity = smaller };
            _warehouseUI.SetItemAtSlot(emptySlot.SlotIndex, splitItem);
        }
    }

    public void BeginDrag(ISlot source) {
        _dragSourceSlot = source;
        _dragGhost.BeginDrag(source);
        UpdateDragPosition(Mouse.current.position.ReadValue());
    }

    public void UpdateDragPosition(Vector2 screenPos) {
        _dragGhost.OnDrag(screenPos);
    }

    public void EndDrag() {
        _dragGhost.EndDrag();
        _dragSourceSlot = null;
    }

    private void OnDestroy() {
        Managers.Input.RemoveKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.Enter, OnEnterInput, InputManager.KeyState.Up);
        Managers.Input.RemoveKeyListener(Key.Tab, OnTabInput, InputManager.KeyState.Up);
        _cts.Cancel();
        _cts.Dispose();
        EndDrag();
    }
}
