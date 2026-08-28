using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴 위에 덮는 순위표 화면. 코드로 직접 짓는다(이 프로젝트의 다른 UI와 같은 방식).
///
/// 모드마다 순위표가 따로 있으므로 위쪽 탭으로 모드를 고른다.
/// 로그인 전에는 목록 대신 로그인 안내를 보여 준다 — 거부해도 게임은 그대로 돌아가야 하므로
/// 이 화면은 언제나 닫을 수 있다.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    // 비상용 스위치. true면 순위표 대신 "추후 출시" 안내만 띄우고 화면을 짓지 않는다.
    // 서비스 쪽에 문제가 생겼을 때, 눌러도 아무 일 없는 로그인 화면을 내보이는 것보다 낫다.
    const bool COMING_SOON = false;

    static readonly string[] MODE_LOC_KEYS = { "mode_normal", "mode_ice", "mode_toggle", "mode_disco" };
    const int ROW_COUNT = 8;      // 한 화면에 보여 줄 줄 수
    const int TOP_COUNT = 20;     // 서비스에서 받아 올 줄 수

    RectTransform _root;
    Text          _titleText;
    Button[]      _tabs   = new Button[MODE_LOC_KEYS.Length];
    Text[]        _tabText = new Text[MODE_LOC_KEYS.Length];
    GameObject    _listGo;
    GameObject    _signInGo;
    Text          _emptyText;
    Text[]        _rowRank  = new Text[ROW_COUNT];
    Text[]        _rowName  = new Text[ROW_COUNT];
    Text[]        _rowScore = new Text[ROW_COUNT];
    int           _mode;

    static readonly Color GOLD    = new Color(1.00f, 0.82f, 0.25f);
    static readonly Color DIM     = new Color(0.62f, 0.62f, 0.72f);
    static readonly Color BORDER  = new Color(0.886f, 0.910f, 0.941f);

    /// <summary>메인 메뉴 캔버스 위에 순위표를 띄운다.</summary>
    public static LeaderboardPanel Open(Transform canvas, int startMode)
    {
        var go = new GameObject("LeaderboardPanel");
        go.transform.SetParent(canvas, false);
        go.transform.SetAsLastSibling();
        var panel = go.AddComponent<LeaderboardPanel>();
        panel.Build(startMode);
        return panel;
    }

    void Build(int startMode)
    {
        _mode = Mathf.Clamp(startMode, 0, MODE_LOC_KEYS.Length - 1);
        var loc = LocalizationManager.Instance;

        var dim   = gameObject.AddComponent<Image>();
        dim.color = new Color(0.03f, 0.03f, 0.07f, 0.96f);
        _root       = GetComponent<RectTransform>();
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = _root.offsetMax = Vector2.zero;

        _titleText = Label(transform, loc.Get("leaderboard"), 70, Color.white,
                           new Vector2(0, 780), new Vector2(900, 110));

        if (COMING_SOON)
        {
            Label(transform, loc.Get("lb_coming_soon"), 62, GOLD,
                  new Vector2(0, 60), new Vector2(900, 140));
            var soonClose = WideButton(transform, loc.Get("cancel"), new Vector2(0, -740), BORDER);
            soonClose.onClick.AddListener(() => Destroy(gameObject));
            return;
        }

        // ── 모드 탭 ──────────────────────────────────────────
        for (int i = 0; i < MODE_LOC_KEYS.Length; i++)
        {
            int idx = i;
            var tabGo = new GameObject($"Tab_{i}");
            tabGo.transform.SetParent(transform, false);
            var img    = tabGo.AddComponent<Image>();
            img.sprite = RoundedSprite(200, 100, 28);
            img.type   = Image.Type.Sliced;
            var rt              = tabGo.GetComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((i - 1.5f) * 252f, 640f);
            rt.sizeDelta        = new Vector2(244, 96);
            _tabs[i]    = tabGo.AddComponent<Button>();
            _tabText[i] = Label(tabGo.transform, loc.Get(MODE_LOC_KEYS[i]), 40, Color.white,
                                Vector2.zero, Vector2.zero, stretch: true);
            _tabs[i].onClick.AddListener(() => { _mode = idx; Refresh(); });
        }

        // ── 순위 목록 ────────────────────────────────────────
        _listGo = new GameObject("List");
        _listGo.transform.SetParent(transform, false);
        var listRt = _listGo.AddComponent<RectTransform>();
        listRt.anchorMin = listRt.anchorMax = new Vector2(0.5f, 0.5f);
        listRt.pivot     = new Vector2(0.5f, 0.5f);
        listRt.sizeDelta = new Vector2(940, 800);

        for (int i = 0; i < ROW_COUNT; i++)
        {
            float y = 430f - i * 100f;
            _rowRank[i]  = Label(_listGo.transform, "", 42, DIM,
                                 new Vector2(-400, y), new Vector2(120, 80));
            _rowName[i]  = Label(_listGo.transform, "", 42, Color.white,
                                 new Vector2(-60, y), new Vector2(520, 80), align: TextAnchor.MiddleLeft);
            _rowScore[i] = Label(_listGo.transform, "", 42, GOLD,
                                 new Vector2(320, y), new Vector2(300, 80), align: TextAnchor.MiddleRight);
        }

        _emptyText = Label(_listGo.transform, "", 38, DIM, new Vector2(0, 300), new Vector2(900, 80));

        // ── 로그인 안내 ──────────────────────────────────────
        _signInGo = new GameObject("SignIn");
        _signInGo.transform.SetParent(transform, false);
        var siRt = _signInGo.AddComponent<RectTransform>();
        siRt.anchorMin = siRt.anchorMax = new Vector2(0.5f, 0.5f);
        siRt.pivot     = new Vector2(0.5f, 0.5f);
        siRt.sizeDelta = new Vector2(940, 800);

        Label(_signInGo.transform, loc.Get("lb_signin_title"), 54, Color.white,
              new Vector2(0, 220), new Vector2(900, 100));
        Label(_signInGo.transform, loc.Get("lb_signin_desc"), 36, DIM,
              new Vector2(0, 110), new Vector2(880, 140));

        var signBtn = WideButton(_signInGo.transform, loc.Get("lb_signin_btn"), new Vector2(0, -40), GOLD);
        signBtn.onClick.AddListener(() =>
        {
            signBtn.interactable = false;
            Leaderboards.Service.SignIn(ok =>
            {
                signBtn.interactable = true;
                if (!ok) _emptyText.text = LocalizationManager.Instance.Get("lb_signin_failed");
                Refresh();
            });
        });

        // ── 닫기 ─────────────────────────────────────────────
        var closeBtn = WideButton(transform, loc.Get("cancel"), new Vector2(0, -740), BORDER);
        closeBtn.onClick.AddListener(() => Destroy(gameObject));

        Refresh();
    }

    void Refresh()
    {
        var loc     = LocalizationManager.Instance;
        var service = Leaderboards.Service;

        _titleText.text = $"{loc.Get("leaderboard")} · {loc.Get(MODE_LOC_KEYS[_mode])}";

        for (int i = 0; i < _tabs.Length; i++)
        {
            bool on = i == _mode;
            _tabs[i].image.color = on ? GOLD : new Color(0.18f, 0.18f, 0.24f);
            _tabText[i].color    = on ? new Color(0.10f, 0.08f, 0.05f) : DIM;
        }

        bool usable = service != null && service.IsAvailable;
        bool signed = usable && service.IsSignedIn;

        _signInGo.SetActive(usable && !signed);
        _listGo.SetActive(signed || !usable);

        if (!usable)
        {
            ClearRows();
            _emptyText.text = loc.Get("lb_unavailable");
            return;
        }
        if (!signed) return;

        ClearRows();
        _emptyText.text = loc.Get("lb_loading");

        int asked = _mode;
        service.LoadTop(asked, TOP_COUNT, entries =>
        {
            // 불러오는 동안 다른 탭으로 옮겼으면 그 결과는 버린다.
            if (this == null || asked != _mode) return;

            ClearRows();
            if (entries == null || entries.Length == 0)
            {
                _emptyText.text = loc.Get("lb_empty");
                return;
            }

            _emptyText.text = "";
            int n = Mathf.Min(entries.Length, ROW_COUNT);
            for (int i = 0; i < n; i++)
            {
                var e = entries[i];
                _rowRank[i].text  = e.rank.ToString();
                _rowName[i].text  = e.name;
                _rowScore[i].text = e.score.ToString("N0");

                Color c = e.isSelf ? GOLD : Color.white;
                _rowName[i].color = c;
                _rowRank[i].color = e.isSelf ? GOLD : DIM;
            }
        });
    }

    void ClearRows()
    {
        for (int i = 0; i < ROW_COUNT; i++)
        {
            _rowRank[i].text  = "";
            _rowName[i].text  = "";
            _rowScore[i].text = "";
        }
    }

    // ── 작은 빌더들 ──────────────────────────────────────────
    Text Label(Transform parent, string txt, int size, Color color, Vector2 pos, Vector2 sizeDelta,
               bool stretch = false, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font          = Font4();
        t.fontSize      = size;
        t.fontStyle     = FontStyle.Bold;
        t.color         = color;
        t.alignment     = align;
        t.text          = txt;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var rt = go.GetComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = sizeDelta;
        }
        return t;
    }

    Button WideButton(Transform parent, string label, Vector2 pos, Color textColor)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var img    = go.AddComponent<Image>();
        img.sprite = RoundedBorderSprite(200, 100, 36, 4);
        img.type   = Image.Type.Sliced;
        img.color  = BORDER;
        var btn = go.AddComponent<Button>();
        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(620, 120);
        Label(go.transform, label, 48, textColor, Vector2.zero, Vector2.zero, stretch: true);
        return btn;
    }

    static Font Font4() =>
        Resources.Load<Font>("Fonts/SCDream4") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    static Sprite RoundedSprite(int w, int h, int r) => MakeRounded(w, h, r, 0);
    static Sprite RoundedBorderSprite(int w, int h, int r, int border) => MakeRounded(w, h, r, border);

    // 둥근 사각형(테두리 두께 0이면 꽉 찬 사각형). 9-slice로 늘려 쓴다.
    static Sprite MakeRounded(int w, int h, int radius, int border)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = InRounded(x, y, w, h, radius);
                bool hole   = border > 0 &&
                              InRounded(x, y, w, h, radius, border);
                px[y * w + x] = (inside && !hole)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        int b = Mathf.Max(radius + border + 1, 2);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    static bool InRounded(int x, int y, int w, int h, int radius, int inset = 0)
    {
        float minX = inset, minY = inset, maxX = w - 1 - inset, maxY = h - 1 - inset;
        if (x < minX || x > maxX || y < minY || y > maxY) return false;
        float r = Mathf.Max(0, radius - inset);
        float cx = Mathf.Clamp(x, minX + r, maxX - r);
        float cy = Mathf.Clamp(y, minY + r, maxY - r);
        float dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= r * r + 0.5f;
    }
}
