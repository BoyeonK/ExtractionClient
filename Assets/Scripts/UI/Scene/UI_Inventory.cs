using System.Collections.Generic;
using UnityEngine;

public class UI_Inventory : UI_Scene {
    TestLobbyScene _scene;

    List<InventoryItem> _inventoryItems = new();
    List<ISlot> _iSlots = new();

    public override void Init() {
        base.Init();
        if (isInit) return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        Transform gridTransform = transform.Find("InventoryWindow/GridPanel/ItemGrid");
        if (gridTransform != null) {
            ISlot[] slots = gridTransform.GetComponentsInChildren<ISlot>(true);
            _iSlots = new List<ISlot>(slots);
        }
        else {
            Util.LogError("[UI_Inventory] ItemGrid 오브젝트를 찾을 수 없습니다.");
        }

        _inventoryItems = new List<InventoryItem>(new InventoryItem[_iSlots.Count]);
        for (int i = 0; i < _iSlots.Count; i++)
            _iSlots[i].Init(i, _scene);

        base.OnInitComplete();
    }

    // 서버에서 받아온 아이템 목록으로 인벤토리를 채움
    public void SetData(List<InventoryItem> items) {
        for (int i = 0; i < _inventoryItems.Count; i++)
            _inventoryItems[i] = i < items.Count ? items[i] : null;
        RefreshSlots();
    }

    private void RefreshSlots() {
        for (int i = 0; i < _iSlots.Count; i++) {
            if (_inventoryItems[i] != null)
                _iSlots[i].SetItem(_inventoryItems[i]);
            else
                _iSlots[i].ClearSlot();
        }
    }

    // UI_Scene override — 드래그 앤 드롭으로 슬롯 데이터 교체
    public override void SetItemAtSlot(int slotIndex, InventoryItem item) {
        if (slotIndex < 0 || slotIndex >= _inventoryItems.Count) return;
        _inventoryItems[slotIndex] = item;
        RefreshSlots();
    }

    private void OnDestroy() {

    }
}
