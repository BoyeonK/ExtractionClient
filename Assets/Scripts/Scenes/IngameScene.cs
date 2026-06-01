using System.Collections.Generic;
using UnityEngine;

public class IngameScene : BaseScene {
    private bool _operationFlag = false;
    private bool _spawnCompleted = false;
    private bool _cursorLocked = true;
    public bool _itemLoaded = false;
    private bool _weaponInitialized = false;

    private const float PLAYER_STATE_INTERVAL = 0.1f;
    private float _playerStateTimer = 0f;

    private bool _isGetResponseSpawnMe = false;
    private Vector3 _spawnPoint;
    private int _characterType = -1;
    private uint _myObjectId = 0;

    // ── HP ──
    private float _maxHp = 100f;
    private float _currentHp = 100f;

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

    IngameISlot _ingameDragSourceSlot;
    IngameDragGhost _ingameDragGhost;

    protected override void Init() {
        base.Init();
        foreach (ObjectData data in Managers.Scene.NextSceneStaticContext.ObjectDatas)
            Managers.Resource.InstantiateFromObjectDataStruct(data);
        
        Managers.Scene.ResetLoadSceneOp();
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

    protected void RequestSpawnMe() {
        Managers.Network.udpManager.SendC2DRequestSpawnMe();
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
    }

    private void SendPlayerState() {
        Managers.Network.udpManager.SendC2DUpdatePlayerState(
            _playerController.ObjectId,
            _playerController.transform.position,
            _playerController.Yaw,
            _playerController.Pitch,
            _playerController.Velocity,
            _playerController.MovementState
        );
    }
}
