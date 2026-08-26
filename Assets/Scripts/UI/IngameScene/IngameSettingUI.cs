using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인게임 설정 창 (ESC로 연다).
//
// LobbySettingUI와 일부러 갈라 놨다 — 그쪽은 창모드·해상도·프레임·FOV까지 다루는데
// 매치 중에 바꾸면 곤란한 값들이라 여기엔 담지 않는다. 담는 것은 마우스 감도와 볼륨 3종뿐이다.
//
// 로비와 갈리는 지점이 하나 더 있다: 여기는 슬라이더를 움직이는 즉시 Managers.Setting에
// 반영하고, 취소하면 Show() 시점 스냅샷으로 되돌린다. 볼륨은 귀로 확인해야 맞출 수 있어서다.
// 그래서 '적용'은 확정이 아니라 '이대로 닫기'이고, 로비처럼 재확인 팝업을 띄우지 않는다.
public class IngameSettingUI : MonoBehaviour {
    IngameScene _scene;

    UI_EventHandler _generalTab, _audioTab;
    Image _generalTabColor, _audioTabColor;
    GameObject _generalActiveIndicator, _audioActiveIndicator;
    GameObject _generalPanel, _audioPanel;

    static readonly Color SELECTED_TAB_COLOR   = new Color(22 / 255f, 22 / 255f, 33 / 255f, 1f);
    static readonly Color UNSELECTED_TAB_COLOR = new Color(33 / 255f, 33 / 255f, 41 / 255f, 1f);
    static readonly Color HOVER_TAB_COLOR      = new Color(28 / 255f, 28 / 255f, 37 / 255f, 1f);

    Button _applyButton, _cancelButton;

    Slider _sensitivitySlider;   TextMeshProUGUI _sensitivityValue;
    Slider _masterVolumeSlider;  TextMeshProUGUI _masterVolumeValue;
    Slider _effectVolumeSlider;  TextMeshProUGUI _effectVolumeValue;
    Slider _bgmVolumeSlider;     TextMeshProUGUI _bgmVolumeValue;

    float _snapshotSensitivity;
    int _snapshotMasterVolume, _snapshotEffectVolume, _snapshotBgmVolume;

    bool _isOpen = false;
    public bool IsOpen => _isOpen;

    enum SelectedTab { General, Audio }
    SelectedTab _selectedTab = SelectedTab.General;

