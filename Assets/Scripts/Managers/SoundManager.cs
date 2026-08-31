using System.Collections.Generic;
using UnityEngine;

public class SoundManager {
    AudioSource[] _audioSources = new AudioSource[(int)Define.Sound.MaxCount];
    Dictionary<string, AudioClip> _audioClips = new Dictionary<string, AudioClip>();

    public void Init() {
        GameObject root = GameObject.Find("@Sound");
        if (root == null) {
            root = new GameObject { name = "@Sound" };
            Object.DontDestroyOnLoad(root);

            string[] soundnames = System.Enum.GetNames(typeof(Define.Sound));
            for (int i=0; i<soundnames.Length-1; i++) {
                GameObject go = new GameObject { name = soundnames[i] };
                _audioSources[i] = go.AddComponent<AudioSource>();
                go.transform.parent = root.transform;
            }

            _audioSources[(int)Define.Sound.Bgm].loop = true;
        }
    }

    public void Play(string path, Define.Sound type=Define.Sound.Effect, float pitch = 1.0f) {
        AudioClip audioClip = GetOrAddAudioClip(path, type);
        Play(audioClip, type, pitch);
    }

    public void Play(AudioClip audioClip, Define.Sound type = Define.Sound.Effect, float pitch = 1.0f) {
        if (audioClip == null)
            return;

        if (type == Define.Sound.Bgm) {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Bgm];
            if (audioSource.isPlaying)
                audioSource.Stop();
            audioSource.pitch = pitch;
            audioSource.clip = audioClip;
            audioSource.Play();
        } else {
            AudioSource audioSource = _audioSources[(int)Define.Sound.Effect];
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(audioClip);
        }
    }

    // UI 피드백 3종. 2D 재생이며 SoundPoint(월드 3D)와 경계를 넘기지 말 것 — 넘기면 클릭음이
    // 가슴팍에서 3D로 난다. 클립 이름을 호출부에서 조립하지 않는 것은 GetGunShotSound와 같은 이유로,
    // 못 찾은 클립은 무음이고 GetOrAddAudioClip이 그 null을 캐시까지 해서 로그조차 남지 않는다.
    // 호출부가 수십 곳이라 문자열을 흩으면 오타 하나가 그 버튼만 영구히 죽인다
    public void PlayUISubmit() => Play("ui_submit");
    public void PlayUIReturn() => Play("ui_return");
    public void PlayInventoryChange() => Play("inventory_change");

    // 월드에서 난 소리 — 오브젝트에 붙은 3D 소스에서 재생한다. UI 피드백은 위의 Play를 쓸 것.
    //
    // 소리별 음량 밸런스는 반드시 volumeScale로 잡는다. source.volume은 소스 단위 속성이라
    // 그 소스에서 울리고 있던 다른 원샷들에까지 소급 적용된다 (설정 볼륨은 전체가 같은 값이라 무해).
    //
    // OPTION: 소리별 음량 밸런스. 호출부 다섯이 전부 volumeScale 기본 1.0이라 발소리·발사음·
    //         재장전음이 같은 크기로 난다. 실측에서 거슬리지 않아 보류했다(2026-08-29 확인).
    //         잡게 되면 프리팹 Volume이 아니라 위 문단대로 호출부 인자로 잡을 것
    //
    // OPTION: 소리 카테고리별 가청 거리 분리. 지금은 발소리·발사음이 SoundPoint 하나를 공유해
    //         MaxDistance(30)가 같은데, 총성은 발소리보다 훨씬 멀리 가야 하는 소리다.
    //         MaxDistance는 소스 단위 속성이라 재생마다 갈아끼우면 울리고 있던 소리의 거리감이
    //         중간에 튄다(걸으며 쏘는 상황이 상시라 실제로 겹친다) — 그래서 이 함수에서 값을
    //         대입하는 방식은 답이 아니다. 프리팹에 근거리·원거리 소스를 따로 두고 호출부가
    //         카테고리로 소스를 고르는 형태가 되어야 하며, 프리팹 작업이 선행된다.
    //         30 자체는 실측에서 거슬리지 않았지만(2026-08-31) 확정된 값이 아니다 — 총성의
    //         가청 거리는 실측으로 조정될 값이고, 발소리와 한 값을 공유하는 한 조정이 불가능한
    //         것이 이 항목이 남아 있는 이유다. "거리가 충분하니 해소됐다"로 읽고 지우지 말 것
    public void PlayOneShotAt(string path, AudioSource source, float volumeScale = 1.0f) {
        if (source == null)
            return;

        // 클립 이름이 계산되어 들어오는 호출부가 있다(재장전 단계 등 — 모르는 단계는 null이다).
        // 이 가드가 없으면 GetOrAddAudioClip의 path.Contains에서 NRE가 난다
        if (string.IsNullOrEmpty(path))
            return;

        AudioClip audioClip = GetOrAddAudioClip(path, Define.Sound.Effect);
        if (audioClip == null)
            return;

        // 설정 볼륨의 출처를 Effect 소스 한 곳으로 유지한다 — 여기서 따로 들고 있으면
        // 설정 창에서 효과음을 내려도 월드 소리만 그대로 난다
        source.volume = _audioSources[(int)Define.Sound.Effect].volume;
        source.PlayOneShot(audioClip, volumeScale);
    }

    public void Clear() {
        foreach (AudioSource audioSource in _audioSources) {
            audioSource.clip = null;
            audioSource.Stop();
        }
        _audioClips.Clear();
    }

    public AudioClip GetOrAddAudioClip(string path, Define.Sound type = Define.Sound.Effect) {
        if (path.Contains("Sounds/") == false)
            path = $"Sounds/{path}";
        AudioClip audioClip = null;

        if (type == Define.Sound.Bgm) {
            audioClip = Managers.Resource.Load<AudioClip>(path);
        }
        else {
            if (_audioClips.TryGetValue(path, out audioClip) == false) {
                audioClip = Managers.Resource.Load<AudioClip>(path);
                _audioClips.Add(path, audioClip);
            }
        }

        return audioClip;
    }

    public void SetVolume(float volume, Define.Sound type) {
        if (_audioSources[(int)type] != null)
            _audioSources[(int)type].volume = volume;
    }
}
