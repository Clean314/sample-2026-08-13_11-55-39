using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 화이트아웃이 걷히면서 WHITE_OUT_END 시각에 도시 야경 배경이 완전히 드러나도록 맞춰져 있다.
/// 화이트아웃의 시작(WHITE_IN_START)과 끝(WHITE_OUT_END)을 양쪽 다 고정하고 그 사이를 채운다.
///   WHITE_IN_START  화면 전체가 흰색으로 페이드인 (InGameUI의 WhiteFlashOverlay가 담당)
///   WHITE_FULL      완전 흰색 도달, WHITE_HOLD_SEC 동안 유지
///   APPEAR_START    도시를 즉시 완전히 노출 (아직 흰색에 완전히 가려진 상태)
///                   이후 흰색이 페이드아웃되며 도시가 드러남
///   WHITE_OUT_END   흰색 완전히 사라짐 = 도시 완전 노출
///   ~FADE_OUT_START 완전 노출 + 창문 개별 깜빡임
///   FADE_OUT_START~FADE_OUT_END  페이드아웃 후 자기 자신 파괴
///
/// 배경은 city_light.png(창문 켜진 완성본)를 그대로 사용하므로 원본 픽셀이 손상 없이 표시된다.
/// 창문 개별 깜빡임은 city_light.png의 노란 픽셀 블롭(flood-fill 감지) 위치에 city_light_on.png의
/// 대응 픽셀 색상을 오버레이로 얹어 표현한다. 오버레이 알파가 0이면 켜진 상태(원본 노란색 보임),
/// 1이면 꺼진 상태(실루엣 색이 창문을 덮음).
///
/// city_light.png / city_light_on.png 텍스처 임포트 설정 isReadable=1이 반드시 필요.
/// </summary>
public class CityLightBackground : MonoBehaviour
{
    // 등장 화이트아웃 램프. 실제 렌더링은 그리드까지 덮어야 하므로
    // InGameUI의 WhiteFlashOverlay가 담당하고 여기서는 타이밍만 정의한다.
    //
    // ── 조정 손잡이 ──
    // 양 끝점을 고정하고 그 사이를 페이드인 → 유지 → 페이드아웃으로 채운다.
    // 화이트아웃을 뒤로 미루고 싶으면 WHITE_IN_START만 올리면 되고,
    // 그만큼 마지막 페이드아웃(도시가 드러나는 구간)이 짧아진다.
    public const double WHITE_IN_START = 147.8;  // 화이트아웃 시작 (미루려면 이 값을 올린다)
    public const double WHITE_OUT_END  = 150;   // 도시 완전 노출 (음악 큐에 맞춘 고정점)
    const double WHITE_IN_SEC   = 0.5;           // 흰색 페이드인 길이
    const double WHITE_HOLD_SEC = 0.25;            // 완전 흰색 유지 길이

    public const double WHITE_FULL   = WHITE_IN_START + WHITE_IN_SEC;  // 완전 흰색 도달
    public const double APPEAR_START = WHITE_FULL + WHITE_HOLD_SEC;    // 도시 스폰 + 흰색 페이드아웃 시작
    // 페이드아웃 길이는 남는 시간이 자동 배정된다: WHITE_OUT_END - APPEAR_START

    // 도시를 유지할 시각까지 노출한 뒤 아주 천천히 사라진다.
    // 드라이빙 배경이 FADE_OUT_START에 도시 뒤에서 함께 등장하므로, 이 구간 전체가
    // "도시가 엷어지며 드라이빙이 드러나는" 크로스페이드가 된다.
    // 크로스페이드를 더 길게/짧게 하려면 FADE_OUT_SEC만 조정하면 된다.
    public const double FADE_OUT_START = 180.6;
    const double FADE_OUT_SEC = 3.0;            // ── 조정 손잡이: 크로스페이드 길이 ──
    public const double FADE_OUT_END   = FADE_OUT_START + FADE_OUT_SEC;

    const byte YELLOW_R_MIN = 130;
    const byte YELLOW_G_MIN = 120;
    const byte YELLOW_B_MAX = 170;

    // 배경/창문 오프 색을 동일 비율로 어둡게 → 원본에 어두운 필터 씌운 인상.
    // 낮출수록 어두워지고, 하늘과 건물 실루엣의 절대 밝기차도 같이 줄어 건물이 묻힌다.
    const float DARKEN = 0.78f;

    // 좌·우 하단에서 부채꼴로 왕복하는 흰색 스포트라이트.
    const float SPOT_PERIOD_SEC   = 14f;   // 한 왕복(왼→오→왼)에 걸리는 시간
    const float SPOT_AMPLITUDE_DEG= 22f;   // 중심 각에서 ±얼마나 흔들리는가
    const float SPOT_LEFT_CENTER  = -38f;  // 좌하단 스포트라이트가 향하는 기본 각도(우상단 쪽)
    const float SPOT_RIGHT_CENTER =  38f;  // 우하단은 좌상단 쪽
    const float SPOT_MAX_ALPHA    = 0.01f;

