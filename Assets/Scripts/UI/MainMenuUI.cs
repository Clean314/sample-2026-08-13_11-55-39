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
    static readonly string[] MODE_IMAGES        = { "normal_mode", "ice_mode", "toggle_mode", "disco_mode" };
    static readonly int[]    MODE_UNLOCK_SCORE  = { 0, 5000, 10000, 5000 };
    static readonly int[]    MODE_UNLOCK_FROM   = { 0, 0,    1,     2     };  // 어떤 모드의 최고점수를 확인할지
    static readonly string[] MODE_LOC_KEYS      = { "mode_normal", "mode_ice", "mode_toggle", "mode_disco" };

    // 마지막 실제 모드 뒤에 "COMING SOON" 슬롯 하나 추가
    int  TotalSlots             => MODE_IMAGES.Length + 1;
    bool IsComingSoon(int idx)  => idx >= MODE_IMAGES.Length;

    int             _currentMode  = 0;
    Image           _modeIcon;
    RectTransform   _modeIconRt;
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

    // 디버그 패널: 현재 보고 있는 모드의 점수를 표시/설정
    InputField _debugField;
    Text       _debugModeNameText;

    void Start()
    {
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
        // 잠금 조건 텍스트도 다시 생성
        UpdateModeDisplay();
    }

    void BuildMainMenu()
    {
        // ── Canvas ──────────────────────────────────────────
        var canvasObj = new GameObject("Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 1f;

        canvasObj.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        BGMManager.GetOrCreate();
        NetworkChecker.GetOrCreate();
        AdManager.GetOrCreate().ShowBanner();

        CreateBackground(canvasObj);
        CreateLogo(canvasObj);
        CreateModeSelector(canvasObj);

        var loc = LocalizationManager.Instance;

        _startBtn   = CreateRoundedButton(canvasObj, loc.Get("start"),
                          anchorY: 0.24f,
                          textColor: new Color(0.886f, 0.910f, 0.941f),
                          out _startBtnText);
        _startBtnCG = _startBtn.gameObject.AddComponent<CanvasGroup>();

        var noAdsBtn = CreateRoundedButton(canvasObj, loc.Get("no_ads"),
                           anchorY: 0.15f,
                           textColor: new Color(1f, 0.85f, 0.3f),
                           out _noAdsBtnText);

        _startBtn.onClick.AddListener(OnStartClicked);
        noAdsBtn.onClick.AddListener(OnRemoveAdsClicked);

        CreateMuteButton(canvasObj);
        CreateLanguageButton(canvasObj);
        CreateDebugPanel(canvasObj);
        UpdateModeDisplay();
    }

    void OnStartClicked()
    {
        if (!IsModeUnlocked(_currentMode)) return;
        // 오버레이로도 막지만, "WiFi off → 같은 프레임에 탭" race를 막기 위해 클릭 시점에 한 번 더 검사.
        // 오프라인이면 그냥 무시 — NetworkChecker.Update가 다음 프레임에 오버레이를 띄움.
        if (Application.internetReachability == NetworkReachability.NotReachable) return;
        ModeSession.SelectedMode = _currentMode;
        SceneManager.LoadScene("InGame");
    }

    bool IsModeUnlocked(int idx)
    {
        if (IsComingSoon(idx)) return false;
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

        var img = obj.AddComponent<Image>();
        ColorUtility.TryParseHtmlString("#161524", out Color bg);
        img.color = bg;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── 로고 (위로 올림) ──────────────────────────────────────
    void CreateLogo(GameObject parent)
    {
        var customFont = Resources.Load<Font>("SCDream8");
        Font logoFont = customFont != null ? customFont : Font4();
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color logoColor);

        // 원본 로고 텍스트
        var obj = new GameObject("Logo");
        obj.transform.SetParent(parent.transform, false);

        var txt = obj.AddComponent<Text>();
        txt.text      = "MATBLAST";
        txt.fontSize  = 150;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = logoColor;
        txt.font  = logoFont;

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 620);
        rt.sizeDelta        = new Vector2(980, 200);

        // 슬레이트 효과: 글자 하단 절반을 어두운 색으로 덮는 클립 영역
        // 로고 중심 y=620, 높이=200 → 하단 절반: y=520~620, 중심 y=570, 높이=100
        var slateClip = new GameObject("LogoSlate");
        slateClip.transform.SetParent(parent.transform, false);
        slateClip.AddComponent<RectMask2D>();
        var slateRt = slateClip.GetComponent<RectTransform>();
        slateRt.anchorMin        = new Vector2(0.5f, 0.5f);
        slateRt.anchorMax        = new Vector2(0.5f, 0.5f);
        slateRt.pivot            = new Vector2(0.5f, 0.5f);
        slateRt.anchoredPosition = new Vector2(0, 570);   // 하단 절반 중심
        slateRt.sizeDelta        = new Vector2(980, 100); // 절반 높이

        // 클립 안의 어두운 텍스트 (원본 위치 기준으로 정렬)
        var darkObj = new GameObject("LogoSlateTxt");
        darkObj.transform.SetParent(slateClip.transform, false);

        var darkTxt = darkObj.AddComponent<Text>();
        darkTxt.text      = "MATBLAST";
        darkTxt.fontSize  = 150;
        darkTxt.fontStyle = FontStyle.Bold;
        darkTxt.alignment = TextAnchor.MiddleCenter;
        darkTxt.font      = logoFont;
        // 원본(#e2e8f0)보다 어두운 색
        ColorUtility.TryParseHtmlString("#a8b2bf", out Color slateColor);
        darkTxt.color = slateColor;

        var darkRt = darkObj.GetComponent<RectTransform>();
        darkRt.anchorMin        = new Vector2(0.5f, 0.5f);
        darkRt.anchorMax        = new Vector2(0.5f, 0.5f);
        darkRt.pivot            = new Vector2(0.5f, 0.5f);
        // 클립 컨테이너(y=570) 기준으로 원본 텍스트 중심(y=620)에 맞춤 → 상대 offset = +50
        darkRt.anchoredPosition = new Vector2(0, 50);
        darkRt.sizeDelta        = new Vector2(980, 200);
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
        var clipRt = clipObj.GetComponent<RectTransform>();
        clipRt.anchorMin        = new Vector2(0.5f, 0.5f);
        clipRt.anchorMax        = new Vector2(0.5f, 0.5f);
        clipRt.pivot            = new Vector2(0.5f, 0.5f);
        clipRt.anchoredPosition = new Vector2(0, 30);
        clipRt.sizeDelta        = new Vector2(480, 480);

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
        _modeIconRt.sizeDelta     = new Vector2(480, 480);

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
        _modeNameText.font      = Resources.Load<Font>("SCDream8") ?? Font4();
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

        // ── 좌우 꺾쇠 버튼 ───────────────────────────────
        CreateChevronButton(container, "<", new Vector2(-430, 30), () => ShiftMode(-1));
        CreateChevronButton(container, ">", new Vector2( 430, 30), () => ShiftMode( 1));
    }

    void CreateChevronButton(GameObject parent, string chevron, Vector2 pos, System.Action onClick)
    {
        var obj = new GameObject(chevron == "<" ? "PrevBtn" : "NextBtn");
        obj.transform.SetParent(parent.transform, false);

        var img = obj.AddComponent<Image>();
        img.color = Color.clear;

        var btn = obj.AddComponent<Button>();
        var bc  = btn.colors;
        bc.normalColor      = Color.white;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.6f);
        bc.pressedColor     = new Color(1f, 1f, 1f, 0.3f);
        bc.fadeDuration     = 0.1f;
        btn.colors = bc;
        btn.onClick.AddListener(() => onClick());

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(120, 200);

        var txtObj = new GameObject("Label");
        txtObj.transform.SetParent(obj.transform, false);
        var txt = txtObj.AddComponent<Text>();
        txt.text      = chevron;
        txt.fontSize  = 130;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color chevronColor);
        txt.color = chevronColor;
        txt.font  = Resources.Load<Font>("SCDream8") ?? Font4();
        var txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
    }

    public void ShiftMode(int dir)
    {
        if (_isAnimating) return;
        int newMode = (_currentMode + dir + TotalSlots) % TotalSlots;
        StartCoroutine(SlideMode(dir, newMode));
    }

    IEnumerator SlideMode(int dir, int newMode)
    {
        _isAnimating = true;

        // 새 비주얼 생성 (클립 컨테이너 안, 화면 밖에서 시작)
        bool comingSoon = IsComingSoon(newMode);
        var nextObj = new GameObject(comingSoon ? "ComingSoonNext" : "ModeIconNext");
        nextObj.transform.SetParent(_modeIconRt.parent, false);

        Image nextImg = null;
        if (comingSoon)
        {
            var txt = nextObj.AddComponent<Text>();
            txt.text      = LocalizationManager.Instance.Get("coming_soon");
            txt.font      = Resources.Load<Font>("SCDream8") ?? Font4();
            txt.fontSize  = 78;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color     = new Color(0.75f, 0.80f, 0.92f);
        }
        else
        {
            nextImg = nextObj.AddComponent<Image>();
            nextImg.preserveAspect = true;
            var tex = Resources.Load<Texture2D>(MODE_IMAGES[newMode]);
            if (tex != null)
                nextImg.sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        var nextRt = nextObj.GetComponent<RectTransform>();
        nextRt.anchorMin        = new Vector2(0.5f, 0.5f);
        nextRt.anchorMax        = new Vector2(0.5f, 0.5f);
        nextRt.pivot            = new Vector2(0.5f, 0.5f);
        nextRt.sizeDelta        = new Vector2(480, 480);
        nextRt.anchoredPosition = new Vector2(dir * 520f, 0);

        // 슬라이드 애니메이션 (SmoothStep 이징)
        float duration = 0.28f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _modeIconRt.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(-dir * 520f, 0), t);
            nextRt.anchoredPosition      = Vector2.Lerp(new Vector2(dir * 520f, 0), Vector2.zero, t);
            yield return null;
        }

        // 정리: 기존 비주얼 제거, 새 비주얼을 현재로 교체
        Destroy(_modeIconRt.gameObject);
        _modeIcon    = nextImg;   // Coming Soon 슬롯이면 null
        _modeIconRt  = nextRt;
        _currentMode = newMode;

        UpdateModeDisplay();
        _isAnimating = false;
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
        {
            var tex = Resources.Load<Texture2D>(MODE_IMAGES[_currentMode]);
            _modeIcon.sprite = tex != null
                ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f))
                : null;
        }

        // 모드 미리보기 BGM 전환 (잠겨 있어도 전환)
        BGMManager.Instance?.PlayBGM(ModeConfig.Modes[_currentMode].bgmClip);

        bool unlocked = IsModeUnlocked(_currentMode);
        _lockOverlay.SetActive(!unlocked);

        // 아이콘 아래: 잠금 여부에 따라 모드 이름 또는 해금 조건 표시
        if (_modeNameText != null)
            _modeNameText.text = unlocked ? loc.Get(MODE_LOC_KEYS[_currentMode]) : UnlockDescription(_currentMode);

        if (_startBtnCG != null)
        {
            _startBtnCG.alpha          = unlocked ? 1f : 0.4f;
            _startBtnCG.interactable   = unlocked;
            _startBtnCG.blocksRaycasts = unlocked;
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

        var img = obj.AddComponent<Image>();
        img.sprite = CreateRoundedRectBorderSprite(200, 100, 36, 4);
        img.type = Image.Type.Sliced;
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color borderColor);
        img.color = borderColor;

        var btn = obj.AddComponent<Button>();
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

        var img = obj.AddComponent<Image>();
        img.sprite = CreateRoundedRectBorderSprite(200, 100, 30, 3);
        img.type   = Image.Type.Sliced;
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color borderColor);
        img.color  = borderColor;

        var btn    = obj.AddComponent<Button>();
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
        _langBtnText.font      = Resources.Load<Font>("SCDream8") ?? Font4();
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

    // ── 둥근 테두리 Sprite 생성 (속이 빈 outline) ────────────
    Sprite CreateRoundedRectBorderSprite(int w, int h, int radius, int border)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool outer = IsInsideRoundedRect(x, y, w, h, radius);
                bool inner = IsInsideRoundedRect(x - border, y - border,
                    w - border * 2, h - border * 2, Mathf.Max(0, radius - border));
                pixels[y * w + x] = (outer && !inner)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            100f, 0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }

    bool IsInsideRoundedRect(int px, int py, int w, int h, int r)
    {
        int cx = px < r ? r : (px > w - 1 - r ? w - 1 - r : px);
        int cy = py < r ? r : (py > h - 1 - r ? h - 1 - r : py);
        float dx = px - cx;
        float dy = py - cy;
        return dx * dx + dy * dy <= (float)r * r;
    }

    // ── BGM 뮤트 토글 버튼 ───────────────────────────────────
    void CreateMuteButton(GameObject parent)
    {
        var bgm = BGMManager.Instance;

        var obj = new GameObject("MuteButton");
        obj.transform.SetParent(parent.transform, false);

        var img = obj.AddComponent<Image>();
        img.preserveAspect = true;
        img.color          = new Color(0.886f, 0.910f, 0.941f);
        img.sprite         = LoadSprite(bgm.IsMuted ? "mute" : "bgm_on");

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
        rt.anchoredPosition = new Vector2(-30, -55);
        rt.sizeDelta        = new Vector2(100, 100);

        btn.onClick.AddListener(() =>
        {
            bgm.ToggleMute();
            img.sprite = LoadSprite(bgm.IsMuted ? "mute" : "bgm_on");
        });
    }

    Sprite LoadSprite(string name)
    {
        var tex = Resources.Load<Texture2D>(name);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    Font Font4() => Resources.Load<Font>("SCDream4") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    // ── [DEBUG] 최고 점수 설정 패널 ──────────────────────────
    // 스와이프로 표시되는 현재 모드의 m{idx}_HighScore를 직접 편집
    void CreateDebugPanel(GameObject parent)
    {
        var panel = new GameObject("DebugPanel");
        panel.transform.SetParent(parent.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin        = new Vector2(0.5f, 0f);
        panelRt.anchorMax        = new Vector2(0.5f, 0f);
        panelRt.pivot            = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0, 18);
        panelRt.sizeDelta        = new Vector2(700, 140);

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
                UpdateModeDisplay();
            }
        });
    }

    void OnRemoveAdsClicked()
    {
        Debug.Log("광고 제거 버튼 클릭됨");
    }
}

// ───────────────────────────────────────────────────────────────
// 모드 아이콘 스와이프 핸들러
// ───────────────────────────────────────────────────────────────
public class ModeSwipeHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
{
    MainMenuUI _ui;
    Vector2    _startPos;

    public void Init(MainMenuUI ui) { _ui = ui; }

    public void OnPointerDown(PointerEventData e) { _startPos = e.position; }
    public void OnDrag(PointerEventData e) { }   // IDragHandler 필수 (EndDrag 수신 조건)

    public void OnEndDrag(PointerEventData e)
    {
        float dx = e.position.x - _startPos.x;
        if (Mathf.Abs(dx) > 80f)
            _ui.ShiftMode(dx < 0 ? 1 : -1);
    }
}
