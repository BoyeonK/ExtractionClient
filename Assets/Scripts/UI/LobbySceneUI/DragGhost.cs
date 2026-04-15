using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.UI;

public class DragGhost : MonoBehaviour {
    RectTransform _canvasRectTrans;     // 기준이 될 캔버스의 크기
    RectTransform _dragGhostRectTrans;  // 움직일 대상
    Image _img;

    public void Init() { 
        _canvasRectTrans = GetComponent<RectTransform>();
        Transform dragGhostTransform = transform.Find("GhostImage");
        _dragGhostRectTrans = dragGhostTransform.GetComponent<RectTransform>();
        _img = dragGhostTransform.GetComponent<Image>();

        Util.Log("DragGhost initialized");

        _img.raycastTarget = false;
        _img.sprite = null;

        _dragGhostRectTrans.gameObject.SetActive(false);
    }

    public void BeginDrag(ISlot slot) {
        //_img = slot.GetComponent<Image>();
        Image slotImg = slot.transform.GetComponent<Image>();
        _img.sprite = slotImg.sprite;

        Color ghostColor = _img.color;
        ghostColor.a = 0.6f;
        _img.color = ghostColor;

        _dragGhostRectTrans.gameObject.SetActive(true);
    }

    public void OnDrag(Vector2 screenPos) {
        Camera uiCamera = null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTrans,
            screenPos,
            uiCamera,
            out Vector2 localPos))
        {
            _dragGhostRectTrans.localPosition = localPos;
        }
    }

    public void EndDrag() {
        _dragGhostRectTrans.gameObject.SetActive(false);
        _img.sprite = null;
    }
}
