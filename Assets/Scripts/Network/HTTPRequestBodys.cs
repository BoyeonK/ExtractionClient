using System;

// ----------------------------------------------------
// [Auth] 계정 관련 요청
// ----------------------------------------------------
[Serializable]
public class AuthRequest {
    public string id;
    public string password;
}

// ----------------------------------------------------
// [Game] 매치메이킹 관련 요청
// ----------------------------------------------------
[Serializable]
public class EquippedItem {
    public int itemId;
    public int quantity;
}

[Serializable]
public class MatchStartRequest {
    public int mapId;
    public string loadoutType;
    public EquippedItem[] equippedItems;
}

[Serializable]
public class MatchCancelRequest {
    public string ticketId;
}

[Serializable]
public class ConnectRequest {
    public string roomToken;
}

// ----------------------------------------------------
// [Items] 아이템 구매 요청
// ----------------------------------------------------
[Serializable]
public class PurchaseRequest {
    public int item_id;
    public int slot_index;
    public int quantity;
    public InventoryItem[] inventory;   // 현재 클라이언트 인벤토리 전체 스냅샷
}