using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class lobbyOnlyConfirm : MonoBehaviour {
    TextMeshProUGUI _bodyText;
    Button _confirmButton;

    public void Init() {
        _bodyText = Util.BindComponent<TextMeshProUGUI>("PopupCard/BodyText", this.gameObject);
        _confirmButton = Util.BindComponent<Button>("PopupCard/ButtonRow/ConfirmButton", this.gameObject);
        DeactiveThisUI();
    }

    public void ActivateThisUI(string bodyText, Action confirmOnClickEvent) {
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
    }

    private void DeactiveThisUI() {
        this.gameObject.SetActive(false);
        _confirmButton.onClick.RemoveAllListeners();
    }

    private void OnDestroy() {
        if (_confirmButton != null) {
            _confirmButton.onClick.RemoveAllListeners();
        }
    }
}
