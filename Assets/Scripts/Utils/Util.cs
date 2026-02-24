using System.Diagnostics;
using UnityEngine;

public class Util {
    #region Custom Logging
    // 에디터에서만 일반 로그 출력
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message) {
        UnityEngine.Debug.Log(message);
    }

    // 에디터에서만 경고 로그 출력
    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message) {
        UnityEngine.Debug.LogWarning(message);
    }

    // 에디터에서만 에러 로그 출력
    [Conditional("UNITY_EDITOR")]
    public static void LogError(object message) {
        UnityEngine.Debug.LogError(message);
    }
    #endregion

    public static T GetOrAddComponent<T>(GameObject go) where T : UnityEngine.Component {
        T component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();
        return component;
    }

    //임의의 GameObject타입인 경우
    public static GameObject FindChild(GameObject go, string name = null, bool recursive = false) {
        Transform transform = FindChild<Transform>(go, name, recursive);
        if (transform == null)
            return null;

        return transform.gameObject;
    }

    //GameObject를 받아서 하위 자식들 중에 <T> type인 것들 중 이름이 name인 친구
    public static T FindChild<T>(GameObject go, string name = null, bool recursive = false) where T : UnityEngine.Object {
        if (go == null)
            return null;
        if (recursive == false) {
            for(int i=0; i<go.transform.childCount; i++) {
                Transform transform = go.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || transform.name == name) {
                    T component = transform.GetComponent<T>();
                    if (component != null)
                        return component;
                }    
            }
        } else {
            foreach(T component in go.GetComponentsInChildren<T>()) {
                if (string.IsNullOrEmpty(name) || component.name == name)
                    return component;
            }
        }

        return null;
    }
}
