using UnityEngine;

// 사망 유예(IngameScene.MATCH_EXIT_DELAY) 동안 재생되는 탑뷰 연출.
//
// 기존 카메라를 움직이지 않고 같은 시점의 카메라를 새로 만들어 전환한다 —
// 기존 카메라는 PlayerController 하위에 있고 ApplyViewRotation()이 매 프레임 시점을
// 덮어쓰기 때문에, 그대로 보간하면 되돌려진다.
public class DeathCameraController : MonoBehaviour {
    // 연출 튜닝값. 상승 높이와 소요 시간이며, 소요 시간은 유예보다 짧아야 한다
    private const float TOP_VIEW_HEIGHT = 3f;
    private const float DURATION = 2f;

    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Vector3 _endPosition;
    private Quaternion _endRotation;
    private float _elapsed;

    // sourceCamera의 시점·화각을 그대로 물려받은 카메라를 만들고, 시신 위로 서서히 올라간다.
    public static void Play(Camera sourceCamera, Transform deadPlayer) {
        if (sourceCamera == null || deadPlayer == null) return;

        GameObject rig = new GameObject("@DeathCamera");
        Camera camera = rig.AddComponent<Camera>();

        camera.fieldOfView = sourceCamera.fieldOfView;
        camera.nearClipPlane = sourceCamera.nearClipPlane;
        camera.farClipPlane = sourceCamera.farClipPlane;
        camera.cullingMask = sourceCamera.cullingMask;
        camera.clearFlags = sourceCamera.clearFlags;
        camera.backgroundColor = sourceCamera.backgroundColor;
        camera.depth = sourceCamera.depth;

        rig.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);

        // AudioListener는 옮기지 않는다. 새 카메라에 붙이면 씬에 둘이 되어 경고가 뜨고,
        // 그대로 두면 소리가 시신 위치에서 들려 연출과도 맞는다
        sourceCamera.enabled = false;

        rig.AddComponent<DeathCameraController>().Init(deadPlayer);
    }

    private void Init(Transform deadPlayer) {
        _startPosition = transform.position;
        _startRotation = transform.rotation;

        // 목표 지점을 오브젝트로 남긴다. 인스펙터에서 높이를 눈으로 확인·조정하기 위한 것
        GameObject topViewPoint = new GameObject("TopViewPoint");
        topViewPoint.transform.SetParent(transform, false);
        _endPosition = deadPlayer.position + Vector3.up * TOP_VIEW_HEIGHT;
        topViewPoint.transform.position = _endPosition;

        // 도착 시점의 시선은 수직으로 내려다보는 방향이다. LookRotation의 up 힌트를
        // 원래 카메라가 보던 수평 방향으로 잡아, 시신이 화면에서 위쪽에 오도록 맞춘다
        Vector3 lookDir = deadPlayer.position - _endPosition;
        Vector3 upHint = Vector3.ProjectOnPlane(_startRotation * Vector3.forward, Vector3.up);
        if (upHint.sqrMagnitude < 0.0001f)
            upHint = Vector3.forward;

        _endRotation = Quaternion.LookRotation(lookDir.normalized, upHint.normalized);
    }

    private void Update() {
        if (_elapsed >= DURATION) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / DURATION);
        t = t * t * (3f - 2f * t);   // smoothstep — 시작·끝을 부드럽게

        transform.SetPositionAndRotation(
            Vector3.Lerp(_startPosition, _endPosition, t),
            Quaternion.Slerp(_startRotation, _endRotation, t));

        // 연출이 끝나는 시점이 게임 결과 UI를 띄울 자리다.
        // 출력은 IngameScene의 이탈 흐름이 맡는다(CompleteMatchExit의 TODO 참조)
    }
}
