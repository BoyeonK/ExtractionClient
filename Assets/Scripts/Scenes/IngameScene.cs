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
    private bool _recallRequested = false;
    private int _uiOpenCount = 0;
    public bool IsAnyUIOpen => _uiOpenCount > 0;

    private const float PLAYER_STATE_INTERVAL = 0.1f;
    private float _playerStateTimer = 0f;

    // TEMP: 귀환 응답 워치독
    //       "귀환이 실패하면 서버가 반드시 알린다"는 전제가 깨졌을 때(통지 유실,
    //       SESSION_LOST/SERVER_INTERNAL처럼 통지 경로 자체가 불안한 사유, UDP 끊김)
    //       _recallRequested가 영구히 잠겨 그 판 탈출이 불가능해지는 것을 막는 임시 안전장치.
    //       결과를 추측하지 않고 로컬 잠금만 해제하므로 판정 권한은 서버에 그대로 있다.
    //       서버 통지 신뢰성이 검증되면 이 블록과 OnUpdate()의 TEMP 블록을 함께 제거할 것.
    private const float RECALL_TIMEOUT = 10f;   // 서버 검사 5초 + 왕복·지터 여유
    private float _recallTimer = 0f;

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
            SpawnObject(data);

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
            _ingameHealthBarUI.Init();
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
            SpawnObject(data);

        Managers.Scene.SceneDynamicContext.Clear();
        Managers.Network.udpManager.SendC2DRequestSpawnPlayerObjects();
    }

    public void SpawnPlayerObject(PlayerSpawnData data) {
        // 응답이 왔으므로 스폰 여부와 무관하게 요청은 끝난 것으로 본다
        _pendingSpawnRequests.Remove(data.ObjectId);

        if (data.ObjectId == _myObjectId) return;
        if (_oppoPlayers.ContainsKey(data.ObjectId)) return;
        if (_despawnedObjectIds.Contains(data.ObjectId)) return;

        GameObject go = Managers.Resource.Instantiate("GameObject/OppoPlayerObject");
        OppoPlayerController controller = go.GetComponent<OppoPlayerController>();
        controller.SetObjectId((int)data.ObjectId);
        controller.SetPosition(data.Position);
        controller.SetRotation(data.Rotation);
        controller.Setup(data.CharacterType);
        controller.EquipWeapon(data.WeaponId);   // 0=맨손. EquipWeapon이 직접 처리한다
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
            if (_despawnedObjectIds.Contains(data.ObjectId)) continue;

            if (_oppoPlayers.TryGetValue(data.ObjectId, out OppoPlayerController controller))
                controller.ApplyState(data);
            else
                RequestSpawnIfUnknown(data.ObjectId);
        }
    }

    // 디스폰된 objectId. 서버가 한 게임 안에서 objectId를 재사용하지 않으므로 만료 없이
    // 씬 수명 내내 들고 간다 — 만료 창을 두면 그보다 늦게 도착한 패킷이 그대로 뚫는다.
    // 플레이어·비플레이어 공용 목록이다(objectId 공간이 공용).
    private HashSet<uint> _despawnedObjectIds = new HashSet<uint>();

    // 스폰을 요청해두고 아직 응답이 오지 않은 objectId.
    // 요청은 reliable이라 ACK될 때까지 알아서 재전송되므로 한 번만 보내면 된다. 매 틱 다시 보내면
    // 같은 내용이 서로 다른 시퀀스로 쌓여 in-flight 32슬롯을 채우고, 넘치는 순간
    // 아직 ACK되지 않은 다른 패킷이 덮어써진다(PacketHandler.MakeReliablePacket).
    // 응답도 디스폰 통보도 오지 않는 objectId는 서버가 모르는 것이므로 재요청해도 결과가 같다.
    private HashSet<uint> _pendingSpawnRequests = new HashSet<uint>();

    // 아직 모르는 objectId를 가리키는 통보가 오면 스폰을 요청한다.
    // 디스폰된 id는 요청하지 않는다 — 되살아난다.
    // objectId 공간이 플레이어·비플레이어 공용이라 응답은 D2CSpawnPlayerObject 또는
    // D2CResponseSpawnByObjectId 중 하나로 갈린다. 어느 쪽인지는 서버가 판단한다.
    private void RequestSpawnIfUnknown(uint objectId) {
        if (_despawnedObjectIds.Contains(objectId)) return;
        if (_oppoPlayers.ContainsKey(objectId)) return;
        if (_sceneObjects.ContainsKey(objectId)) return;
        if (!_pendingSpawnRequests.Add(objectId)) return;

        Managers.Network.udpManager.SendC2DRequestSpawnByObjectId((int)objectId);
    }

    // ── 비플레이어 오브젝트 ──

    private Dictionary<uint, GameObjectController> _sceneObjects = new Dictionary<uint, GameObjectController>();

    // 비플레이어 오브젝트의 유일한 스폰 경로. 정적·동적 초기 스폰, 지연 스폰 응답,
    // 런타임 스폰 통보가 전부 여기로 모여야 레지스트리와 차단 검사에 구멍이 생기지 않는다.
    public void SpawnObject(ObjectData data) {
        _pendingSpawnRequests.Remove(data.ObjectId);

        if (_sceneObjects.ContainsKey(data.ObjectId)) return;
        if (_despawnedObjectIds.Contains(data.ObjectId)) return;

        // Undefined는 키가 있어도 경로가 null이라 두 경우를 함께 걸러낸다
        if (!Define.ObjectPaths.TryGetValue(data.ObjectType, out string path) || string.IsNullOrEmpty(path)) {
            Util.LogError($"매핑되지 않은 objectType={data.ObjectType} (objectId={data.ObjectId}) — Define.ObjectPaths 항목이 필요하다");
            return;
        }

        GameObject go = Managers.Resource.InstantiateFromObjectDataStruct(data);
        if (go == null) return;

        GameObjectController controller = go.GetComponent<GameObjectController>();
        if (controller != null)
            _sceneObjects[data.ObjectId] = controller;
    }

    public void DespawnObject(uint objectId) {
        _despawnedObjectIds.Add(objectId);
        _pendingSpawnRequests.Remove(objectId);

        // 파괴된 컨테이너를 열어둔 상태였다면 UI만 닫는다.
        // 서버가 이미 없앤 오브젝트이므로 C2DCloseContainer는 보내지 않는다.
        if (_isContainerOpen && _inventory.InteractingContainerObjectId == objectId)
            CloseContainerLocal();

        if (!_sceneObjects.TryGetValue(objectId, out GameObjectController controller))
            return;

        _sceneObjects.Remove(objectId);
        Managers.Resource.Destroy(controller.gameObject);
    }

    public void DespawnPlayerObject(uint objectId, int reason) {
        // 스폰 응답보다 디스폰이 먼저 도착할 수 있으므로 등록은 조회 성공 여부와 무관하다
        _despawnedObjectIds.Add(objectId);
        _pendingSpawnRequests.Remove(objectId);

        if (!_oppoPlayers.TryGetValue(objectId, out OppoPlayerController controller))
            return;

        // TODO: DespawnReason별 제거 연출 (1=RECALLED 탈출, 2=DEAD 사망, 3=DISCONNECTED 연출 없음)
        //       현재는 사유와 무관하게 즉시 제거한다. 연출 에셋이 준비되면 분기할 것

        _oppoPlayers.Remove(objectId);
        Managers.Resource.Destroy(controller.gameObject);
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

    // D2CNotifyWeaponChanged. 도착 경로가 셋이며 object_id로 갈린다.
    //   남 + 장착/해제(C2DRequestEquipItem 성공) / 남 + 무기 전환(C2DRequestSwitchWeapon 성공) → 외형 갱신
    //   나 → C2DRequestSwitchWeapon 거부. 성공은 룸의 '나머지'에게만 가므로 본인 수신은 항상 거부다
    public void HandleWeaponChanged(uint objectId, uint weaponId) {
        if (_despawnedObjectIds.Contains(objectId)) return;

        // _myObjectId 초기값 0은 실재하는 objectId다. 스폰 응답 전에는 비교 자체가 성립하지 않는다
        if (_spawnCompleted && objectId == _myObjectId) {
            RollbackWeaponPrediction((int)weaponId);
            return;
        }

        if (_oppoPlayers.TryGetValue(objectId, out OppoPlayerController controller)) {
            // weaponId가 0이어도 그대로 넘긴다 — 맨손 전환이므로 걸러내면 무기가 손에 남는다
            controller.EquipWeapon((int)weaponId);
            return;
        }

        // 아직 스폰되지 않은 플레이어. 스폰 응답의 weapon_id가 이 통보보다 최신이므로
        // weaponId는 들고 있을 필요가 없다
        RequestSpawnIfUnknown(objectId);
    }

    // 서버가 보는 현재 무기로 로컬 예측을 되돌린다.
    // 통보에는 weapon_id만 있고 슬롯이 없어 주/보조가 같은 blueprint면 어느 쪽인지 알 수 없다.
    // 그 경우 외형·스펙이 같아 슬롯 상태를 유지해도 차이가 없다.
    // T9에서 보낸 target_slot을 기억하게 되면 이 함수 안을 그 대조로 승격할 것.
    private void RollbackWeaponPrediction(int weaponId) {
        InventoryItem primary   = _inventory.PrimaryWeapon;
        InventoryItem secondary = _inventory.SecondaryWeapon;

        bool primaryMatches   = primary != null && primary.item_id == weaponId;
        bool secondaryMatches = secondary != null && secondary.item_id == weaponId;

        // 한쪽만 일치할 때만 슬롯을 확정할 수 있다
        if (primaryMatches != secondaryMatches)
            _inventory.IsPrimaryWeaponApplyed = primaryMatches;

        if (_playerController != null)
            _playerController.EquipWeapon(weaponId);
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
        // 실드 예측이 이 안의 방어구 스펙 캐시에 의존하므로 UI 유무와 무관하게 먼저 돈다
        SyncHealthBarMax();

        if (_ingameInventoryUI == null) return;
        _ingameInventoryUI.SyncMyInventory();
        _ingameInventoryUI.SyncEquipment();
    }

    private void SyncHealthBarMax() {
        InventoryItem armor = _inventory.Armor;
        if (armor != null && ItemDBHelper.TryGetArmorSpec(armor.item_id, out ArmorSpec armorSpec)) {
            _maxShieldPoint = armorSpec.MaxShieldPoint;
            _shieldRegenPerSecond = armorSpec.RegenerationPerSecond;
        } else {
            _maxShieldPoint = 0;
            _shieldRegenPerSecond = 0;
        }

        if (_ingameHealthBarUI == null) return;
        _ingameHealthBarUI.SetMaxHP(MAX_HEALTH_POINT);
        _ingameHealthBarUI.SetMaxShield(_maxShieldPoint);

        // 최대치만 넣으면 첫 피격 전까지 프리팹에 저장된 fillAmount가 그대로 보인다.
        // 최대치 → 현재값 순서라 SetArmor가 최대 실드 0에 걸리지 않는다.
        _ingameHealthBarUI.SetHP(_currentHealthPoint);
        _ingameHealthBarUI.SetArmor(_currentShieldPoint);
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
        CloseContainerLocal();
    }

    // 서버 통보 없이 로컬 상태만 정리한다. 컨테이너가 이미 파괴된 경우에 쓴다.
    private void CloseContainerLocal() {
        _inventory.ClearContainer();
        _isContainerOpen = false;
        if (_ingameInventoryUI != null)
            _ingameInventoryUI.DeactiveThis();
        OnUIClosed();
    }

    public void RequestOpenContainer(uint containerObjectId) {
        Managers.Network.udpManager.SendC2DRequestOpenContainer(containerObjectId);
    }

    // ── Recall ──

    // 귀환 요청은 판당 1회만 유효하다. 스팟별이 아닌 씬 단위로 막아야
    // 다른 스팟으로 이동해 재요청하는 경로가 생기지 않는다.
    public void RequestRecall(uint recallSpotIndex) {
        if (_recallRequested) return;
        _recallRequested = true;
        _recallTimer = 0f;   // TEMP: 워치독 시작 (승인 응답·최종 결과 양쪽을 함께 커버)
        Managers.Network.udpManager.SendC2DRequestRecall(recallSpotIndex);
    }

    public void HandleRecallResponse(bool result, uint recallSpotIndex) {
        // 거부 사유는 서버가 내려주지 않는다. 플래그만 되돌려 재시도를 허용한다.
        if (!result) {
            _recallRequested = false;
            return;
        }

        // 승인 시점에는 잠금을 유지한다. 서버가 1초 간격 5회 검사 후
        // D2CNotifyRecallResult로 최종 결과를 통보한다.
        Util.Log($"귀환 승인 (spotIndex={recallSpotIndex})");
    }

    // 승인된 귀환의 최종 결과. reason은 RecallResultReason
    // (0=UNKNOWN, 1=SUCCESS, 2=OUT_OF_ZONE, 3=PLAYER_DEAD, 4=SESSION_LOST, 5=SERVER_INTERNAL)
    public void HandleRecallResult(bool result, uint recallSpotIndex, int reason) {
        if (result) {
            // TODO: 귀환 성공 — 탈출 연출·씬 전환 미구현. 로그만 남기고 잠금을 해제한다.
            //       씬 전환이 붙으면 성공 시에는 잠금을 유지하는 쪽이 맞다(이미 맵을 떠나므로).
            Util.Log($"TEMP: 귀환 성공 (spotIndex={recallSpotIndex}, reason={reason})");
        }
        else {
            // TODO: 귀환 취소 — reason별 분기 미구현. 현재는 사유와 무관하게 재시도를 허용한다.
            Util.Log($"TEMP: 귀환 취소 (spotIndex={recallSpotIndex}, reason={reason})");
        }

        _recallRequested = false;
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

        // 방어구 슬롯이 바뀌면 실드는 0에서 다시 찬다 — 착용·해제·교체 전부 해당
        if (equipmentSlotType == 2)
            ResetShieldPrediction();

        // 무기 슬롯이 변경된 경우 무기 장착 갱신.
        // 해제로 현재 무기가 비면 맨손(0)으로 갱신해야 한다 — 호출을 건너뛰면 손에 든 무기가
        // 그대로 남아, 서버가 남들에게 통보한 맨손 상태와 본인 화면이 어긋난다
        if (equipmentSlotType <= 1 && _playerController != null) {
            InventoryItem currentWeapon = _inventory.IsPrimaryWeaponApplyed
                ? _inventory.PrimaryWeapon
                : _inventory.SecondaryWeapon;
            _playerController.EquipWeapon(currentWeapon != null ? currentWeapon.item_id : 0);
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

    // ── Health ──

    // PLAYER_OBJECT_ID와 값은 같지만 의미가 다르다(가해자 없음 vs 내 인벤토리).
    // objectId 0은 실재하는 값이므로 '미설정'으로 해석하면 안 된다.
    private const uint NO_ATTACKER_OBJECT_ID = 0xFFFFFFFF;
    private const float ATTACKER_TRACK_DURATION = 5f;

    // 서버가 HP 최대치를 어떤 패킷으로도 보내지 않아 이 상수가 유일한 출처다
    private const int MAX_HEALTH_POINT = 100000;

    // 실드 재생 서버 규칙: (재생량 × 경과ms)를 누적해 이 값에 도달할 때마다 1 회복
    private const float SHIELD_REGEN_ACCUM_UNIT = 1000f;

    private int _currentHealthPoint = MAX_HEALTH_POINT;
    private int _currentShieldPoint;
    private uint _lastAttackerObjectId = NO_ATTACKER_OBJECT_ID;
    private float _lastAttackedTime = float.NegativeInfinity;

    // SyncHealthBarMax()가 방어구 스펙에서 갱신
    private int _maxShieldPoint;
    private int _shieldRegenPerSecond;
    private float _shieldRegenAccum;

    public int CurrentHealthPoint => _currentHealthPoint;
    public int CurrentShieldPoint => _currentShieldPoint;

    public bool HasRecentAttacker =>
        _lastAttackerObjectId != NO_ATTACKER_OBJECT_ID
        && Time.realtimeSinceStartup - _lastAttackedTime <= ATTACKER_TRACK_DURATION;

    public uint LastAttackerObjectId => HasRecentAttacker ? _lastAttackerObjectId : NO_ATTACKER_OBJECT_ID;

    public void HandleHealthChange(int healthPoint, int shieldPoint, int reason, uint attackerObjectId) {
        _currentHealthPoint = healthPoint;
        _currentShieldPoint = shieldPoint;
        _shieldRegenAccum = 0f;

        if (attackerObjectId != NO_ATTACKER_OBJECT_ID) {
            _lastAttackerObjectId = attackerObjectId;
            _lastAttackedTime = Time.realtimeSinceStartup;

            // OPTION: 피격 방향 표시. _oppoPlayers에서 가해자 위치를 찾아 캐릭터 forward 기준
            //         signed yaw를 산출한다. 가해자가 아직 스폰되지 않았거나 비플레이어
            //         전투 오브젝트인 경우가 있으므로 위치를 못 찾는 경로를 정상으로 다룰 것.
        }

        Util.Log($"[HealthChange] hp={healthPoint} shield={shieldPoint} reason={reason} attacker={attackerObjectId}");

        if (_ingameHealthBarUI != null) {
            _ingameHealthBarUI.SetHP(healthPoint);
            _ingameHealthBarUI.SetArmor(shieldPoint);
        }
    }

    // 실드 재생은 전용 통보 패킷이 없다. 서버는 매 틱 회복만 시키고 아무것도 보내지 않으므로
    // 클라가 같은 공식으로 예측하고, 피격 통보가 올 때마다 서버 절대값으로 리셋한다.
    private void UpdateShieldRegen() {
        if (_maxShieldPoint <= 0 || _shieldRegenPerSecond <= 0) return;
        if (_currentHealthPoint <= 0) return;
        if (_currentShieldPoint >= _maxShieldPoint) {
            _shieldRegenAccum = 0f;
            return;
        }

        _shieldRegenAccum += _shieldRegenPerSecond * (Time.deltaTime * 1000f);
        if (_shieldRegenAccum < SHIELD_REGEN_ACCUM_UNIT) return;

        int recovered = (int)(_shieldRegenAccum / SHIELD_REGEN_ACCUM_UNIT);
        _shieldRegenAccum -= recovered * SHIELD_REGEN_ACCUM_UNIT;
        _currentShieldPoint = Mathf.Min(_currentShieldPoint + recovered, _maxShieldPoint);

        if (_ingameHealthBarUI != null)
            _ingameHealthBarUI.SetArmor(_currentShieldPoint);
    }

    private void ResetShieldPrediction() {
        _currentShieldPoint = 0;
        _shieldRegenAccum = 0f;

        Util.Log($"[ShieldReset] max={_maxShieldPoint} regen={_shieldRegenPerSecond}/s");

        if (_ingameHealthBarUI != null)
            _ingameHealthBarUI.SetArmor(0);
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

        UpdateShieldRegen();

        // TEMP: 귀환 응답 워치독 — 상단 RECALL_TIMEOUT 주석 참조. 제거 시 함께 삭제할 것
        if (_recallRequested) {
            _recallTimer += Time.deltaTime;
            if (_recallTimer >= RECALL_TIMEOUT) {
                _recallRequested = false;
                Util.LogWarning($"귀환 응답 미수신 ({RECALL_TIMEOUT}초) — 요청 잠금 해제");
            }
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
