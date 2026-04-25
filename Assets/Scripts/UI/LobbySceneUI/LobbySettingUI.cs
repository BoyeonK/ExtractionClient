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

        _generalTab.OnClickHandler += _ => ChangeTab(SelectedTab.General);
        _graphicTab.OnClickHandler += _ => ChangeTab(SelectedTab.Graphic);
        _audioTab.OnClickHandler   += _ => ChangeTab(SelectedTab.Audio);

        _generalTab.OnPointerEnterHandler += _ => { if (_selectedTab != SelectedTab.General) _generalTabColor.color = _hoverTabColor; };
        _generalTab.OnPointerExitHandler  += _ => { if (_selectedTab != SelectedTab.General) _generalTabColor.color = _unselectedTabColor; };
        _graphicTab.OnPointerEnterHandler += _ => { if (_selectedTab != SelectedTab.Graphic) _graphicTabColor.color = _hoverTabColor; };
        _graphicTab.OnPointerExitHandler  += _ => { if (_selectedTab != SelectedTab.Graphic) _graphicTabColor.color = _unselectedTabColor; };
        _audioTab.OnPointerEnterHandler   += _ => { if (_selectedTab != SelectedTab.Audio)   _audioTabColor.color   = _hoverTabColor; };
        _audioTab.OnPointerExitHandler    += _ => { if (_selectedTab != SelectedTab.Audio)   _audioTabColor.color   = _unselectedTabColor; };

        _applyButton.onClick.AddListener(OnClickApply);
        _cancelButton.onClick.AddListener(OnClickCancel);

        ChangeTab(_selectedTab);
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    private void OnClickApply() {
        _scene.ActiveReconfirmConfirmOrCancel("설정을 적용하시겠습니까?", OnApplyConfirmed);
    }

    private void OnClickCancel() {
        _scene.ActiveReconfirmConfirmOrCancel("변경사항을 취소하시겠습니까?", Hide);
    }

    private void OnApplyConfirmed() {
        ApplySetting();
        Hide();
    }

    private void ApplySetting() { }

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