using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SingleKillLog : MonoBehaviour {
    // 한 줄이 화면에 남는 시간
    private const float LIFETIME_SEC = 5f;

    IngameKillLogUI _parentUI;
    int _idx = 0;

    TextMeshProUGUI _killerId;
    TextMeshProUGUI _victimId;

    public void Init(IngameKillLogUI parentUI, int idx, string killerId, string victimId) {
        _parentUI = parentUI;
        _idx = idx;
        _killerId = transform.Find("KillerId").GetComponent<TextMeshProUGUI>();
        _victimId = transform.Find("VictimId").GetComponent<TextMeshProUGUI>();

        _killerId.text = killerId;
        _victimId.text = victimId;

        Invoke(nameof(RemoveSelf), LIFETIME_SEC);
    }

    private void RemoveSelf() {
        if (_parentUI == null) return;
        _parentUI.RemoveSingleKillLog(_idx);
    }
}
