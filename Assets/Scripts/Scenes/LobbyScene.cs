using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LobbyScene : BaseScene {
    UI_TestStart _startUI;
    UI_Auth _authUI;
    UI_Login _loginUI;
    UI_Register _registerUI;
    UI_Header _headerUI;
    UI_Inventory _inventoryUI;
    UI_Warehouse _warehouseUI;
    UI_Shop _shopUI;
    UI_MapSelect _mapSelectUI;
    UI_CharacterSelect _characterSelectUI;
    UI_MatchProcess _matchProgressUI;
    LobbyReconfirmUI _lobbyReconfirmUI;
    LobbySettingUI _lobbySettingUI;
    SelectedCharacter _selectedCharacter;

    const int INVENTORY_SLOT_COUNT = 25;
    const int WAREHOUSE_SLOT_COUNT = 80;
    const int LOADOUT_SLOT_COUNT = 3;
    InventoryItem[] _inventorySlots = new InventoryItem[INVENTORY_SLOT_COUNT];
    InventoryItem[] _warehouseSlots = new InventoryItem[WAREHOUSE_SLOT_COUNT];
    InventoryItem[] _loadoutSlots   = new InventoryItem[LOADOUT_SLOT_COUNT];

    private CancellationTokenSource _cts = new CancellationTokenSource();

    LobbyState _lobbyState = LobbyState.BeforeConnect;
    int _selectedCharacterType = 1;

    HTTPManager.LoginState _loginState => Managers.Network.httpManager.AuthState;

    enum LobbyState {
        BeforeConnect,
        BeforeAuth,
        Lobby,
        Matching,
    }

    protected override void Init() {
        base.Init();
        SceneType = Define.Scene.LobbyScene;
        Managers.Scene.ResetLoadSceneOp();

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
        _characterSelectUI = Managers.UI.CacheSceneUI<UI_CharacterSelect>();
        _matchProgressUI = Managers.UI.CacheSceneUI<UI_MatchProcess>();

        _lobbyReconfirmUI = BindSceneComponent<LobbyReconfirmUI>("LobbyReconfirmUI");
        if (_lobbyReconfirmUI != null) _lobbyReconfirmUI.Init();

        _lobbySettingUI = BindSceneComponent<LobbySettingUI>("LobbySettingUI");
        if (_lobbySettingUI != null) {
            _lobbySettingUI.Init();
            _lobbySettingUI.Hide();
        }

        _selectedCharacter = BindSceneComponent<SelectedCharacter>("SelectedCharacter");
        if (_selectedCharacter != null) {
            _selectedCharacter.Init();
            _selectedCharacter.SetCharacterType(_selectedCharacterType);
        }

        Managers.Input.AddKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.Enter, OnEnterInput, InputManager.KeyState.Up);
        Managers.Input.AddKeyListener(Key.Tab, OnTabInput, InputManager.KeyState.Up);
        Managers.Network.httpManager.OnSessionExpired += OnSessionExpired;
        InitDragGhost();

        // GameResultScene 경유 + 살아있는 세션이면 세션 유지 요청으로 Login 과정을 건너뛴다.
        // 버전 체크(GetVersionCall)는 프로세스 재시작이 아니라 앱 시작 시 검증이 유효하므로 생략한다
        bool tryResume = Managers.Scene.IsReturnFromGameResult
            && Managers.Network.httpManager.AuthState != HTTPManager.LoginState.None
            && !string.IsNullOrEmpty(Managers.Network.httpManager.SessionId);
        Managers.Scene.IsReturnFromGameResult = false;  // 소비 후 리셋

        _lobbyState = LobbyState.BeforeConnect;
        if (tryResume) {
            TryResumeSession();
        } else {
            Managers.UI.ShowSceneUI<UI_TestStart>();
        }

        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        Application.runInBackground = true;
    }

    // GameResultScene 경유 복귀 시 기존 세션으로 Login 과정을 건너뛴다.
    // 만료면 곧바로 폴백하고, 서버에 닿지 못한 경우에만 재시도를 물어본다 — 세션이 살아 있을 수 있어
    // 여기서 로컬 인증 상태를 지우면 복구할 수 없는 상태로 만들기 때문이다
    private async void TryResumeSession() {
        Util.Log("[Lobby] GameResultScene 경유 진입 — 세션 유지 요청");
        HTTPManager.ResumeResult result = await Managers.Network.httpManager.PostResumeSessionCall(_cts.Token);
        switch (result) {
            case HTTPManager.ResumeResult.Success:
                UI_Header.HeaderState hState = Managers.Network.httpManager.AuthState == HTTPManager.LoginState.Guest
                    ? UI_Header.HeaderState.Guest
                    : UI_Header.HeaderState.Logined;
                OnLoginComplete(hState);
                break;

            case HTTPManager.ResumeResult.Unreachable:
                _lobbyReconfirmUI.ActiveConfirmOrCancel(
                    "서버와 통신할 수 없어 세션을 이어받지 못했습니다.\n다시 시도하시겠습니까?\n(취소하면 로그인 화면으로 돌아갑니다)",
                    TryResumeSession,
                    FallbackToStartFlow);
                break;

            default:
                FallbackToStartFlow();
                // 폴백 UI를 먼저 띄운 뒤에 안내한다 — 팝업이 떠 있는 동안에는 다음 팝업이 무시된다
                _lobbyReconfirmUI.ActiveOnlyConfirm("세션이 만료되었습니다.\n다시 로그인해주세요.");
                break;
        }
    }

    private void FallbackToStartFlow() {
        Managers.Network.httpManager.ClearAuthStateLocal();
        _lobbyState = LobbyState.BeforeConnect;
        Managers.UI.ShowSceneUI<UI_TestStart>();
    }

    // -----------------------------------------------------
    // ---------- BeforeConnect 상태에서의 메서드 ----------
    // -----------------------------------------------------
    public async void TryConnectToServer() {
        Util.Log("TryConnectToServer 실행");
        HTTPManager.VersionResult result = await Managers.Network.httpManager.GetVersionCall(_cts.Token);
        if (result == HTTPManager.VersionResult.Success) {
            OnConnectedComplete();
            return;
        }

        // UI 복구를 갈래보다 먼저, 한 번만 부른다 — 갈래마다 복제하면 새 사유가 추가될 때
        // 하나가 빠지고, 그 순간 스피너가 계속 돌며 시작 버튼이 영영 굳는다
        // (푸는 곳이 UI_TestStart.Reload() 하나뿐이다)
        OnConnectedFailed();
        switch (result) {
            // 재시도는 사용자가 다시 누르는 것으로 한다 — 자동 재시도를 붙이지 말 것
            case HTTPManager.VersionResult.Maintenance:
                _lobbyReconfirmUI.ActiveOnlyConfirm("현재 서버가 점검중입니다.");
                break;

            case HTTPManager.VersionResult.VersionMismatch:
                _lobbyReconfirmUI.ActiveOnlyConfirm("버전이 일치하지 않습니다.\n최신 버전을 받아주세요.");
                break;

            default:
                _lobbyReconfirmUI.ActiveOnlyConfirm("서버 버전 확인에 실패했습니다.");
                break;
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
        HTTPManager.LoginResult result = await Managers.Network.httpManager.PostLoginCall(id, password, _cts.Token);
        switch (result) {
            case HTTPManager.LoginResult.Success:
                OnLoginComplete(UI_Header.HeaderState.Logined);
                break;

            // 진행 중인 매치와 매칭 성사를 서버가 같은 409로 보내며 안내도 하나로 묶는다 —
            // 둘 다 매치가 끝나기를 기다리는 것 외에 사용자가 할 수 있는 일이 없다
            case HTTPManager.LoginResult.AlreadyInGame:
                OnAuthRequestFinished();
                _lobbyReconfirmUI.ActiveOnlyConfirm("이미 진행중인 매치가 있습니다");
                break;

            default:
                OnAuthRequestFinished();
                _lobbyReconfirmUI.ActiveOnlyConfirm("아이디와 비밀번호를 확인해주세요.");
                break;
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

        RebuildSlotsFromInventory();

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
            _lobbyReconfirmUI.ActiveOnlyConfirm("계정생성 중 오류가 발생했습니다.");
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
            _lobbyReconfirmUI.ActiveOnlyConfirm("게스트 로그인에 실패했습니다.");
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
        Character,
    }

    UserState _userState = UserState.Main;

    public void ShowSettingUI() {
        _lobbySettingUI.Show();
    }

    public bool ActiveReconfirmConfirmOrCancel(string msg, Action onConfirm, Action onCancel = null) {
        return _lobbyReconfirmUI.ActiveConfirmOrCancel(msg, onConfirm, onCancel);
    }

    public bool ActiveReconfirmOnlyConfirm(string msg, Action onConfirm = null) {
        return _lobbyReconfirmUI.ActiveOnlyConfirm(msg, onConfirm);
    }

    public void LogoutPopup() {
        if (_lobbyState == LobbyState.Lobby) {
            _lobbyReconfirmUI.ActiveConfirmOrCancel("로그아웃을 진행하시겠습니까?", TryLogout);
        } else if (_lobbyState == LobbyState.Matching) {
            _lobbyReconfirmUI.ActiveConfirmOrCancel("매치를 취소하고 로그아웃하시겠습니까?", TryCancelMatchThenLogout);
        }
    }

    public async void TryLogout() {
        bool isSuccess = await Managers.Network.httpManager.PostLogoutCall(_cts.Token);
        if (isSuccess == true) {
            _lobbyReconfirmUI.ActiveOnlyConfirm("로그아웃되었습니다.", OnLogoutComplete);
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
        _matchProgressUI.StopMatching();
        Managers.UI.DisableUI("UI_MapSelect");
        Managers.UI.DisableUI("UI_Inventory");
        Managers.UI.DisableUI("UI_Warehouse");
        Managers.UI.DisableUI("UI_Shop");
        Managers.UI.DisableUI("UI_CharacterSelect");
        Managers.UI.DisableUI("UI_MatchProcess");

        Array.Clear(_inventorySlots, 0, _inventorySlots.Length);
        Array.Clear(_warehouseSlots, 0, _warehouseSlots.Length);
        Array.Clear(_loadoutSlots,   0, _loadoutSlots.Length);
    }

    // 인증 요청이 401을 받았을 때. 감지는 HTTPManager 한 곳에 있고 여기는 결과만 받는다.
    // 정리·전이를 먼저 하고 팝업은 통보만 시킨다 — ActiveOnlyConfirm은 다른 팝업이 떠 있으면
    // 아무것도 하지 않고 false를 돌려주므로, 전이를 버튼 콜백에 걸면 팝업이 묻히는 순간
    // 전이까지 함께 사라져 죽은 세션인 채로 로비에 남는다(TryResumeSession과 같은 순서)
    private void OnSessionExpired() {
        if (_lobbyState != LobbyState.Lobby && _lobbyState != LobbyState.Matching)
            return;

        Managers.Network.httpManager.ClearAuthStateLocal();
        OnLogoutComplete();
        _lobbyReconfirmUI.ActiveOnlyConfirm("세션이 만료되었습니다.\n다시 로그인해주세요.");
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
        Managers.UI.DisableUI("UI_CharacterSelect");
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
        Managers.UI.DisableUI("UI_CharacterSelect");
        Managers.UI.ShowSceneUI<UI_MapSelect>();
    }

    public void ShowShop() {
        if (_lobbyState != LobbyState.Lobby || _userState == UserState.Shop)
            return;

        _userState = UserState.Shop;

        _mapSelectUI.SetNormalState();
        Managers.UI.DisableUI("UI_MapSelect");
        Managers.UI.DisableUI("UI_Inventory");
        Managers.UI.DisableUI("UI_CharacterSelect");
        Managers.UI.ShowSceneUI<UI_Warehouse>();
        Managers.UI.ShowSceneUI<UI_Shop>();
        _warehouseUI.Refresh();
        _shopUI.Refresh();
    }

    public void ShowCharacter() {
        if (_lobbyState != LobbyState.Lobby || _userState == UserState.Character)
            return;

        _userState = UserState.Character;
        _mapSelectUI.SetNormalState();
        Managers.UI.DisableUI("UI_MapSelect");
        Managers.UI.DisableUI("UI_Inventory");
        Managers.UI.DisableUI("UI_Warehouse");
        Managers.UI.DisableUI("UI_Shop");
        Managers.UI.ShowSceneUI<UI_CharacterSelect>();
        _characterSelectUI.Refresh();
    }

    // 공간 검사가 확인 팝업보다 앞이어야 한다 — 확인 콜백이 도는 동안에는 isActive가 아직 true라
    // 거기서 띄우는 팝업은 조용히 무시된다
    public void TryPurchase(int itemId, int quantity) {
        if (FindEmptyPurchaseSlotIndex() < 0) {
            _lobbyReconfirmUI.ActiveOnlyConfirm("창고에 여유 공간이 없습니다.");
            return;
        }

        _lobbyReconfirmUI.ActiveConfirmOrCancel(
            $"{ItemDBHelper.GetName(itemId)} {quantity}개를\n구매하시겠습니까?",
            () => ExecutePurchase(itemId, quantity));
    }

    // 슬롯과 스냅샷은 팝업이 닫힌 이 시점에 다시 만든다 — 사이에 사용자 대기 구간이 있어
    // 진입점에서 잡은 값은 낡을 수 있다
    private async void ExecutePurchase(int itemId, int quantity) {
        int slotIndex = FindEmptyPurchaseSlotIndex();
        if (slotIndex < 0) {
            Util.LogWarning("창고에 여유 공간이 없습니다.");
            return;
        }
        InventoryItem[] snapshot = BuildInventorySnapshot();
        HTTPManager.PurchaseResult result = await Managers.Network.httpManager.PostPurchaseCall(
            itemId, slotIndex, quantity, snapshot, _cts.Token);

        switch (result) {
            case HTTPManager.PurchaseResult.Success:
                OnTradeComplete();
                break;

            // 서버가 아무것도 바꾸지 않았고 내 스냅샷도 여전히 유효하므로 재조회하지 않는다
            case HTTPManager.PurchaseResult.NotEnoughMoney:
                _lobbyReconfirmUI.ActiveOnlyConfirm("잔액이 부족합니다.");
                break;

            // 재조회를 끝낸 뒤에 안내한다 — 먼저 띄우면 갱신되었다는 안내가 갱신 전에 뜬다
            case HTTPManager.PurchaseResult.OutOfSync:
                await ResyncInventory();
                _lobbyReconfirmUI.ActiveOnlyConfirm("인벤토리 정보가 갱신되었습니다.\n다시 시도해주세요.");
                break;

            case HTTPManager.PurchaseResult.Rejected:
                await ResyncInventory();
                _lobbyReconfirmUI.ActiveOnlyConfirm("구매에 실패했습니다.");
                break;

            // 서버에 닿지도 못한 것이라 재조회해도 같은 이유로 실패한다
            case HTTPManager.PurchaseResult.Unreachable:
                _lobbyReconfirmUI.ActiveOnlyConfirm("서버와 통신할 수 없습니다.");
                break;

            // Busy는 요청이 나가지도 않은 것이라 알리지 않는다
        }
    }

    // 재조회가 실패해도 호출자의 안내는 예정대로 나간다 — 구매가 실패했다는 사실은 달라지지 않는다
    private async Task ResyncInventory() {
        bool isSuccess = await Managers.Network.httpManager.GetInventoryCall(_cts.Token);
        if (isSuccess == false) {
            Util.LogWarning("인벤토리 재조회에 실패했습니다.");
            return;
        }

        RebuildSlotsFromInventory();
        _warehouseUI.Refresh();
        _inventoryUI.Refresh();
    }

    // LSlot은 OnSlotClick에서 이미 걸러지므로 대상은 창고·인벤토리 슬롯뿐이다.
    // 팝업이 뜬 뒤 슬롯 내용이 바뀔 수 있어 item_id를 함께 넘겨 전송 직전에 대조한다 —
    // 안 하면 팝업이 말한 것과 다른 물건이 팔린다
    private void TrySell(ISlot slot) {
        if (_lobbyState != LobbyState.Lobby) return;

        InventoryItem item = slot.GetItem();
        if (item == null) return;

        int slotIndex = ToServerSlotIndex(slot);
        if (slotIndex < 0) return;

        int itemId = item.item_id;
        _lobbyReconfirmUI.ActiveConfirmOrCancel(
            $"{ItemDBHelper.GetName(itemId)} {item.quantity}개를\n판매하시겠습니까?",
            () => ExecuteSell(slotIndex, itemId));
    }

    // 변환식은 BuildInventorySnapshot()과 같아야 한다 — 갈리면 서버가 다른 슬롯을 지운다
    private int ToServerSlotIndex(ISlot slot) {
        UI_Scene ui = slot.GetComponentInParent<UI_Scene>();
        if (ui is UI_Warehouse) return slot.SlotIndex;
        if (ui is UI_Inventory) return WAREHOUSE_SLOT_COUNT + slot.SlotIndex;
        return -1;
    }

    // item_id·quantity는 판매 지시가 아니라 서버가 스냅샷과 대조하는 검사값이라,
    // 스냅샷을 만든 뒤 그것이 확정한 값에서 읽는다 — 따로 읽으면 어긋나 ERR_ITEM_MISMATCH가 된다
    private async void ExecuteSell(int slotIndex, int expectedItemId) {
        InventoryItem[] snapshot = BuildInventorySnapshot();

        InventoryItem target = null;
        foreach (var item in snapshot) {
            if (item.slot_index != slotIndex) continue;
            target = item;
            break;
        }
        if (target == null || target.item_id != expectedItemId) {
            Util.LogWarning("판매하려던 아이템이 슬롯에 없습니다.");
            return;
        }

        HTTPManager.SellResult result = await Managers.Network.httpManager.PostSellCall(
            target.item_id, slotIndex, target.quantity, snapshot, _cts.Token);

        switch (result) {
            case HTTPManager.SellResult.Success:
                OnTradeComplete();
                break;

            // 재조회를 끝낸 뒤에 안내한다 — 먼저 띄우면 갱신되었다는 안내가 갱신 전에 뜬다
            case HTTPManager.SellResult.OutOfSync:
                await ResyncInventory();
                _lobbyReconfirmUI.ActiveOnlyConfirm("인벤토리 정보가 갱신되었습니다.\n다시 시도해주세요.");
                break;

            case HTTPManager.SellResult.Rejected:
                await ResyncInventory();
                _lobbyReconfirmUI.ActiveOnlyConfirm("판매에 실패했습니다.");
                break;

            // 서버에 닿지도 못한 것이라 재조회해도 같은 이유로 실패한다
            case HTTPManager.SellResult.Unreachable:
                _lobbyReconfirmUI.ActiveOnlyConfirm("서버와 통신할 수 없습니다.");
                break;

            // Busy는 요청이 나가지도 않은 것이라 알리지 않는다
        }
    }

    // 매매는 서버가 판정하므로 소리도 요청 지점이 아니라 여기서 낸다(인게임 조작과 같은 규칙) —
    // 로컬 판정인 슬롯 드래그·분할이 조작 지점에서 내는 것과 갈리는 자리다
    private void OnTradeComplete() {
        RebuildSlotsFromInventory();

        _shopUI.Refresh();
        _warehouseUI.Refresh();
        _inventoryUI.Refresh();
        Managers.Sound.PlayInventoryChange();
    }

    // HTTPManager.Inventory(0~107 평면 목록)를 창고·인벤토리·로드아웃 세 배열로 되돌린다.
    // 소비자가 셋(로그인 완료·구매 완료·재조회)이라 한 곳에 둔다 — 흩으면 슬롯 경계 계산이 갈린다
    private void RebuildSlotsFromInventory() {
        Array.Clear(_inventorySlots, 0, _inventorySlots.Length);
        Array.Clear(_warehouseSlots, 0, _warehouseSlots.Length);
        Array.Clear(_loadoutSlots,   0, _loadoutSlots.Length);

        InventoryItem[] items = Managers.Network.httpManager.Inventory;
        if (items == null) return;

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

    // 구매 대상 슬롯은 창고(0~79)뿐이다 — 서버가 그 밖의 인덱스를 ERR_INVALID_SLOT으로 거부하므로
    // 인벤토리에 빈 칸이 있어도 후보가 아니다
    private int FindEmptyPurchaseSlotIndex() {
        for (int i = 0; i < WAREHOUSE_SLOT_COUNT; i++) {
            if (_warehouseSlots[i] == null)
                return i;
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

    public int SelectedCharacterType => _selectedCharacterType;

    public void SetCharacterType(int characterType) {
        _selectedCharacterType = characterType;
        if (_selectedCharacter != null)
            _selectedCharacter.SetCharacterType(characterType);
    }

    public async void TryMatchMake(int mapId, string loadoutType) {
        if (_lobbyState != LobbyState.Lobby) return;

        if (loadoutType == "CUSTOM" && _loadoutSlots[0] == null && _loadoutSlots[1] == null) {
            _lobbyReconfirmUI.ActiveOnlyConfirm("무기를 장착해주세요.\n(주무기 또는 보조무기 중 하나 이상 필수)");
            return;
        }

        InventoryItem[] snapshot = loadoutType == "CUSTOM" ? BuildInventorySnapshot() : null;
        bool isSuccess = await Managers.Network.httpManager.StartMatchCall(
            mapId, _selectedCharacterType, loadoutType, snapshot, _cts.Token);
        if (isSuccess) {
            _lobbyState = LobbyState.Matching;
            EnterMatchingState();
        }
    }

    // ------------------------------------------------
    // ---------- Matching 상태에서의 메서드 ----------
    // ------------------------------------------------
    private async void TryCancelMatchThenLogout() {
        bool isSuccess = await Managers.Network.httpManager.CancelMatchCall(_cts.Token);
        if (isSuccess)
            TryLogout();
    }

    public void OnMatchCancelBtnClick() {
        _lobbyReconfirmUI.ActiveConfirmOrCancel(
            "매칭을 취소하시겠습니까?",
            TryCancelMatch,
            _matchProgressUI.RestoreCancelButton);
    }

    public async void TryCancelMatch() {
        bool isSuccess = await Managers.Network.httpManager.CancelMatchCall(_cts.Token);
        if (isSuccess) {
            ExitMatchingState();
            Managers.UI.ShowSceneUI<UI_MapSelect>();
        } else {
            _lobbyReconfirmUI.ActiveOnlyConfirm("매칭 취소에 실패했습니다.");
            _matchProgressUI.RestoreCancelButton();
        }
    }

    private void ExitMatchingState() {
        _lobbyState = LobbyState.Lobby;
        _matchProgressUI.StopMatching();
        Managers.UI.DisableUI("UI_MatchProcess");
    }

    private void OnMatchingSuccess() {
        _matchProgressUI.StopMatching();
        Util.Log("[Matching] 매칭 성공. 게임 씬 로드 예정");
    }

    private void EnterMatchingState() {
        _userState = UserState.Main;
        _mapSelectUI.SetNormalState();
        Managers.UI.DisableUI("UI_MapSelect");
        Managers.UI.DisableUI("UI_Inventory");
        Managers.UI.DisableUI("UI_Warehouse");
        Managers.UI.DisableUI("UI_Shop");
        Managers.UI.DisableUI("UI_CharacterSelect");
        Managers.UI.ShowSceneUI<UI_MatchProcess>();
        Managers.Network.httpManager.StartMatchPolling(OnMatchingSuccess, _cts.Token);
    }


    // ------------------------------------------------
    // ---------- 공통적으로 사용되는 메서드 ----------
    // ------------------------------------------------
    // 한 번의 입력은 가장 위에 있는 것 하나만 소비한다. 팝업·설정 창은 _lobbyState와 무관한
    // 오버레이라 분기 바깥에서 먼저 처리한다 — 안으로 넣으면 상태마다 같은 가드가 복제되고
    // 새 상태가 생길 때 반드시 하나가 빠져, 팝업이 뜬 채 뒤 화면만 바뀐다
    public void OnEscapeInput() {
        if (_lobbyReconfirmUI != null && _lobbyReconfirmUI.IsActive) {
            _lobbyReconfirmUI.DismissByEscape();
            return;
        }

        if (_lobbySettingUI != null && _lobbySettingUI.IsShown) {
            _lobbySettingUI.CancelByEscape();
            return;
        }

        // 팝업을 여는 갈래는 무음이다 — 팝업이 자기 확인·취소음을 내므로 두 번 울린다
        switch (_lobbyState) {
            case LobbyState.BeforeConnect:
                QuitPopup();
                break;
            case LobbyState.BeforeAuth:
                if (_beforeAuthState == BeforeAuthState.NoneSelected) {
                    BackToBeforeConnectPopup();
                } else {
                    Managers.Sound.PlayUIReturn();
                    BackToAuthNoneSelected();
                }
                break;
            case LobbyState.Lobby:
                // Main이 아니면 뒤로가기다. 나열형으로 두면 UserState가 늘 때 조용히 빠뜨린다
                if (_userState == UserState.Main) {
                    LogoutPopup();
                } else {
                    Managers.Sound.PlayUIReturn();
                    BackToLobbyMain();
                }
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
        _lobbyReconfirmUI.ActiveConfirmOrCancel("게임을 종료하시겠습니까?", QuitGameApplication);
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

    // LSlot 제외는 좌·우 공통이다 — 분할은 무기·방어구가 스택되지 않아서고(CanMerge),
    // 판매는 매치 시작 요청과 로드아웃을 두고 경합해 서버 트랜잭션이 필요해지기 때문이다
    public void OnSlotClick(ISlot slot, PointerEventData.InputButton button) {
        if (!IsShiftPressed) return;
        if (slot is LSlot) return;

        if (button == PointerEventData.InputButton.Right) {
            TrySell(slot);
            return;
        }
        if (button != PointerEventData.InputButton.Left) return;

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
            Managers.Sound.PlayInventoryChange();
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
            Managers.Sound.PlayInventoryChange();
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
        // 종료 중에는 OnApplicationQuit이 먼저 돌아 Managers.Instance가 null이다.
        // 취소 토큰 정리는 그 경우에도 해야 하므로 early return이 아니라 이 블록만 감싼다
        if (Managers.Instance != null) {
            Managers.Input.RemoveKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Up);
            Managers.Input.RemoveKeyListener(Key.Enter, OnEnterInput, InputManager.KeyState.Up);
            Managers.Input.RemoveKeyListener(Key.Tab, OnTabInput, InputManager.KeyState.Up);
            Managers.Network.httpManager.OnSessionExpired -= OnSessionExpired;
        }
        _cts.Cancel();
        _cts.Dispose();
        if (_dragGhost != null)
            EndDrag();
    }
}
