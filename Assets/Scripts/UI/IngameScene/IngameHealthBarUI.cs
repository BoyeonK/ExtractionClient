using UnityEngine;
using UnityEngine.UI;

public class IngameHealthBarUI : MonoBehaviour {
    Image _hpFillImage;
    Image _armorFillImage;

    float _maxHP;
    float _maxShield;

    public void SetMaxHP(float max) => _maxHP = max;
    public void SetMaxShield(float max) => _maxShield = max;

    public void SetHP(float current) {
        if (_hpFillImage == null || _maxHP <= 0f) return;
        _hpFillImage.fillAmount = Mathf.Clamp01(current / _maxHP);
    }

    public void SetArmor(float current) {
        if (_armorFillImage == null || _maxShield <= 0f) return;
        _armorFillImage.fillAmount = Mathf.Clamp01(current / _maxShield);
    }
}
