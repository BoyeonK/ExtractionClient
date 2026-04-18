using UnityEngine;

public class UI_Shop : UI_Scene {
    TestLobbyScene _scene;

    public override void Init() {
        base.Init();
        _scene = FindAnyObjectByType<TestLobbyScene>();
    }

    // 구매 버튼 등 UI 이벤트에서 호출
    public void RequestPurchase(int itemId, int quantity) {
        _scene.TryPurchase(itemId, quantity);
    }
}
