using System;

[Serializable]
public class CreateAccountRequestData {
    public string id;
    public string password;
}

[Serializable]
public class LoginRequestData {
    public string id;
    public string password;
}

[Serializable]
public class AuthResponse {
    public bool success;
    public int status;
    public AuthData data; // makeResponse의 'data' 필드에 대응
}

[Serializable]
public class AuthData {
    public string sessionId;
    public int uid;          // 로그인, 회원가입 시 오는 유저 고유 ID
    public string guestId;   // 게스트 로그인 시에만 값이 들어옴
}

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
public class MatchStatusResponse {
    public bool success;
    public int status;
    public MatchStatusData data;
}

[Serializable]
public class MatchStatusData {
    public string status;
    public string roomToken;
}

[Serializable]
public class MatchStartResponse {
    public bool success;
    public int status;
    public MatchTicket data;
}

[Serializable]
public class MatchTicket {
    public string ticketId;
}

[Serializable]
public class BaseResponse {
    public bool success;
    public int status;
    public string message;
}