    const float FLICKER_MIN_INTERVAL = 1.4f;
    const float FLICKER_MAX_INTERVAL = 8.0f;
    const float FADE_SPEED           = 5.5f;
    const float INITIAL_OFF_CHANCE   = 0.18f;

    const int MIN_BLOB_PIXELS = 3;

    public bool  IsActive => _phase != Phase.Done;
    public float CurrentAlpha => _canvasGroup != null ? _canvasGroup.alpha : 0f;

    enum Phase { Idle, FadingIn, Visible, FadingOut, Done }

    Phase        _phase = Phase.Idle;
    BeatTracker  _bt;
    bool         _flickerInitialized;

    RectTransform _root;
    CanvasGroup   _canvasGroup;
    Sprite        _whiteSprite;

    RectTransform _spotLeft, _spotRight;

    struct Window
    {
        public Image  image;         // 실루엣 색 오버레이 (알파로 on/off)
        public bool   isOn;
        public double nextToggleTime;
        public float  offAlpha;      // 현재 오프 알파 (0=완전 켜짐, 1=완전 꺼짐)
        public float  offR, offG, offB;
    }

    readonly List<Window> _windows = new List<Window>();

    public static CityLightBackground Spawn(Canvas parentCanvas, BeatTracker bt)
    {
        var go = new GameObject("CityLightBackground");
        go.transform.SetParent(parentCanvas.transform, false);
        var comp = go.AddComponent<CityLightBackground>();
        comp._bt = bt;
        comp.Build();
        return comp;
    }

    void Build()
    {
        // WHITE_IN_START를 너무 뒤로 미루면 페이드아웃에 남는 시간이 없어져
        // InGameUI의 램프 계산이 0으로 나누게 된다.
        if (APPEAR_START >= WHITE_OUT_END)
            Debug.LogWarning("[CityLight] 화이트아웃이 WHITE_OUT_END를 넘어섭니다. " +
                             "WHITE_IN_START를 앞당기거나 WHITE_IN_SEC/WHITE_HOLD_SEC을 줄이세요.");

        _root = gameObject.AddComponent<RectTransform>();
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;
        // 최종 sibling 순서는 spawn 쪽(InGameUI)에서 _bgImage 위로 지정한다.

        _canvasGroup                = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        _whiteSprite = MakeWhiteSprite();

        var bgSprite  = Resources.Load<Sprite>("Sprites/Effects/city_light");
        var offSprite = Resources.Load<Sprite>("Sprites/Effects/city_light_on");
        BuildBackground(bgSprite);
        BuildWindows(bgSprite, offSprite);
        BuildSpotlights();
    }

    // 좌하단 / 우하단에 부채꼴 콘 스프라이트를 배치. 회전 중심을 하단(pivot y=0)에 두어
    // localRotation Z만 흔들면 부채꼴 궤적으로 왕복한다. 창문 위 레이어에 얹혀야 하므로
    // 창문 생성 뒤에 추가한다.
    void BuildSpotlights()
    {
        var coneSprite = MakeSpotlightSprite(640, 1600);
        // 광원(콘 base = pivot)을 화면 꼭짓점 바깥으로 밀어 콘이 이미 넓어진 상태로 화면에 들어옴.
        _spotLeft  = CreateSpotlight("SpotL", new Vector2(0f, 0f), new Vector2(-220f, -160f), coneSprite);
        _spotRight = CreateSpotlight("SpotR", new Vector2(1f, 0f), new Vector2( 220f, -160f), coneSprite);
    }

