using System.Collections.Generic;
using UnityEngine;

public class IngameInventoryUI : MonoBehaviour {
    IngameScene _scene;

    // MyInventory 영역
    List<IngameISlot> _myInventorySlots;
    IngameLSlot _primaryWeaponSlot, _secondaryWeaponSlot, _armorSlot;
    GameObject MyInventoryGo;

    // Container 영역
    List<IngameISlot> _containerSlots;
    GameObject ContainerGo;

    public void Init(IngameScene scene) {
        _scene = scene;

        Transform myInventoryTr = transform.Find("MyInventoryWindow");
        if (myInventoryTr != null) {
            MyInventoryGo = myInventoryTr.gameObject;
        }

        Transform contanierTr = transform.Find("LootBoxWindow");
        if (contanierTr != null) {
            ContainerGo = contanierTr.gameObject;
        }

        Transform inventoryGridTransform = transform.Find("MyInventoryWindow/GridPanel/ItemGrid");
        if (inventoryGridTransform != null) {
            IngameISlot[] slots = inventoryGridTransform.GetComponentsInChildren<IngameISlot>(true);
            _myInventorySlots = new List<IngameISlot>(slots);
        }

        for (int i = 0; i < _myInventorySlots.Count; i++) {
            _myInventorySlots[i].Init(i, _scene, SlotOwnerType.PlayerInventory);
        }

        Transform containerGridTransform = transform.Find("LootBoxWindow/GridPanel/ItemGrid");
        if (containerGridTransform != null) {
            IngameISlot[] slots = containerGridTransform.GetComponentsInChildren<IngameISlot>(true);
            _containerSlots = new List<IngameISlot>(slots);
        }

        for (int i = 0; i < _containerSlots.Count; i++) {
            _containerSlots[i].Init(i, _scene, SlotOwnerType.Container);
        }

        _primaryWeaponSlot = transform.Find("MyInventoryWindow/EquipmentPanel/EquipPrimaryWpn")?.GetComponent<IngameLSlot>();
        _secondaryWeaponSlot = transform.Find("MyInventoryWindow/EquipmentPanel/EquipSecondaryWpn")?.GetComponent<IngameLSlot>();
        _armorSlot = transform.Find("MyInventoryWindow/EquipmentPanel/EquipBody")?.GetComponent<IngameLSlot>();

        _primaryWeaponSlot?.Init(_scene, ItemType.Weapon, 0);
        _secondaryWeaponSlot?.Init(_scene, ItemType.Weapon, 1);
        _armorSlot?.Init(_scene, ItemType.Armor, 2);

        DeactiveThis();
    }

    public void SyncMyInventory() {
        InventoryItem[] items = _scene.Inventory.InventorySlots;
        for (int i = 0; i < _myInventorySlots.Count; i++) {
            if (i < items.Length && items[i] != null)
                _myInventorySlots[i].SetItem(items[i]);
            else
                _myInventorySlots[i].ClearSlot();
        }
    }

    public void SyncEquipment() {
        SyncOneEquipSlot(_primaryWeaponSlot, _scene.Inventory.PrimaryWeapon);
        SyncOneEquipSlot(_secondaryWeaponSlot, _scene.Inventory.SecondaryWeapon);
        SyncOneEquipSlot(_armorSlot, _scene.Inventory.Armor);
    }

    private void SyncOneEquipSlot(IngameLSlot slot, InventoryItem item) {
        if (slot == null) return;
        if (item != null)
            slot.SetItem(item);
        else
            slot.ClearSlot();
    }

    public void SyncContainer() {
        InventoryItem[] items = _scene.Inventory.InteractingContainerSlots;
        for (int i = 0; i < _containerSlots.Count; i++) {
            if (i < items.Length && items[i] != null)
                _containerSlots[i].SetItem(items[i]);
            else
                _containerSlots[i].ClearSlot();
        }
    }

    public void ActiveMyInventory() {
        gameObject.SetActive(true);
        MyInventoryGo.SetActive(true);
        ContainerGo.SetActive(false);
    }

    public void ActiveLootBox() {
        gameObject.SetActive(true);
        MyInventoryGo.SetActive(true);
        ContainerGo.SetActive(true);
    }

    public void DeactiveThis() {
        gameObject.SetActive(false);
    }
}
