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
            Debug.Log($"Failed to load prefab : {path}");
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
            Debug.Log($"Failed to instantiate prefab : {path}");
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
