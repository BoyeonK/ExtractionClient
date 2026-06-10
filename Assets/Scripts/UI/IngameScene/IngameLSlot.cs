public class IngameLSlot : IngameISlot {
    ItemType _acceptedType;
    public ItemType AcceptedType => _acceptedType;

    public void Init(IngameScene scene, ItemType acceptedType) {
        _acceptedType = acceptedType;
        base.Init(-1, scene);
    }

    public override bool CanAcceptItem(InventoryItem item) {
        if (item == null) return false;
        return ItemDBHelper.GetType(item.item_id) == _acceptedType;
    }
}
