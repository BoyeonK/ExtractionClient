using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LS1LoadingBarController : MonoBehaviour {
    List<GameObject> _Bases = new List<GameObject>();
    List<GameObject> _Loads = new List<GameObject>();

    public void Init(int size) {
        List<Transform> allTransforms = new List<Transform>();
        GetComponentsInChildren<Transform>(true, allTransforms);

        foreach (Transform tr in allTransforms) {
            if (tr == this.transform) continue;
            GameObject obj = tr.gameObject;
            if (obj.name.StartsWith("Load"))
                _Loads.Add(obj);
            else if (obj.name.StartsWith("Base"))
                _Bases.Add(obj);
        }

        _Loads.Sort((a, b) => string.Compare(a.name, b.name));
        _Bases.Sort((a, b) => string.Compare(a.name, b.name));

        for (int i = 0; i < _Loads.Count; i++) {
            SetProgressRate(i, 0);
        }

        if (size < _Loads.Count) {
            for (int i = size; i < _Loads.Count; i++) {
                _Loads[i].SetActive(false);
                _Bases[i].SetActive(false);
            }
        }
    }

    public void SetProgressRate(int playerIdx, float rate) {
        float progress = rate / 9f * 10f;
        _Loads[playerIdx].transform.position = new Vector3(36 - 20 * progress, _Loads[playerIdx].transform.position.y, _Loads[playerIdx].transform.position.z);
        _Loads[playerIdx].transform.localScale = new Vector3(40 * progress, 5, 1);
    }

    void Start() {
        
    }


    void Update() {
        
    }
}
