using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴의 공유 버튼으로 여는 "최고 점수 카드" 화면.
///
/// 위쪽 탭으로 모드를 고르면 가운데 카드가 그 모드로 바뀐다. 이미지 저장을 누르면 화면에
/// 보이는 카드 영역을 그대로 찍어 PNG 로 사진 앱에 넣는다(ImageSaver). 카드를 따로 굽는
/// 카메라를 두지 않은 건, 보이는 그대로가 저장되어야 미리보기와 결과가 어긋나지 않기 때문이다.
///
/// 카드: 로고 → 모드 아이콘 → 모드 이름 → BEST + 점수 → 이름.
/// 배경은 모드를 따른다 — 노말은 메인 메뉴와 같은 남보라 그라데이션, 아이스는 눈 오는 새벽 도시,
/// 토글은 타일 방(어두운·밝은), 디스코는 도시·드라이빙(노을·아침·밤) 장면. 여럿인 모드는 리롤로 고른다.
/// 이름은 리더보드에 로그인했을 때만 구글 플레이 게임 닉네임으로 들어간다. 로그인 안 했으면
/// 그 줄을 비우고, 카드 밖에 "로그인하면 이름이 들어간다"는 안내를 작게 띄운다.
/// </summary>
public class SharePanel : MonoBehaviour
{
    static readonly string[] MODE_LOC_KEYS = { "mode_normal", "mode_ice", "mode_toggle", "mode_disco" };
    static readonly string[] MODE_ICONS    = { "Sprites/Modes/normal_mode", "Sprites/Modes/ice_mode",
                                               "Sprites/Modes/toggle_mode", "Sprites/Modes/disco_mode" };

    // ── 조정 손잡이 ──
    const float CARD_W     = 840f;    // 카드 설계 크기 (저장 비율 4:5)
    const float CARD_H     = 1050f;
    const float CARD_SCALE_MAX = 0.9f;  // 화면에 보이는 크기 = 설계 × 배율. 저장 해상도도 이걸 따른다.
                                        // 화면이 짧으면 위아래에 맞춰 이보다 줄어든다(Layout)
    // 아래 광고 배너 자리. 적응형 배너는 기기에 따라 50~90dp 인데 폰은 대개 50~66dp 다.
    // 넉넉히 66dp 로 잡는다 — 모자라면 닫기 버튼이 배너에 깔려 못 누른다.
    const float BANNER_DP      = 66f;
    const float EDGE_TOP       = 155f;  // 화면 위끝과 탭 사이 — 왼쪽 위 뒤로 가기 버튼(-55~-135) 밑으로
    const float EDGE_BOTTOM    = 24f;   // 배너 위끝과 버튼 사이

    float _cardScale, _cardY;

    static readonly Color BG_TOP    = new Color(0.145f, 0.137f, 0.251f);   // 메인 메뉴 배경과 같은 남보라
    static readonly Color BG_BOTTOM = new Color(0.059f, 0.055f, 0.102f);
    static readonly Color GOLD      = new Color(1.00f, 0.82f, 0.25f);
    static readonly Color DIM       = new Color(0.62f, 0.62f, 0.72f);
    static readonly Color BORDER    = new Color(0.886f, 0.910f, 0.941f);

    // ── 카드 배경 ──
    // 배경이 여럿인 모드(토글 두 방, 디스코 네 장면)는 저장 버튼 옆 리롤 버튼으로 돌려 본다.
    // 하나뿐인 모드는 리롤 버튼을 숨긴다.
    enum Bg { Gradient, IceCity, RoomDark, RoomLight, DiscoCity, DriveSunset, DriveMorning, DriveNight }
    static readonly Bg[][] MODE_BGS =
    {
        new[] { Bg.Gradient },                                                    // 노말
        new[] { Bg.IceCity },                                                     // 아이스
        new[] { Bg.RoomDark, Bg.RoomLight },                                      // 토글
        new[] { Bg.DiscoCity, Bg.DriveSunset, Bg.DriveMorning, Bg.DriveNight },   // 디스코
    };
    readonly int[] _bgIdx = new int[MODE_BGS.Length];

