using UnityEngine;
using UnityEngine.UI;

public class IngameDamageIndicatorContent : MonoBehaviour {
    const float INDICATOR_ANIMATION_DURATION = 0.3f;
    const float INDICATOR_START_ALPHA = 180f / 255f;
    const float INDICATOR_END_SCALE = 1.05f;

    Image _indicatorImg;
    float _elapsed = 0f;

    // 이미지는 위(내 정면)를 가리키도록 저작돼 있고, 이 회전이 그것을 가해자 쪽으로 돌린다.
    // 루트는 화면 중앙의 크기 0 지점이고 이미지가 그 위쪽으로 떨어져 있어, 루트를 돌리면
    // 이미지가 중앙을 축으로 공전한다
    public void Init(float degree) {
        transform.localRotation = Quaternion.Euler(0f, 0f, degree);
        transform.localScale = Vector3.one;

        _indicatorImg = Util.BindComponent<Image>("Image", this.gameObject);
        if (_indicatorImg != null)
            _indicatorImg.color = new Color(1f, 1f, 1f, INDICATOR_START_ALPHA);
    }

    // 부모가 목록을 들지 않으므로 스스로 파괴한다 — SingleKillLog가 부모를 거쳐 사라지는 것과
    // 갈리는 지점이다(그쪽은 딕셔너리에 죽은 참조가 남지 않게 하려는 것이라 이유가 다르다)
    void Update() {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / INDICATOR_ANIMATION_DURATION);

        if (_indicatorImg != null)
            _indicatorImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(INDICATOR_START_ALPHA, 0f, t));

        // 루트를 키우면 자식의 중앙 오프셋도 함께 늘어 바깥으로 퍼지는 형태가 된다
        transform.localScale = Vector3.one * Mathf.Lerp(1f, INDICATOR_END_SCALE, t);

        if (t >= 1f)
            Managers.Resource.Destroy(gameObject);
    }
}
