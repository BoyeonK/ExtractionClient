using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameEscapeCountdownUI : MonoBehaviour {
    TextMeshProUGUI _countdownText;

    public void Init() {
        _countdownText = Util.BindComponent<TextMeshProUGUI>("TextArea/CountdownText", this.gameObject);
    }

    public void SetCountdown(int countdown) {
        if (_countdownText == null) return;
        _countdownText.text = countdown.ToString();
    }

    public void ActiveThis() {
        gameObject.SetActive(true);
    }

    public void DeactiveThis() {
        gameObject.SetActive(false);
    }
}
