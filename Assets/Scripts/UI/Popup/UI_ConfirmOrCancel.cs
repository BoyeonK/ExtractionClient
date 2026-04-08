using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ConfirmOrCancel : UI_Popup {
    TextMeshProUGUI _bodyText;
    Button _confirmButton;
    Button _cancelButton;

    public void Init(string bodyText, Action confirmOnClickEvent, Action cancelOnClickEvent) {
        base.Init();
        if (isInit) return;
        _bodyText = BindComponent<TextMeshProUGUI>("PopupCard/BodyText");
        _confirmButton = BindComponent<Button>("PopupCard/ButtonRow/ConfirmButton");
        _cancelButton = BindComponent<Button>("PopupCard/ButtonRow/CancelButton");

        if (_bodyText != null) {
            _bodyText.text = bodyText;
        }

        if (_confirmButton != null) {
            _confirmButton.onClick.AddListener(() => {
                confirmOnClickEvent?.Invoke();
                Destroy(gameObject);
            });
        }

        if (_cancelButton != null) {
            _cancelButton.onClick.AddListener(() => {
                cancelOnClickEvent?.Invoke();
                Destroy(gameObject);
            });
        }
        base.OnInitComplete();
    }

    private void OnDestroy() {
        if (_confirmButton != null) {
            _confirmButton.onClick.RemoveAllListeners();
        }
        if ( _cancelButton != null) {
            _cancelButton.onClick.RemoveAllListeners();
        }
    }
}
