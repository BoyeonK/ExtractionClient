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
                Managers.Sound.PlayUISubmit();
                confirmOnClickEvent?.Invoke();
                DeactiveThisUI();
            });
        }

        if (_cancelButton != null) {
            _cancelButton.onClick.AddListener(() => {
                Managers.Sound.PlayUIReturn();
                cancelOnClickEvent?.Invoke();
                DeactiveThisUI();
            });
        }
    }

    // 콜백만 따로 부르지 않고 버튼의 onClick을 그대로 태운다 — 리스너에 소리와 DeactiveThisUI가
    // 함께 달려 있어 우회하면 팝업이 화면에 남는다
    public void InvokeCancel() {
        if (_cancelButton != null)
            _cancelButton.onClick.Invoke();
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
