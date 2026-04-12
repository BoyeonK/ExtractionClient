using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_EventHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {
    public Action<PointerEventData> OnBeginDragHandler = null;
    public Action<PointerEventData> OnDragHandler = null;
    public Action<PointerEventData> OnEndDragHandler = null;
    public Action<PointerEventData> OnDropHandler = null;
    public Action<PointerEventData> OnClickHandler = null;
    public Action<PointerEventData> OnPointerEnterHandler = null;
    public Action<PointerEventData> OnPointerExitHandler = null;

    public void OnBeginDrag(PointerEventData eventData) {
        OnBeginDragHandler?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        OnDragHandler?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData) {
        OnEndDragHandler?.Invoke(eventData);
    }

    public void OnDrop(PointerEventData eventData) {
        OnDropHandler?.Invoke(eventData);
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
        OnBeginDragHandler = null;
        OnDragHandler = null;
        OnEndDragHandler = null;
        OnDropHandler = null;
        OnClickHandler = null;
        OnPointerEnterHandler = null;
        OnPointerExitHandler = null;
    }
}