    public void Init(IngameScene scene) {
        _scene = scene;

        _generalTab = Util.BindComponent<UI_EventHandler>("IngameSettingWindow/TabBar/Tab_General", this.gameObject);
        _audioTab   = Util.BindComponent<UI_EventHandler>("IngameSettingWindow/TabBar/Tab_Audio", this.gameObject);

        _generalTabColor = Util.BindComponent<Image>("IngameSettingWindow/TabBar/Tab_General", this.gameObject);
        _audioTabColor   = Util.BindComponent<Image>("IngameSettingWindow/TabBar/Tab_Audio", this.gameObject);

        Transform trGeneralIndicator = transform.Find("IngameSettingWindow/TabBar/Tab_General/ActiveIndicator");
        Transform trAudioIndicator   = transform.Find("IngameSettingWindow/TabBar/Tab_Audio/ActiveIndicator");
        if (trGeneralIndicator != null) _generalActiveIndicator = trGeneralIndicator.gameObject;
        if (trAudioIndicator != null)   _audioActiveIndicator = trAudioIndicator.gameObject;

        Transform trGeneralPanel = transform.Find("IngameSettingWindow/ContentArea/GeneralContent");
        Transform trAudioPanel   = transform.Find("IngameSettingWindow/ContentArea/AudioContent");
        if (trGeneralPanel != null) _generalPanel = trGeneralPanel.gameObject;
        if (trAudioPanel != null)   _audioPanel = trAudioPanel.gameObject;

        if (_generalTab != null) {
            _generalTab.OnClickHandler += _ => ChangeTab(SelectedTab.General);
            _generalTab.OnPointerEnterHandler += _ => { if (_selectedTab != SelectedTab.General) _generalTabColor.color = HOVER_TAB_COLOR; };
            _generalTab.OnPointerExitHandler  += _ => { if (_selectedTab != SelectedTab.General) _generalTabColor.color = UNSELECTED_TAB_COLOR; };
        }
        if (_audioTab != null) {
            _audioTab.OnClickHandler += _ => ChangeTab(SelectedTab.Audio);
            _audioTab.OnPointerEnterHandler += _ => { if (_selectedTab != SelectedTab.Audio) _audioTabColor.color = HOVER_TAB_COLOR; };
            _audioTab.OnPointerExitHandler  += _ => { if (_selectedTab != SelectedTab.Audio) _audioTabColor.color = UNSELECTED_TAB_COLOR; };
        }

        _sensitivitySlider  = Util.BindComponent<Slider>("IngameSettingWindow/ContentArea/GeneralContent/MouseSensitivityRow/SensitivitySlider", this.gameObject);
        _sensitivityValue   = Util.BindComponent<TextMeshProUGUI>("IngameSettingWindow/ContentArea/GeneralContent/MouseSensitivityRow/SensitivityValue", this.gameObject);
        _masterVolumeSlider = Util.BindComponent<Slider>("IngameSettingWindow/ContentArea/AudioContent/MasterVolumeRow/MasterVolumeSlider", this.gameObject);
        _masterVolumeValue  = Util.BindComponent<TextMeshProUGUI>("IngameSettingWindow/ContentArea/AudioContent/MasterVolumeRow/MasterVolumeValue", this.gameObject);
        _effectVolumeSlider = Util.BindComponent<Slider>("IngameSettingWindow/ContentArea/AudioContent/EffectVolumeRow/EffectVolumeSlider", this.gameObject);
        _effectVolumeValue  = Util.BindComponent<TextMeshProUGUI>("IngameSettingWindow/ContentArea/AudioContent/EffectVolumeRow/EffectVolumeValue", this.gameObject);
        _bgmVolumeSlider    = Util.BindComponent<Slider>("IngameSettingWindow/ContentArea/AudioContent/BgmVolumeRow/BgmVolumeSlider", this.gameObject);
        _bgmVolumeValue     = Util.BindComponent<TextMeshProUGUI>("IngameSettingWindow/ContentArea/AudioContent/BgmVolumeRow/BgmVolumeValue", this.gameObject);

        if (_sensitivitySlider != null)  _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (_effectVolumeSlider != null) _effectVolumeSlider.onValueChanged.AddListener(OnEffectVolumeChanged);
        if (_bgmVolumeSlider != null)    _bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);

        _applyButton  = Util.BindComponent<Button>("IngameSettingWindow/Footer/ButtonContainer/ApplyButton", this.gameObject);
        _cancelButton = Util.BindComponent<Button>("IngameSettingWindow/Footer/ButtonContainer/CancelButton", this.gameObject);
        if (_applyButton != null)  _applyButton.onClick.AddListener(Hide);
        if (_cancelButton != null) _cancelButton.onClick.AddListener(CancelAndHide);

        RefreshFromSetting();
        ChangeTab(_selectedTab);

