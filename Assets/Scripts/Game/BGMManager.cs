using UnityEngine;

/// <summary>
/// 씬 전환 시에도 BGM이 끊기지 않도록 DontDestroyOnLoad 싱글톤으로 관리합니다.
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    AudioSource _source;
    bool        _muted;
    Coroutine   _fadeCo;

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

        var clip = Resources.Load<AudioClip>("Audio/BGM/normal_bgm");
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

    /// <summary>현재 재생 위치(초). 일시 정지 중에도 멈춘 자리를 그대로 돌려줍니다.
    /// BeatTracker와 같은 식(timeSamples / frequency)을 써서 연출이 보는 시각과 어긋나지 않습니다.</summary>
    public double PositionSec =>
        _source.clip != null ? (double)_source.timeSamples / _source.clip.frequency : 0.0;

    /// <summary>재생을 그 자리에서 멈춥니다. Resume으로 같은 위치에서 이어집니다.
    /// (Stop과 달리 위치를 잃지 않는다. 디스코 모드가 광과민성 경고 동안 무음을 만드는 데 쓴다.)</summary>
    public void Pause() => _source.Pause();

    /// <summary>Pause로 멈춘 재생을 이어갑니다. 멈춘 적이 없으면 아무 일도 하지 않습니다.</summary>
    public void Resume()
    {
        if (_source.clip != null) _source.UnPause();
    }

    // 테이프 데크의 전원을 끊은 것처럼 감속하며 멎는 소리. pitch는 재생 속도이자 음정이라
    // 이 값을 내리면 느려지는 동시에 음이 낮아진다 — 그게 이 효과의 정체다.
    // 0까지 내리면 정지라 아무 소리도 안 나므로, 거의 멎은 지점에서 끊는다.
    // (음수는 역재생이라 절대 넘기면 안 된다)
    const float TAPE_STOP_MIN_PITCH = 0.06f;

    /// <summary>seconds에 걸쳐 음이 쭉 낮아지며 멎은 뒤 그 자리에서 멈춥니다.
    /// 볼륨·피치가 내려간 채로 남으므로, 다시 들려주려면 반드시 RestorePlayback을 먼저 불러야 합니다.</summary>
    public void TapeStop(float seconds)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(TapeStopRoutine(seconds));
    }

    System.Collections.IEnumerator TapeStopRoutine(float seconds)
    {
        float vol0 = _source.volume;
        float pit0 = _source.pitch;

        for (float e = 0f; e < seconds; e += Time.deltaTime)
        {
            float t = Mathf.Clamp01(e / seconds);

            // 피치는 선형으로 내린다. 음정은 로그로 인지되므로, 재생 속도를 일정하게
            // 떨어뜨리면 귀에는 끝으로 갈수록 급히 처지는 그 특유의 곡선으로 들린다.
            _source.pitch = Mathf.Lerp(pit0, TAPE_STOP_MIN_PITCH, t);

            // 볼륨은 늦게 뺀다. 초반부터 같이 줄이면 음이 처지는 게 안 들리고
            // 그냥 페이드아웃으로 들려서 효과가 사라진다.
            _source.volume = vol0 * (1f - t * t);

            yield return null;
        }

        _source.volume = 0f;
        _source.Pause();
        _fadeCo = null;
    }

    /// <summary>진행 중인 페이드를 끊고 볼륨·피치를 원래대로 되돌립니다.
    /// 씬을 떠나거나 다시 재생하기 전에 부르지 않으면 소리 없이, 혹은 늘어진 채로 재생됩니다.</summary>
    public void RestorePlayback()
    {
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        _source.volume = 1f;
        _source.pitch  = 1f;
    }

    /// <summary>씬 어디서든 호출해도 인스턴스를 반환하거나 새로 생성합니다.</summary>
    public static BGMManager GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("BGMManager").AddComponent<BGMManager>();
    }
}
