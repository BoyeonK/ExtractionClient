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
                Managers.Sound.PlayUISubmit();
                confirmOnClickEvent?.Invoke();
                DeactiveThisUI();
            });
        }
    }

    // 결말이 확인 하나뿐이라 ESC도 이리로 온다. 콜백만 따로 부르지 않는 이유는 LobbyConfirmOrCancel과 같다
    public void InvokeConfirm() {
        if (_confirmButton != null)
            _confirmButton.onClick.Invoke();
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
