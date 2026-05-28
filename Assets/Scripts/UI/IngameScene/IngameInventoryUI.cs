using System.Collections.Generic;
using UnityEngine;

public class IngameInventoryUI : MonoBehaviour {
    IngameScene _scene;

    List<IngameISlot> _slots;
    IngameLSlot _primaryWeaponSlot, _secondaryWeaponSlot, _armorSlot;

    public void Init(IngameScene scene) {
        _scene = scene;

        Transform gridTransform = transform.Find("MyInventoryWindow/GridPanel/ItemGrid");
        if (gridTransform != null) {
            IngameISlot[] slots = gridTransform.GetComponentsInChildren<IngameISlot>(true);
            _slots = new List<IngameISlot>(slots);
        }

        for (int i = 0; i < _slots.Count; i++) {
            //_slots[i].Init(i, _scene);
        }

        _primaryWeaponSlot = transform.Find("MyInventoryWindow/EquipmentPanel/EquipPrimaryWpn")?.GetComponent<IngameLSlot>();
        _secondaryWeaponSlot = transform.Find("MyInventoryWindow/EquipmentPanel/EquipSecondaryWpn")?.GetComponent<IngameLSlot>();
        _armorSlot = transform.Find("MyInventoryWindow/EquipmentPanel/EquipBody")?.GetComponent<IngameLSlot>();

        _primaryWeaponSlot?.Init(_scene, ItemType.Weapon);
        _secondaryWeaponSlot?.Init(_scene, ItemType.Weapon);
        _armorSlot?.Init(_scene, ItemType.Armor);
    }
}
