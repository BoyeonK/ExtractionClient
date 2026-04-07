using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_OnlyConfirm : UI_Popup {
    TextMeshProUGUI _bodyText;
    Button _confirmButton;

    public void Init(string bodyText, Action confirmOnClickEvent) {
        base.Init();
        _bodyText = BindComponent<TextMeshProUGUI>("PopupCard/BodyText");
        _confirmButton = BindComponent<Button>("PopupCard/ButtonRow/ConfirmButton");

        if (_bodyText != null) {
            _bodyText.text = bodyText;
        }

        if (_confirmButton != null) {
            _confirmButton.onClick.AddListener(() => {
                confirmOnClickEvent?.Invoke();
                Destroy(gameObject);
            });
        }
    }

    private void OnDestroy() {
        if (_confirmButton != null) {
            _confirmButton.onClick.RemoveAllListeners();
        }
    }
}
