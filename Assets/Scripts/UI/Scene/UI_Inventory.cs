using System.Collections.Generic;
using UnityEngine;

public class UI_Inventory : UI_Scene {
    TestLobbyScene _scene;

    List<InventoryItem> _inventoryItems = new();
    List<ISlot> _iSlots = new();

    public const int LOADOUT_START = 25;
    LSlot _weaponSlot1, _weaponSlot2, _equipSlot;
    InventoryItem[] _loadoutItems = new InventoryItem[3];

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

        _weaponSlot1 = transform.Find("InventoryWindow/EquipmentPanel/EquipPrimaryWpn")?.GetComponent<LSlot>();
        _weaponSlot2 = transform.Find("InventoryWindow/EquipmentPanel/EquipSecondaryWpn")?.GetComponent<LSlot>();
        _equipSlot   = transform.Find("InventoryWindow/EquipmentPanel/EquipBody")?.GetComponent<LSlot>();

        _weaponSlot1?.Init(LOADOUT_START + 0, _scene, ItemType.Weapon);
        _weaponSlot2?.Init(LOADOUT_START + 1, _scene, ItemType.Weapon);
        _equipSlot?.  Init(LOADOUT_START + 2, _scene, ItemType.Equipment);

        base.OnInitComplete();
    }

    // 서버에서 받아온 아이템 목록으로 인벤토리를 채움
    public void SetData(List<InventoryItem> items) {
        for (int i = 0; i < _inventoryItems.Count; i++)
            _inventoryItems[i] = i < items.Count ? items[i] : null;
        RefreshSlots();
    }

    public void SetLoadoutData(InventoryItem[] loadout) {
        for (int i = 0; i < _loadoutItems.Length; i++)
            _loadoutItems[i] = (loadout != null && i < loadout.Length) ? loadout[i] : null;
        RefreshLoadoutSlots();
    }

    public InventoryItem[] GetLoadout() => (InventoryItem[])_loadoutItems.Clone();

    private void RefreshSlots() {
        for (int i = 0; i < _iSlots.Count; i++) {
            if (_inventoryItems[i] != null)
                _iSlots[i].SetItem(_inventoryItems[i]);
            else
                _iSlots[i].ClearSlot();
        }
    }

    private void RefreshLoadoutSlots() {
        RefreshLSlot(_weaponSlot1, _loadoutItems[0]);
        RefreshLSlot(_weaponSlot2, _loadoutItems[1]);
        RefreshLSlot(_equipSlot,   _loadoutItems[2]);
    }

    private void RefreshLSlot(LSlot slot, InventoryItem item) {
        if (slot == null) return;
        if (item != null) slot.SetItem(item);
        else              slot.ClearSlot();
    }

    // UI_Scene override — 드래그 앤 드롭으로 슬롯 데이터 교체
    public override void SetItemAtSlot(int slotIndex, InventoryItem item) {
        if (slotIndex >= LOADOUT_START) {
            int li = slotIndex - LOADOUT_START;
            if (li < _loadoutItems.Length) {
                _loadoutItems[li] = item;
                RefreshLoadoutSlots();
            }
        } else {
            if (slotIndex < 0 || slotIndex >= _inventoryItems.Count) return;
            _inventoryItems[slotIndex] = item;
            RefreshSlots();
        }
    }
}
