using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class UI_Inventory : UI_Scene {
    TestLobbyScene _scene;

    List<InventoryItem> _inventoryItems = new();
    List<ISlot> _iSlots = new();

    public override void Init() {
        base.Init();
        if (isInit) return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene is TestLobbyScene lobbyScene)
            _scene = lobbyScene;

        Transform gridTransform = transform.Find("InventoryWindow/GridPanel/ItemGrid");
        if (gridTransform != null) {
            ISlot[] slots = gridTransform.GetComponentsInChildren<ISlot>(true);
            _iSlots = new List<ISlot>(slots);
        }
        else {
            Util.LogError("[UI_Inventory] ItemGrid 오브젝트를 찾을 수 없습니다.");
        }

        // TODO :
        // 1. islots의 크기만큼 List의 크기를 할당.

        base.OnInitComplete();
    }

    private void RefreshSlots( ) {
        // TODO :
        // 1. _inventoryItems 리스트를 순회하면서 순서대로 슬롯에 아이템 정보를 표시.
        // 2. 슬롯이 비어있는 경우에는 빈 슬롯으로 표시
    }

    private void OnDestroy() {
        
    }
}