    // 밝은 방 위에서 쓰는 글자색. 흰 글자는 밝은 타일 위에서 안 보인다.
    static readonly Color INK      = new Color(0.13f, 0.15f, 0.20f);
    static readonly Color INK_DIM  = new Color(0.36f, 0.40f, 0.48f);
    static readonly Color INK_GOLD = new Color(0.78f, 0.52f, 0.04f);

    int           _mode;
    Image         _logo, _frame;
    Text          _bestText;
    Shadow[]      _cardShadows;
    Button        _rerollBtn;
    RectTransform _card;
    Image         _icon;
    RectTransform _scene;   // 모드 배경(아이스 도시·토글 방)을 담는 자리. 탭을 바꿀 때마다 새로 세운다
    Text          _modeText, _scoreText, _nameText, _status;
    Button        _saveBtn;
    Button[]      _tabs    = new Button[MODE_LOC_KEYS.Length];
    Text[]        _tabText = new Text[MODE_LOC_KEYS.Length];
    // 구운 그림은 화면을 닫았다 다시 열어도 그대로 쓴다(앱이 켜져 있는 동안). 매번 굽지 않는다.
    static readonly Sprite[] s_iconSprites = new Sprite[MODE_ICONS.Length];
    static Sprite s_bgSprite, s_logoSprite;
    static readonly System.Collections.Generic.Dictionary<string, Sprite> s_btnSprites =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    // 모드 배경은 한 번 세운 것을 꺼 두었다가 다시 켠다. 탭·리롤을 누를 때마다 도시·방을 새로
    // 세우면 그때마다 텍스처를 다시 굽고, 버린 텍스처가 메모리에 남는다.
    readonly System.Collections.Generic.Dictionary<Bg, GameObject> _scenes =
        new System.Collections.Generic.Dictionary<Bg, GameObject>();

    // 권한 콜백이 메인 스레드가 아닐 수 있어서 결과를 여기 두고 Update 에서 받는다.
    volatile bool _resultReady;
    bool          _resultOk;
    string        _resultErr;
    bool          _busy;

    public static SharePanel Open(Transform canvas, int startMode)
    {
        var go = new GameObject("SharePanel");
        go.transform.SetParent(canvas, false);
        go.transform.SetAsLastSibling();
        var p = go.AddComponent<SharePanel>();
        p.Build(Mathf.Clamp(startMode, 0, MODE_LOC_KEYS.Length - 1));
        return p;
    }

