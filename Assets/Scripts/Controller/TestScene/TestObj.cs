using UnityEngine;

// 조준 추적 실험용 표적. 축마다 다른 주기로 왕복한다 — 주기가 같거나 약분되면
// 궤적이 직선이나 짧은 닫힌 곡선이 되어 한 방향의 추적만 검사하게 된다
public class TestObj : MonoBehaviour {
    [SerializeField] private Vector2 _xRange = new Vector2(-2f, 2f);
    [SerializeField] private Vector2 _yRange = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 _zRange = new Vector2(6f, 8f);

    // 축별 초당 라디안. 왕복 주기는 2π/값 초(약 6.3 / 17.0 / 10.0초)
    [SerializeField] private Vector3 _speed = new Vector3(1f, 0.37f, 0.63f);

    void Update() {
        transform.position = new Vector3(
            Oscillate(_xRange, _speed.x),
            Oscillate(_yRange, _speed.y),
            Oscillate(_zRange, _speed.z));
    }

    private static float Oscillate(Vector2 range, float speed) =>
        Mathf.Lerp(range.x, range.y, (Mathf.Sin(Time.time * speed) + 1f) * 0.5f);
}
