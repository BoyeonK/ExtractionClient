using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyConfirmOrCancel : MonoBehaviour {
    TextMeshProUGUI _bodyText;
    Button _confirmButton;
    Button _cancelButton;

    public void Init() {
        _bodyText = Util.BindComponent<TextMeshProUGUI>("PopupCard/BodyText", this.gameObject);
        _confirmButton = Util.BindComponent<Button>("PopupCard/ButtonRow/ConfirmButton", this.gameObject);
        _cancelButton = Util.BindComponent<Button>("PopupCard/ButtonRow/CancelButton", this.gameObject);
        DeactiveThisUI();
    }

    public void ActivateThisUI(string bodyText, Action confirmOnClickEvent, Action cancelOnClickEvent) {
        this.gameObject.SetActive(true);

        if (_bodyText != null) {
            _bodyText.text = bodyText;
        }

        if (_confirmButton != null) {
            _confirmButton.onClick.AddListener(() => {
                confirmOnClickEvent?.Invoke();
                DeactiveThisUI();
            });
        }

        if (_cancelButton != null) {
            _cancelButton.onClick.AddListener(() => {
                cancelOnClickEvent?.Invoke();
                DeactiveThisUI();
            });
        }
    }

    private void DeactiveThisUI() {
        this.gameObject.SetActive(false);
        _confirmButton.onClick.RemoveAllListeners();
        _cancelButton.onClick.RemoveAllListeners();
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
