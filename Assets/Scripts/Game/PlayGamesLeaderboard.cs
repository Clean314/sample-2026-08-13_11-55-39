using System;
using UnityEngine;

#if UNITY_ANDROID
using System.Collections.Generic;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine.SocialPlatforms;
#endif

/// <summary>
/// Play Games Services 순위표 구현. 안드로이드에서만 살아 있고,
/// 다른 플랫폼과 에디터에서는 아예 컴파일되지 않는다(LocalLeaderboard가 그대로 쓰인다).
///
/// 로그인은 두 갈래다.
///  - 앱을 켤 때 한 번 조용히 시도한다(Authenticate). 이미 허락한 사람은 여기서 끝난다.
///  - 거절했거나 처음인 사람은 순위표 화면의 로그인 버튼에서 ManuallyAuthenticate로 다시 묻는다.
/// 어느 쪽이든 실패해도 게임은 그대로 돌아가야 한다 — 실패는 로그만 남기고 삼킨다.
/// </summary>
public class PlayGamesLeaderboard : ILeaderboardService
{
    // ── 순위표 ID ────────────────────────────────────────────────
    // Play Console > 게임 서비스 > 리더보드에서 만든 순위표의 ID(CgkI...로 시작)를
    // ModeSession의 모드 번호 순서대로 넣는다. 비워 두면 그 모드는 조용히 건너뛴다.
    // 앱 ID(613733068913)는 GooglePlayGamesManifest.androidlib/AndroidManifest.xml 에 들어 있다.
    static readonly string[] BOARD_IDS =
    {
        "CgkI8eDdqu4REAIQAQ",   // 0 NORMAL  (MATBLAST 노말)
        "CgkI8eDdqu4REAIQAg",   // 1 ICE     (MATBLAST 아이스)
        "CgkI8eDdqu4REAIQAw",   // 2 TOGGLE  (MATBLAST 토글)
        "CgkI8eDdqu4REAIQBA",   // 3 DISCO   (MATBLAST 디스코)
    };

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// 씬이 열리기 전에 구현체를 갈아 끼운다. 메인 메뉴를 거치지 않고
    /// InGame 씬에서 바로 시작해도 점수 제출이 살아 있도록 여기에 둔다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        Leaderboards.Service = new PlayGamesLeaderboard();
    }
#endif

