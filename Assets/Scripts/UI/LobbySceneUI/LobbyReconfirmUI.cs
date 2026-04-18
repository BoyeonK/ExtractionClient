using System;
using UnityEngine;

public class LobbyReconfirmUI : MonoBehaviour {
    bool isActive = false;
    LobbyConfirmOrCancel _confirmOrCancelUI;
    lobbyOnlyConfirm _onlyConfirmUI;

    public void Init() {
        _confirmOrCancelUI = Util.BindComponent<LobbyConfirmOrCancel>("LobbyConfirmOrCancel", this.gameObject);
        _onlyConfirmUI = Util.BindComponent<lobbyOnlyConfirm>("LobbyOnlyConfirm", this.gameObject);

        _confirmOrCancelUI.Init();
        _onlyConfirmUI.Init();
        isActive = false;
    }

    public bool ActiveConfirmOrCancel(string bodyText, Action confirmOnClickEvent, Action cancelOnClickEvent = null) {
        if (isActive == true) return false;
        isActive = true;

        confirmOnClickEvent += DeactiveFlagThis;
        cancelOnClickEvent += DeactiveFlagThis;

        _confirmOrCancelUI.ActivateThisUI(bodyText, confirmOnClickEvent, cancelOnClickEvent);
        return true;
    }

    public bool ActiveOnlyConfirm(string bodyText, Action confirmOnClickEvent) {
        if (isActive == true) return false;
        isActive = true;

        confirmOnClickEvent += DeactiveFlagThis;

        _onlyConfirmUI.ActivateThisUI(bodyText, confirmOnClickEvent);
        return true;
    }

    private void DeactiveFlagThis() {
        isActive = false;
    }
}
