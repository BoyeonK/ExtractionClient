using UnityEngine;
using UnityEngine.UI;

public class InteractUI : MonoBehaviour {
    IngameScene _scene;
    Text _interactText;

    public void Init(IngameScene scene) {
        _scene = scene;
        _interactText = transform.Find("InteractText")?.GetComponent<Text>();
    }
}
