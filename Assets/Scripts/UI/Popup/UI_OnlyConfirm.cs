using TMPro;
using UnityEngine;

public class UI_OnlyConfirm : UI_Popup {
    TextMeshProUGUI _bodyText;

    public override void Init() {
        base.Init();
        _bodyText = BindComponent<TextMeshProUGUI>("PopupCard/BodyText");
    }
}
