using UnityEngine;
using UnityEngine.InputSystem;

public class GameResultScene : BaseScene {
    protected override void Init() {
        base.Init();
        SceneType = Define.Scene.GameResultScene;
        Managers.Scene.ResetLoadSceneOp();

        LogGameResult();

        Managers.Input.AddKeyListener(Key.Enter, OnEnterInput, InputManager.KeyState.Up);
    }

    // TODO: 결과 표시 UI가 생기면 로그 출력을 UI 바인딩으로 교체
    private void LogGameResult() {
        GameResult? result = Managers.Scene.LastGameResult;
        if (result == null) {
            Util.LogWarning("[GameResult] 저장된 게임 결과가 없다 — CompleteMatchExit()을 거치지 않고 진입했다");
            return;
        }

        GameResult r = result.Value;
        Util.Log($"[GameResult] 이탈 사유: {r.ExitReason}");
        Util.Log($"[GameResult] 킬수 — 플레이어: {r.PlayerKillCount}, 오브젝트: {r.ObjectKillCount}");
        Util.Log($"[GameResult] 주무기: {ItemToString(r.PrimaryWeapon)}, 보조무기: {ItemToString(r.SecondaryWeapon)}, 방어구: {ItemToString(r.Armor)}");
        for (int i = 0; i < r.InventorySlots.Length; i++) {
            if (r.InventorySlots[i] == null) continue;
            Util.Log($"[GameResult] 인벤토리 슬롯 {i}: {ItemToString(r.InventorySlots[i])}");
        }
    }

    private string ItemToString(InventoryItem item) {
        if (item == null) return "없음";
        return $"itemId={item.item_id} x{item.quantity}";
    }

    private void OnEnterInput() {
        MoveToLobby();
    }

    public void MoveToLobby() {
        Managers.Scene.IsReturnFromGameResult = true;
        Managers.Scene.ClearGameResult();
        Managers.Scene.LoadScene(Define.Scene.LobbyScene);
    }

    private void OnDestroy() {
        Managers.Input.RemoveKeyListener(Key.Enter, OnEnterInput, InputManager.KeyState.Up);
    }
}
