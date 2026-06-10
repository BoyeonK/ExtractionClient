public class IngameInventory {
    private const int INVENTORY_SLOT_COUNT = 25;
    private IngameScene _owner = null;
    private uint _inventoryVersion = 0;
    private InventoryItem[] _inventorySlots = new InventoryItem[INVENTORY_SLOT_COUNT];
    private InventoryItem _primaryWeapon;
    private InventoryItem _secondaryWeapon;
    private InventoryItem _armor;
    private InventoryItem _primaryWeaponMagazine;
    private InventoryItem _secondaryWeaponMagazine;
    private int _emptySlotIdx = 0;
    private bool _isPrimaryWeaponApplyed = true;

    private const int CONTAINER_SLOT_COUNT = 30;
    private InventoryItem[] _interactingContainerSlots = new InventoryItem[CONTAINER_SLOT_COUNT];
    private uint _interactingContainerVersion = 0;
    private uint _interactingContainerObjectId = 0;
    private uint _interactingContainerVolume = 0;

    public IngameScene GetIngameScene() {
        if (_owner == null)
            _owner = Managers.Scene.CurrentScene as IngameScene;
        return _owner;
    }

    public uint InventoryVersion => _inventoryVersion;
    public uint InteractingContainerVersion => _interactingContainerVersion;
    public uint InteractingContainerObjectId => _interactingContainerObjectId;
    public uint InteractingContainerVolume => _interactingContainerVolume;
    public int EmptySlotIdx => _emptySlotIdx;
    public bool IsPrimaryWeaponApplyed { get => _isPrimaryWeaponApplyed; set => _isPrimaryWeaponApplyed = value; }
    public InventoryItem[] InventorySlots => _inventorySlots;
    public InventoryItem[] InteractingContainerSlots => _interactingContainerSlots;
    public InventoryItem PrimaryWeapon => _primaryWeapon;
    public InventoryItem SecondaryWeapon => _secondaryWeapon;
    public InventoryItem Armor => _armor;
    public InventoryItem PrimaryWeaponMagazine => _primaryWeaponMagazine;
    public InventoryItem SecondaryWeaponMagazine => _secondaryWeaponMagazine;

    public void ApplyFullSync(uint inventoryVersion, InventoryItem[] slots,
        InventoryItem primaryWeapon, InventoryItem secondaryWeapon, InventoryItem armor,
        InventoryItem primaryWeaponMagazine, InventoryItem secondaryWeaponMagazine) {
        _inventoryVersion = inventoryVersion;

        System.Array.Clear(_inventorySlots, 0, _inventorySlots.Length);
        // slots 배열은 PacketHandler에서 slot_index 기준으로 이미 정렬되어 넘어오므로 순차 복사로 충분
        if (slots != null) {
            for (int i = 0; i < slots.Length && i < INVENTORY_SLOT_COUNT; i++)
                _inventorySlots[i] = slots[i];
        }

        _primaryWeapon = primaryWeapon;
        _secondaryWeapon = secondaryWeapon;
        _armor = armor;
        _primaryWeaponMagazine = primaryWeaponMagazine;
        _secondaryWeaponMagazine = secondaryWeaponMagazine;

        FindEmptySlotIdx();
    }

    public void ApplyContainerSync(uint containerObjectId, uint containerVersion, uint containerVolume, InventoryItem[] slots) {
        _interactingContainerObjectId = containerObjectId;
        _interactingContainerVersion = containerVersion;
        _interactingContainerVolume = containerVolume;
        System.Array.Clear(_interactingContainerSlots, 0, _interactingContainerSlots.Length);
        if (slots != null) {
            for (int i = 0; i < slots.Length && i < CONTAINER_SLOT_COUNT; i++)
                _interactingContainerSlots[i] = slots[i];
        }
    }

    public void ClearContainer() {
        _interactingContainerObjectId = 0;
        _interactingContainerVersion = 0;
        _interactingContainerVolume = 0;
        System.Array.Clear(_interactingContainerSlots, 0, _interactingContainerSlots.Length);
    }

    public int FindEmptySlotIdx() {
        _emptySlotIdx = -1;
        for (int i = 0; i < INVENTORY_SLOT_COUNT; i++) {
            if (_inventorySlots[i] == null) {
                _emptySlotIdx = i;
                break;
            }
        }
        return _emptySlotIdx;
    }

    public void SetInventorySlot(int index, InventoryItem item) {
        if (index >= 0 && index < INVENTORY_SLOT_COUNT)
            _inventorySlots[index] = item;
    }

    public void InitWeapon() {
        if (_primaryWeapon != null) {
            _isPrimaryWeaponApplyed = true;
            GetIngameScene().PlayerController.EquipWeapon(_primaryWeapon.item_id);
        } else if (_secondaryWeapon != null) {
            _isPrimaryWeaponApplyed = false;
            GetIngameScene().PlayerController.EquipWeapon(_secondaryWeapon.item_id);
        }
    }

    public void ApplyWeapon(bool primary) {
        if (primary) {
            if (_isPrimaryWeaponApplyed) return;
            if (_primaryWeapon == null) return;
            _isPrimaryWeaponApplyed = true;
            GetIngameScene().PlayerController.EquipWeapon(_primaryWeapon.item_id);
            // TODO : 추후 서버에 무기전환요청 패킷 보내어 응답으로서 전환 시행하기
        } else {
            if (!_isPrimaryWeaponApplyed) return;
            if (_secondaryWeapon == null) return;
            _isPrimaryWeaponApplyed = false;
            GetIngameScene().PlayerController.EquipWeapon(_secondaryWeapon.item_id);
            // TODO : 추후 서버에 무기전환요청 패킷 보내어 응답으로서 전환 시행하기
        }
    }

    public void SetPrimaryWeapon(InventoryItem item) => _primaryWeapon = item;
    public void SetSecondaryWeapon(InventoryItem item) => _secondaryWeapon = item;
    public void SetArmor(InventoryItem item) => _armor = item;
    public void SetPrimaryWeaponMagazine(InventoryItem item) => _primaryWeaponMagazine = item;
    public void SetSecondaryWeaponMagazine(InventoryItem item) => _secondaryWeaponMagazine = item;
}
