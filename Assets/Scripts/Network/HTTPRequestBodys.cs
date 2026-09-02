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
public class MatchStartRequest {
    public int mapId;
    public int characterType;
    public string loadoutType;
    public InventoryItem[] inventory;
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

// ----------------------------------------------------
// [Items] 아이템 판매 요청
// ----------------------------------------------------
// item_id·quantity는 서버가 스냅샷의 해당 슬롯과 대조하는 검사값이다 —
// quantity는 판매 수량이 아니라 그 슬롯 스택 전체 수량에 대한 주장이라 정확히 일치해야 하고,
// 부분 판매는 클라가 스택을 먼저 나눈 스냅샷을 보내는 것으로 표현한다
[Serializable]
public class SellRequest {
    public int item_id;
    public int slot_index;
    public int quantity;
    public InventoryItem[] inventory;   // 판매 전 인벤토리 전체 스냅샷 (판매할 아이템 포함)
}