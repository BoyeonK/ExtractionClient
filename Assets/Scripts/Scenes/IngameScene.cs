using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 한 번에 하나만 진행되는 플레이어 행동. 잠금이 '사유'까지 들고 있어야
// 같은 사유의 재요청(R 중 R)과 다른 사유의 개입(재장전 중 무기 교체)이 갈린다
public enum PlayerActionKind {
    None,
    Reload,
    WeaponSwitch,
}

// 취소 가능 여부를 가르는 단계. 패킷이 나간 뒤에는 서버가 이미 처리했으므로 되돌릴 수 없다
public enum PlayerActionPhase {
    Local,    // 유예 중(모션 자리). 아직 전송 전이라 취소·재타게팅이 자유롭다
    Pending,  // 전송 후 서버 응답 대기
}

public class IngameScene : BaseScene {
    private bool _operationFlag = false;
    private bool _spawnCompleted = false;
    private bool _cursorLocked = true;
    public bool _itemLoaded = false;
    private bool _weaponInitialized = false;
    private bool _isContainerOpen = false;
    private bool _isInventoryOpen = false;
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
    IngameSettingUI _ingameSettingUI;
    IngameKillLogUI _ingameKillLogUI;
    IngameWeaponUI _ingameWeaponUI;
    IngameStaminaBarUI _ingameStaminaBarUI;

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

        GameObject settingObj = GameObject.Find("IngameSettingUI");
        if (settingObj != null) {
            _ingameSettingUI = settingObj.GetComponent<IngameSettingUI>();
            _ingameSettingUI.Init(this);
        }

        GameObject staminaBarObj = GameObject.Find("IngameStaminaBarUI");
        if (staminaBarObj != null) {
            _ingameStaminaBarUI = staminaBarObj.GetComponent<IngameStaminaBarUI>();
            _ingameStaminaBarUI.Init();
            _ingameStaminaBarUI.SetStamina(_currentStamina, MAX_STAMINA);
        }

        GameObject weaponUIObj = GameObject.Find("IngameWeaponUI");
        if (weaponUIObj != null) {
            _ingameWeaponUI = weaponUIObj.GetComponent<IngameWeaponUI>();
            _ingameWeaponUI.Init();
        }

        GameObject killLogObj = GameObject.Find("IngameKillLogUI");
        if (killLogObj != null) {
            _ingameKillLogUI = killLogObj.GetComponent<IngameKillLogUI>();
            if (_ingameKillLogUI != null)
                _ingameKillLogUI.Init(this);
            else
                Util.LogError("씬의 IngameKillLogUI 오브젝트에 IngameKillLogUI 스크립트가 붙어 있지 않다");
        }

        // OPTION: 씬 오브젝트 없이 코드로 세운 크로스헤어. 정식 IngameSceneUI 자산으로
        //         다시 만들게 되면 이 줄을 지운다 (IngameCrosshair.cs 상단 참조)
        IngameCrosshair.Create(this);

