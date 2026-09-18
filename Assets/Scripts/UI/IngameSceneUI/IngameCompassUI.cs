using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameCompassUI : MonoBehaviour {
    // strip이 한 번에 보여주는 각도 폭. 픽셀 환산도 복제 패딩도 전부 여기서 파생되므로
    // 표시 범위를 바꿀 때 고칠 곳은 이 상수 하나다
    const float SPAN_DEG = 120f;
    const int MINOR_STEP_DEG = 10;
    const int MAJOR_STEP_DEG = 30;

    const float MINOR_WIDTH = 2f;
    const float MINOR_HEIGHT = 12f;
    const float MAJOR_WIDTH = 3f;
    const float MAJOR_HEIGHT = 26f;
    const float TICK_OFFSET_Y = -6f;
    static readonly Color TICK_COLOR = new Color(1f, 1f, 1f, 0.85f);

    // strip 중앙이 현재 방위를 가리키고 오른쪽이 방위 증가 방향이므로, 눈금 각도 deg가
    // 실제로 가리키는 방위는 deg - NORTH_DEG다. "180도 자리가 N"이라는 요구가 이 상수 하나로 들어온다
    const int NORTH_DEG = 180;
    const float LABEL_GAP = 4f;
    const float LABEL_WIDTH = 32f;
    const float LABEL_HEIGHT = 18f;
    const float LABEL_FONT_SIZE = 16f;

    RectTransform _viewport;
    RectTransform _strips;
    RectTransform[] _ticks;

    float _viewportWidth;
    float _pxPerDeg;
    float _shownHeading = float.NaN;   // NaN이라 첫 SetHeading이 반드시 한 번 쓴다

    // 0↔360 이음매에서 strip을 되감을 때 화면 양 끝이 비지 않으려면, 보이는 폭의 절반만큼
    // 눈금을 양쪽에 복제해야 한다(SPAN_DEG 120 → 60도, 즉 -60~420도를 들고 있는다).
    // 큰 눈금 간격의 배수로 올리는 것은 복제 구간의 큰 눈금이 본 구간과 같은 자리에 와야 하기 때문이다
    static int PadDeg => Mathf.CeilToInt(SPAN_DEG * 0.5f / MAJOR_STEP_DEG) * MAJOR_STEP_DEG;

    public void Init() {
        _viewport = Util.BindComponent<RectTransform>("ViewportArea", gameObject);
        _strips = Util.BindComponent<RectTransform>("ViewportArea/Strips", gameObject);
        if (_viewport == null || _strips == null) return;

        BuildTicks();

        // 매치 내내 떠 있으므로 끄지 않는다(IngameTimeoutUI와 같다)
    }

    // 값을 스스로 구하지 않는다 — IngameScene.UpdateCompass()가 플레이어 루트 요를 민다
    public void SetHeading(float yawDeg) {
        if (_strips == null) return;

        RefreshScaleIfNeeded();
        if (_pxPerDeg <= 0f) return;

        float heading = Mathf.Repeat(yawDeg, 360f);
        if (Mathf.Approximately(heading, _shownHeading)) return;
        _shownHeading = heading;

        // 되감기는 눈에 보이지 않는다 — 패딩이 보이는 폭의 절반이라 359.9°에서 오른쪽 끝에
        // 있던 420도 눈금 자리에 0.1°로 감긴 뒤 60도 눈금이 그대로 선다.
        // 세로 위치는 저작값을 그대로 둔다
        _strips.anchoredPosition = new Vector2(-heading * _pxPerDeg, _strips.anchoredPosition.y);
    }

    // rect 폭은 Init 시점(캔버스 레이아웃 전)에 0일 수 있고 해상도가 바뀌면 달라진다.
    // 폭이 바뀐 프레임에만 환산과 눈금 배치를 다시 잡는다
    private void RefreshScaleIfNeeded() {
        float width = _viewport.rect.width;
        if (width <= 0f || Mathf.Approximately(width, _viewportWidth)) return;

        _viewportWidth = width;
        _pxPerDeg = width / SPAN_DEG;
        _shownHeading = float.NaN;   // 환산이 바뀌었으므로 strip 위치도 다시 써야 한다
        LayoutTicks();
    }

    private void BuildTicks() {
        int count = (360 + PadDeg * 2) / MINOR_STEP_DEG + 1;

        _ticks = new RectTransform[count];
        for (int i = 0; i < count; i++)
            _ticks[i] = CreateTick(i);
    }

    private int TickDeg(int index) => -PadDeg + index * MINOR_STEP_DEG;

    private RectTransform CreateTick(int index) {
        int deg = TickDeg(index);

        // 360이 MAJOR_STEP_DEG의 배수라 감지 않은 값으로 갈라도 결과가 같다.
        // 라벨은 그렇지 않아 CardinalLabel이 감아서 판정한다
        bool isMajor = deg % MAJOR_STEP_DEG == 0;

        GameObject go = new GameObject(isMajor ? $"Major_{deg}" : $"Minor_{deg}");
        go.transform.SetParent(_strips, false);

        // 스프라이트를 주지 않은 Image는 단색 사각형으로 그려진다 — 눈금에 자산이 필요 없다
        Image image = go.AddComponent<Image>();
        image.color = TICK_COLOR;
        image.raycastTarget = false;   // HUD가 클릭을 가로채면 겹치는 영역의 드래그를 먹는다

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = isMajor
            ? new Vector2(MAJOR_WIDTH, MAJOR_HEIGHT)
            : new Vector2(MINOR_WIDTH, MINOR_HEIGHT);

        if (isMajor) {
            string label = CardinalLabel(deg);
            if (label != null) CreateLabel(rt, label);
        }

        return rt;
    }

    private static string CardinalLabel(int deg) {
        // 복제 구간(-60·420)도 같은 방위를 내야 하므로 감아서 판정한다
        int bearing = Mathf.RoundToInt(Mathf.Repeat(deg - NORTH_DEG, 360f));
        switch (bearing) {
            case 0: return "N";
            case 90: return "E";
            case 180: return "S";
            case 270: return "W";
            default: return null;
        }
    }

    // 눈금의 자식으로 붙인다 — x는 눈금을 따라가므로 LayoutTicks가 따로 밀 것이 없다
    private void CreateLabel(RectTransform tick, string text) {
        GameObject go = new GameObject($"Label_{text}");
        go.transform.SetParent(tick, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = LABEL_FONT_SIZE;
        tmp.color = TICK_COLOR;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;   // 눈금과 같은 이유 — HUD가 클릭을 가로채지 않는다

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(LABEL_WIDTH, LABEL_HEIGHT);
        rt.anchoredPosition = new Vector2(0f, MAJOR_HEIGHT * 0.5f + LABEL_GAP + LABEL_HEIGHT * 0.5f);
    }

    private void LayoutTicks() {
        if (_ticks == null) return;

        for (int i = 0; i < _ticks.Length; i++)
            _ticks[i].anchoredPosition = new Vector2(TickDeg(i) * _pxPerDeg, TICK_OFFSET_Y);
    }
}
