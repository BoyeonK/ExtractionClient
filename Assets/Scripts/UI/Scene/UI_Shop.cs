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

    int _quantity = 1;
    TextMeshProUGUI _quantityTxt;
    UI_EventHandler _plusBtn;
    UI_EventHandler _minusBtn;
    UI_EventHandler _buyBtn;

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
        }

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

        base.OnInitComplete();
    }

    public void Refresh() {
        int count = Managers.Network.httpManager.ShopItems.Length;
        for(int i=0; i < count; i++) {
            if (i >= _sSlots.Count) break;
            _sSlots[i].ActiveThis();
        }
        for(int i=count; i < _sSlots.Count; i++)
            _sSlots[i].DeactiveThis();
    }

    public void GetShopItems() {
        ShopItem[] itemList = Managers.Network.httpManager.ShopItems;
        for (int i=0; i < itemList.Length; i++) {
            if (i >= _sSlots.Count) break;
            _sSlots[i].SetItem(itemList[i]);
        }
    }

    private void ResetDetailPanel() { 
        // TODO :
        // 1. ItemPreview를 ReDraw
        // 2. 나머지 DetailPanel의 값을 초기화함.
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

    }

    private void OnMinusBtnClick() {

    }

    private void OnBuyBtnClick() {
        _scene.TryPurchase(_selectedItemId, _quantity);
    }
}
