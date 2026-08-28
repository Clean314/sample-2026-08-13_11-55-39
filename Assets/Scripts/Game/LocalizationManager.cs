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

        // ── 인게임 도움말 ─────────────────────────────────────────
        // 규칙 한 줄에 아이콘 하나. 문장이 두 줄을 넘으면 아이콘과 높이가 안 맞으므로
        // 한 줄에 담기는 길이로 쓴다. 영어가 늘 더 길다는 걸 감안해서 자른다.
        { "help_title",    new[] { "게임 방법",            "How to Play"          } },
        { "help_close",    new[] { "닫기",                "Close"                } },

        { "help_drag",     new[] { "조각 세 개를 판 위로 끌어다 놓습니다.",
                                                          "Drag the three pieces onto the board." } },
        { "help_line",     new[] { "가로나 세로 한 줄을 채우면 지워집니다.",
                                                          "Fill a full row or column to clear it." } },

        { "help_combo",    new[] { "세 조각을 쓰는 동안 한 번도 못 지우면 콤보가 끊깁니다.",
                                                          "Clear at least once per set of three, or the combo resets." } },

        { "help_ice_slide",new[] { "줄이 지워지면 남은 블록이 한 칸씩 내려옵니다.",
                                                          "After a clear, the blocks above slide down one row." } },
        { "help_ice_chain",new[] { "내려오다 줄이 또 완성되면 연쇄로 지워집니다.",
                                                          "If that completes another line, it clears too." } },

        // 토글 — 눈이 뜬 블록이 지금 지울 수 있는 색이다. 게이지는 화면 위의 원 세 개.
        { "help_tg_open",  new[] { "눈을 뜬 블록이 지금 활성화된 색입니다. 이 색으로만 채워진 줄이 지워집니다.",
                                                          "An open-eye block is the active color. Only a line filled with it clears." } },
        { "help_tg_closed",new[] { "눈을 감은 블록은 비활성화된 색입니다. 줄에 섞여 있으면 그 줄은 안 지워집니다.",
                                                          "A closed-eye block is inactive. A line containing one will not clear." } },
        { "help_tg_gauge", new[] { "화면 위 게이지는 한 줄 지울 때마다 한 칸 찹니다. 세 칸이 다 차면 색이 뒤집히고 스페셜 블록이 나옵니다.",
                                                          "The gauge at the top fills once per clear. At three, the color flips and a special block appears." } },
        { "help_tg_swap",  new[] { "스페셜 블록을 줄에 끌어다 놓으면 그 줄이 지금 색이 됩니다.",
                                                          "Drag the special block onto a line to repaint it in the active color." } },

        // 디스코 — 하트 게이지와 콤보 표시. 콤보 표시는 색이 곧 경고라 두 줄로 나눠 설명한다.
        { "help_dc_gauge", new[] { "화면 위 하트는 줄을 지울 때마다 찹니다. 셋이 다 차면 무지개 블록이 생깁니다.",
                                                          "The hearts at the top fill as you clear lines. At three, a rainbow block appears." } },
        { "help_dc_rainbow",new[]{ "무지개 블록을 탭하면 그 자리의 가로줄과 세로줄이 함께 지워집니다. 이것도 콤보로 쳐서 못 지운 세트를 넘깁니다.",
                                                          "Tap the rainbow block to clear its row and column at once. That counts as a clear, so the combo carries on." } },
        { "help_dc_safe",  new[] { "콤보 표시가 흰색이면 이번 세트에서 이미 한 번 지웠다는 뜻입니다.",
                                                          "A white combo readout means you have already cleared during this set." } },
        { "help_dc_risk",  new[] { "빨갛게 깜빡이면 아직 못 지운 것입니다. 세 조각을 다 쓰도록 못 지우면 그대로 끝납니다.",
                                                          "Blinking red means you have not cleared yet. Use all three pieces without a clear and the run ends." } },

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
