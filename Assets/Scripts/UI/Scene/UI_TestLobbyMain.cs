using UnityEngine;
using UnityEngine.EventSystems;
using System.Threading;
using TMPro;

public class UI_TestLobbyMain : UI_Scene {
    enum Texts {
        version,
        versionRequest,
        loginRequest,
        newAccountRequest,
    }

    private CancellationTokenSource _cts = new CancellationTokenSource();

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

    private void OnLoginTestFunc(PointerEventData data) {
        
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
