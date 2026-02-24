using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LS1TextUIController : MonoBehaviour {
    TextMeshProUGUI _title;
    List<TextMeshProUGUI> _playerIds = new List<TextMeshProUGUI>();

    public void Init(int gameId, List<string> playerIds) {
        List<TextMeshProUGUI> allTexts = new List<TextMeshProUGUI>();
        GetComponentsInChildren<TextMeshProUGUI>(true, allTexts);
        foreach (TextMeshProUGUI text in allTexts) {
            if (text.name.StartsWith("PlayerId"))
                _playerIds.Add(text);
            else if (text.name.StartsWith("Title"))
                _title = text;
        }
        _playerIds.Sort((a, b) => string.Compare(a.name, b.name));

        SetTitle(gameId);
        SetPlayerIds(playerIds);
    }

    private void SetTitle(int gameId) {
        if (_title != null) {
            string title = "";
            switch (gameId) {
                case 1:
                    title = "테스트용게임";
                    break;
                case 2:
                    title = "핑퐁";
                    break;
                case 3:
                    title = "두더지잡기";
                    break;
                case 4:
                    title = "탄막피하기";
                    break;
                default:
                    break;
            }
            _title.text = title;
        }
    }

    private void SetPlayerIds(List<string> playerIds) {
        int sizeA = _playerIds.Count;
        int sizeB = playerIds.Count;
        if (sizeA < sizeB) { return; }

        for (int i = 0; i < sizeB; i++) {
            _playerIds[i].text = playerIds[i];
        }

        for (int i = sizeB; i < sizeA; i++) {
            _playerIds[i].text = "";
        }
    }

    void Update() {
        
    }
}
