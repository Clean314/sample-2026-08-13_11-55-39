using UnityEngine;
using System.Collections.Generic;

public enum Language { Korean, English }

/// <summary>
/// 한국어 / 영어 전환 매니저 (싱글턴, PlayerPrefs 저장)
/// </summary>
public class LocalizationManager
{
    static LocalizationManager _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    const string PREF_KEY = "Language";

    public Language CurrentLanguage { get; private set; }

    /// <summary>언어가 바뀔 때 발행 — UI가 구독해 텍스트를 갱신합니다.</summary>
    public System.Action OnLanguageChanged;

    // [0] = 한국어, [1] = English
    static readonly Dictionary<string, string[]> _table = new Dictionary<string, string[]>
    {
        // ── 메인 메뉴 ──────────────────────────────────────────────
        { "start",         new[] { "시작",              "Start"         } },
        { "no_ads",        new[] { "광고 제거",          "Remove Ads"    } },
        { "lang_btn",      new[] { "ENG",               "한국어"         } },

        // ── 모드 이름 ──────────────────────────────────────────────
        { "mode_normal",   new[] { "노말",              "Normal"        } },
        { "mode_ice",      new[] { "아이스",             "Ice"           } },
        { "mode_toggle",   new[] { "토글",              "Toggle"        } },
        { "mode_disco",    new[] { "디스코",            "Disco"         } },
        { "coming_soon",   new[] { "COMING\nSOON",     "COMING\nSOON"  } },

        // ── 잠금 해제 조건 (string.Format: {0}=모드명, {1}=점수) ──
        { "unlock_fmt",    new[] { "{0}모드 최고 점수\n{1}점 이상", "{0} mode high score\n{1}+" } },

        // ── 디버그 패널 ────────────────────────────────────────────
        { "debug_label",   new[] { "DEBUG 최고점수:",   "DEBUG High Score:" } },
        { "debug_set",     new[] { "설정",              "Set"           } },

        // ── 인게임 (InGameUI에서 사용) ─────────────────────────────
        { "best",          new[] { "최고",              "BEST"          } },
        { "score",         new[] { "점수",              "SCORE"         } },
        { "game_over",     new[] { "게임 오버",          "GAME OVER"     } },
        // 디스코는 콤보를 놓치는 순간 끝난다. 실패 조건을 그대로 제목으로 쓴다.
        { "game_over_disco", new[] { "리듬을 잃다",        "RHYTHM LOST"   } },
        { "restart",       new[] { "다시 시작",          "Restart"       } },
        { "menu",          new[] { "메뉴",              "Menu"          } },
        { "resume",        new[] { "계속",              "Resume"        } },
        { "pause",         new[] { "일시정지",           "Pause"         } },
        { "high_score",    new[] { "최고 점수",          "High Score"    } },
        { "new_record",    new[] { "신기록!",            "New Record!"   } },

        { "cancel",        new[] { "취소",                "Cancel"               } },

        // ── 리더보드 ──────────────────────────────────────────────
        { "leaderboard",   new[] { "리더보드",            "Leaderboard"          } },
        { "lb_coming_soon",new[] { "추후 출시",            "Coming Soon"          } },
        { "lb_signin_title", new[] { "로그인하고 점수를 뽐내세요",
                                                          "Sign in and show off your score" } },
        { "lb_signin_desc",  new[] { "로그인하면 모드별 순위표에 내 기록이 올라갑니다.\n로그인하지 않아도 게임은 그대로 즐길 수 있습니다.",
                                                          "Sign in to put your record on the mode leaderboards.\nThe game plays just the same without it." } },
        { "lb_signin_btn",   new[] { "로그인",              "Sign In"              } },
        { "lb_signin_failed",new[] { "로그인하지 못했습니다", "Could not sign in"    } },
        { "lb_loading",      new[] { "불러오는 중...",       "Loading..."           } },
        { "lb_empty",        new[] { "아직 기록이 없습니다",  "No scores yet"        } },
        { "lb_unavailable",  new[] { "이 기기에서는 순위표를 쓸 수 없습니다",
                                                          "Leaderboards are unavailable on this device" } },

        // ── 다시 시작 전 광고 제거 제안 ───────────────────────────
        { "iap_title",     new[] { "광고 없이 플레이",     "Play Without Ads"     } },
        { "iap_desc",      new[] { "한 번만 구매하면 배너와 전면 광고가 사라집니다",
                                                          "One purchase removes banners and full-screen ads" } },
        { "iap_buy",       new[] { "광고 제거",            "Remove Ads"           } },
        { "iap_later",     new[] { "결제 추후 연결",        "Payment Coming Soon"  } },
        { "iap_watch",     new[] { "광고 보고 계속하기",    "Watch Ad & Continue"  } },
        { "iap_failed",    new[] { "지금은 구매할 수 없습니다",
                                                          "Purchase unavailable right now" } },

        // ── 디스코 모드 광과민성 경고 ─────────────────────────────
        // 시작 직후 2초만 떴다 사라지므로 설명은 두 줄을 넘기지 않는다.
        { "photo_warn_title", new[] { "광과민성 발작 주의",  "Photosensitivity Warning" } },
        { "photo_warn_desc",  new[] { "이 모드에는 밝은 빛과 빠른 색 변화가 있습니다.\n불편함을 느끼면 플레이를 중단해 주세요.",
                                                          "This mode contains flashing lights and\nrapid color changes. Stop playing if you feel unwell." } },

        // ── 네트워크 연결 안내 ────────────────────────────────────
        { "no_network",    new[] { "인터넷에 연결되어 있지 않습니다.\n네트워크 연결을 확인해 주세요.",
                                                          "No internet connection.\nPlease check your network." } },
        { "retry",         new[] { "다시 시도",            "Retry"                } },
        { "checking",      new[] { "확인 중...",           "Checking..."          } },
    };

    LocalizationManager()
    {
        int saved = PlayerPrefs.GetInt(PREF_KEY, 0);
        CurrentLanguage = (Language)saved;
    }

    /// <summary>키에 해당하는 현재 언어 문자열을 반환합니다.</summary>
    public string Get(string key)
    {
        if (_table.TryGetValue(key, out var arr))
            return arr[(int)CurrentLanguage];
        return key; // 키 미등록 시 키 자체 반환
    }

    public void SetLanguage(Language lang)
    {
        if (CurrentLanguage == lang) return;
        CurrentLanguage = lang;
        PlayerPrefs.SetInt(PREF_KEY, (int)lang);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    public void ToggleLanguage()
    {
        SetLanguage(CurrentLanguage == Language.Korean ? Language.English : Language.Korean);
    }
}
