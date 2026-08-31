using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameTimeoutUI : MonoBehaviour {
    TextMeshProUGUI _countdownText;

    public void Init() {
        _countdownText = Util.BindComponent<TextMeshProUGUI>("TextArea/CountdownText", this.gameObject);
    }

    public void SetCountdown(uint leftTimeMs) {
        if (_countdownText == null) return;

        uint totalSec = leftTimeMs / 1000;
        _countdownText.text = $"{totalSec / 60:D2}:{totalSec % 60:D2}";
    }

    public void ActiveThis() {
        gameObject.SetActive(true);
    }

    public void DeactiveThis() {
        gameObject.SetActive(false);
    }
}
