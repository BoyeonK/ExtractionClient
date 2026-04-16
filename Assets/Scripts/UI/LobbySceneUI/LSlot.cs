public class LSlot : ISlot {
    ItemType _acceptedType;

    public void Init(int index, TestLobbyScene scene, ItemType acceptedType) {
        base.Init(index, scene);
        _acceptedType = acceptedType;
    }

    public override bool CanAcceptItem(InventoryItem item) =>
        item != null && ItemTypeHelper.GetType(item.item_id) == _acceptedType;

    protected override bool CanMerge(InventoryItem item) => false;
}
