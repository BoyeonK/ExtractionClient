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
    public InventoryItem CurrentWeapon => _isPrimaryWeaponApplyed ? _primaryWeapon : _secondaryWeapon;
    public InventoryItem Armor => _armor;
    public InventoryItem PrimaryWeaponMagazine => _primaryWeaponMagazine;
    public InventoryItem SecondaryWeaponMagazine => _secondaryWeaponMagazine;
    // 발사 판정(PlayerController.CurrentMagazine)과 표시(SyncWeaponUI)가 같은 규칙을 써야 하므로
    // CurrentWeapon 옆 한 곳에 둔다. 두 벌로 만들면 쏘는 탄창과 보여주는 탄창이 갈린다
    public InventoryItem CurrentMagazine => _isPrimaryWeaponApplyed ? _primaryWeaponMagazine : _secondaryWeaponMagazine;

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

    // 예비 탄약(WeaponSpec.AmmoType이 곧 탄약 item_id). 장착된 탄창은 별도로 표시되므로
    // 세지 않고, 컨테이너 슬롯도 내 소지품이 아니라 제외한다
    public int CountAmmo(int ammoItemId) {
        int total = 0;
        for (int i = 0; i < _inventorySlots.Length; i++) {
            InventoryItem item = _inventorySlots[i];
            if (item != null && item.item_id == ammoItemId)
                total += item.quantity;
        }
        return total;
    }

    public void SetPrimaryWeapon(InventoryItem item) => _primaryWeapon = item;
    public void SetSecondaryWeapon(InventoryItem item) => _secondaryWeapon = item;
    public void SetArmor(InventoryItem item) => _armor = item;
    public void SetPrimaryWeaponMagazine(InventoryItem item) => _primaryWeaponMagazine = item;
    public void SetSecondaryWeaponMagazine(InventoryItem item) => _secondaryWeaponMagazine = item;

    private const uint PLAYER_OBJECT_ID = 0xFFFFFFFF;

    public InventoryItem GetSlotByObjectId(uint objectId, uint slotIdx) {
        if (objectId == PLAYER_OBJECT_ID) {
            if (slotIdx < INVENTORY_SLOT_COUNT)
                return _inventorySlots[slotIdx];
        } else {
            if (slotIdx < CONTAINER_SLOT_COUNT)
                return _interactingContainerSlots[slotIdx];
        }
        return null;
    }

    public void SetSlotByObjectId(uint objectId, uint slotIdx, InventoryItem item) {
        if (objectId == PLAYER_OBJECT_ID) {
            if (slotIdx < INVENTORY_SLOT_COUNT)
                _inventorySlots[slotIdx] = item;
        } else {
            if (slotIdx < CONTAINER_SLOT_COUNT)
                _interactingContainerSlots[slotIdx] = item;
        }
    }

    public void SetVersionByObjectId(uint objectId, uint version) {
        if (objectId == PLAYER_OBJECT_ID)
            _inventoryVersion = version;
        else
            _interactingContainerVersion = version;
    }

    public InventoryItem GetEquipmentSlot(uint slotType) {
        return slotType switch {
            0 => _primaryWeapon,
            1 => _secondaryWeapon,
            2 => _armor,
            _ => null
        };
    }

    public void SetEquipmentSlot(uint slotType, InventoryItem item) {
        switch (slotType) {
            case 0: _primaryWeapon = item; break;
            case 1: _secondaryWeapon = item; break;
            case 2: _armor = item; break;
        }
    }
}
