using System;

[Serializable]
public class ErrorDetail {
    public string message;
    public string code;
}

[Serializable]
public class BaseResponse {
    public bool success;
    public int code;
    public ErrorDetail error;
}

[Serializable]
public class VersionData {
    public string latestVersion;
    public bool isMaintenance;
}

[Serializable]
public class VersionResponse : BaseResponse {
    public VersionData data;
}

[Serializable]
public class InventoryItem {
    public int item_id;
    public int slot_index;
    public int quantity;
}

[Serializable]
public class AuthData {
    public string sessionId;
    public int uid;
    public int money;
    public InventoryItem[] inventory;
}

[Serializable]
public class AuthResponse : BaseResponse {
    public AuthData data;
}

[Serializable]
public class GuestAuthData {
    public string sessionId;
    public string guestId;
    public int uid;
}

[Serializable]
public class GuestAuthResponse : BaseResponse {
    public GuestAuthData data;
}

[Serializable]
public class InventoryData {
    public InventoryItem[] inventory;
}

[Serializable]
public class InventoryResponse : BaseResponse {
    public InventoryData data;
}

[Serializable]
public class MatchTicketData {
    public string ticketId;
}

[Serializable]
public class MatchStartResponse : BaseResponse {
    public MatchTicketData data;
}

[Serializable]
public class MatchStatusData {
    public string status;
    public string roomToken;
}

[Serializable]
public class MatchStatusResponse : BaseResponse {
    public MatchStatusData data;
}

[Serializable]
public class ConnectResponseData {
    public string ip;
    public int port;
    public string securityKey;
    public int ingameSessionId;
}

[Serializable]
public class ConnectResponse : BaseResponse {
    public ConnectResponseData data;
}