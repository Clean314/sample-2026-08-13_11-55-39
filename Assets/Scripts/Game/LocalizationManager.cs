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
        { "restart",       new[] { "다시 시작",          "Restart"       } },
        { "menu",          new[] { "메뉴",              "Menu"          } },
        { "resume",        new[] { "계속",              "Resume"        } },
        { "pause",         new[] { "일시정지",           "Pause"         } },
        { "high_score",    new[] { "최고 점수",          "High Score"    } },
        { "new_record",    new[] { "신기록!",            "New Record!"   } },

        // ── 토글 모드 스페셜 블럭 색상 변환 팝업 ──────────────────
        { "swap_title",    new[] { "색상 변환",          "Color Swap"           } },
        { "swap_desc",     new[] { "보드의 블럭 색상을 일괄 변환합니다",
                                                          "Swap all block colors on the board" } },
        { "swap_b2w",      new[] { "블랙  →  화이트",     "Black  →  White"      } },
        { "swap_w2b",      new[] { "화이트  →  블랙",     "White  →  Black"      } },
        { "cancel",        new[] { "취소",                "Cancel"               } },

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
