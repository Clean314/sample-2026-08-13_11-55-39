using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인터넷 연결을 매 프레임 확인해서 끊겨 있으면 반투명 검은 오버레이 + 안내 문구를 띄운다.
/// 광고 노출이 핵심이라 네트워크가 없으면 메뉴/게임 진입 자체를 막아야 함.
/// 모바일은 Wi-Fi 또는 셀룰러 데이터 모두 ReachableVia... 로 잡혀서 둘 다 OK 처리됨.
/// DontDestroyOnLoad로 씬 전환 후에도 계속 감시.
/// </summary>
public class NetworkChecker : MonoBehaviour
{
    public static NetworkChecker Instance { get; private set; }

    GameObject _overlayGo;
    Text       _msgText;
    Text       _retryBtnText;
    float      _checkingUntil; // 재시도 버튼 눌렀을 때 "확인 중..." 표시할 종료 시각

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        LocalizationManager.Instance.OnLanguageChanged += RefreshText;
    }

    void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= RefreshText;
    }

    void Update()
    {
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;
        if (_overlayGo.activeSelf != offline) _overlayGo.SetActive(offline);

        // "확인 중..." 표시 종료 시 원래 안내문으로 복귀
        if (_checkingUntil > 0f && Time.unscaledTime >= _checkingUntil)
        {
            _checkingUntil = 0f;
            RefreshText();
        }
    }

    void OnRetryClicked()
    {
        // 실제 reachability 체크는 Update가 매 프레임 처리하므로,
        // 버튼은 사용자 피드백("뭔가 일어난다")만 잠깐 보여 주면 됨.
        var loc = LocalizationManager.Instance;
        _msgText.text  = loc.Get("checking");
        _checkingUntil = Time.unscaledTime + 0.7f;
    }

    void RefreshText()
    {
        var loc = LocalizationManager.Instance;
        if (_msgText      != null && _checkingUntil <= 0f) _msgText.text     = loc.Get("no_network");
        if (_retryBtnText != null)                          _retryBtnText.text = loc.Get("retry");
    }

    void BuildOverlay()
    {
        // 별도 캔버스: 다른 모든 UI 위에 그려지도록 sortingOrder를 높게
        var canvasGo = new GameObject("NetworkOverlayCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0f;   // 가로 기준 — 긴 화면에서 판이 잘리지 않게 (CanvasMetrics 참고)

        canvasGo.AddComponent<GraphicRaycaster>();

        // 풀스크린 검은 반투명 (raycastTarget=true → 아래 입력 차단)
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var bg = overlay.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.94f);
        bg.raycastTarget = true;

        var bgRt = overlay.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        var loc  = LocalizationManager.Instance;
        var font = Resources.Load<Font>("Fonts/SCDream4")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 와이파이 아이콘 (텍스트 위쪽 큰 사이즈)
        var iconTex = Resources.Load<Texture2D>("Sprites/UI/wifi");
        if (iconTex != null)
        {
            var iconGo  = new GameObject("WifiIcon");
            iconGo.transform.SetParent(overlay.transform, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite        = Sprite.Create(iconTex,
                                        new Rect(0, 0, iconTex.width, iconTex.height),
                                        new Vector2(0.5f, 0.5f));
            iconImg.color         = Color.white;
            iconImg.raycastTarget = false;

            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot     = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(360, 360);
            iconRt.anchoredPosition = new Vector2(0, 380);
        }

        // 흰 안내 문구 (가운데)
        var txtGo = new GameObject("Msg");
        txtGo.transform.SetParent(overlay.transform, false);
        _msgText           = txtGo.AddComponent<Text>();
        _msgText.font      = font;
        _msgText.fontSize  = 56;
        _msgText.alignment = TextAnchor.MiddleCenter;
        _msgText.color     = Color.white;
        _msgText.text      = loc.Get("no_network");

        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0.5f, 0.5f);
        txtRt.anchorMax = new Vector2(0.5f, 0.5f);
        txtRt.pivot     = new Vector2(0.5f, 0.5f);
        txtRt.sizeDelta = new Vector2(960, 400);
        txtRt.anchoredPosition = new Vector2(0, 30);

        // 재시도 버튼 (아래쪽)
        var btnGo  = new GameObject("RetryBtn");
        btnGo.transform.SetParent(overlay.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite        = MakeRoundedSprite(80, 80, 28);
        btnImg.type          = Image.Type.Sliced;
        btnImg.color         = new Color(0.20f, 0.20f, 0.28f, 1f);
        btnImg.raycastTarget = true;
        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(OnRetryClicked);

        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot     = new Vector2(0.5f, 0.5f);
        btnRt.sizeDelta        = new Vector2(440, 130);
        btnRt.anchoredPosition = new Vector2(0, -180);

        var btnTxtGo = new GameObject("Label");
        btnTxtGo.transform.SetParent(btnGo.transform, false);
        _retryBtnText           = btnTxtGo.AddComponent<Text>();
        _retryBtnText.font      = font;
        _retryBtnText.fontSize  = 50;
        _retryBtnText.alignment = TextAnchor.MiddleCenter;
        _retryBtnText.color     = Color.white;
        _retryBtnText.text      = loc.Get("retry");

        var btnTxtRt = btnTxtGo.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.offsetMin = btnTxtRt.offsetMax = Vector2.zero;

        _overlayGo = canvasGo;
        _overlayGo.SetActive(false);
    }

    // 단순 둥근 사각 스프라이트 (다른 매니저와 독립적으로 자체 생성)
    static Sprite MakeRoundedSprite(int w, int h, int radius)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dx = x < radius ? radius - x : (x >= w - radius ? x - (w - radius - 1) : 0);
                int dy = y < radius ? radius - y : (y >= h - radius ? y - (h - radius - 1) : 0);
                bool inside = dx == 0 || dy == 0 || (dx * dx + dy * dy <= radius * radius);
                px[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        var border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.Tight, border);
    }

    public static NetworkChecker GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("NetworkChecker").AddComponent<NetworkChecker>();
    }
}