    void Build(int startMode)
    {
        _mode = startMode;
        var loc = LocalizationManager.Instance;

        var dim = gameObject.AddComponent<Image>();
        dim.color = new Color(0.03f, 0.03f, 0.07f, 0.96f);   // 뒤 메뉴 터치를 막는다
        var root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        // ── 세로 배치: 아래(배너 위)에서부터 버튼 줄 → 안내 문구 → 카드, 위에서부터 탭 ──
        float half     = CanvasMetrics.HalfHeight;
        float bottom   = -half + BannerReserve() + EDGE_BOTTOM;
        float rowY     = bottom + 60f;                 // 버튼 줄 가운데 (버튼 높이 120)
        float statusY  = rowY + 60f + 40f;
        float cardLow  = statusY + 34f;                // 카드 아래끝
        float tabY     = half - EDGE_TOP - 48f;        // 탭 가운데 (탭 높이 96)
        float cardHigh = tabY - 48f - 30f;             // 카드 위끝 한계
        _cardScale = Mathf.Min(CARD_SCALE_MAX, (cardHigh - cardLow) / CARD_H);
        // 긴 화면이면 자리가 남는다. 카드만 가운데로 띄우면 버튼과 멀어지므로, 탭·카드·문구·버튼을
        // 한 덩어리로 붙여 두고 남는 자리를 위아래로 반씩 나눠 덩어리째 올린다.
        float shift = ((cardHigh - cardLow) - CARD_H * _cardScale) * 0.5f;
        rowY    += shift;
        statusY += shift;
        cardLow += shift;
        _cardY   = cardLow + CARD_H * _cardScale * 0.5f;
        tabY     = cardLow + CARD_H * _cardScale + 30f + 48f;

        // ── 모드 탭 (리더보드 화면과 같은 모양) ──
        for (int i = 0; i < MODE_LOC_KEYS.Length; i++)
        {
            int idx = i;
            var tabGo = new GameObject($"Tab_{i}");
            tabGo.transform.SetParent(transform, false);
            var img    = tabGo.AddComponent<Image>();
            img.sprite = LeaderboardPanel.RoundedSprite(200, 100, 28);
            img.type   = Image.Type.Sliced;
            var rt = tabGo.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2((i - 1.5f) * 252f, tabY);
            rt.sizeDelta        = new Vector2(244, 96);
            _tabs[i]    = tabGo.AddComponent<Button>();
            _tabText[i] = Label(tabGo.transform, loc.Get(MODE_LOC_KEYS[i]), 40, Color.white, Vector2.zero, Vector2.zero);
            Stretch(_tabText[i].rectTransform);
            _tabs[i].onClick.AddListener(() => { if (_busy) return; _mode = idx; Refresh(); });
        }

        BuildCard();

        // ── 아래: 안내 문구, 그 밑 한 줄에 [이미지 저장] [리롤] ──
        // 닫기는 인게임처럼 왼쪽 위 뒤로 가기 버튼이 맡는다.
        _status = Label(transform, "", 34, DIM, new Vector2(0, statusY), new Vector2(960, 60));

        BuildBackButton();

        _saveBtn = WideButton(loc.Get("share_save"), new Vector2(0f, rowY), GOLD, 520f);
        _saveBtn.onClick.AddListener(() => { if (!_busy) StartCoroutine(CaptureAndSave()); });

        // 리롤: 저장 버튼 오른쪽. 카드 밖이라 저장 이미지에 찍히지 않는다.
        _rerollBtn = IconButton("Sprites/UI/reroll", new Vector2(400f, rowY));
        _rerollBtn.onClick.AddListener(() =>
        {
            if (_busy) return;
            _bgIdx[_mode] = (_bgIdx[_mode] + 1) % MODE_BGS[_mode].Length;
            Refresh();
        });

        Refresh();
    }

