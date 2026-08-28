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

    // 월드에서 난 소리 — 오브젝트에 붙은 3D 소스에서 재생한다. UI 피드백은 위의 Play를 쓸 것.
    //
    // 소리별 음량 밸런스는 반드시 volumeScale로 잡는다. source.volume은 소스 단위 속성이라
    // 그 소스에서 울리고 있던 다른 원샷들에까지 소급 적용된다 (설정 볼륨은 전체가 같은 값이라 무해).
    //
    // OPTION: 소리 카테고리별 가청 거리 분리. 지금은 발소리·발사음이 SoundPoint 하나를 공유해
    //         MaxDistance(30)가 같은데, 총성은 발소리보다 훨씬 멀리 가야 하는 소리다.
    //         MaxDistance는 소스 단위 속성이라 재생마다 갈아끼우면 울리고 있던 소리의 거리감이
    //         중간에 튄다(걸으며 쏘는 상황이 상시라 실제로 겹친다) — 그래서 이 함수에서 값을
    //         대입하는 방식은 답이 아니다. 프리팹에 근거리·원거리 소스를 따로 두고 호출부가
    //         카테고리로 소스를 고르는 형태가 되어야 하며, 프리팹 작업이 선행된다.
    //         지금은 오포 총성이 30m에서 끊기는 것 하나가 증상이라 여유가 생길 때 볼 것
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
