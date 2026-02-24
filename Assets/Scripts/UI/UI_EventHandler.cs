using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_EventHandler : MonoBehaviour, IDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {
    public Action<PointerEventData> OnDragHandler = null;
    public Action<PointerEventData> OnClickHandler = null;
    public Action<PointerEventData> OnPointerEnterHandler = null;
    public Action<PointerEventData> OnPointerExitHandler = null;

    public void OnDrag(PointerEventData eventData) {
        OnDragHandler?.Invoke(eventData);
    }

    public void OnPointerClick(PointerEventData eventData) {
        OnClickHandler?.Invoke(eventData);
    }

    public void OnPointerEnter(PointerEventData eventData) {
        OnPointerEnterHandler?.Invoke(eventData);
    }

    public void OnPointerExit(PointerEventData eventData) {
        OnPointerExitHandler?.Invoke(eventData);
    }

    private void OnDestroy() {
        Clear();
    }

    public void Clear() {
        OnDragHandler = null;
        OnClickHandler = null;
        OnPointerEnterHandler = null;
        OnPointerExitHandler = null;
    }
}