    void BuildCard()
    {
        var go = new GameObject("Card");
        go.transform.SetParent(transform, false);
        _card = go.AddComponent<RectTransform>();
        _card.anchoredPosition = new Vector2(0, _cardY);
        _card.sizeDelta        = new Vector2(CARD_W, CARD_H);
        _card.localScale       = Vector3.one * _cardScale;

        // 바탕: 메인 메뉴와 같은 위→아래 그라데이션. 모서리는 각지게 둔다 — 화면을 잘라
        // 저장하므로 둥글게 하면 모서리에 뒤 화면이 같이 찍힌다. 대신 안쪽에 둥근 테두리를 긋는다.
        var bg = go.AddComponent<Image>();
        bg.sprite = s_bgSprite != null ? s_bgSprite
                  : (s_bgSprite = UIEffects.VerticalGradient(16, 256, BG_TOP, BG_BOTTOM));
        go.AddComponent<RectMask2D>();   // 모드 배경은 화면 크기로 세우므로 카드 밖을 잘라 낸다

        _scene = NewChild("Scene", _card);
        Stretch(_scene);

        var frame = NewChild("Frame", _card);
        var frameImg = _frame = frame.gameObject.AddComponent<Image>();
        frameImg.sprite = LeaderboardPanel.RoundedBorderSprite(200, 200, 44, 3);
        frameImg.type   = Image.Type.Sliced;
        frameImg.color  = new Color(BORDER.r, BORDER.g, BORDER.b, 0.18f);
        frameImg.raycastTarget = false;
        frame.sizeDelta = new Vector2(CARD_W - 56f, CARD_H - 56f);

        // 로고
        var logoTex = Resources.Load<Texture2D>("Sprites/Logo/logo");
        if (logoTex != null)
        {
            var logo = NewChild("Logo", _card);
            var li = _logo = logo.gameObject.AddComponent<Image>();
            // 메인 메뉴 로고처럼 그림자를 구워 넣는다. 카드 로고는 작아서(원본의 약 0.36배) 살짝만.
            // 그림자 자리만큼 그림이 넓어지므로 표시 상자도 같은 비율만큼 키운다.
            if (s_logoSprite == null)
            {
                var px = UIEffects.ReadPixels(logoTex);
                UIEffects.TintVertical(px, logoTex.width, logoTex.height, Color.white, new Color(0.682f, 0.733f, 0.827f));
                s_logoSprite = UIEffects.ToSprite(UIEffects.WithDropShadow(px, logoTex.width, logoTex.height,
                    LOGO_SHADOW_PAD, new Vector2Int(0, -LOGO_SHADOW_DROP), LOGO_SHADOW_BLUR, LOGO_SHADOW_ALPHA, Color.black));
            }
            li.sprite = s_logoSprite;
            li.preserveAspect = true;
            li.raycastTarget  = false;
            float k = LOGO_H / logoTex.height;   // 표시 배율 (높이 기준)
            logo.anchoredPosition = new Vector2(0, 410f);
            logo.sizeDelta        = new Vector2(logoTex.width  * k + LOGO_SHADOW_PAD * 2 * k,
                                                LOGO_H         + LOGO_SHADOW_PAD * 2 * k);
        }

        // 모드 아이콘 (그림자를 구워 둘레가 넓어진 만큼 표시 크기도 키운다 — 메인 메뉴와 같은 값)
        var icon = NewChild("Icon", _card);
        _icon = icon.gameObject.AddComponent<Image>();
        _icon.preserveAspect = true;
        _icon.raycastTarget  = false;
        icon.anchoredPosition = new Vector2(0, 150f);
        icon.sizeDelta        = Vector2.one * (400f * (512 + ICON_PAD * 2) / 512f);

        _modeText  = Label(_card, "", 52, DIM, new Vector2(0, -95f), new Vector2(760, 80));
        var best   = _bestText = Label(_card, LocalizationManager.Instance.Get("best"), 40, GOLD,
                           new Vector2(0, -175f), new Vector2(760, 60));
        best.fontStyle = FontStyle.Bold;
        _scoreText = Label(_card, "", 128, Color.white, new Vector2(0, -280f), new Vector2(800, 160));
        _scoreText.font = Resources.Load<Font>("Fonts/SCDream8") ?? _scoreText.font;
        _nameText  = Label(_card, "", 44, BORDER, new Vector2(0, -410f), new Vector2(760, 70));

        // 도시·방 배경 위에서도 읽히게 카드 글자에만 살짝 그림자를 깐다 (밝은 방에서는 끈다)
        var texts = new[] { _modeText, best, _scoreText, _nameText };
        _cardShadows = new Shadow[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            var sh = _cardShadows[i] = texts[i].gameObject.AddComponent<Shadow>();
            sh.effectColor    = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(0f, -3f);
        }
    }

