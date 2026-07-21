using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class IngameScene : BaseScene {
    private bool _operationFlag = false;
    private bool _spawnCompleted = false;
    private bool _cursorLocked = true;
    public bool _itemLoaded = false;
    private bool _weaponInitialized = false;
    private bool _isContainerOpen = false;
    private int _uiOpenCount = 0;
    public bool IsAnyUIOpen => _uiOpenCount > 0;

    private const float PLAYER_STATE_INTERVAL = 0.1f;
    private float _playerStateTimer = 0f;

    private bool _isGetResponseSpawnMe = false;
    private Vector3 _spawnPoint;
    private int _characterType = -1;
    private uint _myObjectId = 0;

    private GameObject _characterGo;
    private PlayerController _playerController;
    public PlayerController PlayerController => _playerController;
    private Dictionary<uint, OppoPlayerController> _oppoPlayers = new Dictionary<uint, OppoPlayerController>();

    // ── Weapon Prefab Cache ──
    private Dictionary<int, GameObject> _weaponPrefabCache;
    public Dictionary<int, GameObject> WeaponPrefabCache {
        get {
            if (_weaponPrefabCache == null)
                InitWeaponPrefabCache();
            return _weaponPrefabCache;
        }
    }

    // ── Inventory ──
    private IngameInventory _inventory = new IngameInventory();
    public IngameInventory Inventory => _inventory;

    // ── Interact ──
    private bool _canInteract = false;
    private string _interactText;
    private InteractableGameObjectController _interactTarget;
    public bool CanInteract => _canInteract;
    public string InteractText => _interactText;
    public InteractableGameObjectController InteractTarget => _interactTarget;

    public void SetInteractState(bool canInteract, InteractableGameObjectController target) {
        _canInteract = canInteract;
        _interactTarget = target;
        _interactText = target != null ? target.InteractText : null;
    }

    IngameISlot _ingameDragSourceSlot;
    IngameDragGhost _ingameDragGhost;
    IngameInventoryUI _ingameInventoryUI;
    InteractUI _interactUI;
    IngameHealthBarUI _ingameHealthBarUI;

    protected override void Init() {
        base.Init();
        foreach (ObjectData data in Managers.Scene.NextSceneStaticContext.ObjectDatas)
            Managers.Resource.InstantiateFromObjectDataStruct(data);
        
        Managers.Scene.ResetLoadSceneOp();

        GameObject inventoryObj = GameObject.Find("IngameInventoryUI");
        if (inventoryObj != null) {
            _ingameInventoryUI = inventoryObj.GetComponent<IngameInventoryUI>();
            _ingameInventoryUI.Init(this);
        }

        GameObject dragGhostObj = GameObject.Find("IngameDragGhost");
        if (dragGhostObj != null) {
            _ingameDragGhost = dragGhostObj.GetComponent<IngameDragGhost>();
            _ingameDragGhost.Init();
        }

        GameObject interactObj = GameObject.Find("InteractUI");
        if (interactObj != null) {
            _interactUI = interactObj.GetComponent<InteractUI>();
            _interactUI.Init(this);
        }

        GameObject healthBarObj = GameObject.Find("IngameHealthBarUI");
        if (healthBarObj != null) {
            _ingameHealthBarUI = healthBarObj.GetComponent<IngameHealthBarUI>();
        }

        Managers.Input.AddKeyListener(Key.I, TryCloseContainerUI, InputManager.KeyState.Down);
    }

    private void OnDestroy() {
        if (Managers.Instance == null) return;
        Managers.Input.RemoveKeyListener(Key.I, TryCloseContainerUI, InputManager.KeyState.Down);
    }

    public void TryCompleteSpawnMe() {
        if (_spawnCompleted) return;
        if (_isGetResponseSpawnMe && Managers.Scene.SceneDynamicContext.IsComplete())
            SpawnMeAndRequestPlayerObjects();
    }

    public void HandleSpawnSpot(Vector3 spawnPoint, int characterType, uint objectId) {
        _spawnPoint = spawnPoint;
        _characterType = characterType;
        _myObjectId = objectId;
        _isGetResponseSpawnMe = true;
        TryCompleteSpawnMe();
    }

    private void SpawnMeAndRequestPlayerObjects() {
        _characterGo = Managers.Resource.Instantiate("GameObject/PlayerObject");
        _characterGo.transform.position = _spawnPoint;
        _playerController = _characterGo.GetComponent<PlayerController>();
        _playerController.Setup(_characterType);
        _playerController.SetObjectId((int)_myObjectId);
        _spawnCompleted = true;
        TryInitWeapon();
        SetCursorLock(true);

        foreach (ObjectData data in Managers.Scene.SceneDynamicContext.ObjectDatas)
            Managers.Resource.InstantiateFromObjectDataStruct(data);

        Managers.Scene.SceneDynamicContext.Clear();
        Managers.Network.udpManager.SendC2DRequestSpawnPlayerObjects();
    }

    public void SpawnPlayerObject(PlayerSpawnData data) {
        if (data.ObjectId == _myObjectId) return;
        if (_oppoPlayers.ContainsKey(data.ObjectId)) return;

        GameObject go = Managers.Resource.Instantiate("GameObject/OppoPlayerObject");
        OppoPlayerController controller = go.GetComponent<OppoPlayerController>();
        controller.SetObjectId((int)data.ObjectId);
        controller.SetPosition(data.Position);
        controller.SetRotation(data.Rotation);
        controller.Setup(data.CharacterType);
        if (data.WeaponId != 0)
            controller.EquipWeapon(data.WeaponId);
        _oppoPlayers[data.ObjectId] = controller;
    }

    public void SpawnPlayerObjects(List<PlayerSpawnData> players) {
        foreach (PlayerSpawnData data in players)
            SpawnPlayerObject(data);
        _operationFlag = true;
        Managers.Network.udpManager.SendC2DNotifyLoadingComplete();
    }

    public void UpdatePlayerStates(List<PlayerStateData> playerStateDatas) {
        foreach (PlayerStateData data in playerStateDatas) {
            if (data.ObjectId == _myObjectId) continue;

            if (_oppoPlayers.TryGetValue(data.ObjectId, out OppoPlayerController controller)) {
                controller.ApplyState(data);
            } else {
                Managers.Network.udpManager.SendC2DRequestSpawnByObjectId((int)data.ObjectId);
            }
        }
    }

    public void TryInitWeapon() {
        if (_weaponInitialized) return;
        if (!_spawnCompleted || !_itemLoaded) return;
        _weaponInitialized = true;
        _inventory.InitWeapon();
    }

    private void InitWeaponPrefabCache() {
        _weaponPrefabCache = new Dictionary<int, GameObject>();
        GameObject[] allWeapons = Resources.LoadAll<GameObject>("Prefabs/Weapons");
        foreach (var prefab in allWeapons) {
            string[] parts = prefab.name.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int id)) {
                _weaponPrefabCache[id] = prefab;
            }
        }
    }

    public bool IsContainerOpen => _isContainerOpen;

    public void TryInteract() {
        if (IsContainerOpen) {
            CloseContainer();
            return;
        }
        if (!_canInteract || _interactTarget == null)
            return;
        _interactTarget.Interact();
    }

    public void TryCloseContainerUI() {
        if (IsContainerOpen)
            CloseContainer();
    }

    public void SyncInventoryUI() {
        if (_ingameInventoryUI == null) return;
        _ingameInventoryUI.SyncMyInventory();
        _ingameInventoryUI.SyncEquipment();
        SyncHealthBarMax();
    }

    private void SyncHealthBarMax() {
        if (_ingameHealthBarUI == null) return;

        _ingameHealthBarUI.SetMaxHP(100000f);

        InventoryItem armor = _inventory.Armor;
        if (armor != null && ItemDBHelper.TryGetArmorSpec(armor.item_id, out ArmorSpec armorSpec))
            _ingameHealthBarUI.SetMaxShield(armorSpec.MaxShieldPoint);
        else
            _ingameHealthBarUI.SetMaxShield(0f);
    }

    public void SyncContainerUI() {
        if (_ingameInventoryUI == null) return;
        _ingameInventoryUI.SyncContainer();
    }

    public void ShowOpenedContainer() {
        if (_ingameInventoryUI == null) return;
        _ingameInventoryUI.SyncMyInventory();
        _ingameInventoryUI.SyncEquipment();
        _ingameInventoryUI.SyncContainer();
        _ingameInventoryUI.ActiveLootBox();
        _isContainerOpen = true;
        OnUIOpened();
    }

    public void CloseContainer() {
        Managers.Network.udpManager.SendC2DCloseContainer();
        _inventory.ClearContainer();
        _isContainerOpen = false;
        if (_ingameInventoryUI != null)
            _ingameInventoryUI.DeactiveThis();
        OnUIClosed();
    }

    public void RequestOpenContainer(uint containerObjectId) {
        Managers.Network.udpManager.SendC2DRequestOpenContainer(containerObjectId);
    }

    // ── Drag ──

    private const uint PLAYER_OBJECT_ID = 0xFFFFFFFF;
    public IngameISlot DragSource => _ingameDragSourceSlot;

    public void BeginDrag(IngameISlot source) {
        _ingameDragSourceSlot = source;
        _ingameDragGhost.BeginDrag(source);
        UpdateDragPosition(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
    }

    public void UpdateDragPosition(Vector2 screenPos) {
        _ingameDragGhost.OnDrag(screenPos);
    }

    public void EndDrag() {
        _ingameDragGhost.EndDrag();
        _ingameDragSourceSlot = null;
    }

    // ── 서버 요청 ──

    private uint GetObjectId(SlotOwnerType ownerType) {
        return ownerType == SlotOwnerType.PlayerInventory
            ? PLAYER_OBJECT_ID
            : _inventory.InteractingContainerObjectId;
    }

    private uint GetVersion(SlotOwnerType ownerType) {
        return ownerType == SlotOwnerType.PlayerInventory
            ? _inventory.InventoryVersion
            : _inventory.InteractingContainerVersion;
    }

    public void RequestInteractContainerObject(uint interactType, IngameISlot source, IngameISlot target) {
        uint startObjectId = GetObjectId(source.OwnerType);
        uint startVersion  = GetVersion(source.OwnerType);
        uint startSlotIdx  = (uint)source.SlotIndex;
        int  quantity      = source.GetItem() != null ? source.GetItem().quantity : 0;
        uint endObjectId   = GetObjectId(target.OwnerType);
        uint endVersion    = GetVersion(target.OwnerType);
        uint endSlotIdx    = (uint)target.SlotIndex;

        Managers.Network.udpManager.SendC2DRequestInteractContainerObject(
            interactType, startObjectId, startVersion, startSlotIdx,
            quantity, endObjectId, endVersion, endSlotIdx);
    }

    public void RequestEquipItem(uint actionType, uint equipmentSlotType, IngameISlot slot) {
        uint objectId           = GetObjectId(slot.OwnerType);
        uint version            = GetVersion(slot.OwnerType);
        uint slotIdx            = (uint)slot.SlotIndex;
        uint myInventoryVersion = slot.OwnerType == SlotOwnerType.PlayerInventory
            ? 0
            : _inventory.InventoryVersion;

        Managers.Network.udpManager.SendC2DRequestEquipItem(
            actionType, equipmentSlotType, objectId, version, slotIdx, myInventoryVersion);
    }

    // ── 서버 응답 처리 ──

    public void ApplyInteractContainerObject(uint interactType,
        uint startObjectId, uint startVersion, uint startSlotIdx,
        int quantity,
        uint endObjectId, uint endVersion, uint endSlotIdx) {

        InventoryItem startItem = _inventory.GetSlotByObjectId(startObjectId, startSlotIdx);
        InventoryItem endItem   = _inventory.GetSlotByObjectId(endObjectId, endSlotIdx);

        switch (interactType) {
            case 0: // get: 아이템을 빈 슬롯으로 이동
                _inventory.SetSlotByObjectId(endObjectId, endSlotIdx, startItem);
                _inventory.SetSlotByObjectId(startObjectId, startSlotIdx, null);
                break;
            case 1: // swap: 서로 교환
                _inventory.SetSlotByObjectId(startObjectId, startSlotIdx, endItem);
                _inventory.SetSlotByObjectId(endObjectId, endSlotIdx, startItem);
                break;
            case 2: // merge: 수량 합산
                if (endItem != null && startItem != null) {
                    endItem.quantity += startItem.quantity;
                    _inventory.SetSlotByObjectId(endObjectId, endSlotIdx, endItem);
                    _inventory.SetSlotByObjectId(startObjectId, startSlotIdx, null);
                }
                break;
        }

        _inventory.SetVersionByObjectId(startObjectId, startVersion);
        _inventory.SetVersionByObjectId(endObjectId, endVersion);
        _inventory.FindEmptySlotIdx();
        SyncInventoryUI();
        if (_isContainerOpen && _ingameInventoryUI != null)
            _ingameInventoryUI.SyncContainer();
    }

    public void ApplyEquipItem(uint actionType, uint equipmentSlotType,
        uint objectId, uint objectVersion, uint objectSlotIdx, uint myInventoryVersion) {

        if (actionType == 0) {
            // equip: 슬롯 → 장비
            InventoryItem item = _inventory.GetSlotByObjectId(objectId, objectSlotIdx);
            InventoryItem prevEquip = _inventory.GetEquipmentSlot(equipmentSlotType);
            _inventory.SetEquipmentSlot(equipmentSlotType, item);
            _inventory.SetSlotByObjectId(objectId, objectSlotIdx, prevEquip);
        } else {
            // unequip: 장비 → 슬롯
            InventoryItem equipItem = _inventory.GetEquipmentSlot(equipmentSlotType);
            InventoryItem slotItem = _inventory.GetSlotByObjectId(objectId, objectSlotIdx);
            _inventory.SetSlotByObjectId(objectId, objectSlotIdx, equipItem);
            _inventory.SetEquipmentSlot(equipmentSlotType, slotItem);
        }

        _inventory.SetVersionByObjectId(objectId, objectVersion);
        if (objectId != PLAYER_OBJECT_ID)
            _inventory.SetVersionByObjectId(PLAYER_OBJECT_ID, myInventoryVersion);
        _inventory.FindEmptySlotIdx();
        SyncInventoryUI();
        if (_isContainerOpen && _ingameInventoryUI != null)
            _ingameInventoryUI.SyncContainer();

        // 무기 슬롯이 변경된 경우 무기 장착 갱신
        if (equipmentSlotType <= 1 && _playerController != null) {
            InventoryItem currentWeapon = _inventory.IsPrimaryWeaponApplyed
                ? _inventory.PrimaryWeapon
                : _inventory.SecondaryWeapon;
            if (currentWeapon != null)
                _playerController.EquipWeapon(currentWeapon.item_id);
        }
    }

    public void HandleInteractContainerObjectDeny(uint denyReasonMask) {
        Util.LogError($"[InteractContainerObjectDeny] denyReasonMask=0x{denyReasonMask:X}");
        RequestRecentInventoryInfo();
    }

    public void HandleEquipItemDeny(uint denyReasonMask) {
        Util.LogError($"[EquipItemDeny] denyReasonMask=0x{denyReasonMask:X}");
        RequestRecentInventoryInfo();
    }

    public void HandleHealthChange(int healthPoint, int shieldPoint, int reason) {
        if (_ingameHealthBarUI != null) {
            _ingameHealthBarUI.SetHP(healthPoint);
            _ingameHealthBarUI.SetArmor(shieldPoint);
        }
    }

    public void HandleWeaponFireBroadcast(uint shooterObjectId, bool hasHitPoint, Vector3 hitPoint) {
        if (!_oppoPlayers.TryGetValue(shooterObjectId, out OppoPlayerController shooter))
            return;

        // TODO: 발사자 총구 이펙트 (머즐 플래시, 총성 등)

        if (hasHitPoint) {
            // TODO: 탄착 이펙트 재생 (hitPoint 좌표에)
        }
    }

    private void RequestRecentInventoryInfo() {
        Managers.Network.udpManager.SendC2DRequestRecentInventoryInfo(PLAYER_OBJECT_ID);
        if (IsContainerOpen)
            Managers.Network.udpManager.SendC2DRequestRecentInventoryInfo(_inventory.InteractingContainerObjectId);
    }

    protected void RequestSpawnMe() {
        Managers.Network.udpManager.SendC2DRequestSpawnMe();
    }

    public void OnUIOpened() {
        _uiOpenCount++;
        if (_uiOpenCount == 1)
            SetCursorLock(false);
    }

    public void OnUIClosed() {
        _uiOpenCount--;
        if (_uiOpenCount <= 0) {
            _uiOpenCount = 0;
            SetCursorLock(true);
        }
    }

    private void SetCursorLock(bool isLocked) {
        _cursorLocked = isLocked;
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    protected void OnUpdate() {
        if (_operationFlag == false)
            return;

        _playerStateTimer += Time.deltaTime;
        if (_playerStateTimer >= PLAYER_STATE_INTERVAL) {
            _playerStateTimer = 0f;
            SendPlayerState();
        }

        // 인터랙션 UI 업데이트
        if (_interactUI != null) {
            if (_canInteract)
                _interactUI.Show(_interactText);
            else
                _interactUI.Hide();
        }
    }

    private void SendPlayerState() {
        Managers.Network.udpManager.SendC2DUpdatePlayerState(
            _playerController.ObjectId,
            _playerController.transform.position,
            _playerController.Yaw,
            _playerController.Pitch,
            _playerController.Velocity,
            _playerController.MovementState,
            _playerController.ActionState
        );
    }
}
