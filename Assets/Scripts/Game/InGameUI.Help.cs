using UnityEngine;
using UnityEngine.UI;

// InGameUI 의 도움말 부분 — 음량 버튼 왼쪽의 ? 버튼과, 그걸 눌렀을 때 뜨는 규칙 설명.
//
// 설명은 모드마다 다르다. 공통 두 줄(끌어다 놓기 / 줄 채우기) 뒤에 그 모드에서만
// 통하는 규칙을 붙인다. 규칙 하나에 아이콘 하나를 두는데, 아이콘을 새로 그리지 않는 게
// 이 화면의 원칙이다 — 블록은 실제 스프라이트를, 게이지와 콤보 표시는 화면에 뜨는 것과
// 같은 글자를 같은 폰트·같은 색으로 찍는다. 설명에서 본 그림이 판 위에 그대로 나와야
// 둘이 연결되기 때문이다.
//
// 내용이 화면보다 길어질 수 있어(토글·디스코는 여섯 줄) 목록은 스크롤 영역에 담고,
// 닫기 버튼만 아래에 고정한다. 스크롤을 끝까지 내려야 닫을 수 있으면 안 되니까.
public partial class InGameUI
{
    // ── 레이아웃 ────────────────────────────────────────────────
    // 46pt·폭 680 에서 가장 긴 줄(무지개 설명, 콤보 경고 영문)이 네 줄 221px 이다.
    // 상자를 그보다 좁게 잡으면 넘친 글이 옆 줄과 겹친다 — Overflow 라 잘리지는 않는다.
    // 240 이면 여섯 줄 모드도 총높이 1610 으로 뷰포트(1670) 안에 들어가 스크롤이 안 뜬다.
    const float HELP_ROW_H      = 240f;  // ── 조정 손잡이: 규칙 한 줄의 높이 ──
    const float HELP_ICON       = 100f;  // ── 조정 손잡이: 아이콘 크기 ──
    const float HELP_ICON_X     = -368f; // 아이콘 중심. 줄마다 세로로 맞춘다
    // 아이콘 오른쪽 끝(-318)과 문장 왼쪽 끝 사이의 간격이다. 예전엔 28px 이라 글자가
    // 아이콘에 붙어 보였다. 아이콘을 왼쪽으로 더 밀 수는 없다 — 게이지·콤보 줄은
    // 스프라이트가 아니라 HELP_ICON_X 를 중심으로 찍는 글자라 같이 밀리면 잘린다.
    const float HELP_TEXT_X     = 108f;  // ── 조정 손잡이: 문장 상자 중심(클수록 아이콘과 멀어짐) ──
    const float HELP_TEXT_W     = 680f;
    const int   HELP_TITLE_PT   = 64;    // ── 조정 손잡이: 제목 글자 크기 ──
    const int   HELP_BODY_PT    = 46;    // ── 조정 손잡이: 설명 글자 크기 ──
    const int   HELP_GLYPH_PT   = 52;    // 게이지·콤보 아이콘으로 쓰는 글자 크기
    const float HELP_TOP_PAD    = 40f;   // 스크롤 영역 위 여백
    const float HELP_CLOSE_BAND = 210f;  // 아래에 비워 두는 닫기 버튼 자리

    // 오버레이는 거의 불투명하다. 디스코 배경이 밑에서 번쩍이면 글이 안 읽힌다.
    static readonly Color HELP_BACKDROP = new Color(0.03f, 0.03f, 0.06f, 0.97f);
    static readonly Color HELP_BODY_COL = new Color(0.86f, 0.88f, 0.94f);

    // 아이콘을 무엇으로 찍을지. 스프라이트가 없는 것들은 각자 방식이 다르다.
    enum HelpIcon
    {
        Sprite,   // arg = Resources 경로
        Line,     // 네 칸 중 셋이 찬 줄. 굽는다
        Glyph,    // arg = 리치텍스트. 게이지처럼 본문 폰트로 찍는 것
        Combo,    // arg = 리치텍스트. 콤보 표시와 같은 폰트·기울임으로 찍는 것
    }

