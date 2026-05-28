using UnityEngine;
using UnityEngine.UI;

public class IngameHealthBarUI : MonoBehaviour {
    Image _hpFillImage;
    Image _armorFillImage;

    public void SetHP(float current, float max) {
        if (_hpFillImage == null) return;
        float ratio = Mathf.Clamp01(current / max);
        _hpFillImage.fillAmount = ratio;
    }

    public void SetArmor(float current, float max) {
        if (_armorFillImage == null) return;
        float ratio = Mathf.Clamp01(current / max);
        _armorFillImage.fillAmount = ratio;
    }
}
