using UnityEngine;

/// <summary>
/// 씬 전환 시에도 BGM이 끊기지 않도록 DontDestroyOnLoad 싱글톤으로 관리합니다.
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    AudioSource _source;
    bool        _muted;

    const string PREF_MUTE = "bgm_muted";

    void Awake()
    {
        // 이미 인스턴스가 있으면 중복 제거
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source             = gameObject.AddComponent<AudioSource>();
        _source.loop        = true;
        _source.playOnAwake = false;
        _source.volume      = 1f;

        var clip = Resources.Load<AudioClip>("normal_bgm");
        if (clip != null)
            _source.clip = clip;

        _muted        = PlayerPrefs.GetInt(PREF_MUTE, 0) == 1;
        _source.mute  = _muted;

        if (_source.clip != null)
            _source.Play();
    }

    public bool IsMuted => _muted;
    public AudioSource Source => _source;

    /// <summary>지정한 클립으로 BGM을 전환합니다. 이미 재생 중이면 무시합니다.</summary>
    public void PlayBGM(string clipName)
    {
        var clip = Resources.Load<AudioClip>(clipName);
        if (clip == null || _source.clip == clip) return;
        _source.clip = clip;
        _source.Play(); // mute는 _source.mute로 제어되므로 항상 재생
    }

    public void ToggleMute()
    {
        _muted       = !_muted;
        _source.mute = _muted;
        if (!_muted && !_source.isPlaying && _source.clip != null)
            _source.Play();
        PlayerPrefs.SetInt(PREF_MUTE, _muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>BGM 재생 위치를 초 단위로 이동합니다.</summary>
    public void Seek(double seconds)
    {
        if (_source.clip == null) return;
        int sample = (int)(seconds * _source.clip.frequency);
        _source.timeSamples = Mathf.Clamp(sample, 0, _source.clip.samples - 1);
        if (!_source.isPlaying) _source.Play();
    }

    /// <summary>씬 어디서든 호출해도 인스턴스를 반환하거나 새로 생성합니다.</summary>
    public static BGMManager GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("BGMManager").AddComponent<BGMManager>();
    }
}
