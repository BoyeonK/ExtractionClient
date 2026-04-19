using System.Collections.Generic;
using UnityEngine;

public class UI_Inventory : UI_ItemRepo {
    TestLobbyScene _scene;

    List<ISlot> _iSlots = new();

    public const int LOADOUT_START = 25;
    LSlot _weaponSlot1, _weaponSlot2, _equipSlot;

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

    public InventoryItem[] GetLoadout() => (InventoryItem[])_scene.LoadoutSlots.Clone();

    public ISlot FirstEmptySlot {
        get {
            for (int i = 0; i < _iSlots.Count; i++)
                if (_scene.InventorySlots[i] == null) return _iSlots[i];
            return null;
        }
    }
    public bool HasEmptySlot => FirstEmptySlot != null;

    public void Refresh() {
        RefreshSlots();
        RefreshLoadoutSlots();
    }

    private void RefreshSlots() {
        for (int i = 0; i < _iSlots.Count; i++) {
            InventoryItem item = (i < _scene.InventorySlots.Length) ? _scene.InventorySlots[i] : null;
            if (item != null) _iSlots[i].SetItem(item);
            else              _iSlots[i].ClearSlot();
        }
    }

    private void RefreshLoadoutSlots() {
        RefreshLSlot(_weaponSlot1, _scene.LoadoutSlots[0]);
        RefreshLSlot(_weaponSlot2, _scene.LoadoutSlots[1]);
        RefreshLSlot(_equipSlot,   _scene.LoadoutSlots[2]);
    }

    private void RefreshLSlot(LSlot slot, InventoryItem item) {
        if (slot == null) return;
        if (item != null) slot.SetItem(item);
        else              slot.ClearSlot();
    }

    // UI_Scene override — 드래그 앤 드롭으로 슬롯 데이터 교체
    public override void SetItemAtSlot(int slotIndex, InventoryItem item) {
        if (slotIndex >= LOADOUT_START) {
            _scene.SetLoadoutSlot(slotIndex - LOADOUT_START, item);
            RefreshLoadoutSlots();
        } else {
            _scene.SetInventorySlot(slotIndex, item);
            RefreshSlots();
        }
    }
}
