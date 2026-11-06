using UnityEngine;

/// <summary>
/// 짧은 진동. 블록이 지워질 때 손에 "톡" 하고 오는 감각을 주는 데 쓴다.
///
/// Handheld.Vibrate() 는 쓰지 않는다. 길이를 정할 수 없고 기기마다 0.4~0.5초씩 길게 울려
/// "톡"이 아니라 "부르르"가 된다. 대신 안드로이드 Vibrator 에 VibrationEffect.createOneShot 으로
/// 길이와 세기를 직접 준다. (minSdk 26 이라 VibrationEffect 는 항상 있다.)
///
/// 안드로이드가 아닌 곳(에디터·iOS)에서는 아무것도 하지 않는다.
/// </summary>
public static class Haptics
{
    // ── 조정 손잡이 ──
    // 한 줄 지울 때의 기본 진동. 줄이 하나 늘 때마다 조금씩 길고 세진다.
    // 28ms 로 시작했는데 기기에서 전혀 안 느껴졌다. 저가 기기에 많은 회전식 모터는 짧으면 돌기 시작하기도
    // 전에 끝난다. 45ms 면 회전식에서도 느껴지고, 좋은 모터에서는 "톡"이 조금 또렷해질 뿐이다.
    const int BASE_MS        = 45;   // 한 줄일 때 길이(ms)
    const int PER_LINE_MS    = 10;   // 줄이 하나 늘 때마다 더하는 길이
    const int MAX_MS         = 90;
    const int BASE_AMPLITUDE = 150;  // 1~255. 세기를 못 바꾸는 기기는 기본 세기로 울린다
    const int PER_LINE_AMP   = 35;

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject _vibrator;
    static AndroidJavaClass  _effectClass;
    static bool _initTried;
    static bool _loggedFirst;

    static bool Init()
    {
        if (_initTried) return _vibrator != null;
        _initTried = true;
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            if (_vibrator != null && !_vibrator.Call<bool>("hasVibrator")) _vibrator = null;
            if (_vibrator != null) _effectClass = new AndroidJavaClass("android.os.VibrationEffect");

            // 기기에서 진동이 안 느껴질 때 원인을 가리려고 처음 한 번 상태를 남긴다(logcat 에서 [Haptics]).
            //   hasVibrator=false  → 기기가 진동을 못 한다고 답함
            //   amplitude=false    → 세기 조절을 못 하는 모터(대개 저가 기기의 회전 모터). 짧은 진동이 약하다
            Debug.Log(_vibrator == null
                ? "[Haptics] 진동 없음 (hasVibrator=false)"
                : $"[Haptics] 진동 준비됨 (amplitude={_vibrator.Call<bool>("hasAmplitudeControl")})");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptics] 진동을 쓸 수 없음: {e.Message}");
            _vibrator = null;
        }
        return _vibrator != null;
    }
#endif

    /// <summary>줄을 지웠을 때. lines 는 이번에 지운 가로+세로 줄 수.</summary>
    public static void LineClear(int lines)
    {
        if (lines <= 0) return;
        RequestVibratePermission();
        int extra = lines - 1;
        Vibrate(Mathf.Min(MAX_MS, BASE_MS + PER_LINE_MS * extra),
                Mathf.Min(255, BASE_AMPLITUDE + PER_LINE_AMP * extra));
    }

    /// <summary>ms 동안, amplitude(1~255) 세기로 한 번 울린다.</summary>
    public static void Vibrate(int ms, int amplitude)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Init()) return;
        try
        {
            using (var effect = _effectClass.CallStatic<AndroidJavaObject>(
                       "createOneShot", (long)ms, Mathf.Clamp(amplitude, 1, 255)))
            {
                _vibrator.Call("vibrate", effect);
                if (!_loggedFirst) { _loggedFirst = true; Debug.Log($"[Haptics] 진동 요청 {ms}ms / 세기 {amplitude}"); }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptics] 진동 실패: {e.Message}");
        }
#endif
    }

    // 실제로는 울리지 않는 참조(frameCount 는 음수가 되지 않는다). 빌드 시 Unity 는 스크립트에
    // Handheld.Vibrate 가 있어야 AndroidManifest 에 VIBRATE 권한을 넣어 준다. 이게 없으면 위의
    // vibrate 호출이 SecurityException 으로 막힌다. 코드 제거에 걸리지 않게 실제 경로에서 부른다.
    static void RequestVibratePermission()
    {
        if (Time.frameCount < 0) Handheld.Vibrate();
    }
}
