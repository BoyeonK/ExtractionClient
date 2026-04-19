using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class UI_Shop : UI_Scene {
    TestLobbyScene _scene;

    enum SelectedTab {
        All,
        Weapon,
        Equipment,
        Ammo,
        Misc
    }
    SelectedTab _selectedTab = SelectedTab.All;

    List<SSlot> _sSlots = new();

    public override void Init() {
        base.Init();
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        Transform gridTransform = transform.Find("ShopWindow/ShopContent/ShopSection/ShopGrid");
        if (gridTransform != null) {
            SSlot[] slots = gridTransform.GetComponentsInChildren<SSlot>(true);
            _sSlots = new List<SSlot>(slots);
        } else {
            Util.LogError("앙기르무 유가르띠");
        }

        for (int i = 0; i < _sSlots.Count; i++)
            _sSlots[i].Init(this);

        base.OnInitComplete();
    }

    private void GetShopItems() {
        ShopItem[] itemList = Managers.Network.httpManager.ShopItems;
        for (int i=0; i < itemList.Length; i++) {
            if (i >= _sSlots.Count) break;
            _sSlots[i].SetItem(itemList[i]);
        }
    }

    public void SelectItem(int itemId, int price) {

    }

    // 구매 버튼 등 UI 이벤트에서 호출
    public void RequestPurchase(int itemId, int quantity) {
        _scene.TryPurchase(itemId, quantity);
    }
}
