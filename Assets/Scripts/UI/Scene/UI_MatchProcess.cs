using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MatchProcess : UI_Scene {
    TestLobbyScene _scene;

    TextMeshProUGUI _timerTxt;
    Button _cancelBtn;

    float _elapsedTime;
    bool _isMatching;

    public override void Init() {
        base.Init();
        if (isInit) return;
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        _timerTxt = Util.BindComponent<TextMeshProUGUI>("ProgressWindow/Timer", this.gameObject);
        _cancelBtn = Util.BindComponent<Button>("ProgressWindow/ConfirmButton", this.gameObject);

        _cancelBtn.onClick.AddListener(OnClickCancelBtn);

        base.OnInitComplete();
    }

    void Start() { 
        Init();
    }

    private void OnEnable() {
        _elapsedTime = 0f;
        _isMatching = true;
        if (_timerTxt != null) _timerTxt.text = "00:00";
        if (_cancelBtn != null) _cancelBtn.interactable = true;
    }

    private void Update() {
        if (!_isMatching) return;
        _elapsedTime += Time.deltaTime;
        int m = (int)(_elapsedTime / 60f);
        int s = (int)(_elapsedTime % 60f);
        _timerTxt.text = $"{m:D2}:{s:D2}";
    }

    private void OnClickCancelBtn() {
        _cancelBtn.interactable = false;
        _scene.OnMatchCancelBtnClick();
    }

    public void StopMatching() {
        _isMatching = false;
    }

    public void RestoreCancelButton() {
        if (_cancelBtn != null) _cancelBtn.interactable = true;
    }

    private void OnDestroy() {
        if (_cancelBtn != null) _cancelBtn.onClick.RemoveAllListeners();
    }
}
