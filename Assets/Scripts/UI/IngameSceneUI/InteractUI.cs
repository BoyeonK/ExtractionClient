using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractUI : MonoBehaviour {
    IngameScene _scene;
    TextMeshProUGUI _interactText;

    public void Init(IngameScene scene) {
        _scene = scene;
        _interactText = transform.Find("InteractText")?.GetComponent<TextMeshProUGUI>();

        gameObject.SetActive(false);
    }

    public void Show(string text) {
        gameObject.SetActive(true);
        _interactText.text = text;
    }

    public void Hide() {
        _interactText.text = "";
        gameObject.SetActive(false);
    }
}
