using System;
using System.Collections.Generic;
using UnityEngine;

public class SettingManager {
    // ── 영속화 키 ──────────────────────────────────────
    // 한 곳에 모아 둔다. 흩어지면 오타 하나로 그 항목만 조용히 기본값으로 돌아간다.
    // 키 이름을 바꾸면 이미 저장된 값은 읽히지 않고 버려진다
    private const string KEY_EFFECT_VOLUME = "Setting.EffectVolume";
    private const string KEY_BGM_VOLUME    = "Setting.BgmVolume";
    private const string KEY_MASTER_VOLUME = "Setting.MasterVolume";
    private const string KEY_SENSITIVITY   = "Setting.MouseSensitivity";
    private const string KEY_IS_WINDOW     = "Setting.IsWindow";
    private const string KEY_RESOLUTION    = "Setting.Resolution";
    private const string KEY_FRAME_RATE    = "Setting.FrameRate";
    private const string KEY_FOV           = "Setting.Fov";
    private const string KEY_VSYNC         = "Setting.VSync";

    // 슬라이더 Min/Max와 손으로 맞추는 값이다. 여기만 넓히면 슬라이더로는 못 넣는 값이
    // 저장 파일 편집으로 들어올 수 있고, 슬라이더만 넓히면 조작이 이 범위에서 잘린다
    public const float MIN_MOUSE_SENSITIVITY = 0.1f;
    public const float MAX_MOUSE_SENSITIVITY = 5.0f;

    int _effectVolume = 50;
    int _bgmVolume = 10;
    float _ingameMouseSensitivity = 1.0f;
    bool _isWindow = true;
    int _masterVolume = 50;
    Define.Resolution _resolution = Define.Resolution._1920x1080;
    Define.FrameRate _frameRate = Define.FrameRate._60;
    int _fov = 60;
    bool _isVSync = false;

    public void Init() {
        GameObject root = GameObject.Find("@Setting");
        if (root == null) {
            root = new GameObject { name = "@Setting" };
            // using System 이 들어와 Object 가 System.Object 와 모호해진다. 한정을 풀지 말 것
            UnityEngine.Object.DontDestroyOnLoad(root);
        }

        // Apply 계열보다 먼저 와야 한다 — 순서가 뒤집히면 저장된 값이
        // 필드에만 들어가고 실제 적용은 기본값으로 이뤄진다
        Load();

        ApplyFrameRateAndVSync();
        ApplyResolution();
    }

    // ==========================================
    // 영속화
    // ==========================================

    // 저장된 값을 필드에 직접 넣지 않고 전부 setter를 통과시킨다 —
    // 범위 검증(Clamp)이 setter에 있어서, 저장 파일이 손상되거나 손으로 편집된 값이
    // 그대로 들어오는 것을 막는다. PlayerPrefs는 사용자가 편집할 수 있는 영역이다
    private void Load() {
        // 마스터 볼륨을 먼저 넣는다 — SetVolume()이 (볼륨 x 마스터)로 실제 음량을 계산하므로,
        // 순서가 뒤집히면 효과음·BGM이 기본 마스터 기준으로 한 번 잘못 적용된다
        SetMasterVolume(PlayerPrefs.GetInt(KEY_MASTER_VOLUME, _masterVolume));
        SetVolume(PlayerPrefs.GetInt(KEY_EFFECT_VOLUME, _effectVolume), Define.Sound.Effect);
        SetVolume(PlayerPrefs.GetInt(KEY_BGM_VOLUME, _bgmVolume), Define.Sound.Bgm);

        SetMouseSensitivity(PlayerPrefs.GetFloat(KEY_SENSITIVITY, _ingameMouseSensitivity));
        SetIsWindow(PlayerPrefs.GetInt(KEY_IS_WINDOW, _isWindow ? 1 : 0) != 0);
        SetFov(PlayerPrefs.GetInt(KEY_FOV, _fov));
        SetIsVSync(PlayerPrefs.GetInt(KEY_VSYNC, _isVSync ? 1 : 0) != 0);

        SetResolution(LoadEnum(KEY_RESOLUTION, _resolution, Define.Resolution.MaxCount));
        SetFrameRate(LoadEnum(KEY_FRAME_RATE, _frameRate, Define.FrameRate.MaxCount));
    }

    // enum은 정수가 아니라 '이름'으로 저장한다. Define.Resolution이 오름차순으로 정렬돼 있어
    // 새 해상도가 목록 중간에 삽입되기 쉬운데, 정수로 저장하면 그 순간 이미 저장된 값이
    // 다른 항목을 가리키게 된다 — 증상이 "설정이 멋대로 바뀐다"라 원인을 짚기 어렵다.
    // MaxCount는 실재하는 설정이 아니므로 함께 걸러낸다(Define.ResolutionValues 조회가 실패한다)
    private static T LoadEnum<T>(string key, T fallback, T maxCount) where T : struct, Enum {
        string saved = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(saved)) return fallback;

