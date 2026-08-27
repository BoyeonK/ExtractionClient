using System.Collections.Generic;
using UnityEngine;

public class IngameKillLogUI : MonoBehaviour {
    IngameScene _scene;
    Transform _killLogContainer;

    Dictionary<int, SingleKillLog> _killLogs = new Dictionary<int, SingleKillLog>();
    int _killLogIdx = 0;

    public void Init(IngameScene scene) {
        _scene = scene;
        _killLogContainer = transform.Find("KillLogContainer");
    }

    public void MakeSingleKillLog(string killerId, string victimId) {
        GameObject killLogObj = Managers.Resource.Instantiate("UI/IngameSceneUI/SingleKillLog", _killLogContainer);
        if (killLogObj == null) return;

        SingleKillLog singleKillLog = killLogObj.GetComponent<SingleKillLog>();

        int idx = _killLogIdx++;
        _killLogs[idx] = singleKillLog;
        singleKillLog.Init(this, idx, killerId, victimId);
    }

    public void RemoveSingleKillLog(int idx) {
        if (_killLogs.TryGetValue(idx, out SingleKillLog singleKillLog)) {
            _killLogs.Remove(idx);
            Managers.Resource.Destroy(singleKillLog.gameObject);
        }
    }

    public void DeactiveThis() {
        gameObject.SetActive(false);
    }
}
