using System.Collections.Generic;
using UnityEngine;

public class SettingManager {
    int _effectVolume = 50;
    int _bgmVolume = 10;

    public void Init() {
        GameObject root = GameObject.Find("@Setting");
        if (root == null) {
            root = new GameObject { name = "@Setting" };
            Object.DontDestroyOnLoad(root);
        }
    }

    public void ApplyPreviousSceneSetting() {
        SetVolume(_bgmVolume, Define.Sound.Bgm);
        SetVolume(_effectVolume, Define.Sound.Effect);
    }

    public void SetVolume(int volume, Define.Sound type) {
        int clamped = Mathf.Clamp(volume, 0, 100);

        if (type == Define.Sound.Effect) _effectVolume = clamped;
        else if (type == Define.Sound.Bgm) _bgmVolume = clamped;

        float clampedFloatValue = clamped / 100f;
        Managers.Sound.SetVolume(clampedFloatValue, type);
    }

    public int GetBgmVolume() {
        return _bgmVolume;
    }

    public int GetEffectVolume() {
        return _effectVolume;
    }

    public void Clear() {

    }
}
