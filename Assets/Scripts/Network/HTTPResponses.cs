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