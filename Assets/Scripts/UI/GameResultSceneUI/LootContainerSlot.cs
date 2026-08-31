using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LootContainerSlot : MonoBehaviour {
    public void Init(int itemId, int quantity) {
        Image fillImage = Util.BindComponent<Image>("Fill", this.gameObject);
        TextMeshProUGUI quantityText = Util.BindComponent<TextMeshProUGUI>("Quantity", this.gameObject);

        if (fillImage != null) {
            Sprite icon = Resources.Load<Sprite>($"Images/Items/icon_item_{itemId}");
            // 스프라이트 없는 Image는 흰 사각형으로 그려진다 — 파일 누락이 그대로 화면에 노출된다
            fillImage.enabled = icon != null;
            if (icon != null)
                fillImage.sprite = icon;
        }

        if (quantityText != null) {
            // 무기·방어구는 수량 개념이 없다(ISlot.CanMerge와 같은 술어·같은 이유 — 합산되지 않는 타입이다).
            // 슬롯 출처가 아니라 아이템 타입으로 가르므로 인벤토리 칸에 있던 장비도 같은 규칙을 받는다
            ItemType type = ItemDBHelper.GetType(itemId);
            bool hasQuantity = type != ItemType.Weapon && type != ItemType.Armor;
            quantityText.text = hasQuantity ? quantity.ToString() : "";
        }
    }
}