#if UNITY_ANDROID
    // 로그인이 끝나기 전에 올라온 점수를 모아 둔다. 앱을 켜자마자 게임을 끝내면
    // 자동 로그인이 아직 진행 중일 수 있는데, 그 한 판을 버리고 싶지는 않다.
    readonly Dictionary<int, int> _pending = new Dictionary<int, int>();

    public PlayGamesLeaderboard()
    {
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate(status =>
        {
            if (status == SignInStatus.Success) OnSignedIn("자동");
            else Debug.Log($"[PGS] 자동 로그인 안 됨: {status}");
        });
    }

    /// <summary>
    /// 로그인이 성공한 직후 한 번. 성공을 로그로 남기는 게 목적이다 —
    /// 실패만 찍어 두면 "아무 로그도 없음"이 성공인지 아직 안 돌아간 것인지 구분되지 않는다.
    /// </summary>
    void OnSignedIn(string how)
    {
        Debug.Log($"[PGS] {how} 로그인 성공: {PlayGamesPlatform.Instance.GetUserDisplayName()} " +
                  $"(id {PlayGamesPlatform.Instance.GetUserId()})");
        FlushPending();
    }

    public bool IsAvailable
    {
        get
        {
            foreach (var id in BOARD_IDS)
                if (!string.IsNullOrEmpty(id)) return true;
            return false;   // ID를 하나도 안 넣었으면 화면은 "쓸 수 없음"으로 둔다
        }
    }

    public bool IsSignedIn => PlayGamesPlatform.Instance.IsAuthenticated();

    public void SignIn(Action<bool> done)
    {
        if (IsSignedIn) { done?.Invoke(true); return; }

        PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
        {
            bool ok = status == SignInStatus.Success;
            if (ok) OnSignedIn("수동");
            else    Debug.LogWarning($"[PGS] 로그인 실패: {status}");
            done?.Invoke(ok);
        });
    }

    public void ReportScore(int mode, int score)
    {
        string id = BoardId(mode);
        if (id == null) return;

        if (!IsSignedIn)
        {
            // 로그인 뒤에 올린다. 같은 모드가 여러 번 오면 제일 높은 것만 남긴다.
            if (!_pending.TryGetValue(mode, out int kept) || score > kept)
                _pending[mode] = score;
            return;
        }

        PlayGamesPlatform.Instance.ReportScore(score, id, ok =>
        {
            if (ok) Debug.Log($"[PGS] 점수 올림 (mode {mode}, {score})");
            else    Debug.LogWarning($"[PGS] 점수 제출 실패 (mode {mode}, {score})");
        });
    }

    public void LoadTop(int mode, int count, Action<LeaderboardEntry[]> done)
    {
        string id = BoardId(mode);
        if (id == null || !IsSignedIn) { done?.Invoke(Array.Empty<LeaderboardEntry>()); return; }

        PlayGamesPlatform.Instance.LoadScores(
            id,
            LeaderboardStart.TopScores,
            Mathf.Clamp(count, 1, 25),          // 한 번에 25개가 상한이다
            LeaderboardCollection.Public,
            LeaderboardTimeSpan.AllTime,
            data =>
            {
                if (!data.Valid || data.Scores == null || data.Scores.Length == 0)
                {
                    if (!data.Valid) Debug.LogWarning($"[PGS] 순위표 읽기 실패: {data.Status}");
                    done?.Invoke(Array.Empty<LeaderboardEntry>());
                    return;
                }

                ResolveNames(data.Scores, done);
            });
    }

    /// <summary>
    /// 점수에는 사용자 ID만 담겨 오므로 표시 이름을 따로 받아 온다.
    /// 이름을 못 받아도 순위와 점수는 보여 준다 — 목록 전체가 사라지는 편보다 낫다.
    /// </summary>
    void ResolveNames(IScore[] scores, Action<LeaderboardEntry[]> done)
    {
        string me  = PlayGamesPlatform.Instance.GetUserId();
        var    ids = new string[scores.Length];
        for (int i = 0; i < scores.Length; i++) ids[i] = scores[i].userID;

        PlayGamesPlatform.Instance.LoadUsers(ids, profiles =>
        {
            var names = new Dictionary<string, string>();
            if (profiles != null)
                foreach (var p in profiles)
                    if (p != null && !string.IsNullOrEmpty(p.id)) names[p.id] = p.userName;

            var list = new LeaderboardEntry[scores.Length];
            for (int i = 0; i < scores.Length; i++)
            {
                var s = scores[i];
                list[i] = new LeaderboardEntry
                {
                    rank   = s.rank,
                    name   = names.TryGetValue(s.userID, out var n) && !string.IsNullOrEmpty(n)
                             ? n : "…",
                    score  = (int)Math.Min(Math.Max(s.value, 0L), int.MaxValue),
                    isSelf = s.userID == me,
                };
            }

            done?.Invoke(list);
        });
    }

    void FlushPending()
    {
        if (_pending.Count == 0) return;

        var queued = new List<KeyValuePair<int, int>>(_pending);
        _pending.Clear();
        foreach (var kv in queued) ReportScore(kv.Key, kv.Value);
    }

    /// <summary>모드 번호에 걸린 순위표 ID. 없거나 안 채웠으면 null.</summary>
    static string BoardId(int mode)
    {
        if (mode < 0 || mode >= BOARD_IDS.Length) return null;
        return string.IsNullOrEmpty(BOARD_IDS[mode]) ? null : BOARD_IDS[mode];
    }
#else
    // 안드로이드가 아니면 Play Games 어셈블리 자체가 없다. 화면이 안내만 띄우도록 둔다.
    public bool IsAvailable => false;
    public bool IsSignedIn  => false;
    public void SignIn(Action<bool> done) => done?.Invoke(false);
    public void ReportScore(int mode, int score) { }
    public void LoadTop(int mode, int count, Action<LeaderboardEntry[]> done)
        => done?.Invoke(Array.Empty<LeaderboardEntry>());
#endif
}