    // 모드 배경. 인게임 배경은 화면 하나(폭 1080 × 화면 높이) 크기로 세워지므로, 그 크기의
    // 틀을 만들어 카드 폭에 맞게 줄이고 아래끝을 카드 아래끝에 붙인다(도시·도로가 아래에 있다).
    // 위쪽 하늘이 잘린다. 방만은 소실점이 가운데라 가운데를 맞춘다.
    // 디스코 장면은 곡 없이 그 구간을 되풀이하는 미리보기로 세운다(SpawnPreview).
    void BuildScene(Bg bg)
    {
        foreach (var kv in _scenes) kv.Value.SetActive(kv.Key == bg);
        if (bg == Bg.Gradient || _scenes.ContainsKey(bg)) return;

        float scale = CARD_W / CanvasMetrics.REF_W;
        var frame = NewChild("SceneFrame", _scene);
        _scenes[bg] = frame.gameObject;
        frame.sizeDelta        = new Vector2(CanvasMetrics.REF_W, CanvasMetrics.Height);
        frame.localScale       = Vector3.one * scale;
        frame.anchoredPosition = new Vector2(0f, -CARD_H * 0.5f + CanvasMetrics.Height * scale * 0.5f);

        switch (bg)
        {
            case Bg.IceCity:
                IceBackground.Create(frame, 0);
                break;
            case Bg.RoomDark:
            case Bg.RoomLight:
                frame.anchoredPosition = Vector2.zero;
                var img = frame.gameObject.AddComponent<Image>();
                img.sprite        = InGameUI.ShareRoomSprite(bg == Bg.RoomLight);
                img.raycastTarget = false;
                break;
            case Bg.DiscoCity:
                CityLightBackground.SpawnPreview(frame,
                    CityLightBackground.APPEAR_START + 0.5, CityLightBackground.FADE_OUT_START - 0.1);
                break;
            case Bg.DriveSunset:
                DrivingBackground.SpawnPreview(frame,
                    DrivingBackground.APPEAR_START, DrivingBackground.MORNING_START - 0.05);
                break;
            case Bg.DriveMorning:
                DrivingBackground.SpawnPreview(frame,
                    DrivingBackground.MORNING_START, DrivingBackground.NIGHT_START - 0.05);
                break;
            case Bg.DriveNight:
                DrivingBackground.SpawnPreview(frame,
                    DrivingBackground.NIGHT_START, DrivingBackground.COLORFUL_START - 0.05);
                break;
        }
    }

    // 밝은 배경 — 흰 글자가 안 보이는 장면. 지금은 밝은 방뿐이다.
    // 드라이빙 아침은 하늘은 밝아도 점수가 걸리는 가까운 건물·자동차가 어두워서 흰 글자 그대로 둔다.
    static bool IsLightBg(Bg bg) => bg == Bg.RoomLight;

    // 아래쪽만 밝은 배경 — 드라이빙 밤(명암 반전)은 하늘이 검고 도로·자동차·건물이 흰색이다.
    // 위쪽(로고·모드 이름·최고)은 검은 하늘이라 흰 글자 그대로, 흰 도시에 걸리는 점수·이름만 어둡게.
    static bool IsLightBottom(Bg bg) => bg == Bg.DriveNight;

    // 밝은 배경이면 카드 글자·로고·테두리를 어둡게 바꾼다. lightBottom 이면 점수·이름만.
    void ApplyTheme(bool light, bool lightBottom = false)
    {
        bool low = light || lightBottom;
        _modeText.color  = light ? INK_DIM  : DIM;
        _bestText.color  = light ? INK_GOLD : GOLD;
        _scoreText.color = low   ? INK      : Color.white;
        _nameText.color  = low   ? INK      : BORDER;
        // 그림자는 글자색 반대여야 한다 — 어두운 글자에 검은 그림자는 번져 보인다.
        // 순서: 모드 이름, 최고, 점수, 이름
        _cardShadows[0].enabled = !light;
        _cardShadows[1].enabled = !light;
        _cardShadows[2].enabled = !low;
        _cardShadows[3].enabled = !low;
        if (_logo != null) _logo.color = light ? INK : Color.white;
        _frame.color = light ? new Color(INK.r, INK.g, INK.b, 0.18f)
                             : new Color(BORDER.r, BORDER.g, BORDER.b, 0.18f);
    }

    // 카드 로고. 값은 로고 텍스처(948×302) 픽셀 기준이라 화면에선 약 0.36배로 보인다(아래로 약 4px).
    const float LOGO_H            = 110f;
    const int   LOGO_SHADOW_PAD   = 30;
    const int   LOGO_SHADOW_DROP  = 12;
    const int   LOGO_SHADOW_BLUR  = 16;
    const float LOGO_SHADOW_ALPHA = 0.75f;

