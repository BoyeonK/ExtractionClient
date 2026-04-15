using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ISlot : MonoBehaviour {
    InventoryItem _item = null;
    int _slotIndex = -1;
    TestLobbyScene _scene = null;
    UI_EventHandler _eventHandler;
    Image _iconImage;

    public InventoryItem GetItem() => _item;
    public int SlotIndex => _slotIndex;

    public void Init(int index, TestLobbyScene scene) {
        _slotIndex = index;
        _scene = scene;
        _eventHandler = GetComponent<UI_EventHandler>();
        _iconImage = GetComponent<Image>();

        _eventHandler.OnBeginDragHandler = OnBeginDrag;
        _eventHandler.OnDragHandler = OnDrag;
        _eventHandler.OnEndDragHandler = OnEndDrag;
        _eventHandler.OnDropHandler = OnDrop;
    }

    public void SetItem(InventoryItem item) {
        _item = item;
        // TODO: item_id 기반으로 아이콘 스프라이트 설정
        if (_iconImage != null)
            _iconImage.color = Color.white;
    }

    public void ClearSlot() {
        _item = null;
        if (_iconImage != null)
            _iconImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private void OnBeginDrag(PointerEventData eventData) {
        if (_item == null) return;
        _scene.BeginDrag(this);
    }

    private void OnDrag(PointerEventData eventData) {
        _scene.UpdateDragPosition(eventData.position);
    }

    // OnEndDrag는 드롭 성공/실패 모두 항상 호출됨 — 여기서 정리
    private void OnEndDrag(PointerEventData eventData) {
        _scene.EndDrag();
    }

    private void OnDrop(PointerEventData eventData) {
        ISlot source = _scene.DragSource;
        if (source == null || source == this) return;

        UI_Scene sourceUI = source.GetComponentInParent<UI_Scene>();
        UI_Scene targetUI = GetComponentInParent<UI_Scene>();

        InventoryItem sourceItem = source.GetItem();
        InventoryItem targetItem = _item;

        if (targetItem != null && targetItem.item_id == sourceItem.item_id) {
            // 같은 품목: 수량 합산, source 슬롯 비움
            targetItem.quantity += sourceItem.quantity;
            sourceUI.SetItemAtSlot(source.SlotIndex, null);
            targetUI.SetItemAtSlot(SlotIndex, targetItem);
            _scene.SyncSlot(sourceUI, source.SlotIndex, null);
            _scene.SyncSlot(targetUI, SlotIndex, targetItem);
        } else {
            // 다른 품목 or 빈 슬롯: swap
            sourceUI.SetItemAtSlot(source.SlotIndex, targetItem);
            targetUI.SetItemAtSlot(SlotIndex, sourceItem);
            _scene.SyncSlot(sourceUI, source.SlotIndex, targetItem);
            _scene.SyncSlot(targetUI, SlotIndex, sourceItem);
        }
    }

    private void OnDestroy() {
        if (_eventHandler != null)
            _eventHandler.Clear();
    }
}
