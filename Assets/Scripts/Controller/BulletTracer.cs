using UnityEngine;

// 발사 궤적 시각화. 정적 호출이 런타임에 @BulletTracer 리그를 만든다
// (DeathCameraController와 같은 패턴 — GameObjectController 계층이 아니고 프리팹도 없다).
//
// 그리는 선은 총구(MuzzlePoint) → 피탄 지점이고, 피격 판정선은 _shotPoint(가슴팍) → 피탄
// 지점이라 둘은 서로 다른 선이다. 의도된 설계이므로 맞추려 들지 말 것
// (Controller/CLAUDE.md의 '발사선' 참조).
public class BulletTracer : MonoBehaviour {
    // 한 프레임에 나와 여러 오포가 동시에 쏠 수 있어 하나로는 부족하다
    private const int POOL_SIZE = 16;
    private const float LINE_WIDTH = 0.02f;
    private static readonly Color LINE_COLOR = new Color(1f, 0.85f, 0.4f, 1f);

    private static BulletTracer _instance;
    private static Material _sharedMaterial;

    private LineRenderer[] _lines;
    private int[] _spawnFrames;
    private int _next;

    public static void Play(Vector3 from, Vector3 to) {
        // 씬 오브젝트라 씬 전환에 파괴된다. Unity의 == null이 파괴된 오브젝트에도 true라
        // 여기서 자연히 재생성되므로 별도 정리 코드가 필요 없다
        if (_instance == null)
            _instance = new GameObject("@BulletTracer").AddComponent<BulletTracer>();

        _instance.Emit(from, to);
    }

    // 매치마다 새로 만들면 머티리얼이 그만큼 쌓이므로 프로세스당 하나만 만들어 공유한다
    private static Material GetSharedMaterial() {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) {
            Util.LogError("Sprites/Default 셰이더를 찾지 못했다 — 궤적이 마젠타로 보인다. Graphics Settings의 Always Included Shaders 확인 필요");
            return null;
        }

        _sharedMaterial = new Material(shader);
        return _sharedMaterial;
    }

    private void Awake() {
        Material material = GetSharedMaterial();

        _lines = new LineRenderer[POOL_SIZE];
        _spawnFrames = new int[POOL_SIZE];

        for (int i = 0; i < POOL_SIZE; i++) {
            GameObject go = new GameObject($"Tracer_{i}");
            go.transform.SetParent(transform, false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            // material이 아니라 sharedMaterial이다 — material 대입은 렌더러마다 사본을 만들 수 있어
            // 공유하려는 의도가 깨진다
            if (material != null)
                line.sharedMaterial = material;

            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = LINE_WIDTH;
            line.endWidth = LINE_WIDTH;
            line.startColor = LINE_COLOR;
            line.endColor = LINE_COLOR;
            // 얇은 선이 보는 각도에 따라 사라지는 것을 막는다
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            _lines[i] = line;
            _spawnFrames[i] = -1;
        }
    }

    private void Emit(Vector3 from, Vector3 to) {
        LineRenderer line = _lines[_next];
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.enabled = true;
        _spawnFrames[_next] = Time.frameCount;

        _next = (_next + 1) % POOL_SIZE;
    }

    // 끄는 조건은 이 한 곳뿐이다. 지속을 시간 기반 + 페이드로 바꾸게 되면 여기만 고친다.
    //
    // LateUpdate로 옮기지 말 것 — 렌더링보다 먼저 돌아서 선이 한 번도 그려지지 않는다.
    // Update 실행 순서가 발사부보다 앞이든 뒤든 프레임 번호로 비교하므로 결과는 같다
    private void Update() {
        for (int i = 0; i < POOL_SIZE; i++) {
            if (_lines[i].enabled && _spawnFrames[i] != Time.frameCount)
                _lines[i].enabled = false;
        }
    }
}
