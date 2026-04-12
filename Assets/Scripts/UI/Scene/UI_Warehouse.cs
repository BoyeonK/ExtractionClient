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

    // _fullItems: 창고의 전체 아이템
    // _showItems: 현재 탭 필터에 따라 표시되는 아이템 (_iSlots와 1:1 대응)
    // 필터 미구현 시 _showItems = _fullItems (동일 참조)
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

        _fullItems = new List<InventoryItem>(new InventoryItem[_iSlots.Count]);
        _showItems = _fullItems; // 필터 미구현: 동일 참조 사용
        for (int i = 0; i < _iSlots.Count; i++)
            _iSlots[i].Init(i, _scene);

        base.OnInitComplete();
    }

    // 서버에서 받아온 아이템 목록으로 창고를 채움
    public void SetData(List<InventoryItem> items) {
        for (int i = 0; i < _fullItems.Count; i++)
            _fullItems[i] = i < items.Count ? items[i] : null;
        FilterSlot(_selectedTab);
    }

    private void FilterSlot(SelectedTab filter) {
        // TODO: filter에 따라 _showItems 재구성 후 RefreshSlots() 호출
        // 현재는 필터 미구현 — _showItems = _fullItems 동일 참조이므로 그대로 표시
        RefreshSlots();
    }

    private void RefreshSlots() {
        for (int i = 0; i < _iSlots.Count; i++) {
            if (i < _showItems.Count && _showItems[i] != null)
                _iSlots[i].SetItem(_showItems[i]);
            else
                _iSlots[i].ClearSlot();
        }
    }

    // UI_Scene override — 드래그 앤 드롭으로 슬롯 데이터 교체
    // slotIndex는 _showItems 기준 인덱스 (_iSlots와 동일)
    public override void SetItemAtSlot(int slotIndex, InventoryItem item) {
        if (slotIndex < 0 || slotIndex >= _showItems.Count) return;
        _showItems[slotIndex] = item;
        // _showItems == _fullItems (동일 참조)이므로 _fullItems도 자동 반영
        // TODO: 필터 활성화 시 _fullItems의 실제 인덱스를 찾아 별도 업데이트 필요
        RefreshSlots();
    }

    private void OnDestroy() {

    }
}
