using UnityEngine;

/// <summary>
/// 120 BPM 기준 비트 위치 추적. timeSamples 기반으로 프레임 드랍 오차 없음.
/// </summary>
public class BeatTracker : MonoBehaviour
{
    public static BeatTracker Instance { get; private set; }

    public const float BPM = 120f;
    static readonly double BEAT_INTERVAL = 60.0 / BPM;  // 0.5초

    /// <summary>현재 비트 내 위치 (0.0 = 비트 시작, 1.0 = 다음 비트 직전)</summary>
    public float BeatPhase { get; private set; }

    /// <summary>곡 시작부터 경과한 재생 시간(초). 루프되면 0으로 되돌아감.</summary>
    public double PlaybackSec { get; private set; }

    AudioSource _src;

    void Awake() => Instance = this;

    void Start()
    {
        if (BGMManager.Instance != null)
            _src = BGMManager.Instance.Source;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (_src == null || _src.clip == null || !_src.isPlaying)
        {
            BeatPhase   = 0f;
            PlaybackSec = 0.0;
            return;
        }
        double playbackSec = (double)_src.timeSamples / _src.clip.frequency;
        PlaybackSec = playbackSec;
        BeatPhase   = (float)(playbackSec / BEAT_INTERVAL % 1.0);
    }
}
