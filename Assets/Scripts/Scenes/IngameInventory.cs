public class IngameInventory {
    private const int INVENTORY_SLOT_COUNT = 25;
    private uint _inventoryVersion = 0;
    private InventoryItem[] _inventorySlots = new InventoryItem[INVENTORY_SLOT_COUNT];
    private InventoryItem _primaryWeapon;
    private InventoryItem _secondaryWeapon;
    private InventoryItem _armor;

    public uint InventoryVersion => _inventoryVersion;
    public InventoryItem[] InventorySlots => _inventorySlots;
    public InventoryItem PrimaryWeapon => _primaryWeapon;
    public InventoryItem SecondaryWeapon => _secondaryWeapon;
    public InventoryItem Armor => _armor;

    public void ApplyFullSync(uint inventoryVersion, InventoryItem[] slots,
        InventoryItem primaryWeapon, InventoryItem secondaryWeapon, InventoryItem armor) {
        _inventoryVersion = inventoryVersion;

        System.Array.Clear(_inventorySlots, 0, _inventorySlots.Length);
        if (slots != null) {
            for (int i = 0; i < slots.Length && i < INVENTORY_SLOT_COUNT; i++)
                _inventorySlots[i] = slots[i];
        }

        _primaryWeapon = primaryWeapon;
        _secondaryWeapon = secondaryWeapon;
        _armor = armor;
    }

    public void SetInventorySlot(int index, InventoryItem item) {
        if (index >= 0 && index < INVENTORY_SLOT_COUNT)
            _inventorySlots[index] = item;
    }

    public void SetPrimaryWeapon(InventoryItem item) => _primaryWeapon = item;
    public void SetSecondaryWeapon(InventoryItem item) => _secondaryWeapon = item;
    public void SetArmor(InventoryItem item) => _armor = item;
}
