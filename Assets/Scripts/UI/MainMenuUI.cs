using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 빈 씬에 빈 GameObject 하나 만들고 이 스크립트만 붙이면 메인 화면이 자동 생성됩니다.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    // ── 모드 정의 ────────────────────────────────────────────────
    static readonly string[] MODE_IMAGES        = { "Sprites/Modes/normal_mode", "Sprites/Modes/ice_mode", "Sprites/Modes/toggle_mode", "Sprites/Modes/disco_mode" };
    static readonly int[]    MODE_UNLOCK_SCORE  = { 0, 10000, 20000, 8000 };
    static readonly int[]    MODE_UNLOCK_FROM   = { 0, 0,    1,     2     };  // 어떤 모드의 최고점수를 확인할지
    static readonly string[] MODE_LOC_KEYS      = { "mode_normal", "mode_ice", "mode_toggle", "mode_disco" };

    // 마지막 실제 모드 뒤에 "COMING SOON" 슬롯 하나 추가
    int  TotalSlots             => MODE_IMAGES.Length + 1;
    bool IsComingSoon(int idx)  => idx >= MODE_IMAGES.Length;

    int             _currentMode  = 0;
    Image           _modeIcon;
    RectTransform   _modeIconRt;
    Image[]         _modeDots;
    int             _pendingShift;   // 애니메이션 중에 들어온 넘김 요청
    Image           _peekLeft;
    Image           _peekRight;
    static Sprite[] s_modeSpriteCache;   // 그림자를 구워 넣은 것이라 씬을 넘어 들고 있는다

    // 아이콘 한 칸이 움직이는 거리. 미리보기가 서 있는 자리이기도 하다 — 둘이
    // 어긋나면 걸쳐 보이던 그림과 밀려오는 그림이 따로 논다.
    const float SLIDE_DIST = 520f;
    const float PEEK_ALPHA = 0.16f;

    // 결제 버튼 두 개가 한 화면에 같이 뜰 수 있어(광고 제거는 늘 있고, 해금은 잠긴
    // 모드에서 시작 버튼 자리를 대신한다) 색을 달리해 서로 헷갈리지 않게 한다.
    //
    // 문구도 "무엇을 사는지"를 그대로 적는다. 나중에 모드별 해금 상품이 생겨도
    // "모든 모드"라는 말이 남아 있으면 어느 쪽인지 헷갈리지 않는다.
    static readonly Color BUY_TEXT    = new Color(1f,     0.85f,  0.3f);   // 광고 제거
    static readonly Color UNLOCK_TEXT = new Color(0.35f,  0.78f,  1f);     // 모든 모드 해금
    static readonly Color START_TEXT  = new Color(0.886f, 0.910f, 0.941f);
    static readonly Color DOT_ON      = new Color(1f,     0.85f,  0.3f);   // 인게임 BEST 글자(InGameUI.GOLD)와 같은 색

    // 종료는 되돌릴 수 없는 쪽이라 붉게 둔다. "아니오"는 기본 글자색 그대로 두어
    // 무심코 누르는 손이 안전한 쪽으로 가게 한다.
    static readonly Color QUIT_YES_TEXT = new Color(0.96f, 0.45f, 0.42f);

    // ── 그라데이션·그림자 ────────────────────────────────────────
    // 모양은 코드로 굽고(UIEffects), 색과 세기는 여기서만 고친다.
    static readonly Color BG_TOP          = Hex("#252340");
    static readonly Color BG_BOTTOM       = Hex("#0f0e1a");
    static readonly Color LOGO_TOP        = Hex("#ffffff");
    static readonly Color LOGO_BOTTOM     = Hex("#aebbd3");
    static readonly Color BTN_BORDER_FROM = Hex("#e8e7fe");   // 테두리 왼쪽 위
    static readonly Color BTN_BORDER_TO   = Hex("#bbbacb");   // 테두리 오른쪽 아래

    // 그림자는 전부 아래로 떨어지는 같은 빛 방향이다. 값은 텍스처 픽셀 기준.
    const int   LOGO_SHADOW_PAD    = 28;    // 로고 그림이 가장자리까지 차 있어 그림자 자리를 넓힌다
    const int   LOGO_SHADOW_DROP   = 16;    // 어두운 배경 위라 검은 그림자는 진하고 덜 번지게 해야 보인다
    const int   LOGO_SHADOW_BLUR   = 14;
    const float LOGO_SHADOW_ALPHA  = 0.9f;
    const int   ICON_SHADOW_DROP   = 30;    // 아이콘은 둘레를 넓혀 굽는다(ICON_SHADOW_PAD) — 키워도 잘리지 않는다
    const int   ICON_SHADOW_BLUR   = 40;
    const float ICON_SHADOW_ALPHA  = 0.8f;
    // 그림자가 원본 여백(아래 28px)을 넘어도 잘리지 않게 아이콘 둘레를 이만큼 넓힌다.
    // 넓힌 만큼 표시 크기도 키워(ICON_SHOW) 아이콘 자체는 480 그대로 보이게 한다.
    const int   ICON_SHADOW_PAD    = ICON_SHADOW_DROP + ICON_SHADOW_BLUR;
    const int   ICON_TEX           = 512;   // 모드 아이콘 이미지 크기
    const float ICON_SHOW          = 480f * (ICON_TEX + ICON_SHADOW_PAD * 2) / ICON_TEX;
    const int   MUTE_SHADOW_DROP   = 12;     // 음악 아이콘도 둘레를 넓혀 굽는다(MUTE_SHADOW_PAD) — 키워도 잘리지 않는다
    const int   MUTE_SHADOW_BLUR   = 10;
    const float MUTE_SHADOW_ALPHA  = 1f;
    const int   MUTE_SHADOW_PAD    = MUTE_SHADOW_DROP + MUTE_SHADOW_BLUR;
    const int   BTN_SHADOW_DROP    = 12;
    const int   BTN_SHADOW_BLUR    = 12;
    const float BTN_SHADOW_ALPHA   = 0.85f;

    // 구운 결과는 씬을 다시 열어도 그대로 쓴다. 판을 끝내고 메뉴로 돌아올 때마다 굽지 않게.
    static Sprite   s_bgSprite;
    static Sprite   s_logoSprite;
    static readonly System.Collections.Generic.Dictionary<string, Sprite> s_btnSprites =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    static Color Hex(string html)
    {
        ColorUtility.TryParseHtmlString(html, out Color c);
        return c;
    }

    GameObject      _canvasObj;
    GameObject      _lockOverlay;
    Text            _modeNameText;   // 아이콘 아래: 언락 상태면 모드 이름, 락이면 해금 조건
    Button          _startBtn;
    CanvasGroup     _startBtnCG;
    bool            _isAnimating  = false;

    // 언어 전환 시 갱신할 텍스트 참조
    Text _startBtnText;
    Text _noAdsBtnText;
    Text _langBtnText;
    Text _debugLabelText;
    Text _debugSetText;

    // 광고 동의 재설정 버튼(유럽경제지역·영국에서만 나타난다)
    GameObject _adPrivacyBtnObj;
    Text       _adPrivacyText;

    // 디버그 패널: 현재 보고 있는 모드의 점수를 표시/설정
    InputField _debugField;
    Text       _debugModeNameText;

    void Start()
    {
        // 인게임에서 돌아왔으면 방금 하던 모드를 보여 준다. 앱을 처음 켜면 노말(SelectedMode 기본값)이다.
        _currentMode = Mathf.Clamp(ModeSession.SelectedMode, 0, MODE_IMAGES.Length - 1);
        BuildMainMenu();
        LocalizationManager.Instance.OnLanguageChanged += RefreshTexts;
    }

    void OnDestroy()
    {
        LocalizationManager.Instance.OnLanguageChanged -= RefreshTexts;
    }

    void RefreshTexts()
    {
        var loc = LocalizationManager.Instance;
        if (_startBtnText  != null) _startBtnText.text  = loc.Get("start");
        if (_noAdsBtnText  != null) _noAdsBtnText.text  = loc.Get("no_ads");
        if (_langBtnText   != null) _langBtnText.text   = loc.Get("lang_btn");
        if (_debugLabelText!= null) _debugLabelText.text = loc.Get("debug_label");
        if (_debugSetText  != null) _debugSetText.text  = loc.Get("debug_set");
        if (_adPrivacyText != null) _adPrivacyText.text = loc.Get("ad_privacy");
        // 잠금 조건 텍스트도 다시 생성
        UpdateModeDisplay();
    }

    void BuildMainMenu()
    {
        // ── Canvas ──────────────────────────────────────────
        var canvasObj = new GameObject("Canvas");
        _canvasObj    = canvasObj;   // 종료 확인 같은 오버레이를 나중에 붙일 자리
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0f;   // 가로 기준 — 긴 화면에서 판이 잘리지 않게 (CanvasMetrics 참고)

        canvasObj.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        BGMManager.GetOrCreate();
        NetworkChecker.GetOrCreate();
#if UNITY_EDITOR
        // 에디터에는 화면 아래에 디버그 점수 패널이 붙는다. 배너가 그 위를 덮어 입력이
        // 막히므로 메인 메뉴에서는 내려 둔다. 인게임 배너와 기기 빌드는 그대로다.
        AdManager.GetOrCreate().HideBanner();
#else
        AdManager.GetOrCreate().ShowBanner();
#endif

        CreateBackground(canvasObj);
        CreateLogo(canvasObj);
        CreateModeSelector(canvasObj);

        var loc = LocalizationManager.Instance;

        _startBtn   = CreateRoundedButton(canvasObj, loc.Get("start"),
                          anchorY: 0.24f,
                          textColor: START_TEXT,
                          out _startBtnText);
        _startBtnCG = _startBtn.gameObject.AddComponent<CanvasGroup>();

        var noAdsBtn = CreateRoundedButton(canvasObj, loc.Get("no_ads"),
                           anchorY: 0.15f,
                           textColor: BUY_TEXT,
                           out _noAdsBtnText);

        _startBtn.onClick.AddListener(OnStartClicked);
        noAdsBtn.onClick.AddListener(OnRemoveAdsClicked);

        CreateMuteButton(canvasObj);
        CreateShareButton(canvasObj);
        CreateLanguageButton(canvasObj);
        CreateLeaderboardButton(canvasObj);
        CreateAdPrivacyButton(canvasObj);
        // 점수를 직접 고칠 수 있는 패널이라 기기 빌드에는 들어가면 안 된다.
        // 화면에서 숨기는 게 아니라 컴파일 자체에서 빼서, 끄는 걸 잊을 여지를 없앤다.
        // 에디터에서만 나온다.
#if UNITY_EDITOR
        CreateDebugPanel(canvasObj);
#endif
        UpdateModeDisplay();
    }

    void OnStartClicked()
    {
        // 잠긴 모드에서는 같은 버튼이 해금 구매를 맡는다. 자리를 하나 더 만들지 않은 것은,
        // 둘이 동시에 필요한 상황이 없기 때문이다 — 잠겨 있으면 시작할 수 없고, 열려
        // 있으면 살 이유가 없다.
        if (!IsModeUnlocked(_currentMode)) { BuyUnlockAll(); return; }

        // 오버레이로도 막지만, "WiFi off → 같은 프레임에 탭" race를 막기 위해 클릭 시점에 한 번 더 검사.
        // 오프라인이면 그냥 무시 — NetworkChecker.Update가 다음 프레임에 오버레이를 띄움.
        if (Application.internetReachability == NetworkReachability.NotReachable) return;
        ModeSession.SelectedMode = _currentMode;
        SceneManager.LoadScene("InGame");
    }


    /// <summary>
    /// 모든 모드 해금을 산다. 성공하면 화면을 다시 그려 잠금이 걷힌 상태로 바뀐다.
    /// 실패하면(창을 닫았거나 스토어가 거절) 그대로 둔다 — 실패 문구를 띄우기보다
    /// 버튼이 그 자리에 남아 있는 편이 다시 시도하기 쉽다.
    /// </summary>
    void BuyUnlockAll()
    {
        if (!UnlockAll.StoreReady) return;   // 팔 수 없으면 버튼이 애초에 죽어 있다

        UnlockAll.Purchase(
            onSuccess: () =>
            {
                if (this == null) return;   // 결제 중에 씬을 떠났을 수 있다
                UpdateModeDisplay();
            },
            onFailed: () => Debug.Log("[MainMenu] 해금 구매가 완료되지 않았다."));
    }
    bool IsModeUnlocked(int idx)
    {
        if (IsComingSoon(idx)) return false;

        // 해금 상품을 산 사람은 점수 조건을 건너뛴다.
        if (UnlockAll.Owned) return true;

        return PlayerPrefs.GetInt($"m{MODE_UNLOCK_FROM[idx]}_HighScore", 0) >= MODE_UNLOCK_SCORE[idx];
    }

    string UnlockDescription(int idx)
    {
        if (IsComingSoon(idx)) return "";
        if (MODE_UNLOCK_SCORE[idx] == 0) return "";
        var loc      = LocalizationManager.Instance;
        string mName = loc.Get(MODE_LOC_KEYS[MODE_UNLOCK_FROM[idx]]);
        return string.Format(loc.Get("unlock_fmt"), mName, MODE_UNLOCK_SCORE[idx]);
    }

    // ── 배경 ─────────────────────────────────────────────────
    void CreateBackground(GameObject parent)
    {
        var obj = new GameObject("Background");
        obj.transform.SetParent(parent.transform, false);

        // 화면보다 작게 구워 늘린다. 위아래 그라데이션이라 화면 비율이 달라도 모양이 같다.
        if (s_bgSprite == null) s_bgSprite = UIEffects.VerticalGradient(270, 480, BG_TOP, BG_BOTTOM);
        var img = obj.AddComponent<Image>();
        img.sprite        = s_bgSprite;
        img.raycastTarget = false;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── 로고 ──────────────────────────────────────────────────
    // 로고 PNG(948×302)를 원본 크기 그대로 배치한다. 캔버스 기준 해상도가 1080 폭이라
    // 1:1 픽셀 매핑이 되어 리샘플링 없이 가장 선명하고, 좌우 여백도 132px씩 남는다.
    // PNG는 팔레트+tRNS로 배경이 투명하고 글자색이 이미 #E2E8F0이라 tint 없이 흰색으로 둔다.
    const int LOGO_W = 948;
    const int LOGO_H = 302;

    void CreateLogo(GameObject parent)
    {
        var obj = new GameObject("Logo");
        obj.transform.SetParent(parent.transform, false);

        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 620);

        var logoTex = Resources.Load<Texture2D>("Sprites/Logo/logo");
        if (logoTex != null)
        {
            // 원래 한 가지 색(#E2E8F0)인 로고에 위→아래 그라데이션을 입히고 그림자를 깐다.
            // 그림자 자리만큼 텍스처가 커지므로 표시 크기도 같이 키워 1:1 매핑을 지킨다.
            if (s_logoSprite == null)
            {
                var px = UIEffects.ReadPixels(logoTex);
                UIEffects.TintVertical(px, logoTex.width, logoTex.height, LOGO_TOP, LOGO_BOTTOM);
                s_logoSprite = UIEffects.ToSprite(UIEffects.WithDropShadow(
                    px, logoTex.width, logoTex.height, LOGO_SHADOW_PAD,
                    new Vector2Int(0, -LOGO_SHADOW_DROP), LOGO_SHADOW_BLUR, LOGO_SHADOW_ALPHA, Color.black));
            }

            float scale       = LOGO_W / (float)logoTex.width;
            var img           = obj.AddComponent<Image>();
            img.sprite        = s_logoSprite;
            img.color         = Color.white;
            img.raycastTarget = false;
            rt.sizeDelta      = new Vector2(LOGO_W + LOGO_SHADOW_PAD * 2 * scale,
                                            LOGO_H + LOGO_SHADOW_PAD * 2 * scale);
            return;
        }

        // 이미지를 못 찾으면 예전처럼 텍스트로 그려서 화면이 비지 않게 한다
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color logoColor);
        var txt       = obj.AddComponent<Text>();
        txt.text      = "MATBLAST";
        txt.fontSize  = 150;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = logoColor;
        txt.font      = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
        txt.raycastTarget = false;
        rt.sizeDelta  = new Vector2(980, 200);
    }

    // ── 모드 선택기 ───────────────────────────────────────────
    void CreateModeSelector(GameObject parent)
    {
        var container = new GameObject("ModeSelector");
        container.transform.SetParent(parent.transform, false);
        var cRt = container.AddComponent<RectTransform>();
        cRt.anchorMin        = new Vector2(0.5f, 0.5f);
        cRt.anchorMax        = new Vector2(0.5f, 0.5f);
        cRt.pivot            = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(0, 160);
        cRt.sizeDelta        = new Vector2(1000, 560);

        // ── 아이콘 클립 영역 (슬라이드 시 바깥 잘라냄) ──────
        var clipObj = new GameObject("IconClip");
        clipObj.transform.SetParent(container.transform, false);
        clipObj.AddComponent<RectMask2D>();

        // 투명한 판을 깔아 클립 전체가 입력을 받게 한다. 미리보기 자리를 탭했을 때
        // 넘어가려면 그 자리에 레이캐스트를 받는 무언가가 있어야 한다.
        var clipHit = clipObj.AddComponent<Image>();
        clipHit.color         = new Color(0f, 0f, 0f, 0f);
        clipHit.raycastTarget = true;
        var clipRt = clipObj.GetComponent<RectTransform>();
        clipRt.anchorMin        = new Vector2(0.5f, 0.5f);
        clipRt.anchorMax        = new Vector2(0.5f, 0.5f);
        clipRt.pivot            = new Vector2(0.5f, 0.5f);
        clipRt.anchoredPosition = new Vector2(0, 30);
        // 화면(1080)보다 넓게 잡는다. 마스크 경계가 화면 안쪽에 있으면 미리보기가 허공에서
        // 싹둑 잘린 네모로 보인다. 경계를 화면 밖으로 밀어 두면 화면 끝이 대신 자르므로
        // "밖으로 이어진다"로 읽힌다. 슬라이드로 빠지는 아이콘도 화면 밖에서 사라진다.
        // 높이는 그림자까지 들어가게 넓힌 아이콘 크기에 맞춘다.
        clipRt.sizeDelta        = new Vector2(1160, ICON_SHOW);

        // ── 모드 아이콘 (클립 안) ─────────────────────────
        var iconObj = new GameObject("ModeIcon");
        iconObj.transform.SetParent(clipObj.transform, false);
        _modeIcon = iconObj.AddComponent<Image>();
        _modeIcon.preserveAspect = true;
        _modeIconRt               = iconObj.GetComponent<RectTransform>();
        _modeIconRt.anchorMin     = new Vector2(0.5f, 0.5f);
        _modeIconRt.anchorMax     = new Vector2(0.5f, 0.5f);
        _modeIconRt.pivot         = new Vector2(0.5f, 0.5f);
        _modeIconRt.anchoredPosition = Vector2.zero;
        _modeIconRt.sizeDelta     = new Vector2(ICON_SHOW, ICON_SHOW);

        // ── 잠금 오버레이 (클립 밖, 같은 위치에 덮어씌움) ─
        _lockOverlay = new GameObject("LockOverlay");
        _lockOverlay.transform.SetParent(container.transform, false);
        var lockBg   = _lockOverlay.AddComponent<Image>();
        lockBg.color        = new Color(0f, 0f, 0f, 0.65f);
        lockBg.raycastTarget = false;
        var lockBgRt = _lockOverlay.GetComponent<RectTransform>();
        lockBgRt.anchorMin        = new Vector2(0.5f, 0.5f);
        lockBgRt.anchorMax        = new Vector2(0.5f, 0.5f);
        lockBgRt.pivot            = new Vector2(0.5f, 0.5f);
        lockBgRt.anchoredPosition = new Vector2(0, 30);
        lockBgRt.sizeDelta        = new Vector2(480, 480);

        // 자물쇠 기호 (아이콘 중앙)
        var lockSymObj = new GameObject("LockSymbol");
        lockSymObj.transform.SetParent(_lockOverlay.transform, false);
        var lockSym = lockSymObj.AddComponent<Text>();
        lockSym.text      = "🔒";
        lockSym.fontSize  = 140;
        lockSym.alignment = TextAnchor.MiddleCenter;
        lockSym.color     = Color.white;
        lockSym.font      = Font4();
        var lockSymRt = lockSymObj.GetComponent<RectTransform>();
        lockSymRt.anchorMin        = new Vector2(0.5f, 0.5f);
        lockSymRt.anchorMax        = new Vector2(0.5f, 0.5f);
        lockSymRt.pivot            = new Vector2(0.5f, 0.5f);
        lockSymRt.anchoredPosition = Vector2.zero;
        lockSymRt.sizeDelta        = new Vector2(300, 200);

        // ── 모드 이름 / 해금 조건 텍스트 (아이콘 아래) ───
        var nameObj = new GameObject("ModeName");
        nameObj.transform.SetParent(container.transform, false);
        _modeNameText = nameObj.AddComponent<Text>();
        _modeNameText.text      = "";
        _modeNameText.font      = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
        _modeNameText.fontSize  = 56;
        _modeNameText.fontStyle = FontStyle.Normal;
        _modeNameText.alignment = TextAnchor.MiddleCenter;
        _modeNameText.resizeTextForBestFit = true;
        _modeNameText.resizeTextMinSize    = 28;
        _modeNameText.resizeTextMaxSize    = 56;
        _modeNameText.horizontalOverflow   = HorizontalWrapMode.Wrap;
        _modeNameText.verticalOverflow     = VerticalWrapMode.Overflow;
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color nameColor);
        _modeNameText.color = nameColor;
        var nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.anchorMin        = new Vector2(0.5f, 0.5f);
        nameRt.anchorMax        = new Vector2(0.5f, 0.5f);
        nameRt.pivot            = new Vector2(0.5f, 0.5f);
        nameRt.anchoredPosition = new Vector2(0, -320);
        nameRt.sizeDelta        = new Vector2(900, 140);

        // ── 스와이프 핸들러 ───────────────────────────────
        var swiper = clipObj.AddComponent<ModeSwipeHandler>();
        swiper.Init(this);

        // ── 좌우 미리보기 ────────────────────────────────
        _peekLeft  = CreatePeek(clipObj, -1);
        _peekRight = CreatePeek(clipObj, +1);

        // ── 위치 점 ──────────────────────────────────────
        CreateModeDots(container);
    }

    /// <summary>

    /// <summary>
    /// 이웃 모드를 클립 안 좌우에 걸쳐 둔다. 클립 바깥에 두면 슬라이드에 참여하지 못해,
    /// 걸쳐 보이던 그림은 가만히 있고 엉뚱한 사본이 밀려오는 것처럼 보인다. 같은 클립
    /// 안, 그것도 슬라이드가 시작되는 자리에 세워야 "옆에 있던 그것이 온다"가 된다.
    /// </summary>
    Image CreatePeek(GameObject parent, int dir)
    {
        var obj = new GameObject(dir < 0 ? "PeekLeft" : "PeekRight");
        obj.transform.SetParent(parent.transform, false);

        var img = obj.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget  = false;   // 스와이프는 가운데 아이콘이 받는다
        img.enabled        = false;   // 스프라이트가 들어오기 전까지는 흰 사각형이다
        img.color          = new Color(1f, 1f, 1f, PEEK_ALPHA);

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(ICON_SHOW, ICON_SHOW);
        rt.anchoredPosition = new Vector2(dir * SLIDE_DIST, 0f);

        return img;
    }

    /// <summary>슬라이드 도중에는 감춘다 — 들어오는 아이콘과 같은 자리라 두 장으로 겹친다.</summary>
    void SetPeeksVisible(bool on)
    {
        if (_peekLeft  != null) _peekLeft.enabled  = on && _peekLeft.sprite  != null;
        if (_peekRight != null) _peekRight.enabled = on && _peekRight.sprite != null;
    }

    /// <summary>양옆에 걸쳐 보이는 이웃을 현재 위치에 맞춘다.</summary>
    void RefreshPeeks()
    {
        SetPeek(_peekLeft,  (_currentMode - 1 + TotalSlots) % TotalSlots);
        SetPeek(_peekRight, (_currentMode + 1) % TotalSlots);
    }

    void SetPeek(Image img, int slot)
    {
        if (img == null) return;

        // Coming Soon 슬롯은 그림이 없다. 빈 사각형을 걸쳐 두느니 그 방향은 비운다.
        var spr = LoadModeSprite(slot);
        if (spr != null) img.sprite = spr;
        img.enabled = spr != null;
    }

    /// <summary>
    /// 모드 아이콘 스프라이트. 미리보기까지 생기면서 전환 한 번에 셋이 필요해졌다.
    /// 매번 Sprite.Create 를 부르지 않도록 한 번 만든 것은 들고 있는다.
    /// </summary>
    Sprite LoadModeSprite(int slot)
    {
        if (IsComingSoon(slot)) return null;

        if (s_modeSpriteCache == null) s_modeSpriteCache = new Sprite[MODE_IMAGES.Length];
        if (s_modeSpriteCache[slot] != null) return s_modeSpriteCache[slot];

        var tex = Resources.Load<Texture2D>(MODE_IMAGES[slot]);
        if (tex == null) return null;

        // 그림자를 그림 안에 구워 넣는다. 따로 된 그림자 오브젝트였다면 슬라이드·미리보기
        // 투명도·잠금 덮개를 전부 따라다니게 해야 했지만, 한 장이면 지금 코드가 그대로 맞는다.
        // 둘레를 ICON_SHADOW_PAD 만큼 넓혀 굽고, 표시 크기(ICON_SHOW)도 그만큼 키워 둔다.
        var shadowed = UIEffects.WithDropShadow(
            UIEffects.ReadPixels(tex), tex.width, tex.height, ICON_SHADOW_PAD,
            new Vector2Int(0, -ICON_SHADOW_DROP), ICON_SHADOW_BLUR, ICON_SHADOW_ALPHA, Color.black);
        s_modeSpriteCache[slot] = UIEffects.ToSprite(shadowed);
        return s_modeSpriteCache[slot];
    }
    /// 슬롯 개수만큼 점을 깔아 지금 몇 번째인지 보여 준다. 화살표가 하던 "옆에 더 있다"는
    /// 신호를 대신하면서, 화살표가 못 하던 "전부 몇 개인지"까지 같이 말해 준다.
    /// </summary>
    void CreateModeDots(GameObject parent)
    {
        const float gap = 34f;

        var dotSprite = CreateDiamondSprite(32);   // 점마다 텍스처를 새로 굽지 않는다
        _modeDots     = new Image[TotalSlots];
        float startX  = -(TotalSlots - 1) * gap * 0.5f;

        for (int i = 0; i < TotalSlots; i++)
        {
            var dotObj = new GameObject("Dot" + i);
            dotObj.transform.SetParent(parent.transform, false);

            var img = dotObj.AddComponent<Image>();
            img.sprite        = dotSprite;
            img.raycastTarget = false;
            _modeDots[i]      = img;

            var rt = dotObj.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startX + i * gap, -410f);
            rt.sizeDelta        = new Vector2(14f, 14f);
        }
    }

    /// <summary>현재 위치만 밝고 크게. 나머지는 있는 듯 없는 듯 둔다.</summary>
    void RefreshModeDots()
    {
        if (_modeDots == null) return;

        for (int i = 0; i < _modeDots.Length; i++)
        {
            if (_modeDots[i] == null) continue;

            bool on = i == _currentMode;
            // 지금 모드는 인게임 최고 점수(BEST) 글자와 같은 노란색, 나머지는 흐린 흰색.
            _modeDots[i].color = on ? DOT_ON : new Color(1f, 1f, 1f, 0.25f);
            // 마름모는 같은 상자 안에서 원보다 작아 보여서 원(18/12)보다 조금 크게 잡는다.
            _modeDots[i].rectTransform.sizeDelta = Vector2.one * (on ? 22f : 15f);
        }
    }

    /// <summary>
    /// 위치 표시 마름모. 가로세로 중심까지의 거리 합(|x|+|y|)이 반지름 안이면 칠한다.
    /// 경계를 1px 부드럽게 해야 작은 크기에서 톱니가 안 보인다.
    /// </summary>
    static Sprite CreateDiamondSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px   = new Color32[size * size];
        float h  = size * 0.5f;
        float r  = h - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // |x|+|y| 거리는 대각선 방향으로 √2 배 늘어나 있어서, 경계 흐림 폭도 그만큼 맞춘다.
                float d = (Mathf.Abs(x + 0.5f - h) + Mathf.Abs(y + 0.5f - h) - r) / 1.41421f;
                float a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }



    /// <summary>색은 그대로 두고 투명도만 바꾼다.</summary>
    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    public void ShiftMode(int dir)
    {
        // 전환이 도는 동안 들어온 입력을 버리면, 빠르게 두 번 넘겼을 때 두 번째가
        // 사라져 "뜻대로 안 되는" 느낌이 된다. 한 번은 받아 뒀다가 이어서 처리한다.
        if (_isAnimating) { _pendingShift = dir; return; }
        int newMode = (_currentMode + dir + TotalSlots) % TotalSlots;
        StartCoroutine(SlideMode(dir, newMode));
    }

    IEnumerator SlideMode(int dir, int newMode)
    {
        _isAnimating = true;
        SetPeeksVisible(false);   // 들어오는 아이콘과 같은 자리다 — 겹쳐서 두 장이 된다

        // 새 비주얼 생성 (클립 컨테이너 안, 화면 밖에서 시작)
        bool comingSoon = IsComingSoon(newMode);
        var nextObj = new GameObject(comingSoon ? "ComingSoonNext" : "ModeIconNext");
        nextObj.transform.SetParent(_modeIconRt.parent, false);

        Image nextImg = null;
        if (comingSoon)
        {
            var txt = nextObj.AddComponent<Text>();
            txt.text      = LocalizationManager.Instance.Get("coming_soon");
            txt.font      = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
            txt.fontSize  = 78;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color     = new Color(0.75f, 0.80f, 0.92f);
        }
        else
        {
            nextImg = nextObj.AddComponent<Image>();
            nextImg.preserveAspect = true;
            nextImg.sprite = LoadModeSprite(newMode);   // 캐시를 거쳐 중복 생성을 막는다
        }

        var nextRt = nextObj.GetComponent<RectTransform>();
        nextRt.anchorMin        = new Vector2(0.5f, 0.5f);
        nextRt.anchorMax        = new Vector2(0.5f, 0.5f);
        nextRt.pivot            = new Vector2(0.5f, 0.5f);
        nextRt.sizeDelta        = new Vector2(ICON_SHOW, ICON_SHOW);
        nextRt.anchoredPosition = new Vector2(dir * SLIDE_DIST, 0);

        // 들어오는 쪽은 미리보기가 서 있던 밝기에서 출발해 또렷해지고, 나가는 쪽은 반대로
        // 흐려진다. 그래야 옆에 걸쳐 있던 그림이 그대로 밀려온 것으로 읽힌다.
        // Image 와 Text(Coming Soon) 둘 다 Graphic 이라 색으로 한 번에 다룬다.
        var curGfx  = _modeIconRt.GetComponent<Graphic>();
        var nextGfx = nextObj.GetComponent<Graphic>();
        if (nextGfx != null) nextGfx.color = WithAlpha(nextGfx.color, PEEK_ALPHA);

        // 슬라이드 애니메이션 (SmoothStep 이징)
        float duration = 0.28f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _modeIconRt.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(-dir * SLIDE_DIST, 0), t);
            nextRt.anchoredPosition      = Vector2.Lerp(new Vector2(dir * SLIDE_DIST, 0), Vector2.zero, t);

            if (curGfx  != null) curGfx.color  = WithAlpha(curGfx.color,  Mathf.Lerp(1f, PEEK_ALPHA, t));
            if (nextGfx != null) nextGfx.color = WithAlpha(nextGfx.color, Mathf.Lerp(PEEK_ALPHA, 1f, t));
            yield return null;
        }

        // 정리: 기존 비주얼 제거, 새 비주얼을 현재로 교체
        Destroy(_modeIconRt.gameObject);
        _modeIcon    = nextImg;   // Coming Soon 슬롯이면 null
        _modeIconRt  = nextRt;
        _currentMode = newMode;

        UpdateModeDisplay();
        _isAnimating = false;

        if (_pendingShift != 0)
        {
            int d = _pendingShift;
            _pendingShift = 0;
            ShiftMode(d);
        }
    }


    // ── 뒤로가기(안드로이드 백 버튼) ─────────────────────────────
    // 새 Input System 만 켜져 있어(activeInputHandler = 1) Input.GetKeyDown 은 동작하지 않는다.
    // 안드로이드 백 버튼은 Keyboard.escapeKey 로 들어온다.
    GameObject _quitOverlay;

    void Update()
    {
        // 동의 절차가 끝나야 필요 여부를 알 수 있다. 한 번 켜지면 그대로 둔다.
        if (_adPrivacyBtnObj != null && !_adPrivacyBtnObj.activeSelf &&
            AdManager.GetOrCreate().IsPrivacyOptionsRequired)
        {
            _adPrivacyBtnObj.SetActive(true);
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        OnBackPressed();
    }

    /// <summary>
    /// 뒤로가기는 "한 겹 벗기기"로 동작한다. 열려 있는 것이 있으면 그것부터 닫고,
    /// 아무것도 없을 때만 종료를 묻는다. 실수로 앱이 꺼지는 일이 없어야 한다.
    /// </summary>
    void OnBackPressed()
    {
        if (_quitOverlay != null) { CloseQuitDialog(); return; }

        // 순위표가 떠 있으면 그것부터 닫는다.
        var panel = FindFirstObjectByType<LeaderboardPanel>();
        if (panel != null) { Destroy(panel.gameObject); return; }

        // 공유 화면도 마찬가지다. 안 챙기면 공유 화면 위에 종료 확인 창이 뜬다.
        var share = FindFirstObjectByType<SharePanel>();
        if (share != null) { Destroy(share.gameObject); return; }

        ShowQuitDialog();
    }

    void CloseQuitDialog()
    {
        if (_quitOverlay == null) return;
        Destroy(_quitOverlay);
        _quitOverlay = null;
    }

    void ShowQuitDialog()
    {
        var loc = LocalizationManager.Instance;

        _quitOverlay = new GameObject("QuitOverlay");
        _quitOverlay.transform.SetParent(_canvasObj.transform, false);
        _quitOverlay.transform.SetAsLastSibling();

        // 뒤를 가려 두지 않으면 아래 버튼이 그대로 눌린다.
        var dim = _quitOverlay.AddComponent<Image>();
        // 뒤가 비치면 시작 버튼이 눈에 들어와 확인창이 가벼워 보인다. 거의 덮는다.
        dim.color = new Color(0.01f, 0.01f, 0.03f, 0.97f);

        var dimRt = _quitOverlay.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;

        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_quitOverlay.transform, false);

        var title = titleObj.AddComponent<Text>();
        title.text      = loc.Get("quit_title");
        title.font      = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
        title.fontSize  = 68;
        title.alignment = TextAnchor.MiddleCenter;
        title.color     = START_TEXT;
        title.horizontalOverflow = HorizontalWrapMode.Wrap;

        var titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin        = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax        = new Vector2(0.5f, 0.5f);
        titleRt.pivot            = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0, 150);
        titleRt.sizeDelta        = new Vector2(900, 180);

        var yes = CreateRoundedButton(_quitOverlay, loc.Get("quit_yes"),
                      anchorY: 0.44f, textColor: QUIT_YES_TEXT, out _);
        yes.onClick.AddListener(Quit);

        var no = CreateRoundedButton(_quitOverlay, loc.Get("quit_no"),
                     anchorY: 0.33f, textColor: START_TEXT, out _);
        no.onClick.AddListener(CloseQuitDialog);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    void UpdateModeDisplay()
    {
        var loc = LocalizationManager.Instance;

        if (IsComingSoon(_currentMode))
        {
            // Coming Soon 슬롯: 잠금/시작버튼/디버그/이름 모두 숨김, BGM 유지
            _lockOverlay.SetActive(false);
            if (_modeNameText != null) _modeNameText.text = "";
            if (_startBtnCG != null)
            {
                _startBtnCG.alpha          = 0f;
                _startBtnCG.interactable   = false;
                _startBtnCG.blocksRaycasts = false;
            }
            if (_debugField != null)         _debugField.text         = "";
            if (_debugModeNameText != null)  _debugModeNameText.text  = loc.Get("coming_soon").Replace("\n", " ");
            return;
        }

        if (_modeIcon != null)
            _modeIcon.sprite = LoadModeSprite(_currentMode);

        RefreshModeDots();
        RefreshPeeks();

        // 모드 미리보기 BGM 전환 (잠겨 있어도 전환)
        BGMManager.Instance?.PlayBGM(ModeConfig.Modes[_currentMode].bgmClip);

        bool unlocked = IsModeUnlocked(_currentMode);
        _lockOverlay.SetActive(!unlocked);

        // 아이콘 아래: 잠금 여부에 따라 모드 이름 또는 해금 조건 표시
        if (_modeNameText != null)
            _modeNameText.text = unlocked ? loc.Get(MODE_LOC_KEYS[_currentMode]) : UnlockDescription(_currentMode);

        // 잠긴 모드에서는 시작 버튼 자리를 해금 구매가 대신한다. 죽어 있는 버튼으로
        // 못 하는 이유만 보여 주는 것보다, 지금 할 수 있는 선택을 주는 편이 낫다.
        // 팔 수 없는 상태(스토어 미연결)면 예전처럼 흐릿하게 잠근다.
        bool offerUnlock = !unlocked && UnlockAll.StoreReady;
        bool btnLive     = unlocked || offerUnlock;

        if (_startBtnText != null)
        {
            // 별표는 "특별한 선택지"라는 뜻으로 흔히 읽히고, 이모지와 달리 어느 폰트에서나
            // 같은 모양으로 나온다.
            _startBtnText.text  = offerUnlock ? "★ " + loc.Get("unlock_all") : loc.Get("start");
            _startBtnText.color = offerUnlock ? UNLOCK_TEXT : START_TEXT;
        }

        if (_startBtnCG != null)
        {
            _startBtnCG.alpha          = btnLive ? 1f : 0.4f;
            _startBtnCG.interactable   = btnLive;
            _startBtnCG.blocksRaycasts = btnLive;
        }

        // 디버그 패널: 현재 보고 있는 모드의 점수 표시
        if (_debugField != null)
            _debugField.text = PlayerPrefs.GetInt($"m{_currentMode}_HighScore", 0).ToString();
        if (_debugModeNameText != null)
            _debugModeNameText.text = loc.Get(MODE_LOC_KEYS[_currentMode]);
    }

    // ── 둥근 버튼 생성 ────────────────────────────────────────
    Button CreateRoundedButton(GameObject parent, string label, float anchorY, Color textColor, out Text labelOut)
    {
        var obj = new GameObject(label + "Button");
        obj.transform.SetParent(parent.transform, false);

        var skin = CreateButtonSkin(obj, 700, 130, 36, 4);

        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = skin;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration     = 0.1f;
        btn.colors = colors;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, anchorY);
        rt.anchorMax = new Vector2(0.5f, anchorY);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(700, 130);

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);

        var txt = txtObj.AddComponent<Text>();
        txt.text      = label;
        txt.font      = Font4();
        txt.fontSize  = 52;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = textColor;
        txt.alignment = TextAnchor.MiddleCenter;

        var txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        labelOut = txt;
        return btn;
    }

    // ── 언어 선택 버튼 (좌상단) ──────────────────────────────
    void CreateLanguageButton(GameObject parent)
    {
        var obj = new GameObject("LanguageButton");
        obj.transform.SetParent(parent.transform, false);

        var skin = CreateButtonSkin(obj, 160, 80, 30, 3);

        var btn    = obj.AddComponent<Button>();
        btn.targetGraphic = skin;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration     = 0.1f;
        btn.colors = colors;

        var rt              = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30, -55);
        rt.sizeDelta        = new Vector2(160, 80);

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        _langBtnText = txtObj.AddComponent<Text>();
        _langBtnText.text      = LocalizationManager.Instance.Get("lang_btn");
        _langBtnText.font      = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
        _langBtnText.fontSize  = 42;
        _langBtnText.fontStyle = FontStyle.Bold;
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color txtColor);
        _langBtnText.color     = txtColor;
        _langBtnText.alignment = TextAnchor.MiddleCenter;

        var txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() => LocalizationManager.Instance.ToggleLanguage());
    }

    // ── 버튼 겉모양 ──────────────────────────────────────────
    /// <summary>
    /// 버튼에 그림자 진 그라데이션 테두리를 붙이고 그 그림을 돌려준다. 속은 비워 배경이 비친다.
    ///
    /// 자식으로 둔다. 유니티 UI 는 자식을 부모 위에 그려서, 부르는 쪽이 나중에 붙이는
    /// 글자·아이콘이 그 위에 온다. 버튼을 끄거나(광고 설정) 흐리게 해도(시작 버튼의
    /// CanvasGroup) 테두리와 그림자가 같이 따라간다.
    ///
    /// 그림자는 버튼 바깥으로만 떨어지게 같은 텍스처에 구워서, 그림 크기가 그림자 자리만큼 크다.
    /// 누름 영역은 raycastPadding 으로 버튼 크기에 맞춰 되돌린다.
    /// w·h 는 버튼의 실제 크기(sizeDelta)와 같아야 그라데이션이 늘어나지 않는다.
    ///
    /// 인게임 뒤로가기 버튼(InGameUI.BuildBackButton)도 이걸 그대로 쓴다. 두 화면의 버튼 모양이
    /// 한 곳에서 정해지도록 static 으로 열어 두었다.
    /// </summary>
    public static Image CreateButtonSkin(GameObject obj, int w, int h, int radius, int border)
    {
        if (obj.GetComponent<RectTransform>() == null) obj.AddComponent<RectTransform>();

        // 그림자가 아래로 떨어지므로 아래쪽 여백이 가장 커야 한다
        int pad = BTN_SHADOW_BLUR + BTN_SHADOW_DROP;
        if (!s_btnSprites.TryGetValue($"{w}x{h}r{radius}b{border}", out var spr) || spr == null)
        {
            var ring = UIEffects.RoundedRing(w, h, radius, border, BTN_BORDER_FROM, BTN_BORDER_TO);
            // 그림자는 버튼 전체 모양에서 떨어뜨려 바깥에만 보이게 한다 — 얇은 테두리만으로는
            // 그림자가 번져 사라지고, 속까지 어둡게 하면 비어 있는 버튼이 탁해진다.
            var body = UIEffects.RoundedRing(w, h, radius, h, Color.white, Color.white);
            spr = UIEffects.ToSprite(UIEffects.WithDropShadow(ring, w, h, pad,
                new Vector2Int(0, -BTN_SHADOW_DROP), BTN_SHADOW_BLUR, BTN_SHADOW_ALPHA, Color.black, body));
            s_btnSprites[$"{w}x{h}r{radius}b{border}"] = spr;
        }

        var go  = new GameObject("Skin");
        go.transform.SetParent(obj.transform, false);
        var img = go.AddComponent<Image>();
        img.sprite        = spr;
        img.raycastTarget = true;   // 버튼의 누름은 이 그림이 받는다
        img.raycastPadding = new Vector4(pad, pad, pad, pad);
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-pad, -pad);
        rt.offsetMax = new Vector2( pad,  pad);
        return img;
    }

    // ── BGM 뮤트 토글 버튼 ───────────────────────────────────
    void CreateMuteButton(GameObject parent)
    {
        var bgm = BGMManager.Instance;

        var obj = new GameObject("MuteButton");
        obj.transform.SetParent(parent.transform, false);

        var img = obj.AddComponent<Image>();
        img.preserveAspect = true;
        img.sprite         = LoadMuteSprite(bgm.IsMuted);

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
        // 그림은 그림자 자리만큼 사방으로 크다(이미지 100px 을 100 으로 그린다). 아이콘이
        // 예전 자리에 그대로 오도록 넓힌 만큼 밀어 두고, 누름 영역은 아이콘 크기로 되돌린다.
        rt.anchoredPosition = new Vector2(-30 + MUTE_SHADOW_PAD, -55 + MUTE_SHADOW_PAD);
        rt.sizeDelta        = new Vector2(100 + MUTE_SHADOW_PAD * 2, 100 + MUTE_SHADOW_PAD * 2);
        img.raycastPadding  = Vector4.one * MUTE_SHADOW_PAD;

        btn.onClick.AddListener(() =>
        {
            bgm.ToggleMute();
            img.sprite = LoadMuteSprite(bgm.IsMuted);
        });
    }

    // ── 최고 점수 공유 버튼 (음악 버튼 바로 왼쪽) ───────────────
    // 음악 버튼과 같은 크기·그라데이션·그림자. 음악 버튼(오른쪽 끝 -30, 폭 100)에서 한 칸(120) 왼쪽이다.
    void CreateShareButton(GameObject parent)
    {
        var obj = new GameObject("ShareButton");
        obj.transform.SetParent(parent.transform, false);

        var img = obj.AddComponent<Image>();
        img.preserveAspect = true;
        img.sprite         = LoadTintedIcon("Sprites/UI/share");

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
        rt.anchoredPosition = new Vector2(-150 + MUTE_SHADOW_PAD, -55 + MUTE_SHADOW_PAD);
        rt.sizeDelta        = new Vector2(100 + MUTE_SHADOW_PAD * 2, 100 + MUTE_SHADOW_PAD * 2);
        img.raycastPadding  = Vector4.one * MUTE_SHADOW_PAD;

        btn.onClick.AddListener(() =>
            SharePanel.Open(parent.transform, IsComingSoon(_currentMode) ? 0 : _currentMode));
    }

    /// <summary>
    /// 음악 아이콘. 한 가지 색 그림이라 로고와 같은 그라데이션을 입히고 그림자를 깐다.
    /// 원본 여백(아래 16px)으로는 그림자가 잘리므로 둘레를 넓혀 굽는다. 켜짐·꺼짐 둘 다 한 번만 굽는다.
    /// </summary>
    Sprite LoadMuteSprite(bool muted) => LoadTintedIcon(muted ? "Sprites/UI/mute" : "Sprites/UI/bgm_on");

    // 한 가지 색 아이콘에 로고 그라데이션과 그림자를 입힌다. 음악·공유 버튼이 같이 쓴다.
    Sprite LoadTintedIcon(string name)
    {
        if (s_btnSprites.TryGetValue(name, out var spr) && spr != null) return spr;

        var tex = Resources.Load<Texture2D>(name);
        if (tex == null) return null;
        var px = UIEffects.ReadPixels(tex);
        UIEffects.TintVertical(px, tex.width, tex.height, LOGO_TOP, LOGO_BOTTOM);
        spr = UIEffects.ToSprite(UIEffects.WithDropShadow(px, tex.width, tex.height, MUTE_SHADOW_PAD,
            new Vector2Int(0, -MUTE_SHADOW_DROP), MUTE_SHADOW_BLUR, MUTE_SHADOW_ALPHA, Color.black));
        return s_btnSprites[name] = spr;
    }

    Sprite LoadSprite(string name)
    {
        var tex = Resources.Load<Texture2D>(name);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    Font Font4() => Resources.Load<Font>("Fonts/SCDream4") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    // ── [DEBUG] 최고 점수 설정 패널 ──────────────────────────
    // 스와이프로 표시되는 현재 모드의 m{idx}_HighScore를 직접 편집
#if UNITY_EDITOR
    void CreateDebugPanel(GameObject parent)
    {
        var panel = new GameObject("DebugPanel");
        panel.transform.SetParent(parent.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin        = new Vector2(0.5f, 0f);
        panelRt.anchorMax        = new Vector2(0.5f, 0f);
        panelRt.pivot            = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0, 18);
        panelRt.sizeDelta        = new Vector2(700, 145);

        // 상단 레이블: "DEBUG 최고점수:  [모드이름]"
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(panel.transform, false);
        _debugLabelText = labelObj.AddComponent<Text>();
        _debugLabelText.text      = LocalizationManager.Instance.Get("debug_label");
        _debugLabelText.font      = Font4();
        _debugLabelText.fontSize  = 30;
        _debugLabelText.color     = new Color(1f, 0.6f, 0.2f);
        _debugLabelText.alignment = TextAnchor.MiddleLeft;
        var labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin        = new Vector2(0f, 1f);
        labelRt.anchorMax        = new Vector2(0f, 1f);
        labelRt.pivot            = new Vector2(0f, 1f);
        labelRt.anchoredPosition = new Vector2(0, -5);
        labelRt.sizeDelta        = new Vector2(360, 60);

        // 현재 보고 있는 모드 이름 (레이블 옆)
        var modeNameObj = new GameObject("ModeName");
        modeNameObj.transform.SetParent(panel.transform, false);
        _debugModeNameText = modeNameObj.AddComponent<Text>();
        _debugModeNameText.text      = LocalizationManager.Instance.Get(MODE_LOC_KEYS[_currentMode]);
        _debugModeNameText.font      = Font4();
        _debugModeNameText.fontSize  = 32;
        _debugModeNameText.fontStyle = FontStyle.Bold;
        _debugModeNameText.color     = new Color(0.95f, 0.85f, 0.45f);
        _debugModeNameText.alignment = TextAnchor.MiddleLeft;
        var modeNameRt = modeNameObj.GetComponent<RectTransform>();
        modeNameRt.anchorMin        = new Vector2(0f, 1f);
        modeNameRt.anchorMax        = new Vector2(0f, 1f);
        modeNameRt.pivot            = new Vector2(0f, 1f);
        modeNameRt.anchoredPosition = new Vector2(360, -5);
        modeNameRt.sizeDelta        = new Vector2(340, 60);

        // 입력창
        var fieldObj = new GameObject("InputField");
        fieldObj.transform.SetParent(panel.transform, false);
        var fieldImg = fieldObj.AddComponent<Image>();
        fieldImg.color = new Color(0.2f, 0.2f, 0.28f);
        _debugField = fieldObj.AddComponent<InputField>();
        _debugField.contentType = InputField.ContentType.IntegerNumber;
        _debugField.text        = PlayerPrefs.GetInt($"m{_currentMode}_HighScore", 0).ToString();
        var fieldRt = fieldObj.GetComponent<RectTransform>();
        fieldRt.anchorMin        = new Vector2(0f, 0f);
        fieldRt.anchorMax        = new Vector2(0f, 0f);
        fieldRt.pivot            = new Vector2(0f, 0f);
        fieldRt.anchoredPosition = new Vector2(0, 5);
        fieldRt.sizeDelta        = new Vector2(520, 65);

        var phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(fieldObj.transform, false);
        var ph = phObj.AddComponent<Text>();
        ph.text = "0"; ph.font = Font4(); ph.fontSize = 34;
        ph.color = new Color(0.5f, 0.5f, 0.5f);
        ph.alignment = TextAnchor.MiddleCenter;
        var phRt = phObj.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(6, 0); phRt.offsetMax = Vector2.zero;

        var inTxtObj = new GameObject("Text");
        inTxtObj.transform.SetParent(fieldObj.transform, false);
        var inTxt = inTxtObj.AddComponent<Text>();
        inTxt.font = Font4(); inTxt.fontSize = 34;
        inTxt.color = new Color(0.886f, 0.910f, 0.941f);
        inTxt.alignment = TextAnchor.MiddleCenter;
        var inTxtRt = inTxtObj.GetComponent<RectTransform>();
        inTxtRt.anchorMin = Vector2.zero; inTxtRt.anchorMax = Vector2.one;
        inTxtRt.offsetMin = new Vector2(6, 0); inTxtRt.offsetMax = Vector2.zero;

        _debugField.textComponent = inTxt;
        _debugField.placeholder   = ph;


        // 구매 초기화 — 에디터에서 결제는 즉시 성립되고 PlayerPrefs 에 남는다.
        // 한 번 사고 나면 제안 화면과 잠금 화면을 다시 볼 수 없어 시험이 막힌다.
        var revokeObj = new GameObject("RevokeBtn");
        revokeObj.transform.SetParent(panel.transform, false);

        var revokeImg = revokeObj.AddComponent<Image>();
        revokeImg.color = new Color(0.55f, 0.25f, 0.30f);

        var revokeBtn = revokeObj.AddComponent<Button>();
        var revokeRt  = revokeObj.GetComponent<RectTransform>();
        revokeRt.anchorMin        = new Vector2(0f, 0f);
        revokeRt.anchorMax        = new Vector2(0f, 0f);
        revokeRt.pivot            = new Vector2(0f, 0f);
        revokeRt.anchoredPosition = new Vector2(530, 75);
        revokeRt.sizeDelta        = new Vector2(170, 60);

        var revokeTxtObj = new GameObject("Label");
        revokeTxtObj.transform.SetParent(revokeObj.transform, false);
        var revokeTxt = revokeTxtObj.AddComponent<Text>();
        revokeTxt.text      = "구매 초기화";
        revokeTxt.font      = Font4();
        revokeTxt.fontSize  = 26;
        revokeTxt.alignment = TextAnchor.MiddleCenter;
        revokeTxt.color     = Color.white;

        var revokeTxtRt = revokeTxtObj.GetComponent<RectTransform>();
        revokeTxtRt.anchorMin = Vector2.zero;
        revokeTxtRt.anchorMax = Vector2.one;
        revokeTxtRt.offsetMin = Vector2.zero;
        revokeTxtRt.offsetMax = Vector2.zero;

        revokeBtn.onClick.AddListener(() =>
        {
            RemoveAds.DebugRevoke();
            UnlockAll.DebugRevoke();
            Debug.Log("[MainMenu] 구매 상태를 초기화했다.");
            UpdateModeDisplay();
        });
        // 설정 버튼
        var btnObj = new GameObject("SetBtn");
        btnObj.transform.SetParent(panel.transform, false);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.25f, 0.55f, 0.35f);
        var setBtn = btnObj.AddComponent<Button>();
        var btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin        = new Vector2(0f, 0f);
        btnRt.anchorMax        = new Vector2(0f, 0f);
        btnRt.pivot            = new Vector2(0f, 0f);
        btnRt.anchoredPosition = new Vector2(530, 5);
        btnRt.sizeDelta        = new Vector2(170, 65);

        var btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        _debugSetText = btnTxtObj.AddComponent<Text>();
        _debugSetText.text      = LocalizationManager.Instance.Get("debug_set");
        _debugSetText.font      = Font4();
        _debugSetText.fontSize  = 34;
        _debugSetText.color     = Color.white;
        _debugSetText.alignment = TextAnchor.MiddleCenter;
        var btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero; btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.offsetMin = Vector2.zero; btnTxtRt.offsetMax = Vector2.zero;

        setBtn.onClick.AddListener(() =>
        {
            if (IsComingSoon(_currentMode)) return;
            if (int.TryParse(_debugField.text, out int val))
            {
                PlayerPrefs.SetInt($"m{_currentMode}_HighScore", val);
                PlayerPrefs.Save();
                // 숫자만 바꾸면 순위표 제출 경로를 시험할 수 없다. 실제 판을 이겼을 때와
                // 같은 자리를 지나가도록 여기서도 올린다.
                Leaderboards.ReportHighScore(_currentMode, val);
                UpdateModeDisplay();
            }
        });
    }
