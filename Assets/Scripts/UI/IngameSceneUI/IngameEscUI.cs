using UnityEngine;
using UnityEngine.UI;

public class IngameEscUI : MonoBehaviour {
    IngameScene _scene;

    Transform _optionOrExitUI;
    Button _optionButton;
    Button _exitButton;

    Transform _exitConfirmOrCancelUI;
    Button _exitConfirmButton;
    Button _exitCancelButton;

    bool _isOpen = false;
    public bool IsOpen => _isOpen;

    public void Init(IngameScene scene) {
        _scene = scene;

        // 패널 둘도 BindComponent로 잡는다 — transform.Find는 못 찾아도 조용히 null이라
        // 프리팹 계층이 어긋나면 원인 없이 NRE만 남는다
        _optionOrExitUI = Util.BindComponent<Transform>("OptionOrExitUI", this.gameObject);
        _optionButton   = Util.BindComponent<Button>("OptionOrExitUI/OptionButton", this.gameObject);
        _exitButton     = Util.BindComponent<Button>("OptionOrExitUI/ExitButton", this.gameObject);

        _exitConfirmOrCancelUI = Util.BindComponent<Transform>("ExitConfirmOrCancelUI", this.gameObject);
        _exitConfirmButton     = Util.BindComponent<Button>("ExitConfirmOrCancelUI/ButtonRow/ConfirmButton", this.gameObject);
        _exitCancelButton      = Util.BindComponent<Button>("ExitConfirmOrCancelUI/ButtonRow/CancelButton", this.gameObject);

        // 종료 버튼만 무음이다 — 확인 패널이 자기 확인·취소음을 내므로 두 번 울린다
        if (_optionButton != null)
            _optionButton.onClick.AddListener(() => { Managers.Sound.PlayUISubmit(); OpenSetting(); });
        if (_exitButton != null)
            _exitButton.onClick.AddListener(ActiveExitConfirmOrCancelUI);
        if (_exitConfirmButton != null)
            _exitConfirmButton.onClick.AddListener(() => { Managers.Sound.PlayUISubmit(); _scene.QuitFromMatch(); });
        if (_exitCancelButton != null)
            _exitCancelButton.onClick.AddListener(() => { Managers.Sound.PlayUIReturn(); ActiveOptionOrExitUI(); });

        // 씬에는 활성으로 저장하고 여기서 끈다 — GameObject.Find는 비활성을 못 찾는다
        DeactiveThis();
    }

    public void Show() {
        if (_isOpen) return;

        _isOpen = true;
        ActiveOptionOrExitUI();
        _scene.OnUIOpened();
    }

    public void Hide() {
        if (!_isOpen) return;

        _isOpen = false;
        DeactiveThis();
        _scene.OnUIClosed();
    }

    // ESC는 모든 층에서 '취소'다 — 확인 패널에서는 창을 닫지 않고 선택 패널로 돌아간다.
    // 취소 버튼을 Invoke하지 않고 직접 처리하는 것은 리스너에 상태가 없기 때문이다
    // (로비 팝업과 갈리는 지점 — 그쪽은 isActive 해제가 리스너에 달려 있어 반드시 태워야 한다)
    public void CancelByEscape() {
        Managers.Sound.PlayUIReturn();

        if (_exitConfirmOrCancelUI != null && _exitConfirmOrCancelUI.gameObject.activeSelf) {
            ActiveOptionOrExitUI();
            return;
        }
        Hide();
    }

    public void DeactiveThis() {
        gameObject.SetActive(false);
    }

    public void ActiveOptionOrExitUI() {
        gameObject.SetActive(true);
        if (_optionOrExitUI != null) _optionOrExitUI.gameObject.SetActive(true);
        if (_exitConfirmOrCancelUI != null) _exitConfirmOrCancelUI.gameObject.SetActive(false);
    }

    public void ActiveExitConfirmOrCancelUI() {
        gameObject.SetActive(true);
        if (_optionOrExitUI != null) _optionOrExitUI.gameObject.SetActive(false);
        if (_exitConfirmOrCancelUI != null) _exitConfirmOrCancelUI.gameObject.SetActive(true);
    }

    // 설정 창을 연 뒤에 이 창을 닫는다 — 순서를 뒤집으면 _uiOpenCount가 0을 찍어
    // 커서가 한 번 잠겼다 풀린다(IngameInventoryUI의 컨테이너 전환과 같은 이유)
    private void OpenSetting() {
        _scene.ShowSettingUI();
        Hide();
    }
}
