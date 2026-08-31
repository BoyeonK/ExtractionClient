using System;
using UnityEngine;

public class LobbyReconfirmUI : MonoBehaviour {
    LobbyScene _scene;

    bool isActive = false;
    LobbyConfirmOrCancel _confirmOrCancelUI;
    lobbyOnlyConfirm _onlyConfirmUI;

    public void Init() {
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is LobbyScene lobbyScene)
            _scene = lobbyScene;

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

    public bool ActiveOnlyConfirm(string bodyText, Action confirmOnClickEvent = null) {
        if (isActive == true) return false;
        isActive = true;

        confirmOnClickEvent += DeactiveFlagThis;

        _onlyConfirmUI.ActivateThisUI(bodyText, confirmOnClickEvent);
        return true;
    }

    public bool IsActive => isActive;

    // ESC는 확인/취소 팝업에서 취소로, 확인만 있는 팝업에서 확인으로 간다.
    // 어느 쪽이든 버튼을 태우므로 DeactiveFlagThis가 함께 돌아 isActive가 정상 해제된다 —
    // 콜백을 직접 부르면 플래그가 true로 굳어 그 뒤 모든 팝업이 뜨지 않는다
    public void DismissByEscape() {
        if (isActive == false) return;

        if (_confirmOrCancelUI.gameObject.activeSelf)
            _confirmOrCancelUI.InvokeCancel();
        else if (_onlyConfirmUI.gameObject.activeSelf)
            _onlyConfirmUI.InvokeConfirm();
    }

    private void DeactiveFlagThis() {
        isActive = false;
    }
}
