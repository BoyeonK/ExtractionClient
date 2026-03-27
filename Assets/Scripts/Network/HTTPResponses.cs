using System;

// ----------------------------------------------------
// 공통 응답 구조 (Base Wrapper)
// ----------------------------------------------------
[Serializable]
public class BaseResponse {
    public bool success;
    public int code; // ?? OpenAPI 명세서의 'code' 필드에 맞춤 (기존 status)
    public string message; // 에러 발생 시 읽어올 메시지
}

// ----------------------------------------------------
// [System] 시스템 API 응답
// ----------------------------------------------------
[Serializable]
public class VersionData {
    public string latestVersion;
    public bool isMaintenance;
}

[Serializable]
public class VersionResponse : BaseResponse {
    public VersionData data;
}

// ----------------------------------------------------
// [Auth] 계정 API 응답
// ----------------------------------------------------
[Serializable]
public class AuthData {
    public string sessionId;
    public int uid;
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

// ----------------------------------------------------
// [Game] 매치메이킹 API 응답
// ----------------------------------------------------
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
    public string status; // "WAITING", "SUCCESS", "FAILED" 등
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