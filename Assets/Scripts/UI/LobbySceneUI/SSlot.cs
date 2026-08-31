using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SSlot : MonoBehaviour {
    UI_Shop _UI_Shop;
    int _itemId = -1;
    int _price = 0;

    public UI_EventHandler _eventHandler;
    Image _iconImage;
    TextMeshProUGUI _priceTxt;

    public void Init(UI_Shop owner) {
        _UI_Shop = owner;
        _iconImage = Util.BindComponent<Image>("Fill", this.gameObject);
        _priceTxt = Util.BindComponent<TextMeshProUGUI>("Price", this.gameObject);
        _eventHandler = GetComponent<UI_EventHandler>();
        DeactiveThis();
    }

    public void SetItem(ShopItem item) {
        _itemId = item.item_id;
        _price = item.price;

        // 표시·숨김은 Image.enabled로 한다 — 색을 건드리면 프리팹의 Fill 색이 아이콘에 곱해진다(ISlot과 같은 규칙).
        // 못 찾았을 때도 대입해야 직전 아이템의 아이콘이 남지 않는다 — 상점 슬롯은 재사용된다
        string path = $"Images/Items/icon_item_{_itemId}";
        Sprite iconSprite = Resources.Load<Sprite>(path);
        _iconImage.sprite = iconSprite;
        _iconImage.enabled = iconSprite != null;

        _priceTxt.text = _price.ToString();
    }

    private void OnClick(PointerEventData eventData) {
        _UI_Shop.SelectItem(_itemId, _price);
    }

    public void ActiveThis() {
        this.gameObject.SetActive(true);
        _eventHandler.OnClickHandler = OnClick;
    }

    public void DeactiveThis() {
        this.gameObject.SetActive(false);
        _eventHandler.Clear();
    }
}
