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

    // 표시·숨김은 Image.enabled로 한다 — 색을 건드리면 프리팹의 Fill 색이 아이콘에 곱해져
    // 슬롯 이력에 따라 같은 아이콘의 밝기가 달라진다(ISlot과 같은 규칙)
    public void SetItem(InventoryItem item) {
        _item = item;
        if (_iconImage != null && _item != null) {
            string path = $"Images/Items/icon_item_{_item.item_id}";
            Sprite iconSprite = Resources.Load<Sprite>(path);

            // 못 찾았을 때도 대입한다 — 안 그러면 직전 아이템의 아이콘이 새 아이템의 수량과 함께 남는다.
            // 스프라이트 없는 Image는 흰 사각형으로 그려지므로 그때는 꺼서 누락을 드러낸다
            _iconImage.sprite = iconSprite;
            _iconImage.enabled = iconSprite != null;

            if (_quantity != null) {
                _quantity.text = _item.quantity >= 1 ? _item.quantity.ToString() : "";
            }
        }
    }

    public void ClearSlot() {
        _item = null;
        if (_iconImage != null) {
            _iconImage.sprite = null;
            _iconImage.enabled = false;
        }
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
