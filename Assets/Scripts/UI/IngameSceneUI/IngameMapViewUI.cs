using System.Collections;
using UnityEngine;

public class IngameMapViewUI : MonoBehaviour {
    // 지도 그림을 만드는 탑뷰 카메라. 여는 시점에 한 프레임만 켜서 RenderTexture를 갱신한다 —
    // 켜둔 채로 두면 지도를 보지 않는 동안에도 매 프레임 씬을 한 번 더 그린다
    [SerializeField] Camera _mapCamera;

    IngameScene _scene;

    bool _isOpen = false;
    public bool IsOpen => _isOpen;

    public void Init(IngameScene scene) {
        _scene = scene;

        if (_mapCamera == null)
            Util.LogError("IngameMapViewUI에 탑뷰 카메라가 물려 있지 않다 — 지도가 갱신되지 않는다");
        else
            _mapCamera.enabled = false;

        // 씬에는 활성으로 저장하고 여기서 끈다 — GameObject.Find는 비활성을 못 찾는다
        DeactiveThis();
    }

    // 촬영이 여는 순간의 한 프레임뿐이라, 연 채로 움직이면 위치 마커가 그 자리에 멈춰 있다.
    // 지도를 보며 이동하는 것이 의도가 아니라서 감수한 동작이다(결함이 아니다)
    public void Show() {
        if (_isOpen) return;

        _isOpen = true;
        // 코루틴을 돌리려면 먼저 활성이어야 하므로 촬영보다 앞이다
        gameObject.SetActive(true);
        CaptureMap();
        _scene.OnUIOpened();
    }

    public void Hide() {
        if (!_isOpen) return;

        _isOpen = false;
        // 촬영 코루틴이 끝나기 전에 오브젝트가 꺼지면 코루틴이 죽어 카메라가 켜진 채 남는다
        if (_mapCamera != null)
            _mapCamera.enabled = false;
        DeactiveThis();
        _scene.OnUIClosed();
    }

    public void DeactiveThis() {
        gameObject.SetActive(false);
    }

    private void CaptureMap() {
        if (_mapCamera == null) return;

        _mapCamera.enabled = true;
        StartCoroutine(DisableCameraAfterRender());
    }

    // Camera.Render()를 직접 부르지 않는 것은 URP(SRP)에서 지원되지 않기 때문이다.
    // 한 프레임만 켜두면 같은 결과를 얻으면서 SRP 전용 API를 끌어오지 않는다
    private IEnumerator DisableCameraAfterRender() {
        yield return new WaitForEndOfFrame();

        if (_mapCamera != null)
            _mapCamera.enabled = false;
    }
}
