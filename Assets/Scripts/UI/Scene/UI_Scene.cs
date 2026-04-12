using UnityEngine;

public class UI_Scene : UI_Base {
    public virtual void Init() {

    }

    public virtual void OnInitComplete() {
        isInit = true;
    }

    // 드래그 앤 드롭: 특정 슬롯 인덱스의 아이템을 교체 (null = 빈 슬롯)
    public virtual void SetItemAtSlot(int slotIndex, InventoryItem item) { }
}
