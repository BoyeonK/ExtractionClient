using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IngameISlot : MonoBehaviour {
    protected InventoryItem _item = null;
    protected int _slotIndex = -1;
    protected IngameScene _scene = null;
    UI_EventHandler _eventHandler;
    protected Image _iconImage;
    protected TextMeshProUGUI _quantity;

    public InventoryItem GetItem() => _item;
    public int SlotIndex => _slotIndex;

    public void Init(int index, IngameScene scene) {
        _slotIndex = index;
        _scene = scene;
        _eventHandler = GetComponent<UI_EventHandler>();
        
        Transform fillTransform = transform.Find("Fill");
        if (fillTransform != null) {
            _iconImage = fillTransform.GetComponent<Image>();
        }

        Transform textTransform = transform.Find("Quantity");
        if (textTransform != null) {
            _quantity = textTransform.GetComponent<TextMeshProUGUI>();
        }

        _eventHandler.OnBeginDragHandler = OnBeginDrag;
        _eventHandler.OnDragHandler = OnDrag;
        _eventHandler.OnEndDragHandler = OnEndDrag;
        _eventHandler.OnDropHandler = OnDrop;
        _eventHandler.OnClickHandler = OnClick;
    }

    public void SetItem(InventoryItem item) {

    }

    public void ClearSlot() {
        _item = null;
        if (_iconImage != null)
            _iconImage.color = new Color(1f, 1f, 1f, 0f);
        if (_quantity != null)
            _quantity.text = "";
    }

    private void OnBeginDrag(PointerEventData eventData) {

    }

    public Image GetIconImage() => _iconImage;

    private void OnDrag(PointerEventData eventData) {

    }

    // OnEndDrag는 드롭 성공/실패 모두 항상 호출됨 — 여기서 정리
    private void OnEndDrag(PointerEventData eventData) {

    }

    private void OnClick(PointerEventData eventData) {

    }

    public virtual bool CanAcceptItem(InventoryItem item) => true;
    protected virtual bool CanMerge(InventoryItem item) {
        ItemType type = ItemDBHelper.GetType(item.item_id);
        return type != ItemType.Weapon && type != ItemType.Armor;
    }

    private void OnDrop(PointerEventData eventData) {

    }

    private void OnDestroy() {
        if (_eventHandler != null)
            _eventHandler.Clear();
    }
}
