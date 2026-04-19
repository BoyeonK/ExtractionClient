using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEditor.Progress;

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

        string path = $"Images/Items/icon_item_{_itemId}";
        Sprite iconSprite = Resources.Load<Sprite>(path);
        if (iconSprite != null) {
            _iconImage.sprite = iconSprite;

            Color color = _iconImage.color;
            color.a = 1.0f;
            _iconImage.color = color;
        }
        _priceTxt.text = _price.ToString();

        ActiveThis();
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
