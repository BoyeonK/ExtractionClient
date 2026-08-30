using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbySettingUI : MonoBehaviour {
    LobbyScene _scene;

    UI_EventHandler _generalTab;
    UI_EventHandler _graphicTab;
    UI_EventHandler _audioTab;

    Image _generalTabColor;
    Image _graphicTabColor;
    Image _audioTabColor;
    Color _selectedTabColor = new Color(22/255f, 22/255f, 33/255f, 1f);
    Color _unselectedTabColor = new Color(33/255f, 33/255f, 41/255f, 1f);
    Color _hoverTabColor = new Color(28/255f, 28/255f, 37/255f, 1f);

    GameObject _generalActiveIndicator;
    GameObject _graphicActiveIndicator;
    GameObject _audioActiveIndicator;

    TextMeshProUGUI _generalText;
    TextMeshProUGUI _graphicText;
    TextMeshProUGUI _audioText;

    GameObject _generalPanel;
    GameObject _graphicPanel;
    GameObject _audioPanel;

    Button _applyButton;
    Button _cancelButton;

    Slider _sensitivitySlider;
    TextMeshProUGUI _sensitivityValue;

    // Graphic tab
    Button _windowModeButton;
    TextMeshProUGUI _windowModeText;
    Button _resolutionLeftButton;
    Button _resolutionRightButton;
    TextMeshProUGUI _resolutionValue;
    Button _frameRateLeftButton;
    Button _frameRateRightButton;
    TextMeshProUGUI _frameRateValue;
    Toggle _vSyncToggle;
    Slider _fovSlider;
    TextMeshProUGUI _fovValue;

    // Audio tab
    Slider _masterVolumeSlider;
    TextMeshProUGUI _masterVolumeValue;
    Slider _effectVolumeSlider;
    TextMeshProUGUI _effectVolumeValue;
    Slider _bgmVolumeSlider;
    TextMeshProUGUI _bgmVolumeValue;

    bool _pendingIsWindow;
    Define.Resolution _pendingResolution;
    Define.FrameRate _pendingFrameRate;
    bool _pendingVSync;
    int _pendingFov;

    enum SelectedTab {
        General,
        Graphic,
        Audio
    }
    SelectedTab _selectedTab = SelectedTab.General;

    public void Init() {
        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is LobbyScene lobbyScene)
            _scene = lobbyScene;

        _generalTab = Util.BindComponent<UI_EventHandler>("LobbySettingWindow/TabBar/Tab_General", this.gameObject);
        _graphicTab = Util.BindComponent<UI_EventHandler>("LobbySettingWindow/TabBar/Tab_Graphic", this.gameObject);
        _audioTab = Util.BindComponent<UI_EventHandler>("LobbySettingWindow/TabBar/Tab_Audio", this.gameObject);

        _generalText = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/TabBar/Tab_General/TabText", this.gameObject);
        _graphicText = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/TabBar/Tab_Graphic/TabText", this.gameObject);
        _audioText = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/TabBar/Tab_Audio/TabText", this.gameObject);

        Transform trGeneral = transform.Find("LobbySettingWindow/TabBar/Tab_General/ActiveIndicator");
        Transform trGraphic = transform.Find("LobbySettingWindow/TabBar/Tab_Graphic/ActiveIndicator");
        Transform trAudio = transform.Find("LobbySettingWindow/TabBar/Tab_Audio/ActiveIndicator");
        if (trGeneral != null) _generalActiveIndicator = trGeneral.gameObject;
        if (trGraphic != null) _graphicActiveIndicator = trGraphic.gameObject;
        if (trAudio != null) _audioActiveIndicator = trAudio.gameObject;

        _generalTabColor = Util.BindComponent<Image>("LobbySettingWindow/TabBar/Tab_General", this.gameObject);
        _graphicTabColor = Util.BindComponent<Image>("LobbySettingWindow/TabBar/Tab_Graphic", this.gameObject);
        _audioTabColor = Util.BindComponent<Image>("LobbySettingWindow/TabBar/Tab_Audio", this.gameObject);
        Transform trGeneralPanel = transform.Find("LobbySettingWindow/ContentArea/GeneralContent");
        Transform trGraphicPanel = transform.Find("LobbySettingWindow/ContentArea/GraphicContent");
        Transform trAudioPanel = transform.Find("LobbySettingWindow/ContentArea/AudioContent");
        if (trGeneralPanel != null) _generalPanel = trGeneralPanel.gameObject;
        if (trGraphicPanel != null) _graphicPanel = trGraphicPanel.gameObject;
        if (trAudioPanel != null) _audioPanel = trAudioPanel.gameObject;
        _applyButton = Util.BindComponent<Button>("LobbySettingWindow/Footer/ButtonContainer/ApplyButton", this.gameObject);
        _cancelButton = Util.BindComponent<Button>("LobbySettingWindow/Footer/ButtonContainer/CancelButton", this.gameObject);

        // 소리는 ChangeTab 안이 아니라 여기서 낸다 — 그 함수는 Init에서도 불려 창이 뜨기도 전에 울린다
        _generalTab.OnClickHandler += _ => { Managers.Sound.PlayUISubmit(); ChangeTab(SelectedTab.General); };
        _graphicTab.OnClickHandler += _ => { Managers.Sound.PlayUISubmit(); ChangeTab(SelectedTab.Graphic); };
        _audioTab.OnClickHandler   += _ => { Managers.Sound.PlayUISubmit(); ChangeTab(SelectedTab.Audio); };

        _generalTab.OnPointerEnterHandler += _ => { if (_selectedTab != SelectedTab.General) _generalTabColor.color = _hoverTabColor; };
        _generalTab.OnPointerExitHandler  += _ => { if (_selectedTab != SelectedTab.General) _generalTabColor.color = _unselectedTabColor; };
        _graphicTab.OnPointerEnterHandler += _ => { if (_selectedTab != SelectedTab.Graphic) _graphicTabColor.color = _hoverTabColor; };
        _graphicTab.OnPointerExitHandler  += _ => { if (_selectedTab != SelectedTab.Graphic) _graphicTabColor.color = _unselectedTabColor; };
        _audioTab.OnPointerEnterHandler   += _ => { if (_selectedTab != SelectedTab.Audio)   _audioTabColor.color   = _hoverTabColor; };
        _audioTab.OnPointerExitHandler    += _ => { if (_selectedTab != SelectedTab.Audio)   _audioTabColor.color   = _unselectedTabColor; };

        _sensitivitySlider = Util.BindComponent<Slider>("LobbySettingWindow/ContentArea/GeneralContent/MouseSensitivityRow/SensitivitySlider", this.gameObject);
        _sensitivityValue = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/GeneralContent/MouseSensitivityRow/SensitivityValue", this.gameObject);
        _sensitivitySlider.value = Managers.Setting.GetMouseSensitivity();
        _sensitivityValue.text = _sensitivitySlider.value.ToString("F1");
        _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);

        // Graphic tab bindings
        _windowModeButton = Util.BindComponent<Button>("LobbySettingWindow/ContentArea/GraphicContent/WindowModeRow/WindowModeButton", this.gameObject);
        _windowModeText = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/GraphicContent/WindowModeRow/WindowModeButton/WindowModeText", this.gameObject);
        _resolutionLeftButton = Util.BindComponent<Button>("LobbySettingWindow/ContentArea/GraphicContent/ResolutionRow/ResolutionControl/ResolutionLeftButton", this.gameObject);
        _resolutionRightButton = Util.BindComponent<Button>("LobbySettingWindow/ContentArea/GraphicContent/ResolutionRow/ResolutionControl/ResolutionRightButton", this.gameObject);
        _resolutionValue = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/GraphicContent/ResolutionRow/ResolutionControl/ResolutionValue", this.gameObject);
        _frameRateLeftButton = Util.BindComponent<Button>("LobbySettingWindow/ContentArea/GraphicContent/FrameRateRow/FrameRateControl/FrameRateLeftButton", this.gameObject);
        _frameRateRightButton = Util.BindComponent<Button>("LobbySettingWindow/ContentArea/GraphicContent/FrameRateRow/FrameRateControl/FrameRateRightButton", this.gameObject);
        _frameRateValue = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/GraphicContent/FrameRateRow/FrameRateControl/FrameRateValue", this.gameObject);
        _vSyncToggle = Util.BindComponent<Toggle>("LobbySettingWindow/ContentArea/GraphicContent/VSyncRow/VSyncToggle", this.gameObject);
        _fovSlider = Util.BindComponent<Slider>("LobbySettingWindow/ContentArea/GraphicContent/FovRow/FovSlider", this.gameObject);
        _fovValue = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/GraphicContent/FovRow/FovValue", this.gameObject);

        _pendingIsWindow = Managers.Setting.GetIsWindow();
        _pendingResolution = Managers.Setting.GetResolution();
        _pendingFrameRate = Managers.Setting.GetFrameRate();
        _pendingVSync = Managers.Setting.GetIsVSync();
        _pendingFov = Managers.Setting.GetFov();

        RefreshWindowModeText();
        RefreshResolutionText();
        RefreshFrameRateText();
        // SetIsOnWithoutNotify가 아니면 onValueChanged를 타고 되돌리는 값이 다시 '변경'으로 들어온다
        _vSyncToggle.SetIsOnWithoutNotify(_pendingVSync);
        _fovSlider.value = _pendingFov;
        _fovValue.text = _pendingFov.ToString();

        _windowModeButton.onClick.AddListener(OnClickWindowMode);
        _resolutionLeftButton.onClick.AddListener(() => OnClickResolution(-1));
        _resolutionRightButton.onClick.AddListener(() => OnClickResolution(1));
        _frameRateLeftButton.onClick.AddListener(() => OnClickFrameRate(-1));
        _frameRateRightButton.onClick.AddListener(() => OnClickFrameRate(1));
        _vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        _fovSlider.onValueChanged.AddListener(OnFovChanged);

        // Audio tab bindings
        _masterVolumeSlider = Util.BindComponent<Slider>("LobbySettingWindow/ContentArea/AudioContent/MasterVolumeRow/MasterVolumeSlider", this.gameObject);
        _masterVolumeValue = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/AudioContent/MasterVolumeRow/MasterVolumeValue", this.gameObject);
        _effectVolumeSlider = Util.BindComponent<Slider>("LobbySettingWindow/ContentArea/AudioContent/EffectVolumeRow/EffectVolumeSlider", this.gameObject);
        _effectVolumeValue = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/AudioContent/EffectVolumeRow/EffectVolumeValue", this.gameObject);
        _bgmVolumeSlider = Util.BindComponent<Slider>("LobbySettingWindow/ContentArea/AudioContent/BgmVolumeRow/BgmVolumeSlider", this.gameObject);
        _bgmVolumeValue = Util.BindComponent<TextMeshProUGUI>("LobbySettingWindow/ContentArea/AudioContent/BgmVolumeRow/BgmVolumeValue", this.gameObject);

        _masterVolumeSlider.value = Managers.Setting.GetMasterVolume();
        _masterVolumeValue.text = ((int)_masterVolumeSlider.value).ToString();
        _effectVolumeSlider.value = Managers.Setting.GetEffectVolume();
        _effectVolumeValue.text = ((int)_effectVolumeSlider.value).ToString();
        _bgmVolumeSlider.value = Managers.Setting.GetBgmVolume();
        _bgmVolumeValue.text = ((int)_bgmVolumeSlider.value).ToString();

        _masterVolumeSlider.onValueChanged.AddListener(v => _masterVolumeValue.text = ((int)v).ToString());
        _effectVolumeSlider.onValueChanged.AddListener(v => _effectVolumeValue.text = ((int)v).ToString());
        _bgmVolumeSlider.onValueChanged.AddListener(v => _bgmVolumeValue.text = ((int)v).ToString());

        _applyButton.onClick.AddListener(OnClickApply);
        _cancelButton.onClick.AddListener(OnClickCancel);

        ChangeTab(_selectedTab);
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    // 재확인 팝업으로 가는 갈래에서는 소리를 내지 않는다 — 팝업이 자기 확인·취소음을 내므로 두 번 울린다.
    // 변경이 없어 그냥 닫히는 갈래만 여기서 낸다
    private void OnClickApply() {
        if (!HasChanges()) {
            Managers.Sound.PlayUISubmit();
            Hide();
            return;
        }
        _scene.ActiveReconfirmConfirmOrCancel("설정을 적용하시겠습니까?", OnApplyConfirmed);
    }

    private void OnClickCancel() {
        if (!HasChanges()) {
            Managers.Sound.PlayUIReturn();
            Hide();
            return;
        }
        _scene.ActiveReconfirmConfirmOrCancel("변경사항을 취소하시겠습니까?", Hide);
    }

    private void OnApplyConfirmed() {
        ApplySetting();
        Hide();
    }

    private void OnSensitivityChanged(float value) {
        float snapped = Mathf.Round(value * 10f) / 10f;
        if (!Mathf.Approximately(_sensitivitySlider.value, snapped))
            _sensitivitySlider.SetValueWithoutNotify(snapped);
        _sensitivityValue.text = snapped.ToString("F1");
    }

    private void OnClickWindowMode() {
        Managers.Sound.PlayUISubmit();
        _pendingIsWindow = !_pendingIsWindow;
        RefreshWindowModeText();
    }

    private void OnClickResolution(int dir) {
        Managers.Sound.PlayUISubmit();
        int cur = (int)_pendingResolution + dir;
        if (cur < 0) cur = (int)Define.Resolution.MaxCount - 1;
        else if (cur >= (int)Define.Resolution.MaxCount) cur = 0;
        _pendingResolution = (Define.Resolution)cur;
        RefreshResolutionText();
    }

    private void OnClickFrameRate(int dir) {
        Managers.Sound.PlayUISubmit();
        int cur = (int)_pendingFrameRate + dir;
        if (cur < 0) cur = (int)Define.FrameRate.MaxCount - 1;
        else if (cur >= (int)Define.FrameRate.MaxCount) cur = 0;
        _pendingFrameRate = (Define.FrameRate)cur;
        RefreshFrameRateText();
    }

    private void OnVSyncChanged(bool value) {
        _pendingVSync = value;
    }

    private void OnFovChanged(float value) {
        int fov = Mathf.RoundToInt(value);
        _pendingFov = fov;
        _fovValue.text = fov.ToString();
    }

    private void RefreshWindowModeText() {
        _windowModeText.text = _pendingIsWindow ? "창모드" : "전체화면";
    }

    private void RefreshResolutionText() {
        var res = Define.ResolutionValues[_pendingResolution];
        _resolutionValue.text = $"{res.w}x{res.h}";
    }

    private void RefreshFrameRateText() {
        _frameRateValue.text = Define.FrameRateValues[_pendingFrameRate].ToString();
    }

    private bool HasChanges() {
        if (!Mathf.Approximately(_sensitivitySlider.value, Managers.Setting.GetMouseSensitivity()))
            return true;
        if (_pendingIsWindow != Managers.Setting.GetIsWindow())
            return true;
        if (_pendingResolution != Managers.Setting.GetResolution())
            return true;
        if (_pendingFrameRate != Managers.Setting.GetFrameRate())
            return true;
        if (_pendingVSync != Managers.Setting.GetIsVSync())
            return true;
        if (_pendingFov != Managers.Setting.GetFov())
            return true;
        if ((int)_masterVolumeSlider.value != Managers.Setting.GetMasterVolume())
            return true;
        if ((int)_effectVolumeSlider.value != Managers.Setting.GetEffectVolume())
            return true;
        if ((int)_bgmVolumeSlider.value != Managers.Setting.GetBgmVolume())
            return true;
        return false;
    }

    private void ApplySetting() {
        Managers.Setting.SetMouseSensitivity(_sensitivitySlider.value);
        Managers.Setting.SetIsWindow(_pendingIsWindow);
        Managers.Setting.SetResolution(_pendingResolution);
        Managers.Setting.ApplyResolution();
        Managers.Setting.SetFrameRate(_pendingFrameRate);
        Managers.Setting.SetIsVSync(_pendingVSync);
        Managers.Setting.ApplyFrameRateAndVSync();
        Managers.Setting.SetFov(_pendingFov);
        Managers.Setting.SetMasterVolume((int)_masterVolumeSlider.value);
        Managers.Setting.SetVolume((int)_effectVolumeSlider.value, Define.Sound.Effect);
        Managers.Setting.SetVolume((int)_bgmVolumeSlider.value, Define.Sound.Bgm);

        // 여기가 이 창의 확정 지점이다. setter는 메모리에만 쓰므로 여기서 디스크로 내린다 —
        // 종료 시 backstop만 두면 비정상 종료에서 방금 확정한 설정이 통째로 날아간다
        Managers.Setting.Save();
    }

    private void ChangeTab(SelectedTab tab) {
        _selectedTab = tab;
        switch(tab) {
            case SelectedTab.General:
                _generalTabColor.color = _selectedTabColor;
                _graphicTabColor.color = _unselectedTabColor;
                _audioTabColor.color = _unselectedTabColor;

                _generalActiveIndicator.SetActive(true);
                _graphicActiveIndicator.SetActive(false);
                _audioActiveIndicator.SetActive(false);

                _generalPanel.SetActive(true);
                _graphicPanel.SetActive(false);
                _audioPanel.SetActive(false);
                break;
            case SelectedTab.Graphic:
                _generalTabColor.color = _unselectedTabColor;
                _graphicTabColor.color = _selectedTabColor;
                _audioTabColor.color = _unselectedTabColor;

                _generalActiveIndicator.SetActive(false);
                _graphicActiveIndicator.SetActive(true);
                _audioActiveIndicator.SetActive(false);

                _generalPanel.SetActive(false);
                _graphicPanel.SetActive(true);
                _audioPanel.SetActive(false);
                break;
            case SelectedTab.Audio:
                _generalTabColor.color = _unselectedTabColor;
                _graphicTabColor.color = _unselectedTabColor;
                _audioTabColor.color = _selectedTabColor;

                _generalActiveIndicator.SetActive(false);
                _graphicActiveIndicator.SetActive(false);
                _audioActiveIndicator.SetActive(true);

                _generalPanel.SetActive(false);
                _graphicPanel.SetActive(false);
                _audioPanel.SetActive(true);
                break;
        }

    }
}