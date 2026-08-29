using UnityEngine;
using UnityEngine.EventSystems;

public class BaseScene : MonoBehaviour {
    public Define.Scene SceneType { get; protected set; } = Define.Scene.Undefined;

    private void Awake() {
        Init();
    }

    protected virtual void Init() {
        Object obj = GameObject.FindAnyObjectByType(typeof(EventSystem));
        if (obj == null) {
            Debug.Log("Make EventSys");
            Managers.Resource.Instantiate("UI/EventSystem").name = "@EventSyetem";
        }
        else {
            
        }
    }

    // 씬에 배치된 오브젝트를 이름으로 잡는다. 실패를 반드시 드러내는 것이 이 함수의 존재 이유다 —
    // Find + GetComponent를 직접 쓰면 스크립트 미부착이 뒤이은 Init() 호출에서 NRE가 되고,
    // 그 아래 초기화(키 리스너 등록 등)가 통째로 안 돈다
    protected T BindSceneComponent<T>(string objectName) where T : Component {
        GameObject go = GameObject.Find(objectName);
        if (go == null) {
            Util.LogError($"씬에 '{objectName}' 오브젝트가 없다 — 그 기능이 통째로 죽는다 (비활성으로 저장했어도 못 찾는다)");
            return null;
        }

        T component = go.GetComponent<T>();
        if (component == null)
            Util.LogError($"씬의 '{objectName}'에 {typeof(T).Name} 스크립트가 붙어 있지 않다");

        return component;
    }

    public virtual void Clear() { }
}
