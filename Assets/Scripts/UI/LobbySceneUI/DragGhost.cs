using UnityEngine;
using UnityEngine.UI;

public class DragGhost : MonoBehaviour {
    RectTransform _dragGhostRectTrans;
    Image _img;

    public void Init() { 
        Transform dragGhostTransform = transform.Find("GhostImage");
        _dragGhostRectTrans = dragGhostTransform.GetComponent<RectTransform>();
        _img = dragGhostTransform.GetComponent<Image>();
    }

    public void BeginDrag(ISlot slot) {
        _img = slot.GetComponent<Image>();
    }

    public void OnDrag(Vector2 screenPos) {
        // TODO : 마우스 위치로 드래그 고스트 이동
    }

    public void EndDrag() {
        // TODO : 드래그 고스트 숨김
    }
}
