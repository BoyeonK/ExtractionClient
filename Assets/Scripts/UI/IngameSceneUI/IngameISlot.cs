using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum SlotOwnerType {
    PlayerInventory,
    Container
}

public class IngameISlot : MonoBehaviour {
    protected InventoryItem _item = null;
    protected int _slotIndex = -1;
    protected IngameScene _scene = null;
    protected SlotOwnerType _ownerType;
    UI_EventHandler _eventHandler;
    protected Image _iconImage;
    protected TextMeshProUGUI _quantity;

    public InventoryItem GetItem() => _item;
    public int SlotIndex => _slotIndex;
    public SlotOwnerType OwnerType => _ownerType;

    public void Init(int index, IngameScene scene, SlotOwnerType ownerType) {
        _slotIndex = index;
        _scene = scene;
        _ownerType = ownerType;
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
        _item = item;
        if (_iconImage != null && _item != null) {
            string path = $"Images/Items/icon_item_{_item.item_id}";
            Sprite iconSprite = Resources.Load<Sprite>(path);

            if (iconSprite != null) {
                _iconImage.sprite = iconSprite;
                
                Color color = _iconImage.color;
                color.a = 1.0f;
                _iconImage.color = color;
            }

            if (_quantity != null) {
                _quantity.text = _item.quantity >= 1 ? _item.quantity.ToString() : "";
            }
        }
    }

    public void ClearSlot() {
        _item = null;
        if (_iconImage != null)
            _iconImage.color = new Color(1f, 1f, 1f, 0f);
        if (_quantity != null)
            _quantity.text = "";
    }

    private void OnBeginDrag(PointerEventData eventData) {
        if (_item == null) return;
        _scene.BeginDrag(this);
    }

    public Image GetIconImage() => _iconImage;

    private void OnDrag(PointerEventData eventData) {
        _scene.UpdateDragPosition(eventData.position);
    }

    // OnEndDrag는 드롭 성공/실패 모두 항상 호출됨 — 여기서 정리
    private void OnEndDrag(PointerEventData eventData) {
        _scene.EndDrag();
    }

    private void OnClick(PointerEventData eventData) {
    }

    public virtual bool CanAcceptItem(InventoryItem item) => true;
    protected virtual bool CanMerge(InventoryItem item) {
        ItemType type = ItemDBHelper.GetType(item.item_id);
        return type != ItemType.Weapon && type != ItemType.Armor;
    }

    private void OnDrop(PointerEventData eventData) {
        IngameISlot source = _scene.DragSource;
        if (source == null || source == this) return;

        IngameISlot target = this;
        InventoryItem sourceItem = source.GetItem();
        if (sourceItem == null) return;

        // 장비 슬롯 → 일반 슬롯: Unequip
        if (source is IngameLSlot sourceLSlot && !(target is IngameLSlot)) {
            _scene.RequestEquipItem(1, sourceLSlot.EquipmentSlotType, target);
            return;
        }

        // 일반 슬롯 → 장비 슬롯: Equip
        if (target is IngameLSlot targetLSlot && !(source is IngameLSlot)) {
            if (!targetLSlot.CanAcceptItem(sourceItem)) return;
            _scene.RequestEquipItem(0, targetLSlot.EquipmentSlotType, source);
            return;
        }

        // 일반 슬롯 → 일반 슬롯
        InventoryItem targetItem = target.GetItem();

        if (targetItem == null) {
            // get: 빈 슬롯으로 이동
            _scene.RequestInteractContainerObject(0, source, target);
        } else if (targetItem.item_id == sourceItem.item_id && CanMerge(sourceItem)) {
            // merge: 같은 아이템 합산
            _scene.RequestInteractContainerObject(2, source, target);
        } else {
            // swap: 서로 교환
            if (!source.CanAcceptItem(targetItem)) return;
            _scene.RequestInteractContainerObject(1, source, target);
        }
    }

    private void OnDestroy() {
        if (_eventHandler != null)
            _eventHandler.Clear();
    }
}