    // 화면에 실제로 뜨는 것과 같은 글자·색. 여기 색을 바꾸면 실물도 같이 봐야 한다.
    //   토글 게이지  RefreshGauge 의 채운 원 / 빈 원
    //   디스코 하트  HEART_FULL / HEART_EMPTY
    //   콤보 표시    COMBO_STATUS_SAFE / COMBO_STATUS_RISK
    // ● ○ ♥ ♪ 는 SCDream4·8 양쪽 cmap 에 다 있는 걸 확인하고 골랐다(♫ 는 없다).
    const string GLYPH_TG_GAUGE = "<color=#EBEBF0>●●</color><color=#4D4D66>○</color>";
    const string GLYPH_DC_HEART = "<color=#FF477A>♥♥</color><color=#FFFFFF33>♥</color>";
    const string GLYPH_DC_SAFE  = "<color=#FFFFFF>x2 ♪</color>";
    const string GLYPH_DC_RISK  = "<color=#FF4757>x2 ♪</color>";

    GameObject    _helpOverlay;   // 처음 누를 때 한 번 세우고 그 뒤로는 켜고 끄기만 한다
    RectTransform _helpContent;   // 스크롤되는 알맹이. 줄은 전부 여기에 붙는다
    Sprite        _helpLineIcon;

    /// <summary>음량 버튼 왼쪽의 ? 버튼. 위치·크기·색을 음량 버튼과 맞춘다.</summary>
    void BuildHelpButton()
    {
        var obj = new GameObject("HelpButton");
        obj.transform.SetParent(_canvas.transform, false);

        // 물음표는 폰트로 찍고 테두리만 굽는다. ? 모양을 픽셀로 그리는 것보다 폰트 글자가
        // 어느 해상도에서나 깨끗하고, 언어를 안 타는 기호라 번역할 것도 없다.
        var img = obj.AddComponent<Image>();
        img.sprite         = MakeRoundedOutlineSprite(96, 96, 48, 5);
        img.type           = Image.Type.Simple;
        img.preserveAspect = true;
        img.color          = new Color(0.886f, 0.910f, 0.941f);

        var btn    = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration     = 0.1f;
        btn.colors = colors;

        var rt              = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-150, -55);   // 음량 버튼(-30)에서 한 칸 왼쪽
        rt.sizeDelta        = new Vector2(100, 100);

