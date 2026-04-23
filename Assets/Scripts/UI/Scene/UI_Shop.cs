using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Shop : UI_Scene {
    TestLobbyScene _scene;
    int _selectedItemId = -1;
    int _selectedItemPrice = 0;
    Image _selectedItemImage;
    TextMeshProUGUI _selectedItemName;
    TextMeshProUGUI _selectedItemDescription;
    TextMeshProUGUI _totalPrice;
    TextMeshProUGUI _myMoney;

    UI_EventHandler _allBtn;
    UI_EventHandler _weaponBtn;
    UI_EventHandler _equipmentBtn;
    UI_EventHandler _ammoBtn;
    UI_EventHandler _miscBtn;
    Image _allBtnColor;
    Image _weaponBtnColor;
    Image _equipmentBtnColor;
    Image _ammoBtnColor;
    Image _miscBtnColor;
    enum SelectedTab {
        All,
        Weapon,
        Equipment,
        Ammo,
        Misc
    }
    SelectedTab _selectedTab = SelectedTab.All;
    Color _selectedTabColor = new Color(51 / 255f, 153 / 255f, 1f, 1f);
    Color _unselectedTabColor = new Color(33 / 255f, 33 / 255f, 41 / 255f, 1f);
    Color _hoverTabColor = new Color(50 / 255f, 60 / 255f, 75 / 255f, 1f);

    int _quantity = 1;
    TextMeshProUGUI _quantityTxt;
    UI_EventHandler _plusBtn;
    UI_EventHandler _minusBtn;
    UI_EventHandler _buyBtn;

    List<SSlot> _sSlots = new();

    public override void Init() {
        base.Init();
        if (isInit) return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        Transform gridTransform = transform.Find("ShopWindow/ShopContent/ShopSection/ShopGrid");
        if (gridTransform != null) {
            SSlot[] slots = gridTransform.GetComponentsInChildren<SSlot>(true);
            _sSlots = new List<SSlot>(slots);
        }

        _allBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/TabBar/Tab_ALL", this.gameObject);
        _weaponBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/TabBar/Tab_WEAPONS", this.gameObject);
        _equipmentBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/TabBar/Tab_EQUIPMENT", this.gameObject);
        _ammoBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/TabBar/Tab_AMMO", this.gameObject);
        _miscBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/TabBar/Tab_MISC", this.gameObject);
        
        _allBtnColor = Util.BindComponent<Image>("ShopWindow/TabBar/Tab_ALL", this.gameObject);
        _weaponBtnColor = Util.BindComponent<Image>("ShopWindow/TabBar/Tab_WEAPONS", this.gameObject);
        _equipmentBtnColor = Util.BindComponent<Image>("ShopWindow/TabBar/Tab_EQUIPMENT", this.gameObject);
        _ammoBtnColor = Util.BindComponent<Image>("ShopWindow/TabBar/Tab_AMMO", this.gameObject);
        _miscBtnColor = Util.BindComponent<Image>("ShopWindow/TabBar/Tab_MISC", this.gameObject);

        _selectedItemImage = Util.BindComponent<Image>("ShopWindow/ShopContent/DetailPanel/ItemPreview/Fill", this.gameObject);
        _selectedItemName = Util.BindComponent<TextMeshProUGUI>("ShopWindow/ShopContent/DetailPanel/ItemName", this.gameObject);
        _selectedItemDescription = Util.BindComponent<TextMeshProUGUI>("ShopWindow/ShopContent/DetailPanel/ItemDesc", this.gameObject);
        _totalPrice = Util.BindComponent<TextMeshProUGUI>("ShopWindow/ShopContent/DetailPanel/PriceSection/PriceValue", this.gameObject);
        _quantityTxt = Util.BindComponent<TextMeshProUGUI>("ShopWindow/ShopContent/DetailPanel/QuantitySection/QtyValue", this.gameObject);
        _myMoney = Util.BindComponent<TextMeshProUGUI>("ShopWindow/Header/CurrencyDisplay/CurrencyValue", this.gameObject);
        _plusBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/ShopContent/DetailPanel/QuantitySection/BtnPlus", this.gameObject);
        _minusBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/ShopContent/DetailPanel/QuantitySection/BtnMinus", this.gameObject);
        _buyBtn = Util.BindComponent<UI_EventHandler>("ShopWindow/ShopContent/DetailPanel/BuyButton", this.gameObject);

        for (int i = 0; i < _sSlots.Count; i++)
            _sSlots[i].Init(this);

        _plusBtn.OnClickHandler += (e) => OnPlusBtnClick();
        _minusBtn.OnClickHandler += (e) => OnMinusBtnClick();
        _buyBtn.OnClickHandler += (e) => OnBuyBtnClick();
        _allBtn.OnClickHandler += (e) => OnAllBtnClick();
        _weaponBtn.OnClickHandler += (e) => OnWeaponBtnClick();
        _equipmentBtn.OnClickHandler += (e) => OnEquipBtnClick();
        _ammoBtn.OnClickHandler += (e) => OnAmmoBtnClick();
        _miscBtn.OnClickHandler += (e) => OnMiscBtnClick();

        _allBtn.OnPointerEnterHandler       += (e) => { if (_selectedTab != SelectedTab.All)       _allBtnColor.color = _hoverTabColor; };
        _allBtn.OnPointerExitHandler        += (e) => { if (_selectedTab != SelectedTab.All)       _allBtnColor.color = _unselectedTabColor; };
        _weaponBtn.OnPointerEnterHandler    += (e) => { if (_selectedTab != SelectedTab.Weapon)    _weaponBtnColor.color = _hoverTabColor; };
        _weaponBtn.OnPointerExitHandler     += (e) => { if (_selectedTab != SelectedTab.Weapon)    _weaponBtnColor.color = _unselectedTabColor; };
        _equipmentBtn.OnPointerEnterHandler += (e) => { if (_selectedTab != SelectedTab.Equipment) _equipmentBtnColor.color = _hoverTabColor; };
        _equipmentBtn.OnPointerExitHandler  += (e) => { if (_selectedTab != SelectedTab.Equipment) _equipmentBtnColor.color = _unselectedTabColor; };
        _ammoBtn.OnPointerEnterHandler      += (e) => { if (_selectedTab != SelectedTab.Ammo)      _ammoBtnColor.color = _hoverTabColor; };
        _ammoBtn.OnPointerExitHandler       += (e) => { if (_selectedTab != SelectedTab.Ammo)      _ammoBtnColor.color = _unselectedTabColor; };
        _miscBtn.OnPointerEnterHandler      += (e) => { if (_selectedTab != SelectedTab.Misc)      _miscBtnColor.color = _hoverTabColor; };
        _miscBtn.OnPointerExitHandler       += (e) => { if (_selectedTab != SelectedTab.Misc)      _miscBtnColor.color = _unselectedTabColor; };

        base.OnInitComplete();
    }

    public void Refresh() {
        _myMoney.text = Managers.Network.httpManager.Money.ToString();
        FilterBy(_selectedTab);
    }

    private void ResetDetailPanel() {
        if (_selectedItemId != -1) {
            string path = $"Images/Items/icon_item_{_selectedItemId}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                _selectedItemImage.sprite = sprite;
                Color color = _selectedItemImage.color;
                color.a = 1.0f;
                _selectedItemImage.color = color;
            }

            _quantity = 1;
            _quantityTxt.text = "1";
            _totalPrice.text = _selectedItemPrice.ToString();

            ItemType type = ItemTypeHelper.GetType(_selectedItemId);
            bool canStack = type != ItemType.Weapon && type != ItemType.Equipment;
            _plusBtn.gameObject.SetActive(canStack);
            _minusBtn.gameObject.SetActive(canStack);
        } else {
            Color color = _selectedItemImage.color;
            color.a = 0f;
            _selectedItemImage.color = color;
            _selectedItemName.text = "";
            _selectedItemDescription.text = "";
            _totalPrice.text = "";
            _quantityTxt.text = "";
            _plusBtn.gameObject.SetActive(false);
            _minusBtn.gameObject.SetActive(false);
        }
    }

    // --------------------------------------------------
    // ---------- EventHandler에 등록할 함수들 ----------
    // --------------------------------------------------
    public void SelectItem(int itemId, int price) {
        if (_selectedItemId == itemId) return;
        _selectedItemId = itemId;
        _selectedItemPrice = price;

        ResetDetailPanel();
    }


    private void OnPlusBtnClick() {
        if (_quantity >= 99) return;
        _quantity++;
        _quantityTxt.text = _quantity.ToString();
        _totalPrice.text = (_selectedItemPrice * _quantity).ToString();
    }

    private void OnMinusBtnClick() {
        if (_quantity <= 1) return;
        _quantity--;
        _quantityTxt.text = _quantity.ToString();
        _totalPrice.text = (_selectedItemPrice * _quantity).ToString();
    }

    private void OnBuyBtnClick() {
        if (_selectedItemId == -1) return;
        _scene.TryPurchase(_selectedItemId, _quantity);
    }

    private void OnAllBtnClick() {
        if (_selectedTab == SelectedTab.All) return;
        _selectedTab = SelectedTab.All;
        SetUnselectedColor();
        _allBtnColor.color = _selectedTabColor;
        FilterBy(_selectedTab);
    }

    private void OnWeaponBtnClick() {
        if (_selectedTab == SelectedTab.Weapon) return;
        _selectedTab = SelectedTab.Weapon;
        SetUnselectedColor();
        _weaponBtnColor.color = _selectedTabColor;
        FilterBy(_selectedTab);
    }

    private void OnEquipBtnClick() {
        if (_selectedTab == SelectedTab.Equipment) return;
        _selectedTab = SelectedTab.Equipment;
        SetUnselectedColor();
        _equipmentBtnColor.color = _selectedTabColor;
        FilterBy(_selectedTab);
    }

    private void OnAmmoBtnClick() {
        if (_selectedTab == SelectedTab.Ammo) return;
        _selectedTab = SelectedTab.Ammo;
        SetUnselectedColor();
        _ammoBtnColor.color = _selectedTabColor;
        FilterBy(_selectedTab);
    }

    private void OnMiscBtnClick() {
        if (_selectedTab == SelectedTab.Misc) return;
        _selectedTab = SelectedTab.Misc;
        SetUnselectedColor();
        _miscBtnColor.color = _selectedTabColor;
        FilterBy(_selectedTab);
    }

    private ItemType TabToItemType(SelectedTab tab) => tab switch {
        SelectedTab.Weapon    => ItemType.Weapon,
        SelectedTab.Equipment => ItemType.Equipment,
        SelectedTab.Ammo      => ItemType.Ammo,
        SelectedTab.Misc      => ItemType.Misc,
        _                     => ItemType.None,
    };

    private void FilterBy(SelectedTab tab) {
        ShopItem[] items = Managers.Network.httpManager.ShopItems;
        if (items == null) return;

        int slotIndex = 0;
        foreach (ShopItem item in items) {
            if (slotIndex >= _sSlots.Count) break;
            bool show = tab == SelectedTab.All
                || ItemTypeHelper.GetType(item.item_id) == TabToItemType(tab);
            if (show) {
                _sSlots[slotIndex].SetItem(item);
                _sSlots[slotIndex].ActiveThis();
                slotIndex++;
            }
        }
        for (int i = slotIndex; i < _sSlots.Count; i++)
            _sSlots[i].DeactiveThis();

        if (_selectedItemId != -1) {
            bool stillVisible = false;
            foreach (ShopItem item in items) {
                if (item.item_id != _selectedItemId) continue;
                if (tab == SelectedTab.All || ItemTypeHelper.GetType(item.item_id) == TabToItemType(tab))
                    stillVisible = true;
                break;
            }
            if (!stillVisible) {
                _selectedItemId = -1;
                ResetDetailPanel();
            }
        }
    }

    private void SetUnselectedColor() {
        _allBtnColor.color = _unselectedTabColor;
        _weaponBtnColor.color = _unselectedTabColor;
        _equipmentBtnColor.color = _unselectedTabColor;
        _ammoBtnColor.color = _unselectedTabColor;
        _miscBtnColor.color = _unselectedTabColor;
    }
}