        if (!Enum.TryParse(saved, out T parsed)) {
            Util.LogWarning($"[Setting] 알 수 없는 {typeof(T).Name} 값 '{saved}' — 기본값으로 되돌린다");
            return fallback;
        }

        if (Convert.ToInt32(parsed) >= Convert.ToInt32(maxCount)) {
            Util.LogWarning($"[Setting] 범위를 벗어난 {typeof(T).Name} 값 '{saved}' — 기본값으로 되돌린다");
            return fallback;
        }

        return parsed;
    }

    // 메모리에 쌓인 값을 실제 로컬 저장소에 기록한다(동기 I/O).
    // 호출 지점은 셋 — 로비 설정 창의 '적용', 인게임 설정 창의 '닫힘', 그리고 종료 시 backstop.
    // setter에는 걸지 않는다 — 인게임 설정 창이 슬라이더를 드래그하는 매 프레임 setter를
    // 부르므로, 거기서 플러시하면 드래그 내내 디스크를 때린다
    public void Save() {
        PlayerPrefs.Save();
    }

    // 둘을 한 함수에서 함께 적용한다 — VSync가 켜지면 targetFrameRate가 통째로 무시되므로
    // 따로 적용하면 "상한을 바꿨는데 안 먹는다"가 된다. 나눠서 부르는 형태로 바꾸지 말 것
    public void ApplyFrameRateAndVSync() {
        QualitySettings.vSyncCount = _isVSync ? 1 : 0;
        Application.targetFrameRate = Define.FrameRateValues[_frameRate];
    }

    // 해상도와 창모드를 함께 적용한다 — Screen.SetResolution()이 둘을 한 번에 받는다.
    // 전체화면은 ExclusiveFullScreen이 아니라 FullScreenWindow(테두리 없는 창)다.
    // 전자는 디스플레이 모드를 실제로 바꿔 알트탭 복귀가 느리고 멀티모니터에서 말썽이 난다.
    // 에디터에서는 Unity가 이 호출을 무시한다(Game 뷰 드롭다운이 지배한다) — 검증은 빌드에서
    public void ApplyResolution() {
        var res = Define.ResolutionValues[_resolution];
        Screen.SetResolution(res.w, res.h, _isWindow ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
    }

    public void ApplyPreviousSceneSetting() {
        SetVolume(_bgmVolume, Define.Sound.Bgm);
        SetVolume(_effectVolume, Define.Sound.Effect);
    }

    public void SetVolume(int volume, Define.Sound type) {
        int clamped = Mathf.Clamp(volume, 0, 100);

        if (type == Define.Sound.Effect) {
            _effectVolume = clamped;
            PlayerPrefs.SetInt(KEY_EFFECT_VOLUME, clamped);
        }
        else if (type == Define.Sound.Bgm) {
            _bgmVolume = clamped;
            PlayerPrefs.SetInt(KEY_BGM_VOLUME, clamped);
        }

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
        _ingameMouseSensitivity = Mathf.Clamp(sensitivity, MIN_MOUSE_SENSITIVITY, MAX_MOUSE_SENSITIVITY);
        PlayerPrefs.SetFloat(KEY_SENSITIVITY, _ingameMouseSensitivity);
    }

    public float GetMouseSensitivity() {
        return _ingameMouseSensitivity;
    }

    public void SetIsWindow(bool isWindow) {
        _isWindow = isWindow;
        PlayerPrefs.SetInt(KEY_IS_WINDOW, isWindow ? 1 : 0);
    }

    public bool GetIsWindow() {
        return _isWindow;
    }

    public void SetMasterVolume(int volume) {
        _masterVolume = Mathf.Clamp(volume, 0, 100);
        PlayerPrefs.SetInt(KEY_MASTER_VOLUME, _masterVolume);
        SetVolume(_effectVolume, Define.Sound.Effect);
        SetVolume(_bgmVolume, Define.Sound.Bgm);
    }

    public int GetMasterVolume() {
        return _masterVolume;
    }

    public void SetResolution(Define.Resolution resolution) {
        _resolution = resolution;
        PlayerPrefs.SetString(KEY_RESOLUTION, resolution.ToString());
    }

    public Define.Resolution GetResolution() {
        return _resolution;
    }

    public void SetFrameRate(Define.FrameRate frameRate) {
        _frameRate = frameRate;
        PlayerPrefs.SetString(KEY_FRAME_RATE, frameRate.ToString());
    }

    public Define.FrameRate GetFrameRate() {
        return _frameRate;
    }

    public void SetIsVSync(bool isVSync) {
        _isVSync = isVSync;
        PlayerPrefs.SetInt(KEY_VSYNC, isVSync ? 1 : 0);
    }

    public bool GetIsVSync() {
        return _isVSync;
    }

    public void SetFov(int fov) {
        _fov = Mathf.Clamp(fov, 60, 80);
        PlayerPrefs.SetInt(KEY_FOV, _fov);
    }

    public int GetFov() {
        return _fov;
    }

    public void Clear() {

    }
}
