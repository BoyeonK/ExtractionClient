using System.Collections.Generic;
using UnityEngine;

public class SettingManager {
    int _effectVolume = 50;
    int _bgmVolume = 10;
    float _ingameMouseSensitivity = 1.0f;
    bool _isWindow = true;
    int _masterVolume = 50;
    Define.Resolution _resolution = Define.Resolution._1920x1080;
    Define.FrameRate _frameRate = Define.FrameRate._60;
    int _fov = 60;

    public void Init() {
        GameObject root = GameObject.Find("@Setting");
        if (root == null) {
            root = new GameObject { name = "@Setting" };
            Object.DontDestroyOnLoad(root);
        }

        ApplyFrameRate();
    }

    public void ApplyFrameRate() {
        int fps = _frameRate == Define.FrameRate._30 ? 30 : 60;
        Application.targetFrameRate = fps;
    }

    public void ApplyPreviousSceneSetting() {
        SetVolume(_bgmVolume, Define.Sound.Bgm);
        SetVolume(_effectVolume, Define.Sound.Effect);
    }

    public void SetVolume(int volume, Define.Sound type) {
        int clamped = Mathf.Clamp(volume, 0, 100);

        if (type == Define.Sound.Effect) _effectVolume = clamped;
        else if (type == Define.Sound.Bgm) _bgmVolume = clamped;

        float clampedFloatValue = (clamped / 100f) * (_masterVolume / 100f);
        Managers.Sound.SetVolume(clampedFloatValue, type);
    }

    public int GetBgmVolume() {
        return _bgmVolume;
    }

    public int GetEffectVolume() {
        return _effectVolume;
    }

    public void SetMouseSensitivity(float sensitivity) {
        _ingameMouseSensitivity = sensitivity;
    }

    public float GetMouseSensitivity() {
        return _ingameMouseSensitivity;
    }

    public void SetIsWindow(bool isWindow) {
        _isWindow = isWindow;
    }

    public bool GetIsWindow() {
        return _isWindow;
    }

    public void SetMasterVolume(int volume) {
        _masterVolume = Mathf.Clamp(volume, 0, 100);
        SetVolume(_effectVolume, Define.Sound.Effect);
        SetVolume(_bgmVolume, Define.Sound.Bgm);
    }

    public int GetMasterVolume() {
        return _masterVolume;
    }

    public void SetResolution(Define.Resolution resolution) {
        _resolution = resolution;
    }

    public Define.Resolution GetResolution() {
        return _resolution;
    }

    public void SetFrameRate(Define.FrameRate frameRate) {
        _frameRate = frameRate;
    }

    public Define.FrameRate GetFrameRate() {
        return _frameRate;
    }

    public void SetFov(int fov) {
        _fov = Mathf.Clamp(fov, 60, 90);
    }

    public int GetFov() {
        return _fov;
    }

    public void Clear() {

    }
}
