using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class UI_TestLobbyMain : UI_Scene {
    enum Texts {
        version,
        versionRequest,
        loginRequest,
        newAccountRequest,
    }

    public override void Init() {
        base.Init();
        Bind<TextMeshProUGUI>(typeof(Texts));

        //TODO : 버전 정보는 Manager클래스에서 가져올것
        GetText((int)Texts.version).text = "v1.0.0";

        GameObject versionTextGo = GetText((int)Texts.versionRequest).gameObject;
        BindEvent(versionTextGo, OnClickVersionText, Define.UIEvent.Click);
        GameObject loginRequestGo = GetText((int)Texts.loginRequest).gameObject;
        BindEvent(loginRequestGo, OnLoginTestFunc, Define.UIEvent.Click);
    }

    private void OnClickVersionText(PointerEventData data) {
        Debug.Log("버전 텍스트가 클릭되었습니다!");
    }

    private void OnLoginTestFunc(PointerEventData data) {
        Managers.Network.TestCall();
    }

    void Start() {
        Init();
    }
    void Update() {
        
    }
}