#endif

    // 언어 버튼 오른쪽에 나란히. 언어 버튼이 (30, -55)에 160 폭이라 그 뒤로 16 띄운 자리다.
    void CreateLeaderboardButton(GameObject parent)
    {
        var obj = new GameObject("LeaderboardButton");
        obj.transform.SetParent(parent.transform, false);

        var skin = CreateButtonSkin(obj, 160, 80, 30, 3);

        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = skin;
        btn.onClick.AddListener(() =>
            LeaderboardPanel.Open(parent.transform, IsComingSoon(_currentMode) ? 0 : _currentMode));

        var rt              = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(206, -55);
        rt.sizeDelta        = new Vector2(160, 80);

        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(obj.transform, false);
        var icon            = iconObj.AddComponent<Image>();
        icon.sprite         = LoadSprite("Sprites/Logo/rank");
        icon.preserveAspect = true;
        icon.raycastTarget  = false;
        var iconRt              = iconObj.GetComponent<RectTransform>();
        iconRt.anchorMin        = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot            = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta        = new Vector2(56, 56);
    }

    // 리더보드 버튼 오른쪽 자리. 언어(30) → 리더보드(206) → 여기(382), 폭 160에 간격 16이다.
    //
    // 유럽경제지역·영국에서만 나타난다. 동의 절차가 비동기라 메뉴를 다 만든 뒤에야 필요
    // 여부를 알 수 있으므로, 꺼진 채로 만들어 두고 Update 가 켠다.
    void CreateAdPrivacyButton(GameObject parent)
    {
        var obj = new GameObject("AdPrivacyButton");
        obj.transform.SetParent(parent.transform, false);
        obj.SetActive(false);
        _adPrivacyBtnObj = obj;

        var skin = CreateButtonSkin(obj, 160, 80, 30, 3);

        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = skin;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration     = 0.1f;
        btn.colors = colors;
        btn.onClick.AddListener(() => AdManager.GetOrCreate().ShowPrivacyOptions());

        var rt              = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(382, -55);
        rt.sizeDelta        = new Vector2(160, 80);

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        _adPrivacyText           = txtObj.AddComponent<Text>();
        _adPrivacyText.text      = LocalizationManager.Instance.Get("ad_privacy");
        _adPrivacyText.font      = Font4();
        _adPrivacyText.fontSize  = 28;
        _adPrivacyText.alignment = TextAnchor.MiddleCenter;
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color txtColor);
        _adPrivacyText.color     = txtColor;
        _adPrivacyText.raycastTarget = false;

        var txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
    }

    void OnRemoveAdsClicked()
    {
        if (RemoveAds.Owned) return;
        RemoveAds.Purchase(
            onSuccess: () => Debug.Log("[MainMenu] 광고 제거 구매 완료"),
            onFailed:  () => Debug.LogWarning("[MainMenu] 광고 제거 구매 실패"));
    }
}