    RectTransform CreateSpotlight(string name, Vector2 anchor, Vector2 offsetFromAnchor, Sprite spr)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        var img = go.AddComponent<Image>();
        img.sprite        = spr;
        img.color         = new Color(1f, 1f, 1f, SPOT_MAX_ALPHA);
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0f); // 콘 base(=회전 중심)
        rt.anchoredPosition = offsetFromAnchor;
        rt.sizeDelta        = new Vector2(2400f, 2500f); // 폭↓ 콘 좁게, 높이↑ 어떤 회전각에서도 화면 밖까지 뻗음
        return rt;
    }

    // Flat 2D 부채꼴: 콘 안은 단색(alpha 1), 밖은 0. gradient 없음.
    Sprite MakeSpotlightSprite(int w, int h)
    {
        const float HALF_SPREAD = 0.42f; // 콘 반각 tan (전체 약 46도)
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        float halfW = w * 0.5f;

        for (int y = 0; y < h; y++)
        {
            float normY    = (float)y / Mathf.Max(h - 1, 1);
            float coneEdge = normY * HALF_SPREAD * halfW;
            for (int x = 0; x < w; x++)
            {
                float dx = x - halfW;
                byte a = (coneEdge > 0.5f && Mathf.Abs(dx) < coneEdge) ? (byte)255 : (byte)0;
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f));
    }

    void BuildBackground(Sprite sprite)
    {
        if (sprite == null) return;

        var go = new GameObject("Background");
        go.transform.SetParent(_root, false);
        var img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.preserveAspect = false;
        img.raycastTarget  = false;
        img.color          = new Color(DARKEN, DARKEN, DARKEN, 1f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void BuildWindows(Sprite litSprite, Sprite offSprite)
    {
        if (litSprite == null) return;
        var litTex = litSprite.texture;
        int W = litTex.width, H = litTex.height;

        Color32[] litPx;
        try { litPx = litTex.GetPixels32(); }
        catch (UnityException)
        {
            Debug.LogWarning("[CityLight] city_light 텍스처 isReadable=1이 아니라 창문을 스캔할 수 없습니다.");
            return;
        }

        // city_light_on 픽셀은 창문 오프 색을 뽑는 참조. 없거나 읽기 실패면 fallback으로 어두운 남색.
        Color32[] offPx = null;
        if (offSprite != null && offSprite.texture.width == W && offSprite.texture.height == H)
        {
            try { offPx = offSprite.texture.GetPixels32(); }
            catch (UnityException) { offPx = null; }
        }
        var fallback = new Color32(12, 15, 40, 255);

        var visited = new bool[W * H];
        var queue   = new Queue<int>();

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int idx = y * W + x;
                if (visited[idx]) continue;
                visited[idx] = true;
                if (!IsYellow(litPx[idx])) continue;

                int minX = x, maxX = x, minY = y, maxY = y;
                int cnt = 0;

                queue.Clear();
                queue.Enqueue(idx);
                while (queue.Count > 0)
                {
                    int p   = queue.Dequeue();
                    int pxX = p % W;
                    int pxY = p / W;
                    cnt++;
                    if (pxX < minX) minX = pxX;
                    if (pxX > maxX) maxX = pxX;
                    if (pxY < minY) minY = pxY;
                    if (pxY > maxY) maxY = pxY;

                    if (pxX > 0     && !visited[p - 1] && IsYellow(litPx[p - 1])) { visited[p - 1] = true; queue.Enqueue(p - 1); }
                    if (pxX < W - 1 && !visited[p + 1] && IsYellow(litPx[p + 1])) { visited[p + 1] = true; queue.Enqueue(p + 1); }
                    if (pxY > 0     && !visited[p - W] && IsYellow(litPx[p - W])) { visited[p - W] = true; queue.Enqueue(p - W); }
                    if (pxY < H - 1 && !visited[p + W] && IsYellow(litPx[p + W])) { visited[p + W] = true; queue.Enqueue(p + W); }
                }

                if (cnt < MIN_BLOB_PIXELS) continue;
                SpawnWindow(minX, minY, maxX, maxY, W, H, offPx, fallback);
            }
        }

        Debug.Log($"[CityLight] tex {W}x{H}, windows spawned = {_windows.Count}");
    }

    // 창문의 실루엣 색은 city_light_on의 blob 주변 픽셀에서 뽑는다. blob 자체가 아니라 살짝 바깥
    // 픽셀을 쓰는 이유: city_light_on에서도 창문 위치는 원본 실루엣이 아니라 배경 하늘 색일 수도 있음.
    void SpawnWindow(int x0, int y0, int x1, int y1, int W, int H,
                     Color32[] offPx, Color32 fallback)
    {
        Color32 offColor = fallback;
        if (offPx != null)
        {
            // blob 바운딩박스를 1px 확장한 영역의 평균 색을 창문 오프 색으로 삼는다.
            int expX0 = Mathf.Max(0, x0 - 1);
            int expX1 = Mathf.Min(W - 1, x1 + 1);
            int expY0 = Mathf.Max(0, y0 - 1);
            int expY1 = Mathf.Min(H - 1, y1 + 1);
            long r = 0, g = 0, b = 0;
            int cnt = 0;
            for (int y = expY0; y <= expY1; y++)
                for (int x = expX0; x <= expX1; x++)
                {
                    var c = offPx[y * W + x];
                    r += c.r; g += c.g; b += c.b; cnt++;
                }
            if (cnt > 0)
                offColor = new Color32((byte)(r / cnt), (byte)(g / cnt), (byte)(b / cnt), 255);
        }

        var wgo = new GameObject($"W_{_windows.Count}");
        wgo.transform.SetParent(_root, false);
        var img = wgo.AddComponent<Image>();
        img.sprite        = _whiteSprite;
        img.raycastTarget = false;
        img.color         = new Color32(offColor.r, offColor.g, offColor.b, 0);

        float u0 = (float)x0       / W;
        float u1 = (float)(x1 + 1) / W;
        float v0 = (float)y0       / H;
        float v1 = (float)(y1 + 1) / H;

        var wrt = wgo.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(u0, v0);
        wrt.anchorMax = new Vector2(u1, v1);
        wrt.offsetMin = Vector2.zero;
        wrt.offsetMax = Vector2.zero;

        bool startOn = Random.value > INITIAL_OFF_CHANCE;
        _windows.Add(new Window {
            image          = img,
            isOn           = startOn,
            nextToggleTime = 0.0,
            offAlpha       = startOn ? 0f : 1f,
            offR = offColor.r / 255f * DARKEN,
            offG = offColor.g / 255f * DARKEN,
            offB = offColor.b / 255f * DARKEN,
        });
    }

    static bool IsYellow(Color32 c)
    {
        if (c.a < 128) return false;
        if (c.r < YELLOW_R_MIN) return false;
        if (c.g < YELLOW_G_MIN) return false;
        if (c.b > YELLOW_B_MAX) return false;
        // 노란색은 파랑보다 빨강/초록이 뚜렷하게 우세.
        return c.r > c.b + 25 && c.g > c.b + 15;
    }

    void Update()
    {
        if (_bt == null) return;
        double t = _bt.PlaybackSec;

        // 등장 구간 이전으로 되감긴 경우(에디터 시크 등): 화면을 덮은 채 얼어붙지 않도록 스스로 정리한다.
        // 정상 재생 중에는 InGameUI가 APPEAR_START 이후에만 스폰하므로 이 분기를 탈 일이 없다.
        // 파괴되면 InGameUI가 _bgImage/그리드 배경을 복원하고, 다시 앞으로 시크하면 새로 스폰된다.
        if (t < APPEAR_START || t >= FADE_OUT_END)
        {
            _phase = Phase.Done;
            Destroy(gameObject);
            return;
        }

        if (!_flickerInitialized)
        {
            InitializeFlickerTimers(t);
            _flickerInitialized = true;
        }

        if (t < FADE_OUT_START)
        {
            _phase = Phase.Visible;
            _canvasGroup.alpha = 1f;
        }
        else
        {
            _phase = Phase.FadingOut;
            _canvasGroup.alpha = Mathf.Clamp01(1f - (float)((t - FADE_OUT_START) / (FADE_OUT_END - FADE_OUT_START)));
        }

        FlickerWindows(t);
        SweepSpotlights(t);
    }

    // 두 스포트라이트를 서로 반대 위상으로 부채꼴 왕복시킨다.
    // 좌하단은 z=SPOT_LEFT_CENTER(-38도) 기준으로 우상단 스캔, 우하단은 반대.
    void SweepSpotlights(double t)
    {
        if (_spotLeft == null || _spotRight == null) return;
        double elapsed = t - APPEAR_START;
        float  phase   = (float)(elapsed * (Mathf.PI * 2.0 / SPOT_PERIOD_SEC));
        float  swingL  = SPOT_AMPLITUDE_DEG * Mathf.Sin(phase);
        float  swingR  = SPOT_AMPLITUDE_DEG * Mathf.Sin(phase + Mathf.PI);
        _spotLeft.localRotation  = Quaternion.Euler(0f, 0f, SPOT_LEFT_CENTER  + swingL);
        _spotRight.localRotation = Quaternion.Euler(0f, 0f, SPOT_RIGHT_CENTER + swingR);
    }

    void InitializeFlickerTimers(double now)
    {
        for (int i = 0; i < _windows.Count; i++)
        {
            var w = _windows[i];
            w.nextToggleTime = now + Random.Range(0.3f, FLICKER_MAX_INTERVAL);
            _windows[i] = w;
        }
    }

    void FlickerWindows(double now)
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _windows.Count; i++)
        {
            var w = _windows[i];
            if (now >= w.nextToggleTime)
            {
                w.isOn = !w.isOn;
                w.nextToggleTime = now + Random.Range(FLICKER_MIN_INTERVAL, FLICKER_MAX_INTERVAL);
            }
            float target = w.isOn ? 0f : 1f;
            w.offAlpha = Mathf.MoveTowards(w.offAlpha, target, dt * FADE_SPEED);
            w.image.color = new Color(w.offR, w.offG, w.offB, w.offAlpha);
            _windows[i] = w;
        }
    }

    Sprite MakeWhiteSprite()
    {
        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        var w = new Color32(255, 255, 255, 255);
        tex.SetPixels32(new[] { w, w, w, w });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
    }
}