    const int   ICON_PAD   = 50;     // 아이콘 그림자 자리 (메인 메뉴 ICON_SHADOW_PAD 와 같다)
    const int   ICON_DROP  = 30;
    const int   ICON_BLUR  = 20;
    const float ICON_ALPHA = 0.8f;

    Sprite IconSprite(int mode)
    {
        if (s_iconSprites[mode] != null) return s_iconSprites[mode];
        var tex = Resources.Load<Texture2D>(MODE_ICONS[mode]);
        if (tex == null) return null;
        var tx = UIEffects.WithDropShadow(UIEffects.ReadPixels(tex), tex.width, tex.height, ICON_PAD,
                                          new Vector2Int(0, -ICON_DROP), ICON_BLUR, ICON_ALPHA, Color.black);
        return s_iconSprites[mode] = UIEffects.ToSprite(tx);
    }

    void Refresh()
    {
        var loc = LocalizationManager.Instance;
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool on = i == _mode;
            _tabs[i].image.color = on ? GOLD : new Color(0.18f, 0.18f, 0.24f);
            _tabText[i].color    = on ? new Color(0.10f, 0.08f, 0.05f) : DIM;
        }

        _icon.sprite    = IconSprite(_mode);
        var bgs = MODE_BGS[_mode];
        var bg  = bgs[_bgIdx[_mode] % bgs.Length];
        BuildScene(bg);
        ApplyTheme(IsLightBg(bg), IsLightBottom(bg));
        _rerollBtn.gameObject.SetActive(bgs.Length > 1);
        _modeText.text  = loc.Get(MODE_LOC_KEYS[_mode]);
        _scoreText.text = PlayerPrefs.GetInt($"m{_mode}_HighScore", 0).ToString("N0");