        Managers.Input.AddKeyListener(Key.I, ToggleMyInventory, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Tab, ToggleMyInventory, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Digit1, SwitchToPrimaryWeapon, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.Digit2, SwitchToSecondaryWeapon, InputManager.KeyState.Down);
        Managers.Input.AddKeyListener(Key.R, RequestReload, InputManager.KeyState.Down);
    }

    // Managers.Clear() → Scene.Clear() → 여기. 씬 전환 시 자동으로 불린다.
    // 이탈 유예를 다 쓰지 않고 나가는 경로(강제 씬 전환 등)에서도 정리가 보장된다
    public override void Clear() {
        SetCursorLock(false);
        Managers.Network.udpManager.Disconnect();
    }

    private void OnDestroy() {
        if (Managers.Instance == null) return;
        Managers.Input.RemoveKeyListener(Key.I, ToggleMyInventory, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.Tab, ToggleMyInventory, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.Escape, OnEscapeInput, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.Digit1, SwitchToPrimaryWeapon, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.Digit2, SwitchToSecondaryWeapon, InputManager.KeyState.Down);
        Managers.Input.RemoveKeyListener(Key.R, RequestReload, InputManager.KeyState.Down);
    }

    private void SwitchToPrimaryWeapon() => RequestSwitchWeapon(0);
    private void SwitchToSecondaryWeapon() => RequestSwitchWeapon(1);

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
        // 내 objectId는 _oppoPlayers에 없어 가드가 없으면 나 자신의 스폰을 요청하게 된다.
        // _myObjectId 초기값 0은 실재 objectId라 스폰 전에는 비교가 성립하지 않는다
        if (_spawnCompleted && objectId == _myObjectId) return;
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
        SyncWeaponUI();
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

    // ── 행동 잠금 ──
    //
    // 재장전·무기 교체처럼 '유예를 거쳐 서버로 나가는' 행동을 하나의 잠금으로 묶는다.
    // 잠금이 사유(PlayerActionKind)와 단계(PlayerActionPhase)를 함께 들고 있어야 다음이 갈린다.
    //   같은 사유 + 같은 대상 → 무시   : 재요청이 자기 행동을 스스로 무효화하지 않게 한다(R 중 R, 1 중 1)
    //   같은 사유 + 다른 대상 → 재타게팅 : 전송 전이라 자유롭게 갈아탄다
    //   다른 사유 + Local     → 취소 후 진입 : 재장전 유예 중 무기 교체가 우선한다
    //   Pending               → 전부 무시 : 서버가 확정하기 전에는 새 행동의 전제(손에 든 무기,
    //                                       인벤토리 버전)가 미정이라 무엇을 보내도 어긋난다
    //
    // in-flight 요청은 여전히 항상 1개다 — 재타게팅이 Local(전송 전)에서만 일어나므로
    // 응답 순서 역전이 정상 경로에서 발생하지 않는다. rSeqNum 기반 순서 방어를 끌어올 필요가 없는
    // 근거가 이것이므로, Pending 중 재전송을 허용하는 변경은 그 방어를 함께 가져와야 한다.
    // 유예 시간. 무기별 차등 계획이 없어(2026-08-27 서버 확인) DB가 아니라 클라이언트가
    // 주도하는 값이며, 두 상수가 유일한 출처다. 플레이 검증에서 손에 맞는 것이 확인된 확정치다 —
    // TODO: 애니메이션이 붙으면 클립을 이 길이에 맞춰 만들 것. 반대로 클립 길이에 상수를
    //       맞추면 확정된 조작 감각이 조용히 바뀐다
    private const float RELOAD_LOCAL_SEC = 2f;
    private const float SWITCH_LOCAL_SEC = 0.5f;

    // TEMP: 응답 워치독. 응답이 오지 않으면 발사가 그 판 내내 막히므로 잠금만 풀어둔다.
    //       결과를 추측하지 않고 로컬 잠금만 해제하므로 판정 권한은 서버에 그대로 있다.
    //       서버 응답 신뢰성이 검증되면 이 상수와 UpdateAction()의 TEMP 블록을 함께 제거할 것.
    private const float ACTION_PENDING_TIMEOUT = 3f;

    private PlayerActionKind  _actionKind  = PlayerActionKind.None;
    private PlayerActionPhase _actionPhase = PlayerActionPhase.Local;
    private float             _actionTimer = 0f;

    // 대기 중인 교체 요청이 없음을 뜻한다. 슬롯 값(0/1)과 겹치지 않기만 하면 된다
    private const uint NO_PENDING_SLOT = 0xFFFFFFFF;
    private uint _switchPendingSlot = NO_PENDING_SLOT;   // WeaponSwitch 행동의 대상 슬롯

    // 행동 중에는 발사를 막는다. 교체는 reliable(교체)과 unreliable(사격) 사이에 순서 보장이 없어
    // 사격이 먼저 처리되면 weapon_dbid 불일치로 조용히 버려지고, 재장전은 유예가 곧 모션 시간이다
    public bool IsActionBusy => _actionKind != PlayerActionKind.None;

    private bool IsPlayerRunning => _playerController != null && _playerController.IsRunning;

    // PlayerController.ProcessRun()이 매 프레임 묻는 두 가지.
    // 진입은 RUN_ENTRY_STAMINA, 유지는 0 초과 — 달리는 중에 20 아래로 떨어져도 계속 달린다
    public bool CanStartRunning => _currentStamina >= RUN_ENTRY_STAMINA;
    public bool HasStamina => _currentStamina > 0f;

    // 행동 진입의 유일한 경로. 같은 사유의 재요청은 여기서 무시되므로,
    // 대상이 있는 행동(무기 교체)의 재타게팅은 호출부가 먼저 취소하고 들어온다
    private bool TryBeginAction(PlayerActionKind kind) {
        if (IsInputLocked) return false;
        if (!_spawnCompleted) return false;

        if (IsActionBusy) {
            if (_actionPhase == PlayerActionPhase.Pending) return false;
            if (_actionKind == kind) return false;
            ClearAction();
        }

        _actionKind  = kind;
        _actionPhase = PlayerActionPhase.Local;
        _actionTimer = 0f;
        return true;
    }

    // 취소·완료·워치독 만료가 공유하는 유일한 해제 경로
    private void ClearAction() {
        _actionKind  = PlayerActionKind.None;
        _actionPhase = PlayerActionPhase.Local;
        _actionTimer = 0f;
        _switchPendingSlot = NO_PENDING_SLOT;
    }

    // 유예(Local) → 전송(Pending) → 응답. 응답에 의한 해제는 각 결과 핸들러가 한다
    private void UpdateAction() {
        if (!IsActionBusy) return;

        _actionTimer += Time.deltaTime;

        if (_actionPhase == PlayerActionPhase.Pending) {
            // TEMP: 응답 워치독 — 상단 ACTION_PENDING_TIMEOUT 주석 참조. 제거 시 함께 삭제할 것
            if (_actionTimer >= ACTION_PENDING_TIMEOUT) {
                Util.LogWarning($"[Action] {_actionKind} 응답 미수신 ({ACTION_PENDING_TIMEOUT}초) — 행동 잠금 해제");
                ClearAction();
            }
            return;
        }

        // 달리기는 재장전의 진입 조건이자 유지 조건이다. 매 프레임 같은 식을 보므로
        // RequestReload()의 진입 판정과 여기의 취소가 한 규칙으로 묶인다
        if (_actionKind == PlayerActionKind.Reload && IsPlayerRunning) {
            ClearAction();
            return;
        }

        float localSec = _actionKind == PlayerActionKind.Reload ? RELOAD_LOCAL_SEC : SWITCH_LOCAL_SEC;
        if (_actionTimer < localSec) return;

        SendActionRequest();
    }

    // 유예 동안 인벤토리가 바뀌었을 수 있다. 버전과 대상 검증은 반드시 전송 시점 값으로 한다 —
    // 시작 시점 값을 캐시하면 자기 조작 때문에 DENY_VERSION_MISMATCH로 거부된다
    private void SendActionRequest() {
        switch (_actionKind) {
            case PlayerActionKind.Reload:
                Managers.Network.udpManager.SendC2DRequestReload(_inventory.InventoryVersion);
                break;

            case PlayerActionKind.WeaponSwitch: {
                uint heldSlot = _inventory.IsPrimaryWeaponApplyed ? 0u : 1u;
                // 유예 중 장착 조작으로 대상이 비거나 이미 손에 들린 슬롯이 되었으면 보낼 것이 없다
                if (_switchPendingSlot == heldSlot || _inventory.GetEquipmentSlot(_switchPendingSlot) == null) {
                    ClearAction();
                    return;
                }
                Managers.Network.udpManager.SendC2DRequestSwitchWeapon(_switchPendingSlot, _inventory.InventoryVersion);
                break;
            }
        }

        _actionPhase = PlayerActionPhase.Pending;
        _actionTimer = 0f;
    }

    // ── 재장전 ──

    // D2CResponseReload.deny_reason_mask 비트
    private const uint DENY_RELOAD_VERSION_MISMATCH = 0x0001;

    // DENY_VERSION_MISMATCH 재요청을 판당 무한히 반복하지 않기 위한 1회 한정 플래그.
    // 응답에 실린 스냅샷이 곧 재동기화이므로 새 버전으로 한 번 더 시도하는 것이 proto의 지시지만,
    // 인벤토리 버전이 계속 바뀌는 상황에서는 그대로 두면 자동 재시도가 루프가 된다
    private bool _reloadRetried = false;

    // R 키. 탄창 잔량과 무관하게 보낸다 — 가득 찼는지, 해당 탄종이 있는지는 서버가 판정한다
    public void RequestReload() {
        // 달리는 중에는 시작하지 않는다. UpdateAction()의 취소 조건과 같은 식이라
        // '달리면 재장전이 성립하지 않는다'는 규칙이 진입·유지 양쪽에서 한 벌로 유지된다
        if (IsPlayerRunning) return;
        if (!TryBeginAction(PlayerActionKind.Reload)) return;
        _reloadRetried = false;
    }

    // D2CResponseReload. 성공·거부 모두 '처리 후의 인벤토리 전체'가 실려 온다.
    // D2CFullInventorySync와 담긴 메시지는 같지만 수신 경로를 공유하지 않는다 —
    // 전체 동기화 쪽에는 버전 비교가 없어 낡은 스냅샷을 버릴 자리가 없다
    public void HandleReloadResponse(bool result, uint denyReasonMask, uint inventoryVersion,
        InventoryItem[] slots, InventoryItem primaryWeapon, InventoryItem secondaryWeapon,
        InventoryItem armor, InventoryItem primaryWeaponMagazine, InventoryItem secondaryWeaponMagazine) {

        // reliable은 전달과 중복 제거만 보장하고 순서는 보장하지 않는다.
        // 낡은 스냅샷이 최신 상태를 덮지 않도록 통째로 버린다(fire_sequence는 인벤토리와 무관해
        // PacketHandler가 이 가드보다 앞에서 이미 반영했다)
        if (inventoryVersion < _inventory.InventoryVersion) {
            Util.LogWarning($"[Reload] 낡은 스냅샷 폐기 (응답={inventoryVersion}, 로컬={_inventory.InventoryVersion})");
            return;
        }

        if (_actionKind == PlayerActionKind.Reload)
            ClearAction();

        // 최초 1회용 초기화(_itemLoaded·TryInitWeapon)는 건드리지 않는다.
        // 재장전은 탄창마다 오므로 전투 중 매번 돌게 된다
        _inventory.ApplyFullSync(inventoryVersion, slots, primaryWeapon, secondaryWeapon, armor,
            primaryWeaponMagazine, secondaryWeaponMagazine);
        SyncInventoryUI();

        if (result) return;

        // DENY_SLOT_EMPTY(맨손·이미 가득·탄종 없음)는 재요청해도 결과가 같다.
        // 표시는 위의 스냅샷 반영으로 이미 서버 값에 맞춰졌으므로 따로 되돌릴 것이 없다
        if ((denyReasonMask & DENY_RELOAD_VERSION_MISMATCH) == 0) return;
        if (_reloadRetried) return;

        // 워치독이 먼저 잠금을 풀고 그 사이에 다른 행동이 시작됐다면 재요청이 그것을 밀어낸다.
        // 유저가 직접 시작한 행동을 뒤늦은 응답이 가로채지 않도록 여기서 포기한다
        if (IsActionBusy) return;

        // 방금 반영한 스냅샷이 재동기화 그 자체다. 유예를 다시 두지 않고 새 버전으로 곧바로 보낸다
        if (!TryBeginAction(PlayerActionKind.Reload)) return;
        _reloadRetried = true;
        Managers.Network.udpManager.SendC2DRequestReload(_inventory.InventoryVersion);
        _actionPhase = PlayerActionPhase.Pending;
        _actionTimer = 0f;
    }

    // ── 무기 교체 ──

    // 1/2 키. target_slot은 절대 지정이라 같은 슬롯을 다시 요청하는 것은 의미가 없다
    public void RequestSwitchWeapon(uint targetSlot) {
        if (IsInputLocked) return;
        if (!_spawnCompleted) return;

        // 유예 중 대상이 바뀐 경우. 취소하고 아래 공통 경로에서 새 대상으로 다시 시작한다.
        // 슬롯이 0/1뿐인 지금은 '다른 대상' = '손에 든 슬롯' 이라 아래에서 그대로 소멸하지만,
        // 슬롯이 셋 이상으로 늘면 이 경로가 진짜 재타게팅이 된다
        if (_actionKind == PlayerActionKind.WeaponSwitch && _actionPhase == PlayerActionPhase.Local) {
            if (targetSlot == _switchPendingSlot) return;
            ClearAction();
        }

        uint heldSlot = _inventory.IsPrimaryWeaponApplyed ? 0u : 1u;
        if (targetSlot == heldSlot) return;

        // 서버 거부 조건(대상 슬롯이 비어 있음)을 미리 걸러내 불필요한 유예와 왕복을 없앤다.
        // equipmentSlotType과 target_slot은 0=주무기, 1=보조무기로 값이 같다
        if (_inventory.GetEquipmentSlot(targetSlot) == null) return;

        if (!TryBeginAction(PlayerActionKind.WeaponSwitch)) return;
        _switchPendingSlot = targetSlot;
    }

    // D2CNotifyWeaponChanged. 도착 경로가 셋이며 object_id로 갈린다.
    //   남 + 장착/해제(C2DRequestEquipItem 성공) / 남 + 무기 전환(C2DRequestSwitchWeapon 성공) → 외형 갱신
    //   나 + 무기 전환 결과(성공·거부 모두) → 손에 든 슬롯 확정
    // 도착한 slot/weapon_id가 항상 서버 권위값이므로 성공·거부의 상태 반영은 같다.
    // 갈리는 것은 재동기화 여부 하나뿐이다.
    public void HandleWeaponChanged(uint objectId, uint weaponId, uint slot, uint inventoryVersion) {
        if (_despawnedObjectIds.Contains(objectId)) return;

        // _myObjectId 초기값 0은 실재하는 objectId다. 스폰 응답 전에는 비교 자체가 성립하지 않는다
        if (_spawnCompleted && objectId == _myObjectId) {
            // OPTION: 통보 순서 역전 방어. 클라가 수신 reliable을 중복 제거하지 않아
            //         (PacketHandler가 디스패치 전 중복 검사를 하지 않는다), ACK 유실로 재전송된
            //         옛 통보가 새 통보 뒤에 도착하면 낡은 슬롯이 다시 적용된다. 확정 전 재요청을
            //         막고 있어 정상 경로에서는 발생하지 않고 다음 교체에서 교정되므로 방치 가능.
            //         막으려면 마지막으로 반영한 UDP 헤더 rSeqNum을 기억해야 하는데 HandlerFunc가
            //         페이로드만 받아 델리게이트 시그니처 변경이 선행된다.
            //         무기 교체 로컬 예측을 도입하면 그때는 필수가 된다
            ApplyServerWeaponState(slot, (int)weaponId);

            bool rejected = slot != _switchPendingSlot;

            // 워치독이 먼저 잠금을 풀고 그 사이에 다른 행동이 시작됐다면 그것까지 지우면 안 된다
            if (_actionKind == PlayerActionKind.WeaponSwitch)
                ClearAction();

            // 버전이 어긋나 거부된 경우에만 재동기화한다. 버전만 통보값으로 맞추면
            // 슬롯 내용은 낡은 채로 다음 요청이 통과하므로, 갱신은 재동기화 응답에 맡긴다.
            // 재요청은 보내지 않는다 — 인벤토리 버전이 계속 바뀌면 자동 재시도가 루프가 된다
            if (rejected && inventoryVersion != _inventory.InventoryVersion)
                RequestRecentInventoryInfo();
            return;
        }

        if (_oppoPlayers.TryGetValue(objectId, out OppoPlayerController controller)) {
            // weaponId가 0이어도 그대로 넘긴다 — 맨손 전환이므로 걸러내면 무기가 손에 남는다.
            // 남의 slot·inventory_version은 쓸 곳이 없다(후자는 항상 0xFFFFFFFF로 온다)
            controller.EquipWeapon((int)weaponId);
            return;
        }

        // 아직 스폰되지 않은 플레이어. 스폰 응답의 weapon_id가 이 통보보다 최신이므로
        // weaponId는 들고 있을 필요가 없다
        RequestSpawnIfUnknown(objectId);
    }

    // 서버가 확정한 '손에 든 슬롯/무기'를 그대로 반영한다. 성공·거부 구분 없이 이 값이 권위값이다
    private void ApplyServerWeaponState(uint slot, int weaponId) {
        _inventory.IsPrimaryWeaponApplyed = slot == 0;

        if (_playerController != null)
            _playerController.EquipWeapon(weaponId);

        SyncWeaponUI();
    }

    // 무기 슬롯 조작 뒤 손에 든 무기를 서버 규칙대로 맞춘다.
    // 규칙: 들고 있던 슬롯이 비었을 때만 반대쪽으로 옮긴다(양쪽 다 비면 맨손).
    //       그 외에는 손에 든 슬롯을 유지하고, 그 슬롯의 무기가 바뀌었으면 새 무기로 갱신한다
    private void SyncHeldWeapon() {
        if (_playerController == null) return;

        uint heldSlot = _inventory.IsPrimaryWeaponApplyed ? 0u : 1u;
        InventoryItem held = _inventory.GetEquipmentSlot(heldSlot);

        if (held == null) {
            uint otherSlot = heldSlot == 0u ? 1u : 0u;
            InventoryItem other = _inventory.GetEquipmentSlot(otherSlot);
            if (other != null) {
                _inventory.IsPrimaryWeaponApplyed = otherSlot == 0u;
                held = other;
            }
        }

        _playerController.EquipWeapon(held != null ? held.item_id : 0);

        // ApplyEquipItem이 SyncInventoryUI()를 먼저 부르고 여기를 나중에 부른다 — 앞의 호출
        // 시점에는 손에 든 무기가 아직 낡은 값이라, 여기서 한 번 더 덮어야 표시가 맞는다
        SyncWeaponUI();
    }

    public bool IsContainerOpen => _isContainerOpen;

    public void TryInteract() {
        if (IsInputLocked) return;
        if (IsContainerOpen) {
            CloseContainer();
            return;
        }
        // 컨테이너 외의 UI가 떠 있으면 상호작용을 막는다. 커서가 풀리면 시점이 멈춘 채
        // 직전 조준 대상이 그대로 남아, 창을 띄운 상태로 컨테이너·귀환이 눌린다
        if (IsAnyUIOpen) return;
        if (!_canInteract || _interactTarget == null)
            return;
        _interactTarget.Interact();
    }

    // Tab·I 공용. 컨테이너를 뒤지는 중이면 그 상호작용을 끝내는 것이 먼저다
    public void ToggleMyInventory() {
        if (IsInputLocked) return;
        if (IsContainerOpen) {
            CloseContainer();
            return;
        }
        // 설정 창 위에 겹쳐 열지 않는다. ESC로 설정을 닫은 뒤가 순서다
        if (_ingameSettingUI != null && _ingameSettingUI.IsOpen) return;

        if (_isInventoryOpen) CloseMyInventory();
        else ShowMyInventory();
    }

    // ESC는 항상 '가장 위에 있는 것'을 닫고, 닫을 것이 없을 때만 설정을 연다.
    // 매치 이탈 중에도 막지 않는다 — 설정은 서버로 나가는 요청이 없다
    private void OnEscapeInput() {
        if (_ingameSettingUI != null && _ingameSettingUI.IsOpen) {
            _ingameSettingUI.CancelAndHide();
            return;
        }
        if (IsContainerOpen) {
            CloseContainer();
            return;
        }
        if (_isInventoryOpen) {
            CloseMyInventory();
            return;
        }
        if (_ingameSettingUI != null)
            _ingameSettingUI.Show();
    }

    private void ShowMyInventory() {
        if (_ingameInventoryUI == null) return;
        if (_isInventoryOpen) return;

        _ingameInventoryUI.SyncMyInventory();
        _ingameInventoryUI.SyncEquipment();
        _ingameInventoryUI.ActiveMyInventory();
        _isInventoryOpen = true;
        OnUIOpened();
    }

    private void CloseMyInventory() {
        if (!_isInventoryOpen) return;

        _isInventoryOpen = false;
        if (_ingameInventoryUI != null)
            _ingameInventoryUI.DeactiveThis();
        OnUIClosed();
    }

    public void SyncInventoryUI() {
        // 실드 예측이 이 안의 방어구 스펙 캐시에 의존하므로 UI 유무와 무관하게 먼저 돈다
        SyncHealthBarMax();

        // 아래 인벤토리 UI 가드보다 앞이어야 한다 — 무기 표시는 인벤토리 UI 존재에 종속되지 않는다
        SyncWeaponUI();

        if (_ingameInventoryUI == null) return;
        _ingameInventoryUI.SyncMyInventory();
        _ingameInventoryUI.SyncEquipment();
    }

    // 손에 든 무기의 이름·탄창·예비탄 표시. 갱신 지점이 다섯이라(전체 동기화·재장전 응답·
    // 아이템 조작 / 장착·해제 / 무기 전환 확정 / 최초 장착 / 매 발사) 전부 이 함수를 거치게 한다.
    // 흩어서 각자 텍스트를 건드리면 반드시 한 경로가 빠져 표시가 어긋난다
    public void SyncWeaponUI() {
        if (_ingameWeaponUI == null) return;

        InventoryItem weapon = _inventory.CurrentWeapon;
        if (weapon == null) {
            // 맨손은 설계상 없는 상태다. 무기 슬롯 둘을 모두 비우는 인벤토리 조작 중에만
            // 잠깐 지나가므로 값을 비우기만 하고 창은 그대로 둔다
            _ingameWeaponUI.SetWeapon("", 0, 0);
            return;
        }

        InventoryItem magazine = _inventory.CurrentMagazine;
        int magazineAmmo = magazine != null ? magazine.quantity : 0;

        // 스펙을 못 찾으면 탄종을 몰라 예비탄을 셀 수 없다. 이름·탄창은 그대로 보여준다
        int spareAmmo = 0;
        if (ItemDBHelper.TryGetWeaponSpec(weapon.item_id, out WeaponSpec weaponSpec))
            spareAmmo = _inventory.CountAmmo(weaponSpec.AmmoType);

        _ingameWeaponUI.SetWeapon(ItemDBHelper.GetName(weapon.item_id), magazineAmmo, spareAmmo);
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

        TryInitShield();

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

        // 내 인벤토리와 같은 오브젝트를 컨테이너 레이아웃으로 바꿔 다는 것이라, 열려 있었다면
        // 그 열림을 반납해야 컨테이너를 닫을 때 _uiOpenCount가 1로 남지 않는다.
        // 반납을 OnUIOpened 뒤에 두는 것은 카운트가 0으로 떨어져 커서가 한 번 잠기는 것을 피하려는 것
        if (_isInventoryOpen) {
            _isInventoryOpen = false;
            OnUIClosed();
        }
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
        if (IsInputLocked) return;
        Managers.Network.udpManager.SendC2DRequestOpenContainer(containerObjectId);
    }

    // ── Recall ──

    // 귀환 요청은 판당 1회만 유효하다. 스팟별이 아닌 씬 단위로 막아야
    // 다른 스팟으로 이동해 재요청하는 경로가 생기지 않는다.
    //
    // TODO: 귀환·상호작용을 행동 잠금(PlayerActionKind)에 편입할지 미결. 귀환은 _recallRequested가
    //       사실상 같은 구조라 그대로 흡수되지만, 상호작용은 컨테이너가 열려 있는 동안 지속되는
    //       상태라 Pending으로 잡으면 여는 내내 모든 행동이 잠긴다. 편입 범위를 정한 뒤 옮길 것
    public void RequestRecall(uint recallSpotIndex) {
        if (IsInputLocked) return;
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
    private const int RECALL_RESULT_PLAYER_DEAD  = 3;
    private const int RECALL_RESULT_SESSION_LOST = 4;

    public void HandleRecallResult(bool result, uint recallSpotIndex, int reason) {
        if (result) {
            Util.Log($"귀환 성공 (spotIndex={recallSpotIndex})");
            // 잠금은 해제하지 않는다 — 이미 맵을 떠났으므로 재요청 경로가 열려선 안 된다
            BeginMatchExit(MatchExitReason.Recalled);
            return;
        }

        Util.Log($"귀환 취소 (spotIndex={recallSpotIndex}, reason={reason})");

        switch (reason) {
            case RECALL_RESULT_PLAYER_DEAD:
                // 사망 흐름이 이미 이탈을 잡고 있다. 잠금도 그대로 둔다
                break;
            case RECALL_RESULT_SESSION_LOST:
                // 세션 이탈 확정이라 재요청이 불가능하다 — 출구로 보낸다
                BeginMatchExit(MatchExitReason.ConnectionLost);
                break;
            default:
                // OUT_OF_ZONE·SERVER_INTERNAL·UNKNOWN은 상황이 바뀌면 다시 시도할 수 있다
                _recallRequested = false;
                break;
        }
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
        if (IsInputLocked) return;
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
        if (IsInputLocked) return;
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

        // 무기 슬롯 조작으로 손에 든 슬롯이 바뀌는 경우 본인에게는 통보가 오지 않는다.
        // 서버 규칙을 클라가 직접 반영해야 한다
        if (equipmentSlotType <= 1)
            SyncHeldWeapon();
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
    // D2CNotifyPlayerKilled.killer_object_id도 같은 의미(가해자 없는 죽음)라 이 상수를 함께 쓴다.
    private const uint NO_ATTACKER_OBJECT_ID = 0xFFFFFFFF;
    private const float ATTACKER_TRACK_DURATION = 5f;

    // 서버가 HP 최대치를 어떤 패킷으로도 보내지 않아 이 상수가 유일한 출처다.
    // 서버 PlayerObject::DEFAULT_MAX_HP(200.00 HP)와 반드시 같은 값이어야 한다
    private const int MAX_HEALTH_POINT = 20000;

    // 실드 재생 서버 규칙: (재생량 × 경과ms)를 누적해 이 값에 도달할 때마다 1 회복
    private const float SHIELD_REGEN_ACCUM_UNIT = 1000f;

    // 스태미나는 서버에 필드가 없는 완전한 클라 로컬 상태다 — 실드 예측과 달리
    // 서버 절대값으로 리셋되는 경로가 아예 없으므로 이 다섯 상수가 유일한 출처다
    private const float MAX_STAMINA = 60f;
    private const float RUN_ENTRY_STAMINA = 20f;      // 달리기 '진입'에만 걸린다. 유지 조건이 아니다
    private const float STAMINA_DRAIN_PER_SEC = 10f;
    private const float STAMINA_REGEN_PER_SEC = 10f;
    private const float STAMINA_REGEN_DELAY = 1f;     // 달리기가 끝난 뒤 회복이 시작되기까지

    // 스태미나 바가 보이는 구간. RUN_ENTRY_STAMINA와 값이 같지만 다른 개념이라 상수를 따로 둔다 —
    // '달리기를 시작할 수 있는가'와 '게이지를 보여줄 것인가'는 함께 움직여야 할 이유가 없다.
    // 진입 문턱을 바꿀 때 이쪽도 따라가야 하는지는 그때 판단할 것
    private const float STAMINA_SHOW_THRESHOLD = 20f;

    private float _currentStamina = MAX_STAMINA;
    private bool _wasRunning = false;
    private float _staminaRegenBlockedUntil = float.NegativeInfinity;

    private int _currentHealthPoint = MAX_HEALTH_POINT;
    private int _currentShieldPoint;
    private uint _lastAttackerObjectId = NO_ATTACKER_OBJECT_ID;
    private float _lastAttackedTime = float.NegativeInfinity;

    // SyncHealthBarMax()가 방어구 스펙에서 갱신
    private int _maxShieldPoint;
    private int _shieldRegenPerSecond;
    private float _shieldRegenAccum;
    private bool _initialShieldApplied = false;

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

        // 사망 판정을 HP 0으로 하지 않는다 — 서버가 D2CNotifyPlayerKilled를 사망 확정 신호로
        // 지정했고 그 패킷은 피해자에게도 온다. 여기서 함께 감지하면 기점이 이중화된다

        if (_ingameHealthBarUI != null) {
            _ingameHealthBarUI.SetHP(healthPoint);
            _ingameHealthBarUI.SetArmor(shieldPoint);
        }
    }

    // ── 킬 피드 ──

    // D2CNotifyPlayerKilled. 피해자를 포함한 룸 전체가 받으며,
    // 피해자에게는 '사망 확정' 신호를 겸한다(공통 사항 6).
    // 남의 캐릭터 제거 연출은 D2CDespawnPlayerObject(T7)가 담당한다.
    public void HandlePlayerKilled(uint victimObjectId, uint killerObjectId,
                                   string victimObjectName, string killerObjectName) {
        // 킬러는 살아있는 플레이어이므로 모르는 objectId면 채워둔다.
        // 피해자는 같은 타이밍에 디스폰 통보가 오므로 요청하지 않는다
        if (killerObjectId != NO_ATTACKER_OBJECT_ID)
            RequestSpawnIfUnknown(killerObjectId);

        RecordPlayerKill(victimObjectId, killerObjectId);

        Util.Log($"[KillFeed] {DescribePlayer(killerObjectId)} → {DescribePlayer(victimObjectId)}");

        // 가해자 이름이 비면(가해자 없음·서버 미확인) 가해자를 표시하지 않는다.
        // 판정은 objectId가 아니라 이름으로 한다 — 0xFFFFFFFF도 이름이 비어 오므로 함께 걸린다
        if (_ingameKillLogUI != null)
            _ingameKillLogUI.MakeSingleKillLog(
                string.IsNullOrEmpty(killerObjectName) ? string.Empty : KillLogName(killerObjectId, killerObjectName),
                KillLogName(victimObjectId, victimObjectName));

        // 자기 캐릭터의 디스폰 통보는 오지 않는다 — 유예 동안 화면에 남겨두라는 뜻이며,
        // 정리는 이탈 흐름이 담당한다
        if (_spawnCompleted && victimObjectId == _myObjectId)
            BeginMatchExit(MatchExitReason.Dead);
    }

    // ── 킬 카운트 ──

    // 내가 죽인 대상의 objectId. 킬수는 Count로 읽는다.
    // 수신 reliable에 중복 제거가 없어(HandleWeaponChanged의 OPTION 주석 참고) 재전송된
    // 킬 통보가 두 번 디스패치될 수 있다 — objectId는 재사용되지 않으므로 Set이면 멱등이다
    private HashSet<uint> _playerKillVictims = new HashSet<uint>();
    private HashSet<uint> _objectKillVictims = new HashSet<uint>();

    private void RecordPlayerKill(uint victimObjectId, uint killerObjectId) {
        // _myObjectId 초기값 0은 실재 objectId라 _spawnCompleted 전에는 비교가 성립하지 않는다.
        // NO_ATTACKER_OBJECT_ID는 killer 비교에서 함께 걸러진다
        if (!_spawnCompleted || killerObjectId != _myObjectId) return;
        if (victimObjectId == _myObjectId) return;
        _playerKillVictims.Add(victimObjectId);
    }

    private void RecordObjectKill(uint victimObjectId, uint killerObjectId) {
        if (!_spawnCompleted || killerObjectId != _myObjectId) return;
        _objectKillVictims.Add(victimObjectId);
    }

    // D2CNotifyObjectKilled. 비플레이어 오브젝트의 처치 통보이며 제거 신호를 겸한다 —
    // 처치로 인한 제거에는 D2CNotifyDespawnObject가 오지 않으므로 여기서 직접 치운다.
    // 킬러가 미스폰이어도 스폰을 요청하지 않는다. 표시부가 없어 쓸 곳이 없고,
    // 근처 플레이어는 어차피 상태 스트림이 채운다 — 불필요한 reliable을 늘리지 않는다
    // TEMP: killerObjectName은 킬 피드에 쓰지 않는다 — 오브젝트 킬은 킬 피드 미적용이다.
    //       킬러가 미스폰이라 DescribePlayer가 식별을 못 하는 구간을 확인하려고 로그에만 실었으며,
    //       확인이 끝나면 Handle_D2CNotifyObjectKilled의 추출과 함께 되돌린다
    public void HandleObjectKilled(uint victimObjectId, uint killerObjectId, string killerObjectName) {
        RecordObjectKill(victimObjectId, killerObjectId);

        Util.Log($"[ObjectKill] name={killerObjectName} {DescribePlayer(killerObjectId)} → objectId={victimObjectId}");

        DespawnObject(victimObjectId);
    }

    // ── 매치 이탈 ──

    // MatchExitReason은 GameResult가 함께 쓰므로 SceneManagerEx.cs 파일 스코프에 있다

    // 서버의 사망 유예는 5초다(proto 공통 사항 6). 4초를 쓰는 이유는 둘 —
    // 서버가 세션을 정리하기 전에 클라가 먼저 정리하고, 통보의 ACK가 나갈 시간을 번다.
    // 서버 값에 맞춰 5초로 "고치지" 말 것.
    private const float MATCH_EXIT_DELAY = 4f;

    private bool _matchExitStarted = false;
    private bool _matchExitCompleted = false;
    private float _matchExitTimer = 0f;
    private MatchExitReason _matchExitReason;

    // 이탈이 시작되면 서버가 하트비트를 제외한 내 요청을 전부 버린다.
    // 클라도 함께 막지 않으면 반응 없는 조작이 된다
    public bool IsInputLocked => _matchExitStarted;

    private void BeginMatchExit(MatchExitReason reason) {
        if (_matchExitStarted) return;

        _matchExitStarted = true;
        _matchExitReason = reason;
        _matchExitTimer = 0f;

        // 유예 중 송신은 하트비트뿐이라 그냥 두면 통보 ACK가 최대 3초 늦게 나간다.
        // 연결이 이미 끊긴 경우에는 보낼 곳이 없다
        if (reason != MatchExitReason.ConnectionLost)
            Managers.Network.udpManager.SendHeartbeatNow();

        // 조작 안내와 열린 컨테이너를 정리한다. 서버는 이미 내 요청을 받지 않는다
        ClearAction();
        SetInteractState(false, null);
        if (_interactUI != null)
            _interactUI.Hide();
        if (_isContainerOpen)
            CloseContainerLocal();
        CloseMyInventory();
        // 값은 이미 반영된 뒤라 되돌리지 않고 그대로 닫는다
        if (_ingameSettingUI != null)
            _ingameSettingUI.Hide();

        Util.Log($"[MatchExit] 이탈 시작 (reason={reason}) — {MATCH_EXIT_DELAY}초 뒤 연결을 종료한다");

        // 사망은 시신을 내려다보는 탑뷰로 전환한다. 자기 캐릭터는 디스폰 통보가 오지 않아
        // 유예 동안 그 자리에 남아 있으므로 그대로 연출 대상이 된다
        if (reason == MatchExitReason.Dead && _playerController != null)
            DeathCameraController.Play(_playerController.ViewCamera, _playerController.transform);

        // TODO: 탈출(귀환) 연출
    }

    private void CompleteMatchExit() {
        // 스냅샷은 유예가 끝난 여기서 뜬다 — 킬 통보가 사격보다 한 틱 뒤라, 죽기 직전에 쏜
        // 탄의 킬(상호 킬)이 유예 중에 도착한다. BeginMatchExit으로 당기면 그 킬이 빠진다.
        // 인벤토리는 이탈 시작 후 서버가 요청을 전부 버리므로 시점과 무관하게 같다
        Managers.Scene.SetGameResult(BuildGameResult());

        Managers.Network.udpManager.Disconnect();
        Util.Log($"[MatchExit] 연결 종료 (reason={_matchExitReason})");

        // 씬 전환은 잡큐로 예약해 다음 프레임에 수행한다 — 현재 업데이트 루프 안에서 씬을 내리지 않는다
        Managers.ExecuteAtMainThread(() => Managers.Scene.LoadScene(Define.Scene.GameResultScene));
    }

    private GameResult BuildGameResult() {
        // 슬롯 배열은 얕은 복사 — InventoryItem은 순수 데이터라 씬이 죽어도 참조가 유효하고,
        // 배열만 새로 떠서 IngameInventory 전체가 결과 씬까지 붙들리는 것을 막는다
        return new GameResult {
            ExitReason      = _matchExitReason,
            InventorySlots  = (InventoryItem[])_inventory.InventorySlots.Clone(),
            PrimaryWeapon   = _inventory.PrimaryWeapon,
            SecondaryWeapon = _inventory.SecondaryWeapon,
            Armor           = _inventory.Armor,
            PlayerKillCount = _playerKillVictims.Count,
            ObjectKillCount = _objectKillVictims.Count,
        };
    }

    public void HandleConnectionLost() {
        BeginMatchExit(MatchExitReason.ConnectionLost);
    }

    // 킬러의 무기는 통보에 실려 오지 않는다 — 통보가 사격보다 한 틱 뒤라 그 사이 교체되면
    // 틀린 값이 되기 때문이다. 스폰·무기 변경 통보로 추적해둔 값을 쓴다
    // 킬 피드에 찍을 표시명. 서버가 통보에 실어주는 이름(userId)이 유일한 재료이며
    // 자기 자신도 특별 취급하지 않는다 — 로그용 DescribePlayer()가 "나"를 붙이는 것과 갈리는 지점이다.
    // 표시명은 여기 한 곳에서만 만든다
    private string KillLogName(uint objectId, string objectName) {
        // 서버가 이름을 못 채운 경우의 최후 표기. 가해자를 숨기는 경로는 호출부가 먼저 거른다
        if (string.IsNullOrEmpty(objectName))
            return $"objectId={objectId}";

        return objectName;
    }

    private string DescribePlayer(uint objectId) {
        if (objectId == NO_ATTACKER_OBJECT_ID)
            return "가해자 없음";

        if (_spawnCompleted && objectId == _myObjectId)
            return $"나 objectId={objectId}(weaponId={(_playerController != null ? _playerController.EquippedWeaponId : 0)})";

        if (_oppoPlayers.TryGetValue(objectId, out OppoPlayerController controller))
            return $"objectId={objectId}(weaponId={controller.EquippedWeaponId})";

        return $"objectId={objectId}(미스폰)";
    }

    // 달리기 소모 / 정지 시 회복. 값도 판정도 여기 하나뿐이고 PlayerController는 조회만 한다.
    //
    // 회복 잠금을 '중단 사유'마다 걸지 않고 여기서 true→false 엣지 한 번으로 세우는 것이 요점이다 —
    // 중단 경로가 넷(Shift 해제 · 이동 정지 · 스태미나 0 · 이탈로 입력 차단)이라 사유를 열거하면
    // 반드시 하나가 빠진다. 어떤 이유로 멈췄든 이 함수의 엣지를 지나간다
    private void UpdateStamina() {
        bool isRunning = IsPlayerRunning;

        if (_wasRunning && !isRunning)
            _staminaRegenBlockedUntil = Time.time + STAMINA_REGEN_DELAY;
        _wasRunning = isRunning;

        if (isRunning)
            _currentStamina = Mathf.Max(_currentStamina - STAMINA_DRAIN_PER_SEC * Time.deltaTime, 0f);
        else if (Time.time >= _staminaRegenBlockedUntil)
            _currentStamina = Mathf.Min(_currentStamina + STAMINA_REGEN_PER_SEC * Time.deltaTime, MAX_STAMINA);

        if (_ingameStaminaBarUI != null) {
            // 채우기를 먼저 — 숨어 있던 게이지가 낡은 값으로 한 프레임 보이는 것을 막는다
            _ingameStaminaBarUI.SetStamina(_currentStamina, MAX_STAMINA);
            _ingameStaminaBarUI.SetVisible(isRunning || _currentStamina <= STAMINA_SHOW_THRESHOLD);
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

    // 서버는 스폰 시 실드를 최대치로 준다. 최대치의 출처가 방어구 스펙뿐이라 HP처럼 필드
    // 초기값으로 둘 수 없고, 인벤토리가 도착해 _maxShieldPoint가 채워진 뒤에야 적용할 수 있다.
    // _spawnCompleted는 보지 않는다 — 캐릭터가 서기 전에는 실드가 깎일 경로가 없고,
    // 조건을 늘리면 스폰 완료 시점에도 호출부를 둬야 한다(도착 순서가 보장되지 않는다).
    private void TryInitShield() {
        if (_initialShieldApplied || !_itemLoaded) return;
        _initialShieldApplied = true;
        _currentShieldPoint = _maxShieldPoint;
        _shieldRegenAccum = 0f;

        Util.Log($"[ShieldInit] 스폰 실드 최대치 적용 — {_currentShieldPoint}");
    }

    // 최초 스폰과 달리 방어구 조작은 실드가 0에서 다시 찬다(서버 규칙).
    // TryInitShield()의 일회성 플래그가 이 둘을 가른다
    private void ResetShieldPrediction() {
        _currentShieldPoint = 0;
        _shieldRegenAccum = 0f;

        Util.Log($"[ShieldReset] max={_maxShieldPoint} regen={_shieldRegenPerSecond}/s");

        if (_ingameHealthBarUI != null)
            _ingameHealthBarUI.SetArmor(0);
    }

    public void HandleWeaponFireBroadcast(uint shooterObjectId, bool hasHitPoint, Vector3 hitPoint) {
        // 룸 전체 브로드캐스트라 내 발사도 여기로 돌아오지만, 나는 _oppoPlayers에 없어서
        // 이 반환에 걸린다. 내 궤적은 PlayerController가 직접 그리므로
        // 이 가드를 풀면 내 발사가 두 겹으로 그려진다.
        // 모르는 발사자에게 스폰을 요청하지도 않는다 — 발사는 빈도가 높아 reliable이 폭주하고,
        // 근처 플레이어라면 상태 스트림이 곧 채운다
        if (!_oppoPlayers.TryGetValue(shooterObjectId, out OppoPlayerController shooter))
            return;

        // TODO: 발사자 총구 이펙트 (머즐 플래시, 총성 등)

        if (hasHitPoint) {
            // 빗나간 발사는 hit_point가 실리지 않아 방향 자체를 모르므로 그리지 않는다.
            // 내 궤적은 레이를 직접 갖고 있어 빗나가도 그리는데, 정보량 차이에서 오는 비대칭이다
            if (shooter.MuzzlePoint != null)
                BulletTracer.Play(shooter.MuzzlePoint.position, hitPoint);

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

        // 매치 이탈 유예 — 상단 MATCH_EXIT_DELAY 주석 참조.
        // 상태 전송·상호작용 갱신은 멈추되 수신은 그대로 처리해 관전 화면을 유지한다
        if (_matchExitStarted) {
            if (!_matchExitCompleted) {
                _matchExitTimer += Time.deltaTime;
                if (_matchExitTimer >= MATCH_EXIT_DELAY) {
                    _matchExitCompleted = true;
                    CompleteMatchExit();
                }
            }
            return;
        }

        _playerStateTimer += Time.deltaTime;
        if (_playerStateTimer >= PLAYER_STATE_INTERVAL) {
            _playerStateTimer = 0f;
            SendPlayerState();
        }

        UpdateShieldRegen();
        UpdateStamina();

        // TEMP: 귀환 응답 워치독 — 상단 RECALL_TIMEOUT 주석 참조. 제거 시 함께 삭제할 것
        if (_recallRequested) {
            _recallTimer += Time.deltaTime;
            if (_recallTimer >= RECALL_TIMEOUT) {
                _recallRequested = false;
                Util.LogWarning($"귀환 응답 미수신 ({RECALL_TIMEOUT}초) — 요청 잠금 해제");
            }
        }

        UpdateAction();

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
