using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_CharacterSelect : UI_Scene {
    LobbyScene _scene;

    TextMeshProUGUI _hb0Text;
    TextMeshProUGUI _hb1Text;
    TextMeshProUGUI _hb2Text;
    TextMeshProUGUI _descriptionText;
    Button _selectBtn;

    UI_EventHandler _hb0Handler;
    UI_EventHandler _hb1Handler;
    UI_EventHandler _hb2Handler;

    int _selectedType = 0;

    Color _selectedColor = new Color(1f, 1f, 1f, 1f);
    Color _normalColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    public override void Init() {
        base.Init();
        if (isInit) return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is LobbyScene lobbyScene)
            _scene = lobbyScene;

        _hb0Text = Util.BindComponent<TextMeshProUGUI>("SelectPanel/HB0", this.gameObject);
        _hb1Text = Util.BindComponent<TextMeshProUGUI>("SelectPanel/HB1", this.gameObject);
        _hb2Text = Util.BindComponent<TextMeshProUGUI>("SelectPanel/HB2", this.gameObject);
        _selectBtn = Util.BindComponent<Button>("SelectPanel/SelectBtn", this.gameObject);
        _descriptionText = Util.BindComponent<TextMeshProUGUI>("DescriptionPanel/Description", this.gameObject);

        _hb0Handler = Util.GetOrAddComponent<UI_EventHandler>(_hb0Text.gameObject);
        _hb1Handler = Util.GetOrAddComponent<UI_EventHandler>(_hb1Text.gameObject);
        _hb2Handler = Util.GetOrAddComponent<UI_EventHandler>(_hb2Text.gameObject);

        _hb0Handler.OnClickHandler += (e) => SelectCharacter(0);
        _hb1Handler.OnClickHandler += (e) => SelectCharacter(1);
        _hb2Handler.OnClickHandler += (e) => SelectCharacter(2);

        _selectBtn.onClick.AddListener(OnClickSelectBtn);

        UpdateVisual();
        base.OnInitComplete();
    }

    void SelectCharacter(int type) {
        Managers.Sound.PlayUISubmit();
        _selectedType = type;
        _scene.SetCharacterType(type);
        UpdateVisual();
    }

    void UpdateVisual() {
        _hb0Text.color = _selectedType == 0 ? _selectedColor : _normalColor;
        _hb1Text.color = _selectedType == 1 ? _selectedColor : _normalColor;
        _hb2Text.color = _selectedType == 2 ? _selectedColor : _normalColor;

        if (Define.CharacterDescriptions.TryGetValue(_selectedType, out string desc))
            _descriptionText.text = desc;
    }

    void OnClickSelectBtn() {
        Managers.Sound.PlayUISubmit();
        _scene.BackToLobbyMain();
    }

    public void Refresh() {
        _selectedType = _scene.SelectedCharacterType;
        UpdateVisual();
    }

    private void OnDestroy() {
        _selectBtn.onClick.RemoveAllListeners();
    }
}
