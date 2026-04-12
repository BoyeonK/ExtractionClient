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

        // TODO : 다른종류의 아이템인 경우 교환. 같은 종류의 아이템인 경우 합치기
        InventoryItem sourceItem = source.GetItem();
        InventoryItem targetItem = _item;
    }

    private void OnDestroy() {
        if (_eventHandler != null)
            _eventHandler.Clear();
    }
}
