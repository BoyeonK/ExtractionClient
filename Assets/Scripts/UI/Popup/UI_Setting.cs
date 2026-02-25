using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_Setting : UI_Popup {
    enum Panels {
        General,
        Graphic,
        Audio,
        GeneralContent,
        GraphicContent,
        AudioContent,
    }

    enum Texts {
        GeneralText,
        GraphicText,
        AudioText,
    }

    private int _currentPanelIndex = 0;


    public override void Init() {
        base.Init();
        Bind<TextMeshProUGUI>(typeof(Texts));
        Bind<GameObject>(typeof(Panels));

        GameObject generalTab = Get<GameObject>((int)Panels.General);
        BindEvent(generalTab, OnClickGeneral, Define.UIEvent.Click);

        GameObject graphicTab = Get<GameObject>((int)Panels.Graphic);
        BindEvent(graphicTab, OnClickGraphic, Define.UIEvent.Click);

        GameObject audioTab = Get<GameObject>((int)Panels.Audio);
        BindEvent(audioTab, OnClickAutio, Define.UIEvent.Click);

        UpdatePanel();
    }

    private void OnClickGeneral(PointerEventData data) {
        _currentPanelIndex = 0;
        UpdatePanel();
    }

    private void OnClickGraphic(PointerEventData data) {
        _currentPanelIndex = 1;
        UpdatePanel();
    }

    private void OnClickAutio(PointerEventData data) {
        _currentPanelIndex = 2;
        UpdatePanel();
    }

    private void UpdatePanel() { 
        switch (_currentPanelIndex) {
            case 0:
                Get<GameObject>((int)Panels.GeneralContent).SetActive(true);
                Get<GameObject>((int)Panels.GraphicContent).SetActive(false);
                Get<GameObject>((int)Panels.AudioContent).SetActive(false);
                break;
            case 1:
                Get<GameObject>((int)Panels.GeneralContent).SetActive(false);
                Get<GameObject>((int)Panels.GraphicContent).SetActive(true);
                Get<GameObject>((int)Panels.AudioContent).SetActive(false);
                break;
            case 2:
                Get<GameObject>((int)Panels.GeneralContent).SetActive(false);
                Get<GameObject>((int)Panels.GraphicContent).SetActive(false);
                Get<GameObject>((int)Panels.AudioContent).SetActive(true);
                break;
        }
    }

    void Start() {
        
    }

    void Update() {
        
    }
}
