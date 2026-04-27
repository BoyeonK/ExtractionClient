using UnityEngine;

public class NetworkManager {
    public UDPManager udpManager = new UDPManager();
    public HTTPManager httpManager = new HTTPManager();

    public void OnUpdate() {
        udpManager.OnUpdate();
    }
}
