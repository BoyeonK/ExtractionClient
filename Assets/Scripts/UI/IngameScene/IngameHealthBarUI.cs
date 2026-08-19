using UnityEngine;
using UnityEngine.UI;

public class IngameHealthBarUI : MonoBehaviour {
    // TODO: Fill 이미지 바인딩. 두 필드는 어디서도 대입되지 않아 영구 null이고,
    //       그 탓에 SetHP/SetArmor가 전부 무효화된다(게이지가 갱신되지 않는다).
    //       같은 폴더의 InteractUI·IngameInventoryUI처럼 Init()을 만들어
    //       transform.Find(경로)로 바인딩하고, IngameScene.Init()에서 호출할 것.
    //       현재 IngameScene.Init()은 GetComponent만 하고 Init() 호출이 없다.
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
