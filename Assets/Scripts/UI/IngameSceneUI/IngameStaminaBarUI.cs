using UnityEngine;
using UnityEngine.UI;

public class IngameStaminaBarUI : MonoBehaviour {
    Image _staminaFillImage;

    public void Init() {
        _staminaFillImage = transform.Find("StaminaBarBg/StaminaBarFill").GetComponent<Image>();

        // 평소에는 숨어 있다. 씬에는 활성으로 저장해야 GameObject.Find가 잡으므로
        // 끄는 것은 바인딩이 끝난 여기여야 한다 (InteractUI와 같은 규칙)
        gameObject.SetActive(false);
    }

    // 표시 조건 판단은 IngameScene.UpdateStamina()에 있다 — 문턱값(20)이 씬 상수라 여기서 알 수 없다.
    // 매 프레임 불리므로 상태가 같으면 빠져나간다
    public void SetVisible(bool visible) {
        if (gameObject.activeSelf == visible) return;
        gameObject.SetActive(visible);
    }

    // 값도 판정도 IngameScene.UpdateStamina()가 쥐고 여기는 대입만 한다.
    // 매 프레임 불리지만 fillAmount setter가 값 비교를 하므로 같은 값이면 리빌드가 없다
    public void SetStamina(float current, float max) {
        if (_staminaFillImage == null || max <= 0f) return;
        _staminaFillImage.fillAmount = Mathf.Clamp01(current / max);
    }
}
