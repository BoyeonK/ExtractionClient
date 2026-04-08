using UnityEngine;

public class UI_Popup : UI_Base {
    public bool is_forcusable = true;

    public virtual void Init() {

    }

    public virtual void ClosePopupUI() {

    }

    public virtual void OnInitComplete() {
        isInit = true;
    }
}