using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ISlot : MonoBehaviour {
    protected InventoryItem _item = null;
    protected int _slotIndex = -1;
    protected LobbyScene _scene = null;
    UI_EventHandler _eventHandler;
    protected Image _iconImage;
    protected TextMeshProUGUI _quantity;

    public InventoryItem GetItem() => _item;
    public int SlotIndex => _slotIndex;

    public void Init(int index, LobbyScene scene) {
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

    // 표시·숨김은 Image.enabled로 한다 — 색을 건드리면 프리팹의 Fill 색이 아이콘에 곱해져
    // 슬롯 이력에 따라 같은 아이콘의 밝기가 달라진다(LootContainerSlot·IngameWeaponUI와 같은 방식)
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

    // 버튼을 그대로 넘긴다 — IPointerClickHandler는 우클릭에도 발화하므로, 씬이 버튼을 보지 않으면
    // Shift+우클릭(판매)이 Shift+좌클릭(분할)과 같은 조작으로 처리된다
    private void OnClick(PointerEventData eventData) {
        _scene.OnSlotClick(this, eventData.button);
    }

    public virtual bool CanAcceptItem(InventoryItem item) => true;
    protected virtual bool CanMerge(InventoryItem item) {
        ItemType type = ItemDBHelper.GetType(item.item_id);
        return type != ItemType.Weapon && type != ItemType.Armor;
    }

    private void OnDrop(PointerEventData eventData) {
        ISlot source = _scene.DragSource;
        if (source == null || source == this) return;

        if (!CanAcceptItem(source.GetItem())) return;

        UI_ItemRepo sourceUI = source.GetComponentInParent<UI_ItemRepo>();
        UI_ItemRepo targetUI = GetComponentInParent<UI_ItemRepo>();

        InventoryItem sourceItem = source.GetItem();
        InventoryItem targetItem = _item;

        if (targetItem != null && targetItem.item_id == sourceItem.item_id && CanMerge(sourceItem)) {
            // 같은 품목: 수량 합산, source 슬롯 비움
            targetItem.quantity += sourceItem.quantity;
            sourceUI.SetItemAtSlot(source.SlotIndex, null);
            targetUI.SetItemAtSlot(SlotIndex, targetItem);
        } else {
            // 다른 품목 or 빈 슬롯: swap
            // source 슬롯도 target 아이템을 수용할 수 있는지 확인
            if (targetItem != null && !source.CanAcceptItem(targetItem)) return;

            sourceUI.SetItemAtSlot(source.SlotIndex, targetItem);
            targetUI.SetItemAtSlot(SlotIndex, sourceItem);
        }

        // 로비 인벤토리는 판정이 완전히 로컬이라 여기가 곧 확정 시점이다 — 인게임(IngameISlot)은
        // 서버가 판정하므로 요청 지점이 아니라 응답 핸들러에서 낸다. 두 곳의 자리가 다르다
        Managers.Sound.PlayInventoryChange();
    }

    private void OnDestroy() {
        if (_eventHandler != null)
            _eventHandler.Clear();
    }
}
