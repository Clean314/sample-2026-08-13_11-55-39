using System;
using UnityEngine;

/// <summary>순위표 한 줄.</summary>
public struct LeaderboardEntry
{
    public int    rank;
    public string name;
    public int    score;
    public bool   isSelf;   // 내 기록이면 목록에서 도드라지게 그린다
}

/// <summary>
/// 순위표 서비스. 안드로이드는 Play Games Services, iOS는 Game Center를 쓴다.
/// 둘은 각자 자기 순위표를 갖는다 — 두 플랫폼의 기록이 한 줄에 섞이지 않는다.
/// 합치려면 별도 백엔드가 필요한데, 그 값을 치를 이유가 아직 없다고 판단했다.
///
/// 화면(LeaderboardPanel)은 이 인터페이스만 보고 그린다. 플러그인을 넣기 전에도
/// LocalLeaderboard로 화면을 띄워 볼 수 있고, 넣은 뒤에는 구현체만 갈아 끼우면 된다.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>이 플랫폼/빌드에서 순위표를 쓸 수 있는지. false면 화면이 안내만 띄운다.</summary>
    bool IsAvailable { get; }

    bool IsSignedIn { get; }

    /// <summary>로그인을 시도한다. 거부해도 게임은 그대로 돌아가야 한다.</summary>
    void SignIn(Action<bool> done);

    /// <summary>모드별 최고 점수를 올린다. 로그인 전이면 조용히 무시해도 된다.</summary>
    void ReportScore(int mode, int score);

    /// <summary>상위 기록을 읽어 온다. 실패하면 빈 배열로 부른다.</summary>
    void LoadTop(int mode, int count, Action<LeaderboardEntry[]> done);
}

/// <summary>
/// 지금 쓰이는 순위표 구현을 들고 있는 자리.
///
/// 플랫폼 플러그인을 넣은 뒤에는 앱 시작 지점에서 한 줄만 바꾸면 된다:
///     Leaderboards.Service = new PlayGamesLeaderboard();   // 또는 GameCenterLeaderboard
/// 나머지 코드는 손대지 않는다.
/// </summary>
public static class Leaderboards
{
    static ILeaderboardService _service;

    public static ILeaderboardService Service
    {
        get => _service ??= new LocalLeaderboard();
        set => _service = value;
    }

    /// <summary>최고 점수가 갱신됐을 때 부른다. 서비스가 없거나 로그인 전이면 알아서 넘어간다.</summary>
    public static void ReportHighScore(int mode, int score)
    {
        try { Service?.ReportScore(mode, score); }
        catch (Exception e) { Debug.LogWarning($"[Leaderboards] 점수 제출 실패: {e.Message}"); }
    }
}

/// <summary>
/// 플러그인 없이 쓰는 임시 구현. 내 기록만 보여 준다.
/// 화면 배치를 잡고 로그인 전/후 두 상태를 모두 시험해 보려고 둔 것이라,
/// 로그인도 실제로는 아무 데도 접속하지 않고 표시만 바꾼다.
/// </summary>
public class LocalLeaderboard : ILeaderboardService
{
    const string SIGNED_KEY = "lb_local_signed";

    public bool IsAvailable => true;
    public bool IsSignedIn  => PlayerPrefs.GetInt(SIGNED_KEY, 0) == 1;

    public void SignIn(Action<bool> done)
    {
        PlayerPrefs.SetInt(SIGNED_KEY, 1);
        PlayerPrefs.Save();
        done?.Invoke(true);
    }

    // 최고 점수는 이미 PlayerPrefs에 있으므로 따로 올릴 곳이 없다.
    public void ReportScore(int mode, int score) { }

    public void LoadTop(int mode, int count, Action<LeaderboardEntry[]> done)
    {
        int mine = PlayerPrefs.GetInt($"m{mode}_HighScore", 0);

        var list = new System.Collections.Generic.List<LeaderboardEntry>();
        if (mine > 0)
            list.Add(new LeaderboardEntry { rank = 1, name = "ME", score = mine, isSelf = true });

#if UNITY_EDITOR
        // 목록이 여러 줄일 때의 배치를 확인하려고 에디터에서만 채워 넣는 자리표시자.
        // 빌드에는 절대 들어가지 않는다 — 가짜 기록이 실제 순위표처럼 보이면 안 된다.
        for (int i = list.Count; i < Mathf.Min(count, 8); i++)
            list.Add(new LeaderboardEntry
            {
                rank  = i + 1,
                name  = $"(샘플 {i + 1})",
                score = Mathf.Max(0, mine - (i + 1) * 1200),
            });
#endif

        done?.Invoke(list.ToArray());
    }
}
