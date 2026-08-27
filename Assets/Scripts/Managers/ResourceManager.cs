using System;
using UnityEngine;
using static Define;
using Object = UnityEngine.Object;

public class ResourceManager {
    public T Load<T>(string path) where T : Object {
        return Resources.Load<T>(path);
    }

    public GameObject Instantiate(string path, Transform parent = null) {
        GameObject original = Load<GameObject>($"Prefabs/{path}");
        if (original == null) {
            // 경로 오타나 프리팹 미제작이 여기로 온다. 호출부 대부분이 반환값을 그대로
            // 참조하므로 조용히 넘어가면 원인이 아니라 NRE부터 보게 된다
            Util.LogError($"프리팹 로드 실패 : Prefabs/{path}");
            return null;
        }
        GameObject go = Object.Instantiate(original, parent);
        go.name = original.name;

        return go;
    }

    public GameObject InstantiateFromObjectDataStruct(ObjectData data) {
        Define.ObjectPaths.TryGetValue((int)data.ObjectType, out string path);
        GameObject go = Instantiate(path, null);
        if (go == null) {
            // Define.ObjectPaths에 경로는 있는데 프리팹이 아직 없는 상태다.
            // 매핑 누락(SpawnObject의 LogError)과 달리 여기서 조용히 새면
            // "그 오브젝트만 안 보이는데 로그도 없다"가 되므로 같은 급으로 드러낸다
            Util.LogError($"프리팹 인스턴스화 실패 : {path} (objectType={data.ObjectType})");
            return null;
        }
        GameObjectController controller = go.GetComponent<GameObjectController>();
        controller.SetDataFromStruct(data);
        return go;
    }

    public void Destroy(GameObject go) {
        if (go == null)
            return;

        Object.Destroy(go);
    }
}
