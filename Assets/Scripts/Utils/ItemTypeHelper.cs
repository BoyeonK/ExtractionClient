public enum ItemType { None, Weapon, Equipment, Ammo, Misc }

public static class ItemTypeHelper {
    public static ItemType GetType(int itemId) {
        if (itemId >= 1 && itemId < 4) return ItemType.Weapon;
        if (itemId >= 4 && itemId < 5) return ItemType.Equipment;
        if (itemId >= 5 && itemId < 7) return ItemType.Ammo;
        return ItemType.Misc;
    }
}
