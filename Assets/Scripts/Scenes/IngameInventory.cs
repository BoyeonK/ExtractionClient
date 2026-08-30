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

    // 무기 슬롯 조작(장착·해제) 뒤 그 슬롯의 탄창을 인벤토리로 쏟는다. 배치 규칙은 서버와 같아야 한다 —
    // ① 같은 item_id 스택 중 최소 인덱스에 합산(수량 상한 없음) ② 없으면 최소 인덱스 빈 칸 ③ 폐기.
    // 목적지는 언제나 플레이어 인벤토리다. 컨테이너 대상 조작이어도 탄약은 컨테이너로 가지 않는다.
    // 옮긴 칸의 최종 상태를 돌려주고 옮긴 것이 없으면 null이다(호출부가 서버값과 대조한다)
    public InventoryItem UnloadMagazineToInventory(uint equipmentSlotType) {
        if (equipmentSlotType > 1) return null;

        bool isPrimary = equipmentSlotType == 0;
        InventoryItem magazine = isPrimary ? _primaryWeaponMagazine : _secondaryWeaponMagazine;

        // 무기가 슬롯을 떠난 이상 탄창은 어느 갈래로 가든 비운다(서버도 네 경로 전부 Clear한다).
        // 배치 성공 여부에 묶으면 폐기된 경우에 무기 없는 탄창이 남는다
        if (isPrimary) SetPrimaryWeaponMagazine(null);
        else SetSecondaryWeaponMagazine(null);

        // 서버는 잔탄 0을 빈 슬롯으로 만들지만 클라는 발사마다 로컬 예측으로 차감해
        // quantity == 0인 탄창이 다음 동기화 전까지 실재한다. 둘을 같게 봐야 검산이 헛돌지 않는다
        if (magazine == null || magazine.quantity <= 0) return null;

        for (int i = 0; i < INVENTORY_SLOT_COUNT; i++) {
            InventoryItem slot = _inventorySlots[i];
            if (slot == null || slot.item_id != magazine.item_id) continue;
            slot.quantity += magazine.quantity;
            // slot_index는 로컬 이동 경로에서 갱신되지 않아 낡아 있을 수 있다. 검산이 이 값을 쓰므로
            // 돌려주기 전에 실제 배열 인덱스로 맞춘다
            slot.slot_index = i;
            return slot;
        }

        for (int i = 0; i < INVENTORY_SLOT_COUNT; i++) {
            if (_inventorySlots[i] != null) continue;
            magazine.slot_index = i;
            _inventorySlots[i] = magazine;
            return magazine;
        }

        return null;
    }
}