// ───────────────────────────────────────────────────────────────
// 모드 아이콘 스와이프 핸들러
// ───────────────────────────────────────────────────────────────
public class ModeSwipeHandler : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    // 가운데 아이콘 폭의 절반. 이 안쪽을 누른 것은 "옆으로 넘기려는 뜻"이 아니다.
    const float TAP_DEAD_ZONE = 240f;

    // 넘김으로 칠 최소 이동량. 유니티는 10px 만 움직여도 드래그로 보고 클릭을 안 보내므로,
    // 이 값이 크면 그 사이 구간이 드래그로도 탭으로도 처리되지 않는 죽은 영역이 된다.
    const float SWIPE_MIN = 50f;

    MainMenuUI _ui;
    Vector2    _startPos;

    public void Init(MainMenuUI ui) { _ui = ui; }

    public void OnPointerDown(PointerEventData e) { _startPos = e.position; }
    public void OnDrag(PointerEventData e) { }   // IDragHandler 필수 (EndDrag 수신 조건)

    public void OnEndDrag(PointerEventData e)
    {
        float dx = e.position.x - _startPos.x;
        if (Mathf.Abs(dx) > SWIPE_MIN)
        {
            _ui.ShiftMode(dx < 0 ? 1 : -1);
            return;
        }

        // 짧게 끌린 것은 탭과 구분할 이유가 없다. 눌린 자리로 방향을 정한다.
        TryTapShift(e);
    }

    /// <summary>
    /// 스와이프가 서툴러도 넘길 수 있게, 양옆에 걸쳐 보이는 미리보기를 탭하면 그쪽으로 간다.
    /// </summary>
    public void OnPointerClick(PointerEventData e) => TryTapShift(e);

    void TryTapShift(PointerEventData e)
    {
        if (_ui == null) return;

        var rt = transform as RectTransform;
        if (rt == null) return;

        // 누른 지점을 클립 안 좌표로 바꿔 좌우를 가른다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, e.pressPosition, e.pressEventCamera, out Vector2 local))
            return;

        if      (local.x < -TAP_DEAD_ZONE) _ui.ShiftMode(-1);
        else if (local.x >  TAP_DEAD_ZONE) _ui.ShiftMode( 1);
    }
}
