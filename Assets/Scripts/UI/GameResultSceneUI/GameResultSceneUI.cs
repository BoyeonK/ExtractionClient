using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameResultSceneUI : MonoBehaviour {
    const float SUMMARY_DELAY = 0.3f;
    const float LOOT_DELAY = 0.3f;
    const float SLOT_INTERVAL = 0.2f;
    const float CONFIRM_DELAY = 0.3f;

    static readonly Color RECALLED_COLOR = new Color(0.44f, 0.80f, 1.00f);
    static readonly Color DEAD_COLOR = new Color(0.90f, 0.28f, 0.28f);

    GameResultScene _scene;
    TextMeshProUGUI _gameResultText;
    Transform _lootContainer;
    TextMeshProUGUI _lostItemText;
    Button _confirmButton;

    bool _hasResult;
    bool _isExtractSuccess;
    MatchExitReason _exitReason;
    List<InventoryItem> _loots = new List<InventoryItem>();
    int _spawnedCount;
    Coroutine _sequence;

    // Enter 잠금의 단일 근거다. 씬이 별도 플래그를 들면 "버튼은 떴는데 Enter가 안 먹는" 상태가 생긴다
    public bool IsConfirmActive => _confirmButton != null && _confirmButton.gameObject.activeSelf;

    // 결과를 참조로 들고 매 단계 다시 읽지 않고 여기서 값으로 뜬다 — MoveToLobby()가 ClearGameResult()를
    // 즉시 부르고 LoadScene만 다음 프레임으로 미루므로, 연출이 도는 중에 원본이 null이 되는 창이 실재한다
    public void Init(GameResultScene scene, GameResult? result) {
        _scene = scene;

        _gameResultText = Util.BindComponent<TextMeshProUGUI>("GameResultSummary/GameResultText", this.gameObject);
        _lostItemText = Util.BindComponent<TextMeshProUGUI>("LootLists/LostItemsText", this.gameObject);
        _confirmButton = Util.BindComponent<Button>("BottomBar/ConfirmButton", this.gameObject);
        _lootContainer = transform.Find("LootLists/LootContainer");
        if (_lootContainer == null)
            Util.LogError($"[{name}] 'LootLists/LootContainer'가 없다 — 전리품이 하나도 그려지지 않는다");

        if (_gameResultText != null)
            _gameResultText.text = "";

        if (_lostItemText != null)
            _lostItemText.gameObject.SetActive(false);

        if (_confirmButton != null) {
            _confirmButton.onClick.AddListener(OnClickConfirm);
            _confirmButton.gameObject.SetActive(false);
        }

        if (result == null) {
            // CompleteMatchExit()을 우회해 들어온 경우다. 연출을 돌리지 않으면 확인 버튼이 영영 켜지지 않아
            // 로비로 돌아갈 수단이 사라진다
            CompleteImmediately();
            return;
        }

        _hasResult = true;
        _exitReason = result.Value.ExitReason;
        ClassifyResult();

        // 실패면 목록을 만들지 않는다 — 생성 루프 둘(코루틴·CompleteImmediately)이 자연히 비어
        // 갈래 가드를 두 곳에 달 필요가 없어진다. 가드를 추가하지 말고 목록으로 가를 것
        if (_isExtractSuccess)
            BuildLootList(result.Value);

        _sequence = StartCoroutine(PlayResultSequence());
    }

    // 문구·색과 전리품/분실 갈래가 같은 술어를 쓰게 하는 유일한 분류기다 —
    // 갈래를 따로 두면 사유가 추가될 때 "탈출 성공"인데 분실 문구가 뜨는 상태가 나온다
    private void ClassifyResult() {
        switch (_exitReason) {
            case MatchExitReason.Recalled:
                _isExtractSuccess = true;
                break;
            case MatchExitReason.Dead:
            case MatchExitReason.ConnectionLost:
                _isExtractSuccess = false;
                break;
            default:
                // ConnectionLost를 default에 묻어두지 않는 이유가 여기다 — 사유가 추가되면
                // 조용히 사망으로 처리되는 대신 콘솔에 드러난다
                Util.LogError($"[GameResult] 표시 규칙이 없는 이탈 사유: {_exitReason}");
                _isExtractSuccess = false;
                break;
        }
    }

    // 순서를 먼저 확정해 둔다 — 루프 안에서 갈래를 판단하면 타이머와 갈래 판정이 엉키고,
    // CompleteImmediately()가 '남은 것부터 끝까지'를 표현할 수 없게 된다
    private void BuildLootList(GameResult r) {
        _loots.Clear();
        AddLoot(r.PrimaryWeapon);
        AddLoot(r.SecondaryWeapon);
        AddLoot(r.Armor);

        if (r.InventorySlots == null) return;
        for (int i = 0; i < r.InventorySlots.Length; i++)
            AddLoot(r.InventorySlots[i]);
    }

    private void AddLoot(InventoryItem item) {
        if (item != null)
            _loots.Add(item);
    }

    private IEnumerator PlayResultSequence() {
        yield return new WaitForSeconds(SUMMARY_DELAY);
        ShowResultSummary();

        yield return new WaitForSeconds(LOOT_DELAY);
        ShowLostItemNotice();
        while (_spawnedCount < _loots.Count) {
            SpawnNextLootSlot();
            if (_spawnedCount < _loots.Count)
                yield return new WaitForSeconds(SLOT_INTERVAL);
        }

        yield return new WaitForSeconds(CONFIRM_DELAY);
        ActiveConfirmButton();
        _sequence = null;
    }

    // 연출을 끊고 최종 상태로 점프하는 유일한 지점이다. 지금 소비자는 '결과 없이 진입'뿐이지만
    // 나중에 붙을 '연출 건너뛰기'도 여기를 부르면 된다 — 단계마다 중단 분기를 흩지 말 것.
    // 코루틴과 이 함수가 같은 단계 메서드를 부르는 것이 두 경로를 갈리지 않게 하는 방식이다
    public void CompleteImmediately() {
        if (_sequence != null) {
            StopCoroutine(_sequence);
            _sequence = null;
        }

        ShowResultSummary();
        ShowLostItemNotice();
        while (_spawnedCount < _loots.Count)
            SpawnNextLootSlot();
        ActiveConfirmButton();
    }

    private void ShowResultSummary() {
        if (_gameResultText == null || _hasResult == false) return;

        _gameResultText.text = _isExtractSuccess ? "탈출 성공" : "사망";
        _gameResultText.color = _isExtractSuccess ? RECALLED_COLOR : DEAD_COLOR;
    }

    // 전리품 생성이 시작될 자리에서 그것을 대신한다. 요약 문구와 함께 켜면 시점이 어긋난다.
    // 결과 없이 진입한 경로에서는 켜지 않는다 — 잃은 것이 없는데 잃었다고 표시하는 꼴이 된다
    private void ShowLostItemNotice() {
        if (_lostItemText == null || _hasResult == false || _isExtractSuccess) return;
        _lostItemText.gameObject.SetActive(true);
    }

    private void SpawnNextLootSlot() {
        // 카운터를 먼저 올린다 — 아래에서 조기 반환해도 CompleteImmediately()의 루프가 전진해야 한다
        InventoryItem item = _loots[_spawnedCount++];
        if (_lootContainer == null) return;

        GameObject slotObj = Managers.Resource.Instantiate("UI/GameResultSceneUI/LootContainerSlot", _lootContainer);
        if (slotObj == null) return;

        LootContainerSlot slot = slotObj.GetComponent<LootContainerSlot>();
        if (slot == null) {
            Util.LogError("LootContainerSlot 프리팹에 LootContainerSlot 스크립트가 붙어 있지 않다");
            return;
        }

        slot.Init(item.item_id, item.quantity);
    }

    private void ActiveConfirmButton() {
        if (_confirmButton == null) return;
        _confirmButton.gameObject.SetActive(true);
    }

    // 씬 전환은 반드시 MoveToLobby()를 거친다 — LoadScene을 직접 부르면 Managers.Clear()가
    // UI 이벤트 순회 중에 돈다. Enter 경로와 같은 이유다
    private void OnClickConfirm() {
        if (_scene != null)
            _scene.MoveToLobby();
    }

    private void OnDestroy() {
        if (_confirmButton != null)
            _confirmButton.onClick.RemoveAllListeners();
    }
}
