using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameWeaponUI : MonoBehaviour {
    TextMeshProUGUI _weaponName;
    TextMeshProUGUI _magazineAmmoCount;
    TextMeshProUGUI _remainAmmoCount;
    Image _weaponImage;

    // 직전에 그린 무기. 0(맨손)도 실재하는 값이라 초기값은 -1이어야 첫 갱신이 통과한다
    int _shownWeaponId = -1;

    public void Init() {
        _weaponName = transform.Find("WeaponUIWindow/Header/WeaponName").GetComponent<TextMeshProUGUI>();
        _magazineAmmoCount = transform.Find("WeaponUIWindow/WeaponInfoPanel/MagazineAmmoCount").GetComponent<TextMeshProUGUI>();
        _remainAmmoCount = transform.Find("WeaponUIWindow/WeaponInfoPanel/RemainAmmoCount").GetComponent<TextMeshProUGUI>();
        _weaponImage = transform.Find("WeaponUIWindow/WeaponInfoPanel/WeaponImage").GetComponent<Image>();
    }

    // 값은 전부 IngameScene.SyncWeaponUI()가 계산해 넘긴다 — 탄창 선택 규칙을
    // 여기서 다시 판단하면 발사 판정과 표시가 갈린다
    public void SetWeapon(string weaponName, int weaponId, int magazineAmmo, int spareAmmo) {
        if (_weaponName == null) return;

        _weaponName.text = weaponName;
        _magazineAmmoCount.text = magazineAmmo.ToString();
        _remainAmmoCount.text = spareAmmo.ToString();

        SyncWeaponImage(weaponId);
    }

    // weaponId가 바뀔 때만 로드한다. SetWeapon()은 발사마다 불리고, 이 값의 출처는
    // 서버가 확정한 '손에 든 무기' 하나뿐이라 가드를 통과하는 시점이 곧 교체 확정 시점이다
    void SyncWeaponImage(int weaponId) {
        if (_weaponImage == null || weaponId == _shownWeaponId) return;
        _shownWeaponId = weaponId;

        Sprite sprite = weaponId != 0
            ? Resources.Load<Sprite>($"Images/WeaponSprites/weapon_sprite_{weaponId}")
            : null;

        _weaponImage.sprite = sprite;
        // 스프라이트 없는 Image는 흰 사각형으로 그려진다. 맨손과 파일 누락이 같은 처리로 덮인다
        _weaponImage.enabled = sprite != null;
    }
}
