using System;
using UnityEngine;

public class NetworkManager {
    public void TestCall() {
        Managers.ExecuteAtMainThread(() => {
            Debug.Log("NetworkManager.TestCall()이 메인 스레드에서 실행되었습니다!");
        });
    }
}
