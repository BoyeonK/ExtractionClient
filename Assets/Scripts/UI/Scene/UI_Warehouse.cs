using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UI_Warehouse : UI_ItemRepo {
    TestLobbyScene _scene;

    enum SelectedTab {
        All,
        Weapon,
        Equipment,
        Ammo,
        Misc
    }
    SelectedTab _selectedTab = SelectedTab.All;
    Color _selectedColor = new Color(51f / 255f, 153f / 255f, 1f, 1f);
    Color _unselectedColor = new Color(33f / 255f, 33f / 255f, 41f / 255f, 1f);

    UI_EventHandler _tabAll;
    UI_EventHandler _tabWeapon;
    UI_EventHandler _tabEquipment;
    UI_EventHandler _tabAmmo;
    UI_EventHandler _tabMisc;

    Image _imageAll;
    Image _imageWeapon;
    Image _imageEquipment;
    Image _imageAmmo;
    Image _imageMisc;

    List<ISlot> _iSlots = new();

    public override void Init() {
        base.Init();
        if (isInit) return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        _tabAll = BindComponent<UI_EventHandler>("WarehouseWindow/TabBar/Tab_ALL");
        _tabWeapon = BindComponent<UI_EventHandler>("WarehouseWindow/TabBar/Tab_WEAPONS");
        _tabEquipment = BindComponent<UI_EventHandler>("WarehouseWindow/TabBar/Tab_EQUIPMENT");
        _tabAmmo = BindComponent<UI_EventHandler>("WarehouseWindow/TabBar/Tab_AMMO");
        _tabMisc = BindComponent<UI_EventHandler>("WarehouseWindow/TabBar/Tab_MISC");

        _imageAll = BindComponent<Image>("WarehouseWindow/TabBar/Tab_ALL");
        _imageWeapon = BindComponent<Image>("WarehouseWindow/TabBar/Tab_WEAPONS");
        _imageEquipment = BindComponent<Image>("WarehouseWindow/TabBar/Tab_EQUIPMENT");
        _imageAmmo = BindComponent<Image>("WarehouseWindow/TabBar/Tab_AMMO");
        _imageMisc = BindComponent<Image>("WarehouseWindow/TabBar/Tab_MISC");

        Transform gridTransform = transform.Find("WarehouseWindow/WarehouseSection/WarehouseGrid");
        if (gridTransform != null) {
            ISlot[] slots = gridTransform.GetComponentsInChildren<ISlot>(true);
            _iSlots = new List<ISlot>(slots);
        }
        else {
            Util.LogError("[UI_Warehouse] WarehouseGrid 오브젝트를 찾을 수 없습니다.");
        }

        for (int i = 0; i < _iSlots.Count; i++)
            _iSlots[i].Init(i, _scene);

        base.OnInitComplete();
    }

    public ISlot FirstEmptySlot {
        get {
            for (int i = 0; i < _iSlots.Count; i++)
                if (_scene.WarehouseSlots[i] == null) return _iSlots[i];
            return null;
        }
    }
    public bool HasEmptySlot => FirstEmptySlot != null;

    public void Refresh() {
        RefreshSlots();
    }

    private void RefreshSlots() {
        for (int i = 0; i < _iSlots.Count; i++) {
            InventoryItem item = (i < _scene.WarehouseSlots.Length) ? _scene.WarehouseSlots[i] : null;
            if (item != null) _iSlots[i].SetItem(item);
            else              _iSlots[i].ClearSlot();
        }
    }

    // UI_Scene override — 드래그 앤 드롭 및 split으로 슬롯 데이터 교체
    public override void SetItemAtSlot(int slotIndex, InventoryItem item) {
        _scene.SetWarehouseSlot(slotIndex, item);
        RefreshSlots();
    }

    private void OnDestroy() {

    }
}
