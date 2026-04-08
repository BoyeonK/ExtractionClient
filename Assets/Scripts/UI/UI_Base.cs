using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Base : MonoBehaviour {
    protected bool isInit = false;

    protected T BindComponent<T>(string path) where T : UnityEngine.Component {
        Transform t = transform.Find(path);
        if (t != null) {
            T component = t.GetComponent<T>();
            if (component != null) {
                return component;
            }
            Util.LogError($"[{gameObject.name}] '{path}' 오브젝트는 찾았지만, {typeof(T).Name} 컴포넌트가 없습니다!");
            return null;
        } 
        
        Util.LogError($"[{gameObject.name}] '{path}' 경로의 오브젝트를 아예 찾을 수 없습니다!");
        return null;
    }
}