        string name = Leaderboards.Service != null ? Leaderboards.Service.PlayerName : null;
        _nameText.text = name ?? "";
        _status.text   = name == null ? loc.Get("share_name_hint") : "";
    }

    // 모드 배경(아이스 도시·디스코 장면)이 구운 텍스처는 화면을 닫으면 쓸 데가 없다. 그 배경들이
    // 스스로 텍스처를 지우지 않으므로, 닫을 때 안 쓰는 것을 한 번 비운다. 메인 메뉴에 오래 머물며
    // 공유 화면을 여러 번 열어도 메모리가 쌓이지 않는다. 위의 static 그림들은 쓰이는 중이라 남는다.
    void OnDestroy()
    {
        Resources.UnloadUnusedAssets();
    }

    IEnumerator CaptureAndSave()
    {
        _busy = true;
        _saveBtn.interactable = false;
        string hint = _status.text;
        _status.text = "";   // 카드 밖이라 찍히지는 않지만, 저장 중에는 안내를 잠깐 거둔다

        // 화면이 다 그려진 뒤에 읽어야 이번 프레임 카드가 담긴다.
        yield return new WaitForEndOfFrame();

        // 오버레이 캔버스라 월드 좌표가 곧 화면 픽셀이다.
        var corners = new Vector3[4];
        _card.GetWorldCorners(corners);
        int x = Mathf.Clamp(Mathf.RoundToInt(corners[0].x), 0, Screen.width);
        int y = Mathf.Clamp(Mathf.RoundToInt(corners[0].y), 0, Screen.height);
        int w = Mathf.Clamp(Mathf.RoundToInt(corners[2].x) - x, 1, Screen.width  - x);
        int h = Mathf.Clamp(Mathf.RoundToInt(corners[2].y) - y, 1, Screen.height - y);

        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        string file = $"MATBLAST_{MODE_LOC_KEYS[_mode].Replace("mode_", "")}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        _status.text = LocalizationManager.Instance.Get("share_saving");
        _resultReady = false;
        ImageSaver.SavePng(png, file, (ok, err) =>
        {
            _resultOk    = ok;
            _resultErr   = err;
            _resultReady = true;
        });

        while (!_resultReady) yield return null;

        var loc = LocalizationManager.Instance;
        _status.text = _resultOk ? loc.Get("share_saved") : loc.Get(_resultErr ?? ImageSaver.ERR_FAILED);
        _status.color = _resultOk ? GOLD : DIM;
        _saveBtn.interactable = true;
        _busy = false;
        if (string.IsNullOrEmpty(_status.text)) _status.text = hint;
    }

    // ── 작은 빌더들 ──
    static RectTransform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text Label(Transform parent, string txt, int size, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var rt = NewChild("Label", parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font          = Resources.Load<Font>("Fonts/SCDream4") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = size;
        t.fontStyle     = FontStyle.Bold;
        t.color         = color;
        t.alignment     = TextAnchor.MiddleCenter;
        t.text          = txt;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = sizeDelta;
        return t;
    }

    // 인게임 뒤로 가기 버튼과 같은 것 (InGameUI.BuildBackButton). 왼쪽 위, 160×80, ← 글자.
    void BuildBackButton()
    {
        const int W = 160, H = 80;
        var rt   = NewChild("BackButton", transform);
        var skin = MainMenuUI.CreateButtonSkin(rt.gameObject, W, H, 30, 3);

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = skin;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration     = 0.1f;
        btn.colors = colors;
        btn.onClick.AddListener(() => { if (!_busy) Destroy(gameObject); });

        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30, -55);
        rt.sizeDelta        = new Vector2(W, H);

        // ← (U+2190) 는 SCDream8 에 있다. 굵은 ⬅ 는 폰트에 없다.
        var t = Label(rt, "←", 48, new Color32(0xE2, 0xE8, 0xF0, 255), Vector2.zero, Vector2.zero);
        t.font = Resources.Load<Font>("Fonts/SCDream8") ?? t.font;
        Stretch(t.rectTransform);
    }

    // 메인 메뉴 음악·공유 버튼과 같은 모양의 아이콘 버튼 (로고 그라데이션 + 그림자).
    const int ICON_BTN_PAD = 22;
    Button IconButton(string path, Vector2 pos)
    {
        var rt  = NewChild("IconBtn", transform);
        var img = rt.gameObject.AddComponent<Image>();
        img.preserveAspect = true;
        if (!s_btnSprites.TryGetValue(path, out var spr) || spr == null)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                var px = UIEffects.ReadPixels(tex);
                UIEffects.TintVertical(px, tex.width, tex.height, Color.white, new Color(0.682f, 0.733f, 0.827f));
                spr = s_btnSprites[path] = UIEffects.ToSprite(UIEffects.WithDropShadow(px, tex.width, tex.height,
                                            ICON_BTN_PAD, new Vector2Int(0, -12), 10, 1f, Color.black));
            }
        }
        img.sprite = spr;
        var btn    = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = Vector2.one * (100 + ICON_BTN_PAD * 2);
        img.raycastPadding  = Vector4.one * ICON_BTN_PAD;
        return btn;
    }

    // 광고를 안 지운 사람에게는 메인 메뉴 배너가 공유 화면 밑에도 떠 있다. 그 높이를 캔버스 단위로.
    static float BannerReserve()
    {
        if (RemoveAds.Owned) return 0f;
        float dpi = Screen.dpi > 0f ? Screen.dpi : 420f;
        float px  = BANNER_DP * dpi / 160f;
        return px * CanvasMetrics.REF_W / Mathf.Max(1, Screen.width);
    }

    Button WideButton(string label, Vector2 pos, Color textColor, float width)
    {
        var rt = NewChild(label + "Btn", transform);
        var img    = rt.gameObject.AddComponent<Image>();
        img.sprite = LeaderboardPanel.RoundedBorderSprite(200, 100, 36, 4);
        img.type   = Image.Type.Sliced;
        img.color  = BORDER;
        var btn = rt.gameObject.AddComponent<Button>();
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(width, 120);
        var t = Label(rt, label, 48, textColor, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        return btn;
    }
}
