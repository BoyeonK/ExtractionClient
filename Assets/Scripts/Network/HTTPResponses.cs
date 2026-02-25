using System;

[Serializable]
public class GuestLoginResponse {
    public bool success;
    public int status;
    public GuestData data;
}

[Serializable]
public class GuestData {
    public string sessionId;
    public string guestId;
}