        // GameObject.Find는 비활성 오브젝트를 찾지 못한다. 씬에는 활성 상태로 두고 여기서 끈다
        gameObject.SetActive(false);
    }

    public void Show() {
        if (_isOpen) return;

        _snapshotSensitivity  = Managers.Setting.GetMouseSensitivity();
        _snapshotMasterVolume = Managers.Setting.GetMasterVolume();
        _snapshotEffectVolume = Managers.Setting.GetEffectVolume();
        _snapshotBgmVolume    = Managers.Setting.GetBgmVolume();

        RefreshFromSetting();
        gameObject.SetActive(true);
        _isOpen = true;
        _scene.OnUIOpened();
    }

    // 지금 값을 그대로 두고 닫는다 ('적용'과 매치 이탈 정리가 쓴다)
    public void Hide() {
        if (!_isOpen) return;
        _isOpen = false;
        gameObject.SetActive(false);
        _scene.OnUIClosed();

        // 이 창은 슬라이더를 움직이는 즉시 반영하므로 '적용'이 확정 지점이 아니다.
        // 조작이 끝나는 시점은 '닫힘' 하나뿐이고, CancelAndHide()도 되돌린 뒤 여기로 오므로
        // 취소까지 한 자리로 덮인다 — 취소를 빠뜨리면 메모리와 디스크가 어긋난 채 남는다
        Managers.Setting.Save();
    }

    public void CancelAndHide() {
        Managers.Setting.SetMouseSensitivity(_snapshotSensitivity);
        // 마스터가 효과·BGM을 다시 밀어내므로 마스터를 먼저 되돌린다
        Managers.Setting.SetMasterVolume(_snapshotMasterVolume);
        Managers.Setting.SetVolume(_snapshotEffectVolume, Define.Sound.Effect);
        Managers.Setting.SetVolume(_snapshotBgmVolume, Define.Sound.Bgm);
        Hide();
    }

    // SetValueWithoutNotify로 넣는다 — onValueChanged를 타면 되돌리는 값이 다시 '변경'으로 적용된다
    private void RefreshFromSetting() {
        if (_sensitivitySlider != null) {
            _sensitivitySlider.SetValueWithoutNotify(Managers.Setting.GetMouseSensitivity());
            if (_sensitivityValue != null) _sensitivityValue.text = _sensitivitySlider.value.ToString("F1");
        }
        if (_masterVolumeSlider != null) {
            _masterVolumeSlider.SetValueWithoutNotify(Managers.Setting.GetMasterVolume());
            if (_masterVolumeValue != null) _masterVolumeValue.text = ((int)_masterVolumeSlider.value).ToString();
        }
        if (_effectVolumeSlider != null) {
            _effectVolumeSlider.SetValueWithoutNotify(Managers.Setting.GetEffectVolume());
            if (_effectVolumeValue != null) _effectVolumeValue.text = ((int)_effectVolumeSlider.value).ToString();
        }
        if (_bgmVolumeSlider != null) {
            _bgmVolumeSlider.SetValueWithoutNotify(Managers.Setting.GetBgmVolume());
            if (_bgmVolumeValue != null) _bgmVolumeValue.text = ((int)_bgmVolumeSlider.value).ToString();
        }
    }

    private void OnSensitivityChanged(float value) {
        float snapped = Mathf.Round(value * 10f) / 10f;
        if (!Mathf.Approximately(_sensitivitySlider.value, snapped))
            _sensitivitySlider.SetValueWithoutNotify(snapped);
        if (_sensitivityValue != null) _sensitivityValue.text = snapped.ToString("F1");
        Managers.Setting.SetMouseSensitivity(snapped);
    }

    private void OnMasterVolumeChanged(float value) {
        int volume = (int)value;
        if (_masterVolumeValue != null) _masterVolumeValue.text = volume.ToString();
        Managers.Setting.SetMasterVolume(volume);
    }

    private void OnEffectVolumeChanged(float value) {
        int volume = (int)value;
        if (_effectVolumeValue != null) _effectVolumeValue.text = volume.ToString();
        Managers.Setting.SetVolume(volume, Define.Sound.Effect);
    }

    private void OnBgmVolumeChanged(float value) {
        int volume = (int)value;
        if (_bgmVolumeValue != null) _bgmVolumeValue.text = volume.ToString();
        Managers.Setting.SetVolume(volume, Define.Sound.Bgm);
    }

    private void ChangeTab(SelectedTab tab) {
        _selectedTab = tab;
        bool isGeneral = tab == SelectedTab.General;

        if (_generalTabColor != null) _generalTabColor.color = isGeneral ? SELECTED_TAB_COLOR : UNSELECTED_TAB_COLOR;
        if (_audioTabColor != null)   _audioTabColor.color   = isGeneral ? UNSELECTED_TAB_COLOR : SELECTED_TAB_COLOR;

        if (_generalActiveIndicator != null) _generalActiveIndicator.SetActive(isGeneral);
        if (_audioActiveIndicator != null)   _audioActiveIndicator.SetActive(!isGeneral);

        if (_generalPanel != null) _generalPanel.SetActive(isGeneral);
        if (_audioPanel != null)   _audioPanel.SetActive(!isGeneral);
    }
}
