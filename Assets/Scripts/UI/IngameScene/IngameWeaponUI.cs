using TMPro;
using UnityEngine;

public class IngameWeaponUI : MonoBehaviour {
    // TODO: WeaponImage 출력 미구현 — 쓸 이미지가 아직 정해지지 않았다

    TextMeshProUGUI _weaponName;
    TextMeshProUGUI _magazineAmmoCount;
    TextMeshProUGUI _remainAmmoCount;

    public void Init() {
        _weaponName = transform.Find("WeaponUIWindow/Header/WeaponName").GetComponent<TextMeshProUGUI>();
        _magazineAmmoCount = transform.Find("WeaponUIWindow/WeaponInfoPanel/MagazineAmmoCount").GetComponent<TextMeshProUGUI>();
        _remainAmmoCount = transform.Find("WeaponUIWindow/WeaponInfoPanel/RemainAmmoCount").GetComponent<TextMeshProUGUI>();
    }

    // 값은 전부 IngameScene.SyncWeaponUI()가 계산해 넘긴다 — 탄창 선택 규칙을
    // 여기서 다시 판단하면 발사 판정과 표시가 갈린다
    public void SetWeapon(string weaponName, int magazineAmmo, int spareAmmo) {
        if (_weaponName == null) return;

        _weaponName.text = weaponName;
        _magazineAmmoCount.text = magazineAmmo.ToString();
        _remainAmmoCount.text = spareAmmo.ToString();
    }
}
