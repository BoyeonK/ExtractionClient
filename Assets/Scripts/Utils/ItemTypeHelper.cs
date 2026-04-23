using System.Collections.Generic;

public enum ItemType { None, Weapon, Equipment, Ammo, Misc }

public static class ItemTypeHelper {
    static Dictionary<int, ItemType> _typeMap = new() {
        { 1, ItemType.Weapon },
        { 2, ItemType.Weapon },
        { 3, ItemType.Weapon },
        { 4, ItemType.Equipment },
        { 5, ItemType.Ammo },
        { 6, ItemType.Ammo },
        { 7, ItemType.Misc },
    };

    static Dictionary<int, string> _nameMap = new() {
        { 1, "AK-47" },
        { 2, "M4A1" },
        { 3, "M16" },
        { 4, "경량 조끼" },
        { 5, "5.56mm" },
        { 6, "7.62mm" },
        { 7, "돌맹이" },
    };

    static Dictionary<int, string> _descriptionMap = new() {
        { 1, "테스트용 임시데이터" },
        { 2, "테스트용 임시데이터" },
        { 3, "테스트용 임시데이터" },
        { 4, "테스트용 임시데이터" },
        { 5, "테스트용 임시데이터" },
        { 6, "테스트용 임시데이터" },
        { 7, "테스트용 임시데이터" },
    };

    public static ItemType GetType(int id) =>
        _typeMap.TryGetValue(id, out var t) ? t : ItemType.Misc;

    public static string GetName(int id) =>
        _nameMap.TryGetValue(id, out var n) ? n : "버그 발생";

    public static string GetDescription(int id) =>
        _descriptionMap.TryGetValue(id, out var d) ? d : "조속히 클라이언트를 종료할 수 있도록!";
}
