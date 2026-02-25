using UnityEngine;
using UnityEngine.EventSystems;
using System.Threading;
using TMPro;

public class UI_TestLobbyMain : UI_Scene {
    TestLobbyScene _scene;

    enum Texts {
        version,
        versionRequest,
        loginRequest,
        newAccountRequest,
        option,
        guestLoginRequest,
        exit,
    }

    private CancellationTokenSource _cts = new CancellationTokenSource();

    public override void Init() {
        base.Init();
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetText((int)Texts.version).text = "v." + Managers.Network.version;

        GameObject versionTextGo = GetText((int)Texts.versionRequest).gameObject;
        BindEvent(versionTextGo, OnClickVersionText, Define.UIEvent.Click);
        GameObject loginRequestGo = GetText((int)Texts.loginRequest).gameObject;
        BindEvent(loginRequestGo, OnLoginTestFunc, Define.UIEvent.Click);
        GameObject newAccountRequestGo = GetText((int)Texts.newAccountRequest).gameObject;
        BindEvent(newAccountRequestGo, OnCreateAccountTestFunc, Define.UIEvent.Click);
        GameObject optionGo = GetText((int)Texts.option).gameObject;
        BindEvent(optionGo, OnClickOptionText, Define.UIEvent.Click);
        GameObject guestLoginRequestGo = GetText((int)Texts.guestLoginRequest).gameObject;
        BindEvent(guestLoginRequestGo, OnGuestTestFunc, Define.UIEvent.Click);
        GameObject exitGo = GetText((int)Texts.exit).gameObject;
        BindEvent(exitGo, OnClickExitText, Define.UIEvent.Click);

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;
    }

    private async void OnClickVersionText(PointerEventData data) {
        GetText((int)Texts.versionRequest).raycastTarget = false;
        bool isSuccess = await Managers.Network.TestCall(_cts.Token);
        //동작 원리 : await를 만난 이후의 코드는, 응답을 받기 전까지 실행되지 않음.
        //이것이 블로킹된다는 것을 의미하지는 않는다.
        //다만, await 이후의 코드는 언제 실행될지 모름.
        //즉, 이후의 코드의 실행 시점에서 이 객체가 파괴되어있거나, 씬이 바뀌거나, 다른 UI로 넘어갈 수도 있음.
        if (this == null) return;
        GetText((int)Texts.versionRequest).raycastTarget = true;
    }

    private async void OnLoginTestFunc(PointerEventData data) {
        GetText((int)Texts.loginRequest).raycastTarget = false;
        // 당연히 내 비밀번호는 qwe123따위가 아니다.
        bool isSuccess = await Managers.Network.TestLoginCall("tetepiti149", "qwe123", _cts.Token);
        if (this == null) return;
        GetText((int)Texts.loginRequest).raycastTarget = true;
    }

    private async void OnCreateAccountTestFunc(PointerEventData data) {
        GetText((int)Texts.newAccountRequest).raycastTarget = false;
        // 당연히 내 비밀번호는 qwe123따위가 아니다.
        bool isSuccess = await Managers.Network.TestCreateAccountCall("tetepiti149", "qwe123", _cts.Token);
        if (this == null) return;
        GetText((int)Texts.newAccountRequest).raycastTarget = true;
    }

    private async void OnGuestTestFunc(PointerEventData data) {
        GetText((int)Texts.versionRequest).raycastTarget = false;
        bool isSuccess = await Managers.Network.GuestLoginCall(_cts.Token);
        if (this == null) return;
        GetText((int)Texts.versionRequest).raycastTarget = true;
    }

    private async void OnClickExitText(PointerEventData data) {
        GetText((int)Texts.exit).raycastTarget = false;
        bool isSuccess = await Managers.Network.TestLogoutCall(_cts.Token);
        if (this == null) return;
        GetText((int)Texts.exit).raycastTarget = true;
    }

    private void OnClickOptionText(PointerEventData data) {
        _scene.ShowSettingUI();
    }

    private void OnDestroy() {
        _cts.Cancel();
        _cts.Dispose();
    }

    void Start() {
        Init();
    }
    void Update() {
        
    }
}
