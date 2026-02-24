using TMPro;
using UnityEngine;

public class UI_PrintMessage : UI_Scene {
    TextMeshProUGUI _messageTop;
    TextMeshProUGUI _messageMiddle;
    TextMeshProUGUI _messageBottom;
    bool _isInitialized = false;

    private void OnEnable() {
        Init();
    }

    public override void Init() {
        base.Init();
        if (!_isInitialized) {
            Transform t = gameObject.transform.Find("MessageTop");
            if (t != null)
                _messageTop = t.GetComponent<TextMeshProUGUI>();

            Transform m = gameObject.transform.Find("MessageMiddle");
            if (m != null)
                _messageMiddle = m.GetComponent<TextMeshProUGUI>();

            Transform b = gameObject.transform.Find("MessageBottom");
            if (b != null)
                _messageBottom = b.GetComponent<TextMeshProUGUI>();
        }
        _isInitialized = true;
        ClearText();
    }

    public void SetTextTop(string message) {
        if (_messageTop != null)
            _messageTop.text = message;
    }

    public void SetTextMiddle(string message) {
        if (_messageMiddle != null)
            _messageMiddle.text = message;
    }

    public void SetTextBottom(string message) {
        if (_messageBottom != null)
            _messageBottom.text = message;
    }

    public void SetFontSizeTop(int size) {
        if (_messageTop != null)
            _messageTop.fontSize = size;
    }

    public void SetFontSizeMiddle(int size) {
        if (_messageMiddle != null)
            _messageMiddle.fontSize = size;
    }

    public void SetFontSizeBottom(int size) {
        if (_messageBottom != null)
            _messageBottom.fontSize = size;
    }

    public void SetTextColorTop(int r, int g, int b, int a) {
        if (_messageTop != null)
            _messageTop.color = new Color32((byte)r, (byte)g, (byte)b, (byte)a);
    }

    public void SetTextColorMiddle(int r, int g, int b, int a) {
        if (_messageMiddle != null)
            _messageMiddle.color = new Color32((byte)r, (byte)g, (byte)b, (byte)a);
    }

    public void SetTextColorBottom(int r, int g, int b, int a) {
        if (_messageBottom != null)
            _messageBottom.color = new Color32((byte)r, (byte)g, (byte)b, (byte)a);
    }

    public void ClearText() {
        if (_messageTop != null)
            _messageTop.text = "";
        if (_messageMiddle != null)
            _messageMiddle.text = "";
        if (_messageBottom != null)
            _messageBottom.text = "";
    }

    private void OnDisable() {
        ClearText();
        Clear();
    }

    private void Clear() {

    }
}