        var q = new GameObject("Mark");
        q.transform.SetParent(obj.transform, false);
        var t = q.AddComponent<Text>();
        t.font          = Font4();
        t.fontSize      = 52;
        t.fontStyle     = FontStyle.Bold;
        t.color         = Color.white;
        t.alignment     = TextAnchor.MiddleCenter;
        t.text          = "?";
        t.raycastTarget = false;   // 버튼이 받아야 하므로 글자는 터치를 통과시킨다
        var qrt       = q.GetComponent<RectTransform>();
        qrt.anchorMin = Vector2.zero;
        qrt.anchorMax = Vector2.one;
        qrt.offsetMin = qrt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(ShowHelp);
    }

    void ShowHelp()
    {
        if (_helpOverlay == null) BuildHelpOverlay();
        _helpOverlay.transform.SetAsLastSibling();   // 형제 순서 = 그리는 순서. 전부 덮어야 한다
        _helpOverlay.SetActive(true);
    }

    void HideHelp()
    {
        if (_helpOverlay != null) _helpOverlay.SetActive(false);
    }

    /// <summary>
    /// 모드별 설명 줄. 앞의 둘은 어느 모드에서나 같고, 뒤가 그 모드의 규칙이다.
    /// </summary>
    (HelpIcon kind, string arg, string key)[] HelpRows()
    {
        if (ModeSession.IsIce)
            return new (HelpIcon, string, string)[]
            {
                (HelpIcon.Sprite, "Sprites/UI/drag",         "help_drag"),
                (HelpIcon.Line,   null,                      "help_line"),
                (HelpIcon.Sprite, "Sprites/Puzzles/ice",     "help_ice_slide"),
                (HelpIcon.Sprite, "Sprites/Effects/melting", "help_ice_chain"),
            };

        if (ModeSession.IsToggle)
            return new (HelpIcon, string, string)[]
            {
                (HelpIcon.Sprite, "Sprites/UI/drag",               "help_drag"),
                (HelpIcon.Line,   null,                            "help_line"),
                (HelpIcon.Sprite, "Sprites/Puzzles/white_on",      "help_tg_open"),
                (HelpIcon.Sprite, "Sprites/Puzzles/white_off",     "help_tg_closed"),
                (HelpIcon.Glyph,  GLYPH_TG_GAUGE,                  "help_tg_gauge"),
                (HelpIcon.Sprite, "Sprites/Puzzles/special_block", "help_tg_swap"),
            };

        if (ModeSession.IsDisco)
            return new (HelpIcon, string, string)[]
            {
                (HelpIcon.Sprite, "Sprites/UI/drag",               "help_drag"),
                (HelpIcon.Line,   null,                            "help_line"),
                (HelpIcon.Glyph,  GLYPH_DC_HEART,                  "help_dc_gauge"),
                (HelpIcon.Sprite, "Sprites/Puzzles/rainbow_disco", "help_dc_rainbow"),
                (HelpIcon.Combo,  GLYPH_DC_SAFE,                   "help_dc_safe"),
                (HelpIcon.Combo,  GLYPH_DC_RISK,                   "help_dc_risk"),
            };

        return new (HelpIcon, string, string)[]
        {
            (HelpIcon.Sprite, "Sprites/UI/drag",     "help_drag"),
            (HelpIcon.Line,   null,                  "help_line"),
            (HelpIcon.Sprite, "Sprites/Puzzles/sky", "help_combo"),
        };
    }

    void BuildHelpOverlay()
    {
        var loc  = LocalizationManager.Instance;
        var rows = HelpRows();

        _helpOverlay = new GameObject("HelpOverlay");
        _helpOverlay.transform.SetParent(_canvas.transform, false);

        var backdrop           = _helpOverlay.AddComponent<Image>();
        backdrop.color         = HELP_BACKDROP;
        backdrop.raycastTarget = true;   // 뒤로 터치가 새면 설명을 읽다가 조각이 놓인다

        var rt       = _helpOverlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // ── 스크롤 영역 ─────────────────────────────────────────
        // 뷰포트 자신이 ScrollRect 를 들고, 알맹이는 그 자식이다. 뷰포트에 투명 Image 를
        // 붙이는 건 보이려는 게 아니라 드래그를 받기 위해서다 — 레이캐스트 대상이 없으면
        // 손가락이 배경으로 빠져 스크롤이 안 먹는다.
        var viewGo = new GameObject("HelpViewport");
        viewGo.transform.SetParent(_helpOverlay.transform, false);
        var viewImg           = viewGo.AddComponent<Image>();
        viewImg.color         = new Color(0f, 0f, 0f, 0f);
        viewImg.raycastTarget = true;
        var viewRt       = viewGo.GetComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.offsetMin = new Vector2(0f,  HELP_CLOSE_BAND);
        viewRt.offsetMax = new Vector2(0f, -HELP_TOP_PAD);
        viewGo.AddComponent<RectMask2D>();   // 넘치는 줄을 뷰포트 밖에서 잘라 준다

        var contentGo = new GameObject("HelpContent");
        contentGo.transform.SetParent(viewGo.transform, false);
        _helpContent                  = contentGo.AddComponent<RectTransform>();
        _helpContent.anchorMin        = new Vector2(0.5f, 1f);
        _helpContent.anchorMax        = new Vector2(0.5f, 1f);
        _helpContent.pivot            = new Vector2(0.5f, 1f);
        _helpContent.anchoredPosition = Vector2.zero;
        _helpContent.sizeDelta        = new Vector2(1000f, 130f + rows.Length * HELP_ROW_H + 40f);

        var scroll               = viewGo.AddComponent<ScrollRect>();
        scroll.content           = _helpContent;
        scroll.viewport          = viewRt;
        scroll.horizontal        = false;
        scroll.vertical          = true;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // 알맹이는 위에서부터 쌓는다. 세로 가운데 정렬을 하면 줄 수가 적은 모드(노말 3줄)와
        // 많은 모드(6줄)에서 제목 높이가 달라지고, 화면비마다 또 달라진다.
        AddHelpText(_helpContent, loc.Get("help_title"), HELP_TITLE_PT, Color.white,
                    new Vector2(0f, -70f), new Vector2(900f, 90f), TextAnchor.MiddleCenter);

        for (int i = 0; i < rows.Length; i++)
            AddHelpRow(rows[i].kind, rows[i].arg, rows[i].key,
                       -(130f + HELP_ROW_H * (i + 0.5f)));

        AddHelpClose(loc.Get("help_close"));

        // 다 들어가면 스크롤을 끄고 세로 가운데에 둔다. 켜 둔 채로 놔두면 Clamped 가
        // 알맹이를 늘 위로 붙여서, 줄이 적은 모드(노말 세 줄)는 아래가 휑하게 빈다.
        // 오버레이는 탭한 순간에 세우므로 캔버스가 이미 잡혀 있어 rect 를 바로 읽어도 된다.
        float viewH = viewRt.rect.height;
        if (viewH > 1f && _helpContent.sizeDelta.y <= viewH)
        {
            scroll.vertical = false;
            _helpContent.anchoredPosition =
                new Vector2(0f, -(viewH - _helpContent.sizeDelta.y) * 0.5f);
        }

        // 배경 여백을 눌러도 닫힌다. 스크롤 영역 안은 일부러 뺐다 — 목록을 쓸어 올리다가
        // 손을 떼는 것까지 탭으로 읽히면 읽는 도중에 창이 닫힌다.
        var bgBtn = _helpOverlay.AddComponent<Button>();
        bgBtn.targetGraphic = backdrop;
        bgBtn.transition    = Selectable.Transition.None;
        bgBtn.onClick.AddListener(HideHelp);
    }

    /// <summary>아이콘 하나와 문장 한 줄. 문장은 왼쪽 맞춤이라 줄마다 시작점이 같다.</summary>
    void AddHelpRow(HelpIcon kind, string arg, string key, float y)
    {
        if (kind == HelpIcon.Sprite || kind == HelpIcon.Line)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(_helpContent, false);
            var img            = iconGo.AddComponent<Image>();
            img.sprite         = kind == HelpIcon.Line ? HelpLineIcon() : LoadSpriteFromPath(arg);
            img.preserveAspect = true;
            img.raycastTarget  = false;
            var irt              = iconGo.GetComponent<RectTransform>();
            irt.anchorMin        = irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot            = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(HELP_ICON_X, y);
            irt.sizeDelta        = new Vector2(HELP_ICON, HELP_ICON);
        }
        else
        {
            // 게이지와 콤보 표시는 스프라이트가 아니라 글자다. 실물과 같은 폰트로 찍어야
            // "화면 위의 그것"이라는 게 바로 읽힌다 — 콤보는 SCDream8 굵은 기울임이다.
            var t = AddHelpText(_helpContent, arg, HELP_GLYPH_PT, Color.white,
                                new Vector2(HELP_ICON_X, y), new Vector2(230f, HELP_ICON),
                                TextAnchor.MiddleCenter);
            if (kind == HelpIcon.Combo)
            {
                t.font      = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
                t.fontStyle = FontStyle.BoldAndItalic;
            }
        }

        var body = AddHelpText(_helpContent, LocalizationManager.Instance.Get(key),
                               HELP_BODY_PT, HELP_BODY_COL,
                               new Vector2(HELP_TEXT_X, y),
                               new Vector2(HELP_TEXT_W, HELP_ROW_H - 20f),
                               TextAnchor.MiddleLeft);
        // 영어 문장이 한국어보다 길어 한 줄을 넘길 때가 있다. 줄바꿈으로 받는다.
        //
        // BestFit 은 일부러 안 쓴다. 그걸 켜면 짧은 줄은 제 크기로, 긴 줄은 줄어들어 한
        // 화면 안에서 글자 크기가 들쭉날쭉해진다. 넘치면 잘라내는 대신 흘려보내고,
        // 정 길어지면 스크롤이 받는다 — 규칙 설명은 끝까지 읽혀야 한다.
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow   = VerticalWrapMode.Overflow;
    }

    /// <summary>
    /// 도움말 전용 글자. InGameUI.AddText 는 가운데 정렬 고정이고 만든 걸 돌려주지 않아서,
    /// 왼쪽 맞춤과 폰트 교체가 필요한 여기서는 따로 만든다.
    /// </summary>
    Text AddHelpText(Transform parent, string txt, int size, Color color,
                     Vector2 pos, Vector2 sizeDelta, TextAnchor anchor)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.font          = Font4();
        t.fontSize      = size;
        t.fontStyle     = FontStyle.Bold;
        t.color         = color;
        t.alignment     = anchor;
        t.text          = txt;
        t.raycastTarget = false;   // 스크롤 드래그가 글자에 막히면 안 된다

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = sizeDelta;
        return t;
    }

    /// <summary>닫기 버튼. 스크롤 영역 밖에 고정이라 목록이 아무리 길어도 늘 손에 닿는다.</summary>
    void AddHelpClose(string label)
    {
        var go = new GameObject("HelpClose");
        go.transform.SetParent(_helpOverlay.transform, false);

        var img    = go.AddComponent<Image>();
        img.sprite = MakeRoundedSprite(340, 104, 26);
        img.type   = Image.Type.Sliced;
        img.color  = new Color(1f, 1f, 1f, 0.16f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(HideHelp);

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, HELP_CLOSE_BAND * 0.5f);
        rt.sizeDelta        = new Vector2(340f, 104f);

        AddText(go.transform, label, 38, Color.white, Vector2.zero, new Vector2(340, 104), true);
    }

    /// <summary>
    /// "줄을 채우면 지워진다"를 위한 아이콘. 판에 그런 스프라이트가 따로 없어서 굽는다.
    /// 네 칸 중 셋만 채우고 마지막 칸은 테두리만 남긴다 — 한 칸이 비었다는 게 보여야
    /// "채우면 지워진다"가 그림만으로 읽힌다.
    /// </summary>
    Sprite HelpLineIcon()
    {
        if (_helpLineIcon != null) return _helpLineIcon;

        const int W = 128, H = 32, CELL = 28, GAP = 5;
        var tex = new Texture2D(W, H, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[W * H];

        for (int i = 0; i < 4; i++)
        {
            int  x0     = i * (CELL + GAP);
            bool filled = i < 3;
            for (int y = 2; y < H - 2; y++)
                for (int x = x0; x < x0 + CELL && x < W; x++)
                {
                    bool edge = y < 4 || y >= H - 4 || x <= x0 + 1 || x >= x0 + CELL - 2;
                    if (!filled && !edge) continue;
                    px[y * W + x] = filled ? new Color32(255, 255, 255, 255)
                                           : new Color32(255, 255, 255, 130);
                }
        }

        tex.SetPixels32(px);
        tex.Apply();
        _helpLineIcon = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        return _helpLineIcon;
    }
}
