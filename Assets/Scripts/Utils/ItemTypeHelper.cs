using System.Collections.Generic;

public enum ItemType { None, Weapon, Equipment, Ammo, Misc }

// AI로 DB긁어서 Dictionary채우기 자동화.
public static class ItemTypeHelper {
    public static ItemType GetType(int itemId) {
        if (itemId >= 1 && itemId < 4) return ItemType.Weapon;
        if (itemId >= 4 && itemId < 5) return ItemType.Equipment;
        if (itemId >= 5 && itemId < 7) return ItemType.Ammo;
        return ItemType.Misc;
    }

    /*
    public static ItemType GetType(int itemId) =>
        _typeMap.TryGetValue(itemId, out var type) ? type : ItemType.Misc;

    public static string GetName(int itemId) =>
        _nameMap.TryGetValue(itemId, out var name) ? name : "알 수 없는 아이템";

    public static string GetDescription(int itemId) =>
        _descriptionMap.TryGetValue(itemId, out var desc) ? desc : "";

    static Dictionary<int, ItemType> _typeMap = new();
    static Dictionary<int, string> _nameMap = new();
    static Dictionary<int, string> _descriptionMap = new();
    */
}
