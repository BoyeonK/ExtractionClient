using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class UIManager {
    //대전제 : 같은 이름을 가지는 UI는 만들면 안 된다.
           //: SceneUI의 개수는 합해도 20이 넘지 않으며, PopupUI는 SceneUI보다 앞에 출력된다.
    private Dictionary<string, GameObject> _uiCache = new Dictionary<string, GameObject>();

    private List<UI_Popup> _popups = new List<UI_Popup>();

    private int _sceneOrder = 0;
    private int _popupOrder = 20;
    private Transform _root = null;

    public void Init() {
        if (_root == null) {
            GameObject go = GameObject.Find("@UI_Root");
            if (go == null)
                go = new GameObject { name = "@UI_Root" };
            _root = go.transform;
            UnityEngine.Object.DontDestroyOnLoad(_root.gameObject);
        }
    }

    //name -> prefab의 이름
    //name이라는 이름을 가진 UI_Popup에 해당하는 GameObject를 생성하고 <T> 컴포넌트를 부착.
    public T ShowPopupUI<T>(string name = null) where T : UI_Popup {
        Init();

        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        T popup = null;
        GameObject go = null;

        if (_uiCache.TryGetValue(name, out GameObject ext)) {
            go = ext;
            go.SetActive(true);
            popup = go.GetComponent<T>();
        }
        else {
            go = Managers.Resource.Instantiate($"UI/Popup/{name}");
            popup = Util.GetOrAddComponent<T>(go);

            
            go.transform.SetParent(_root.transform);
            _uiCache.Add(name, go);
        }

        popup.Init();

        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        FocusPopupUI(popup);

        return popup;
    }

    //name이라는 이름을 가진 UI_Scene에 해당하는 GameObject를 생성하고 <T> 컴포넌트를 부착.
    public T ShowSceneUI<T>(string name = null) where T : UI_Scene {
        Init();

        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        if (_uiCache.TryGetValue(name, out GameObject ext)) {
            ext.SetActive(true);
            T cachedSceneUI = ext.GetComponent<T>();

            cachedSceneUI.Init();

            return cachedSceneUI;
        }

        GameObject go = Managers.Resource.Instantiate($"UI/Scene/{name}");
        T sceneUI = Util.GetOrAddComponent<T>(go);
        _uiCache.Add(name, go);

        go.transform.SetParent(_root.transform);

        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        int newOrder = Interlocked.Increment(ref _sceneOrder);
        canvas.sortingOrder = newOrder;

        sceneUI.Init();

        return sceneUI;
    }

    //이미 존재하는 UI를 잠시 SetActive(false)
    public void DisableUI(string uiName) {
        //딕셔너리 컨테이너에서, uiName에 해당하는 object의 포인터를 꺼낸다.
        //그리고 Active값을 변경
        if (_uiCache.TryGetValue(uiName, out GameObject uiObj)) {
            if (uiObj != null)
                uiObj.SetActive(false);
            else 
                _uiCache.Remove(uiName);
        }
    }

    //이미 존재하면서, SetActive(false)인 UI를 enable
    public void EnableUI(string uiName) {
        if (_uiCache.TryGetValue(uiName, out GameObject uiObj))
            uiObj.SetActive(true);
    }

    public T CacheSceneUI<T>() where T : UI_Scene {
        T sceneUI = ShowSceneUI<T>();
        DisableUI(typeof(T).Name);
        return sceneUI;
    }

    public T CachePopupUI<T>() where T : UI_Popup {
        T popupUI = ShowPopupUI<T>();
        DisableUI(typeof(T).Name);
        return popupUI;
    }

    public void Clear() {
        //캐시를 비우고, sortOrder를 정상화.
        foreach (var pair in _uiCache) {
            Managers.Resource.Destroy(pair.Value);
        }
        _uiCache.Clear();
        _popups.Clear();
        _sceneOrder = 0;
        _popupOrder = 20;
    }

    public void ClosePopupUI(UI_Popup popup) {
        if (_popups.Count == 0) return;

        if (_popups.Remove(popup)) {
            DisableUI(popup.gameObject.name);

            if (_popups.Count == 0) {
                _popupOrder = 20;
            }
        }
    }

    public void FocusPopupUI(UI_Popup popup) {
        if (popup.is_forcusable == false) {
            return;
        }

        if (_popups.Contains(popup)) {
            _popups.Remove(popup);
        }

        Canvas canvas = Util.GetOrAddComponent<Canvas>(popup.gameObject);
        int newOrder = Interlocked.Increment(ref _popupOrder);
        canvas.sortingOrder = newOrder;

        _popups.Add(popup);
    }

    /*
    public UI_ErrorOnlyConfirm ShowErrorUIOnlyConfirm(string errorDetail, Action confirmOnClickEvent = null) {
        //에러 발생 UI는 캐싱하지 않음.
        //정상적인 상황은 아니기 때문에 보수적으로 접근한다.
        GameObject go = Managers.Resource.Instantiate("UI/Popup/UI_ErrorOnlyConfirm");
        UI_ErrorOnlyConfirm uiError = Util.GetOrAddComponent<UI_ErrorOnlyConfirm>(go);

        Init();
        go.transform.SetParent(_root.transform);

        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        //최상단에 위치하도록
        int order = _popupOrder + 1000;
        canvas.sortingOrder = order;

        //에러 내용 + 확인 버튼 클릭시 추가적으로 실행할 함수를 담아 초기화.
        if (uiError != null) {
            uiError.Init(errorDetail, confirmOnClickEvent);
        }

        return uiError;
    }

    public UI_ErrorConfirmOrCancel ShowErrorUIConfirmOrCancel(string errorDetail, Action confirmOnClickEvent = null, Action cancelOnClickEvent = null) {
        GameObject go = Managers.Resource.Instantiate("UI/Popup/UI_ErrorConfirmOrCancel");
        UI_ErrorConfirmOrCancel uiError = Util.GetOrAddComponent<UI_ErrorConfirmOrCancel>(go);

        Init();
        go.transform.SetParent(_root.transform);

        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;

        //최상단에 위치하도록
        int order = _popupOrder + 1000;
        canvas.sortingOrder = order;

        //에러 내용 + 각 버튼 클릭시 실행할 함수
        if (uiError != null) {
            uiError.Init(errorDetail, confirmOnClickEvent, cancelOnClickEvent);
        }

        return uiError;
    }
    */
}
