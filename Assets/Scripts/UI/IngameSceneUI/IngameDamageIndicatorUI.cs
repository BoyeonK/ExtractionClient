using UnityEngine;

public class IngameDamageIndicatorUI : MonoBehaviour {
    const string CONTENT_PATH = "UI/IngameSceneUI/IngameDamageIndicatorContent";

    public void Init() {
    }

    // 호출마다 인스턴스를 하나 만든다 — 개수 상한을 두지 않는 것은 여러 방향에서 맞으면
    // 그만큼 겹쳐 보이는 것이 정상이기 때문이고, 각자 0.3초 뒤 스스로 사라져 쌓이지 않는다.
    // 부모는 이 오브젝트다. Content 루트가 anchor·pivot 모두 0.5라 오프셋만 0이면
    // 화면 중앙에 놓이므로 중간 컨테이너에 기대지 않는다
    public void ShowIndicator(float degree) {
        GameObject go = Managers.Resource.Instantiate(CONTENT_PATH, transform);
        if (go == null) return;

        // 프리팹에 1920×1080 기준 저작 좌표가 남아 있어, 그대로 두면 화면 밖으로 밀린다
        if (go.transform is RectTransform rect)
            rect.anchoredPosition = Vector2.zero;

        IngameDamageIndicatorContent content = go.GetComponent<IngameDamageIndicatorContent>();
        if (content != null)
            content.Init(degree);
    }
}
