using UnityEngine;
using UnityEngine.UI;

// InGameUI 의 신기록 공유 안내 — 이번 판이 최고 점수를 넘었으면, 게임오버 화면에서 부활하지 않고
// "다시 시작"을 누른 순간 "신기록!" 안내를 한 장 띄우고 공유 카드 화면(SharePanel)으로 바로 갈 수 있게 한다.
// 닫으면 원래 하려던 다시 시작으로 이어지고, 카드를 만들러 가면 공유 화면을 닫았을 때 게임오버 화면으로 돌아온다.
//
// 공유 화면은 메인 메뉴와 같은 SharePanel 을 이 캔버스 위에 그대로 연다. 씬을 옮기지 않으므로
// 공유를 마치고 뒤로 가면 게임오버 화면(부활·다시 시작)이 그대로 남아 있다.
public partial class InGameUI
{
    GameObject    _sharePromo;          // 떠 있는 안내. 뒤로 가기 키가 먼저 닫는다
    System.Action _sharePromoContinue;  // 안내를 마치면 이어서 할 일(다시 시작)

    const float PROMO_IMG_W = 780f;   // ── 조정 손잡이: 카드 그림 폭 (원본 782×500) ──

    // 세로 배치(가운데 0 기준): 제목 560 · 점수 460 · 카드 그림 150(약 400~-100) · 문구 -160 · 공유 안내 -232
    // · 버튼 -380 / -520. 버튼 위는 문구와 약 90 띄운다. 아래 배너(16:9 에서 위끝 약 -787) 자리를 남기려고
    // 전체를 위쪽에 둔다.
    void ShowSharePromo(System.Action onClose)
    {
        if (_sharePromo != null || _gameOverOverlay == null) return;
        var loc = LocalizationManager.Instance;

        // 게임오버 화면 위에 한 겹 더. 뒤를 거의 덮어 안내에만 눈이 가게 한다.
        var go = new GameObject("SharePromo");
        go.transform.SetParent(_canvas.transform, false);
        go.transform.SetAsLastSibling();
        var dim = go.AddComponent<Image>();
        // 불투명하게 덮는다. 반투명이면 뒤의 게임오버 글자·버튼이 비쳐 겹쳐 보인다. 터치도 막는다.
        dim.color = new Color(0.03f, 0.03f, 0.07f, 1f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _sharePromo = go;

        AddText(go.transform, loc.Get("new_record"), 88, GOLD, new Vector2(0, 560), new Vector2(900, 120));
        AddText(go.transform, _gm.HighScore.ToString("N0"), 64, Color.white, new Vector2(0, 460), new Vector2(900, 90));

        // 카드 견본 그림
        var imgGo = new GameObject("PromoCards");
        imgGo.transform.SetParent(go.transform, false);
        var img = imgGo.AddComponent<Image>();
        img.sprite         = LoadSpriteFromPath("Sprites/UI/share_promo");
        img.preserveAspect = true;
        img.raycastTarget  = false;
        var irt = img.rectTransform;
        irt.anchoredPosition = new Vector2(0, 150);
        irt.sizeDelta        = new Vector2(PROMO_IMG_W, PROMO_IMG_W * 500f / 782f);

        AddText(go.transform, loc.Get("share_promo_desc"), 50, new Color(0.86f, 0.88f, 0.94f),
                new Vector2(0, -160), new Vector2(940, 70));

        // "나중에 공유 버튼으로도 만들 수 있다" — 메인 화면의 공유 아이콘을 작게 같이 보여 준다.
        AddShareLaterRow(go.transform, loc.Get("share_promo_later"), new Vector2(0, -232));

        _sharePromoContinue = onClose;

        // 카드 만들기 → 공유 화면. 이 안내는 이미 "판을 접겠다"고 고른 뒤에 뜬다. 그래서 공유 화면을
        // 닫으면 게임오버 화면(부활 버튼이 있는)으로 돌아가지 않고, 하려던 다시 시작으로 이어진다.
        var make = PromoButton(go.transform, loc.Get("share_promo_btn"), new Vector2(0, -380), GOLD);
        make.onClick.AddListener(() =>
        {
            var next = _sharePromoContinue;
            CloseSharePromo();
            var panel = SharePanel.Open(_canvas.transform, ModeSession.SelectedMode);
            panel.OnClosed = next;
        });

        var close = PromoButton(go.transform, loc.Get("help_close"), new Vector2(0, -520),
                                new Color(0.886f, 0.910f, 0.941f));
        close.onClick.AddListener(FinishSharePromo);
    }

    // 안내를 닫고 하려던 일(다시 시작)로 넘어간다. 닫기 버튼과 뒤로 가기 키가 같이 쓴다.
    void FinishSharePromo()
    {
        var next = _sharePromoContinue;
        CloseSharePromo();
        next?.Invoke();
    }

    // [공유 아이콘] 문구 — 아이콘과 글자를 한 덩어리로 가운데 맞춘다.
    void AddShareLaterRow(Transform parent, string text, Vector2 pos)
    {
        const float ICON = 50f, GAP = 14f;
        var dimCol = new Color(0.62f, 0.62f, 0.72f);

        var row = new GameObject("ShareLater");
        row.transform.SetParent(parent, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchoredPosition = pos;
        rowRt.sizeDelta        = new Vector2(940, 60);

        var tGo = new GameObject("Text");
        tGo.transform.SetParent(row.transform, false);
        var t = tGo.AddComponent<Text>();
        t.font          = Font4();
        t.fontSize      = 40;
        t.color         = dimCol;
        t.alignment     = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.text          = text;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        float tw = t.preferredWidth;
        float left = -(ICON + GAP + tw) * 0.5f;
        var tRt = t.rectTransform;
        tRt.sizeDelta        = new Vector2(tw + 4f, 60);
        tRt.anchoredPosition = new Vector2(left + ICON + GAP + tw * 0.5f, 0f);

        var iGo = new GameObject("Icon");
        iGo.transform.SetParent(row.transform, false);
        var icon = iGo.AddComponent<Image>();
        icon.sprite         = LoadSpriteFromPath("Sprites/UI/share");
        icon.preserveAspect = true;
        icon.color          = dimCol;
        icon.raycastTarget  = false;
        var iRt = icon.rectTransform;
        iRt.sizeDelta        = new Vector2(ICON, ICON);
        iRt.anchoredPosition = new Vector2(left + ICON * 0.5f, 0f);
    }

    void CloseSharePromo()
    {
        if (_sharePromo != null) Destroy(_sharePromo);
        _sharePromo         = null;
        _sharePromoContinue = null;
    }

    // 게임오버 화면의 버튼과 같은 모양(테두리만 있는 둥근 사각형)
    Button PromoButton(Transform parent, string label, Vector2 pos, Color textColor)
    {
        var go = new GameObject("PromoBtn");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = BtnBorderSprite;
        img.type   = Image.Type.Sliced;
        img.color  = new Color(0.886f, 0.910f, 0.941f);
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.8f);
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(620, 120);
        AddText(go.transform, label, 50, textColor, Vector2.zero, Vector2.zero, fullStretch: true);
        return btn;
    }

    /// <summary>뒤로 가기 키: 공유 화면 → 신기록 안내 순으로 한 겹씩 닫는다. 닫았으면 true.</summary>
    bool CloseShareLayers()
    {
        var share = FindFirstObjectByType<SharePanel>();
        if (share != null) { share.Close(); return true; }
        if (_sharePromo != null) { FinishSharePromo(); return true; }
        return false;
    }
}
