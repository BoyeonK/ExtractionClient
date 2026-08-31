using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// 에디터 자산 없이 코드로만 만든 크로스헤어.
//
// 왜 이렇게 만들었나
//   히트박스 반지름 튜닝(progress.md 우선순위 0번)에 조준 기준선이 필요한데,
//   씬 오브젝트 제작을 기다리면 그 작업이 막힌다. 그래서 프리팹·스프라이트·머티리얼
//   어느 것에도 의존하지 않는 형태로 먼저 세웠다.
//
// OPTION: 정식 IngameSceneUI 자산으로 다시 만들기
//   런타임 검증에서 사용감이 나쁘지 않아 이대로 유지하기로 했다(사용자 판단).
//   없어도 동작에 문제가 없으므로 상시 보류 가능하며, 모양을 손보고 싶어지면 그때 옮긴다.
//   이 프로젝트의 인게임 UI는 씬에 배치된 오브젝트를 IngameScene.Init()이
//   GameObject.Find()로 붙잡는 방식이다(IngameHealthBarUI·InteractUI 등).
//   이 파일은 그 규칙의 유일한 예외이며, 정식 자산을 만들면 통째로 대체된다.
//   모양·색·두께를 에디터에서 만질 수 없는 것이 이 방식의 대가다.
//
// 대체·롤백 절차 (건드릴 곳이 셋뿐이도록 일부러 좁혀 뒀다)
//   1. 이 파일 삭제
//   2. IngameScene.Init()의 IngameCrosshair.Create(this) 한 줄 삭제
//   3. PlayerController.CurrentSpread 접근자 — 새 UI도 스프레드를 읽어야 하므로
//      대체 시에는 남겨 두는 편이 맞다. 크로스헤어 자체를 걷어낼 때만 지운다
//   IngameScene에 필드로 들고 있지 않은 것도 같은 이유다. 이 클래스가 씬을 참조해
//   스스로 갱신하므로, 지울 때 씬 쪽에서 정리할 상태가 남지 않는다
// ─────────────────────────────────────────────────────────────────────────────
public class IngameCrosshair : MonoBehaviour {
    // 정식 자산이 생기면 전부 인스펙터 값으로 옮겨갈 튜닝 상수들
    private const float BAR_THICKNESS = 2f;
    private const float BAR_LENGTH = 8f;
    private const float MIN_GAP = 3f;     // 기본 스프레드에서도 점으로 뭉치지 않게
    private static readonly Color BAR_COLOR = new Color(1f, 1f, 1f, 0.85f);

    // 인게임 UI 중 가장 뒤. 어차피 다른 UI가 열리면 통째로 숨지만,
    // SceneUI(0~)·PopupUI(20~)와 겹치지 않게 아래로 빼 둔다
    private const int SORTING_ORDER = -1;

    private IngameScene _scene;
    private RectTransform _top, _bottom, _left, _right;
    private GameObject _group;

    public static void Create(IngameScene scene) {
        GameObject root = new GameObject("@IngameCrosshair");

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;
        // CanvasScaler를 붙이지 않는다 — 1 유닛 = 1 픽셀이 되어야
        // 아래 각도 → 픽셀 환산이 스케일러 설정에 휘둘리지 않는다

        root.AddComponent<IngameCrosshair>().Init(scene, canvas);
    }

    private void Init(IngameScene scene, Canvas canvas) {
        _scene = scene;

        _group = new GameObject("Bars");
        _group.transform.SetParent(canvas.transform, false);
        RectTransform groupRt = _group.AddComponent<RectTransform>();
        groupRt.anchorMin = groupRt.anchorMax = new Vector2(0.5f, 0.5f);
        groupRt.sizeDelta = Vector2.zero;
        groupRt.anchoredPosition = Vector2.zero;

        // pivot을 막대의 '안쪽 끝'에 둔다. 그래야 anchoredPosition에 간격만 넣으면
        // 막대가 중심에서 바깥으로 자라고, 길이를 바꿔도 간격이 흔들리지 않는다
        _top = CreateBar("Top", new Vector2(0.5f, 0f), new Vector2(BAR_THICKNESS, BAR_LENGTH));
        _bottom = CreateBar("Bottom", new Vector2(0.5f, 1f), new Vector2(BAR_THICKNESS, BAR_LENGTH));
        _left = CreateBar("Left", new Vector2(1f, 0.5f), new Vector2(BAR_LENGTH, BAR_THICKNESS));
        _right = CreateBar("Right", new Vector2(0f, 0.5f), new Vector2(BAR_LENGTH, BAR_THICKNESS));
    }

    private RectTransform CreateBar(string name, Vector2 pivot, Vector2 size) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_group.transform, false);

        // 스프라이트를 주지 않은 Image는 단색 사각형으로 그려진다 — 자산이 필요 없다
        Image image = go.AddComponent<Image>();
        image.color = BAR_COLOR;
        image.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = size;

        return rt;
    }

    private void Update() {
        // 씬이 내려가는 프레임에 파괴 순서가 보장되지 않는다. 씬 컴포넌트가 먼저
        // 파괴된 뒤 이 Update가 한 번 더 돌면 MissingReferenceException이 된다
        if (_scene == null) return;

        PlayerController player = _scene.PlayerController;

        // 스폰 전에는 플레이어도 카메라도 없다.
        // 숨김 조건은 '다른 UI가 열려 있음'과 '매치 이탈 중'(사망 연출이 카메라를
        // 가져가므로 조준선이 남으면 안 된다) 둘뿐이다.
        // 달리는 중에는 발사가 막히지만 숨기지 않는다 — 이동할 때마다 깜빡여 거슬린다
        bool visible = player != null
                       && player.ViewCamera != null
                       && !_scene.IsAnyUIOpen
                       && !_scene.IsInputLocked;

        if (_group.activeSelf != visible)
            _group.SetActive(visible);

        if (!visible) return;

        SetGap(SpreadToPixels(player.CurrentSpread, player.ViewCamera));
    }

    // 스프레드는 발사 원뿔의 반각(도)이다. 같은 각도라도 화면에서 차지하는 크기는
    // 화각과 해상도에 따라 달라지므로, 화면 절반 높이를 기준으로 환산한다
    private static float SpreadToPixels(float spreadDeg, Camera camera) {
        float halfFovTan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        if (halfFovTan <= 0f) return MIN_GAP;

        float px = (Screen.height * 0.5f) * Mathf.Tan(spreadDeg * Mathf.Deg2Rad) / halfFovTan;
        return Mathf.Max(px, MIN_GAP);
    }

    private void SetGap(float gap) {
        _top.anchoredPosition = new Vector2(0f, gap);
        _bottom.anchoredPosition = new Vector2(0f, -gap);
        _left.anchoredPosition = new Vector2(-gap, 0f);
        _right.anchoredPosition = new Vector2(gap, 0f);
    }
}
