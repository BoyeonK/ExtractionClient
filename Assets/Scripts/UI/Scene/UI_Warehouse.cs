using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_Warehouse : UI_Scene {
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

    List<InventoryItem> _fullItems = new();
    List<InventoryItem> _showItems = new();
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

        // TODO :
        // 1. islots의 크기만큼 List의 크기를 할당.

        base.OnInitComplete();
    }

    private void FilterSlot(SelectedTab filter) {
        // TODO :
        // 1. showitems 리스트를 초기화.
        // 2. fullitems 리스트를 순회하면서 아이템 타입에 따라 showitems 리스트에 아이템을 추가.
        // 3. RefreshSlots() 메서드를 호출하여 슬롯을 업데이트.
    }

    private void RefreshSlots( ) {
        // TODO :
        // 1. showitems 리스트를 순회하면서 순서대로 슬롯에 아이템 정보를 표시.
        // 2. 슬롯이 비어있는 경우에는 빈 슬롯으로 표시
    }

    private void OnDestroy() {
        
    }
}
