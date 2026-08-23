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

    // 씬 전환은 잡큐로 예약해 다음 프레임에 수행한다 — 키 리스너 안에서 바로 부르면
    // LoadScene()의 Managers.Clear()가 InputManager의 순회 중인 _keyActions를 비워 예외가 난다
    public void MoveToLobby() {
        Managers.Scene.IsReturnFromGameResult = true;
        Managers.Scene.ClearGameResult();
        Managers.ExecuteAtMainThread(() => Managers.Scene.LoadScene(Define.Scene.LobbyScene));
    }

    private void OnDestroy() {
        // 종료 중에는 OnApplicationQuit이 먼저 돌아 Managers.Instance가 null이다
        if (Managers.Instance == null) return;
        Managers.Input.RemoveKeyListener(Key.Enter, OnEnterInput, InputManager.KeyState.Up);
    }
}
