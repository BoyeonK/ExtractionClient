using UnityEngine;

public class SelectedCharacter : MonoBehaviour {
    GameObject _hb0Selected;
    GameObject _hb1Selected;
    GameObject _hb2Selected;

    public void Init() {
        _hb0Selected = Util.BindComponent<Transform>("HB0Selected", this.gameObject).gameObject;
        _hb1Selected = Util.BindComponent<Transform>("HB1Selected", this.gameObject).gameObject;
        _hb2Selected = Util.BindComponent<Transform>("HB2Selected", this.gameObject).gameObject;
    }

    public void SetCharacterType(int characterType) {
        _hb0Selected.SetActive(characterType == 0);
        _hb1Selected.SetActive(characterType == 1);
        _hb2Selected.SetActive(characterType == 2);
    }
}
