public class IngameLSlot : IngameISlot {
    ItemType _acceptedType;
    uint _equipmentSlotType;
    public ItemType AcceptedType => _acceptedType;
    public uint EquipmentSlotType => _equipmentSlotType;

    public void Init(IngameScene scene, ItemType acceptedType, uint equipmentSlotType) {
        _acceptedType = acceptedType;
        _equipmentSlotType = equipmentSlotType;
        base.Init(-1, scene, SlotOwnerType.PlayerInventory);
    }

    public override bool CanAcceptItem(InventoryItem item) {
        if (item == null) return false;
        return ItemDBHelper.GetType(item.item_id) == _acceptedType;
    }
}
