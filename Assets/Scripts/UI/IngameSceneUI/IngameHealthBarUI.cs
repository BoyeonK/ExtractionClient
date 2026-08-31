using UnityEngine;
using UnityEngine.UI;

public class IngameHealthBarUI : MonoBehaviour {
    Image _hpFillImage;
    Image _armorFillImage;

    float _maxHP;
    float _maxShield;

    public void SetMaxHP(float max) => _maxHP = max;
    public void SetMaxShield(float max) => _maxShield = max;

    public void Init() {
        _hpFillImage = transform.Find("HealthBarBg/HealthBarFill").GetComponent<Image>();
        _armorFillImage = transform.Find("ArmorBarBg/ArmorBarFill").GetComponent<Image>();
    }

    public void SetHP(float current) {
        if (_hpFillImage == null || _maxHP <= 0f) return;
        _hpFillImage.fillAmount = Mathf.Clamp01(current / _maxHP);
    }

    // 최대 실드가 0이면(방어구 해제 등) 조기 반환하지 않고 바를 비운다.
    // 반환하면 벗기 직전 fillAmount가 그대로 남는다.
    public void SetArmor(float current) {
        if (_armorFillImage == null) return;
        _armorFillImage.fillAmount = _maxShield > 0f ? Mathf.Clamp01(current / _maxShield) : 0f;
    }
}
