using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 도시 구간이 끝난 뒤 이어지는 드라이빙 배경.
///   하늘 + 속도가 다른 세 겹의 건물(멀수록 느리게 흐른다) + 가로등 + 고정된 도로(차선이 흐른다)
///   + 도로 위 한가운데의 자동차. 차는 박자마다 살짝 튀고, 어두운 페이즈에선 헤드라이트를 켠다.
///
/// 하늘 교체는 페이드 없이 즉시 이뤄지고, 마지막 전환만 페이드다:
///   ~MORNING_START   노을. 지평선 근처에 해가 걸려 있고 건물 창에 불이 켜져 있다
///   MORNING_START~   아침 하늘. 불은 꺼지고 유리창만 하늘빛을 받는다
///   NIGHT_START~     완전 검은 하늘 + 명암 반전 — 도로·자동차는 흰색, 건물은 반투명 흰색,
///                    켜진 창은 검게 뚫린다. 하늘엔 별
///   COLORFUL_START~  하늘·건물 그룹(Scenery)이 COLORFUL_FADE_SEC 동안 엷어지며 아래에 깔린
///                    디스코 셀이 서서히 드러난다. 색은 Night 그대로 유지하고 알파만 내린다.
///                    도로·차선·자동차는 그룹 밖이라 끝까지 남는다.
///
/// 이미지 에셋 없이 전부 절차적으로 그린다. 각 겹은 STRIP_W 폭 안에 건물을 깔고 그 폭을 주기로
/// 감아 무한히 흐른다. 경계를 걸치는 건물은 만들지 않아야 이음매가 안 보인다.
///
/// 캔버스는 referenceResolution 1080×1920 / matchWidth=0 이라 가로는 항상 1080 단위이고
/// 세로만 화면 비율에 따라 변한다. STRIP_W는 그 1080 보다 넉넉해야 건물이 끊기지 않는다.
/// </summary>
public class DrivingBackground : MonoBehaviour
{
    // 도시가 페이드아웃을 시작하는 순간 도시 "뒤에서" 함께 등장한다.
    // 도시가 천천히 엷어지는 동안 이쪽이 서서히 드러나 크로스페이드가 된다.
    // 페이드인은 없다 — 여기서도 페이드인을 하면 두 배경이 동시에 반투명해지는 순간이 생겨
    // 그 아래 디스코 셀 레이어가 언뜻 비친다.
    public const double APPEAR_START = CityLightBackground.FADE_OUT_START;

    // 페이즈 전환 시각. 하늘 교체(노을→아침→밤)는 페이드 없이 즉시 바뀐다.
    public const double MORNING_START  = 196.6;   // 3분 16.6초 — 노을 → 아침
    public const double NIGHT_START    = 212.6;   // 3분 32.6초 — 아침 → 검은 하늘(명암 반전)
    public const double COLORFUL_START = 229.6;   // 3분 49.6초 — 하늘·건물이 걷히고 디스코 셀이 드러남

    // 마지막 전환만은 페이드다. 하늘·건물이 이 시간에 걸쳐 엷어지며 아래 셀 배경이 드러난다.
    const float COLORFUL_FADE_SEC = 2.5f;   // ── 조정 손잡이: 셀 배경으로 넘어가는 페이드 길이 ──

    // 화면이 검게 덮이기 시작하는 시각. ── 조정 손잡이: 2차 터널 진입 타이밍 ──
    // 이 배경은 sibling 순서상 InGameUI의 검정 배경(_bgImage, sibling 0)보다 위에 깔리므로,
    // 배경만 검게 만들어서는 도로·자동차가 그대로 남는다. 여기서 루트 CanvasGroup을 같은 램프로
    // 내려 이미 검어진 배경 위로 녹아 없어지게 한다. InGameUI.BLACK2_START가 이 값을 참조하므로
    // 이 숫자 하나만 옮기면 배경 검정·디스코 셀·도로·자동차가 다 같이 따라온다.
    public const double BLACKOUT_START = 242.0;

    // 완전히 검어져 배경을 통째로 정리하고 2차 터널에 자리를 넘기는 시각.
    public const double DISAPPEAR_AT = 245.2;   // 4분 5.2초

    // ── 도로·자동차 ─────────────────────────────────────────────
    const float GROUND_H = 180f;
    // 자동차 그림(Sprites/Effects/car.png, 440x181)의 비율에 맞춘 크기.
    const float CAR_W    = 380f;
    const float CAR_H    = CAR_W * 181f / 440f;
    // 그림 속 램프 위치 (그림 폭·높이에 대한 비율, 왼쪽 아래가 0). 빛줄기와 번짐을 여기에 맞춘다.
    static readonly Vector2 HEADLIGHT_UV = new Vector2(0.925f, 0.467f);
    static readonly Vector2 TAILLIGHT_UV = new Vector2(0.058f, 0.483f);

    // 페이즈별 차 그림. 색 보정은 코드로 하지 않고 그림을 바꿔 끼운다 — 빛이 비치는 방향까지
    // 담을 수 있어 코드로 색을 고르게 입히는 것보다 자연스럽다.
    //   car.png         아침 (기본). 없으면 예전 실루엣 도형으로 대신한다
    //   car_night.png   노을(주황빛 도시)에 맞춰 색을 입힌 차. 없으면 car.png
    //   car_white.png   밤(명암 반전)의 흰 차. 없으면 car.png
    // 세 장은 크기(440x181)와 차 위치가 같아야 한다. 램프 위치(HEADLIGHT_UV 등)를 같이 쓴다.
    const float CAR_Y    = GROUND_H - 6f;   // 바퀴가 도로 표면에 살짝 물리도록
    const float CAR_BOB  = 6f;              // ── 조정 손잡이: 박자마다 차가 튀는 높이 ──

    // 가장 가까운 겹이 흐르는 속도. 나머지는 여기에 비율을 곱한다.
    const float SCROLL_SPEED = 360f;        // ── 조정 손잡이: 전체 속도(units/sec) ──
    const float STRIP_W      = 2400f;

    // 도로 단면 (바닥에서부터 y):
    //   0 ~ GROUND_H  아스팔트. 아래로 갈수록 살짝 어두워진다
    //   맨 위          차가 올라선 연석 — 밝은 윗면 + 그 아래 어두운 옆면 한 줄
    //   LANE_Y        중앙 점선 (차 바로 아래로 지나간다)
    //   EDGE_Y        바깥 실선
    //   + 일정 간격의 이음매가 차선과 같이 흘러 노면 질감과 속도감을 준다
    const float LANE_DASH_W = 90f, LANE_DASH_H = 10f, LANE_GAP = 120f;
    const float LANE_SPEED  = 1.6f;         // 도로는 건물보다 가까우니 더 빨리 흐른다
    const float LANE_Y      = 110f;
    const float EDGE_Y      = 30f,  EDGE_H = 5f;
    const float CURB_H      = 8f,   CURB_SHADE_H = 6f;
    const float JOINT_GAP   = 340f, JOINT_W = 3f;
    const float SHADOW_W    = 340f, SHADOW_H = 24f;
    const float POST_SPACING = 640f, POST_H = 300f, POST_SPEED = 1.25f;

    const int SEED = 23;

    enum ScenePhase { Sunset, Morning, Night, Colorful }

    // ── 색 ──────────────────────────────────────────────────────
    // 페이즈별 [노을, 아침, 밤]. Colorful 은 밤 색을 그대로 쓴다.
    struct Palette { public Color body, side, glass, lit; }

    // 하늘: 노을은 세 단(위 남보라 → 중간 자주 → 지평선 주황), 아침은 위 파랑 → 지평선 크림.
    static readonly Color SUNSET_TOP = new Color(0.08f, 0.05f, 0.14f);
    static readonly Color SUNSET_MID = new Color(0.36f, 0.12f, 0.20f);
    static readonly Color SUNSET_LOW = new Color(0.88f, 0.42f, 0.16f);
    static readonly Color MORNING_TOP    = new Color(0.36f, 0.58f, 0.82f);
    static readonly Color MORNING_BOTTOM = new Color(0.88f, 0.86f, 0.76f);
    // 도로 색 [노을, 아침, 밤]. 밤은 명암 반전이라 흰 노면에 검은 선이다.
    struct RoadPalette { public Color road, curb, curbShade, lane, edge, joint, shade, shadow; }
    static readonly RoadPalette[] ROAD_PAL = {
        new RoadPalette { road = new Color(0.05f, 0.03f, 0.05f), curb = new Color(0.32f, 0.16f, 0.16f),
                          curbShade = new Color(0.02f, 0.01f, 0.02f), lane = new Color(0.88f, 0.68f, 0.38f),
                          edge = new Color(0.45f, 0.38f, 0.36f), joint = new Color(0f, 0f, 0f, 0.45f),
                          shade = new Color(0f, 0f, 0f, 0.40f), shadow = new Color(0f, 0f, 0f, 0.60f) },
        new RoadPalette { road = new Color(0.16f, 0.17f, 0.20f), curb = new Color(0.58f, 0.60f, 0.65f),
                          curbShade = new Color(0.09f, 0.09f, 0.11f), lane = new Color(0.93f, 0.89f, 0.72f),
                          edge = new Color(0.80f, 0.80f, 0.78f), joint = new Color(0f, 0f, 0f, 0.30f),
                          shade = new Color(0f, 0f, 0f, 0.35f), shadow = new Color(0f, 0f, 0f, 0.50f) },
        new RoadPalette { road = Color.white, curb = new Color(0.55f, 0.55f, 0.55f),
                          curbShade = new Color(0.82f, 0.82f, 0.82f), lane = Color.black,
                          edge = Color.black, joint = new Color(0f, 0f, 0f, 0.15f),
                          shade = new Color(0f, 0f, 0f, 0.12f), shadow = new Color(0f, 0f, 0f, 0.28f) },
    };

    // 겹마다 멀수록 하늘에 가깝다. 밤(명암 반전)은 흰색의 불투명도로 거리를 낸다.
    static readonly Palette[] FAR_PAL = {
        new Palette { body = new Color(0.42f, 0.17f, 0.20f), side = new Color(0.37f, 0.15f, 0.19f) },
        new Palette { body = new Color(0.64f, 0.72f, 0.82f), side = new Color(0.60f, 0.68f, 0.79f) },
        new Palette { body = new Color(1f, 1f, 1f, 0.16f),   side = new Color(1f, 1f, 1f, 0.05f) },
    };
    static readonly Palette[] MID_PAL = {
        new Palette { body = new Color(0.22f, 0.08f, 0.13f), side = new Color(0.17f, 0.06f, 0.11f),
                      glass = new Color(0.16f, 0.06f, 0.10f), lit = new Color(1.00f, 0.74f, 0.42f, 0.85f) },
        new Palette { body = new Color(0.42f, 0.50f, 0.62f), side = new Color(0.36f, 0.43f, 0.55f),
                      glass = new Color(0.62f, 0.74f, 0.88f), lit = new Color(0f, 0f, 0f, 0f) },
        new Palette { body = new Color(1f, 1f, 1f, 0.25f),   side = new Color(1f, 1f, 1f, 0.07f),
                      glass = new Color(0f, 0f, 0f, 0f),     lit = new Color(0f, 0f, 0f, 0.55f) },
    };
    static readonly Palette[] NEAR_PAL = {
        new Palette { body = new Color(0.07f, 0.03f, 0.06f), side = new Color(0.04f, 0.02f, 0.04f),
                      glass = new Color(0.05f, 0.02f, 0.04f), lit = new Color(1.00f, 0.80f, 0.48f, 1f) },
        new Palette { body = new Color(0.20f, 0.24f, 0.32f), side = new Color(0.15f, 0.18f, 0.25f),
                      glass = new Color(0.50f, 0.62f, 0.78f), lit = new Color(0f, 0f, 0f, 0f) },
        new Palette { body = new Color(1f, 1f, 1f, 0.38f),   side = new Color(1f, 1f, 1f, 0.10f),   // 흰 자동차가 묻히지 않을 만큼만
                      glass = new Color(0f, 0f, 0f, 0f),     lit = new Color(0f, 0f, 0f, 0.8f) },
    };

    // ── 겹 설정 ─────────────────────────────────────────────────
    class Layer
    {
        public string    name;
        public float     speed;               // SCROLL_SPEED 에 곱하는 비율
        public float     minW, maxW, minH, maxH, gapMin, gapMax;
        public float     cell;                // 창 한 칸 크기(0 이면 창 없음)
        public Palette[] pal;

        // 만들어진 것들. 페이즈가 바뀌면 색을 갈아끼운다.
        public readonly List<Image>    bodies = new List<Image>();
        public readonly List<Image>    sides  = new List<Image>();
        public readonly List<RawImage> glass  = new List<RawImage>();
        public readonly List<RawImage> lit    = new List<RawImage>();
        public RectTransform[] rts;
        public float[]         baseX;
        public float           scrollX;
    }

    Layer _far, _mid, _near;
    Layer[] _layers;   // 매 프레임 배열을 새로 만들지 않게

    // 창 무늬. 한 칸을 6x6 px 로 그리고 Point 로 늘린다 — 겹마다 칸 크기만 다르게 보여 주면 된다.
    // 켜진 창 무늬는 16x32 칸짜리 한 장을 건물마다 다른 위치에서 잘라 쓴다.
    const int CELL_PX = 6, WIN_PX0 = 1, WIN_PX1 = 5;   // 칸 안에서 창이 차지하는 px 구간 [1,5)
    const int PAT_COLS = 16, PAT_ROWS = 32;
    const float LIT_RATE = 0.22f;

    RectTransform _root;
    CanvasGroup   _canvasGroup;
    BeatTracker   _bt;
    System.Random _rnd;

    Sprite    _slantSprite, _softDot, _disc;
    Texture2D _glassTex, _litTex;

    Image      _skyImg, _groundImg, _carImg, _headlight, _taillight;
    Sprite     _sunsetSky, _morningSky, _nightSky;
    GameObject _sunsetSun, _morningSun, _stars;
    readonly List<Image> _lanes      = new List<Image>();
    readonly List<Image> _joints     = new List<Image>();
    Image           _roadShade, _curb, _curbShade, _edge, _carShadow;
    RectTransform[] _jointRts;
    float[]         _jointBaseX;
    float           _jointStrip;
    RectTransform   _carShadowRt;
    readonly List<Image> _posts      = new List<Image>();
    readonly List<Image> _postLamps  = new List<Image>();
    RectTransform[] _laneRts, _postRts;
    float[]         _laneBaseX, _postBaseX;
    float           _laneScroll, _postScroll;
    float           _laneStrip, _postStrip;
    RectTransform   _carRt;

    ScenePhase _phase = ScenePhase.Sunset;

    // 하늘+건물 묶음. Colorful로 넘어갈 때 이 그룹만 페이드아웃해 아래 디스코 셀을 드러낸다.
    GameObject  _sceneryGo;
    CanvasGroup _sceneryGroup;

    /// Colorful 페이즈부터는 하늘·건물이 걷히며 아래 레이어가 드러나므로 기본 배경이 다시 필요하다.
    /// 페이드가 시작되는 순간부터 false — 아직 하늘이 불투명할 때 미리 아래를 준비시켜야
    /// 엷어지는 동안 셀이 이미 그 자리에 있다.
    public bool CoversScreen => _phase != ScenePhase.Colorful;

    // ── 미리보기 (공유 카드) ─────────────────────────────────────
    // 곡 없이 한 구간을 그대로 되풀이한다. BeatTracker 대신 스스로 시간을 흘리고, 구간 끝에
    // 닿으면 처음으로 돌아간다. 구간 밖으로 나가 스스로 사라지는 일도 없다.
    bool   _preview;
    double _previewT, _previewLo, _previewHi;

    public static DrivingBackground SpawnPreview(Transform parent, double fromSec, double toSec)
    {
        var go = new GameObject("DrivingBackgroundPreview");
        go.transform.SetParent(parent, false);
        var comp = go.AddComponent<DrivingBackground>();
        comp._preview   = true;
        comp._previewLo = fromSec;
        comp._previewHi = toSec;
        comp._previewT  = fromSec;
        comp.Build();
        return comp;
    }

    double NowSec()
    {
        if (!_preview) return _bt.PlaybackSec;
        _previewT += Time.deltaTime;
        if (_previewT >= _previewHi) _previewT = _previewLo;
        return _previewT;
    }

    float NowBeatPhase(double t) =>
        _preview ? (float)(t * BeatTracker.BPM / 60.0 % 1.0) : _bt.BeatPhase;

    public static DrivingBackground Spawn(Canvas parentCanvas, BeatTracker bt)
    {
        var go = new GameObject("DrivingBackground");
        go.transform.SetParent(parentCanvas.transform, false);
        var comp = go.AddComponent<DrivingBackground>();
        comp._bt = bt;
        comp.Build();
        return comp;
    }

    void Build()
    {
        _root = gameObject.AddComponent<RectTransform>();
        Stretch(_root);
        // 최종 sibling 순서는 spawn 쪽(InGameUI)에서 지정한다.

        _canvasGroup                = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 1f;   // 페이드인 없음 — 도시 뒤에서 처음부터 불투명
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        _rnd         = new System.Random(SEED);
        _slantSprite = MakeSlantSprite(64, 64);
        _softDot     = MakeDotSprite(64, true);
        _disc        = MakeDotSprite(64, false);
        _glassTex    = MakeGlassTile();
        _litTex      = MakeLitPattern();

        _sceneryGo = new GameObject("Scenery");
        _sceneryGo.transform.SetParent(_root, false);
        Stretch(_sceneryGo.AddComponent<RectTransform>());
        _sceneryGroup = _sceneryGo.AddComponent<CanvasGroup>();
        _sceneryGroup.interactable   = false;
        _sceneryGroup.blocksRaycasts = false;

        _far = new Layer { name = "Far", speed = 0.25f, minW = 110f, maxW = 220f, minH = 500f, maxH = 1000f,
                           gapMin = 10f, gapMax = 50f, cell = 0f, pal = FAR_PAL };
        _mid = new Layer { name = "Mid", speed = 0.55f, minW = 140f, maxW = 260f, minH = 420f, maxH = 880f,
                           gapMin = 30f, gapMax = 90f, cell = 26f, pal = MID_PAL };
        _near = new Layer { name = "Near", speed = 1f, minW = 180f, maxW = 320f, minH = 220f, maxH = 560f,
                            gapMin = 60f, gapMax = 160f, cell = 34f, pal = NEAR_PAL };

        // 그리는 순서 = 겹치는 순서. 하늘 → 해·별 → 먼 겹 → 중간 → 가까운 겹 → 가로등
        // → (그룹 밖) 도로 → 차선 → 헤드라이트 → 자동차.
        _layers = new[] { _far, _mid, _near };

        BuildSky();
        BuildSkyObjects();
        BuildLayer(_far);
        BuildLayer(_mid);
        BuildLayer(_near);
        BuildPosts();
        BuildGround();
        BuildLanes();
        BuildCar();

        ApplyPhase(ScenePhase.Sunset);
        ApplyAllPositions();
    }

    // ── 하늘 ────────────────────────────────────────────────────

    void BuildSky()
    {
        float groundFrac = GROUND_H / CanvasMetrics.Height;
        _sunsetSky = MakeGradient(256, up =>
        {
            // 지평선(도로 윗변)에서 주황, 화면 40% 높이쯤 자주, 꼭대기 남보라.
            float t = Mathf.Clamp01((up - groundFrac) / (1f - groundFrac));
            return t < 0.4f ? Color.Lerp(SUNSET_LOW, SUNSET_MID, Smooth(t / 0.4f))
                            : Color.Lerp(SUNSET_MID, SUNSET_TOP, Smooth((t - 0.4f) / 0.6f));
        });
        _morningSky = MakeGradient(256, up => Color.Lerp(MORNING_BOTTOM, MORNING_TOP, Smooth(up)));
        _nightSky   = MakeGradient(2, up => Color.black);

        var rt = NewRect("Sky", _sceneryGo.transform);
        Stretch(rt);
        _skyImg = AddImage(rt.gameObject, Color.white);
        _skyImg.sprite = _sunsetSky;
    }

    void BuildSkyObjects()
    {
        // 노을 해: 먼 건물 지붕선 뒤에 반쯤 걸린다. 건물이 지평선을 꽉 채우고 있어 그보다 낮으면 안 보인다.
        _sunsetSun = new GameObject("SunsetSun");
        _sunsetSun.transform.SetParent(_sceneryGo.transform, false);
        Stretch(_sunsetSun.AddComponent<RectTransform>());
        AddBottomDot(_sunsetSun.transform, new Vector2(240f, GROUND_H + 980f), 820f, _softDot, new Color(1f, 0.55f, 0.25f, 0.45f));
        AddBottomDot(_sunsetSun.transform, new Vector2(240f, GROUND_H + 980f), 240f, _disc,    new Color(1f, 0.80f, 0.50f, 1f));

        // 아침 해: 높이 떠서 옅게 번지기만 한다. 또렷한 원을 두면 아침이 아니라 한낮 같다.
        _morningSun = new GameObject("MorningSun");
        _morningSun.transform.SetParent(_sceneryGo.transform, false);
        Stretch(_morningSun.AddComponent<RectTransform>());
        float hi = CanvasMetrics.Height - 520f;
        AddBottomDot(_morningSun.transform, new Vector2(-300f, hi), 700f, _softDot, new Color(1f, 0.98f, 0.88f, 0.55f));
        AddBottomDot(_morningSun.transform, new Vector2(-300f, hi), 170f, _softDot, new Color(1f, 1f, 0.95f, 0.9f));

        // 밤 별: 명암 반전된 검은 하늘에 흰 점.
        _stars = new GameObject("Stars");
        _stars.transform.SetParent(_sceneryGo.transform, false);
        Stretch(_stars.AddComponent<RectTransform>());
        for (int i = 0; i < 50; i++)
        {
            float s = Range(2f, 5f);
            AddBottomDot(_stars.transform, new Vector2(Range(-540f, 540f), Range(GROUND_H + 500f, CanvasMetrics.Height)),
                         s, _softDot, new Color(1f, 1f, 1f, Range(0.35f, 0.9f)));
        }
    }

    // ── 건물 겹 ─────────────────────────────────────────────────

    void BuildLayer(Layer L)
    {
        var layerRt = NewRect(L.name, _sceneryGo.transform);
        Stretch(layerRt);

        var rts = new List<RectTransform>();
        var xs  = new List<float>();
        float cursor = 0f;
        while (true)
        {
            float bw = Range(L.minW, L.maxW);
            if (cursor + bw > STRIP_W) break;      // 경계를 넘으면 배치 중단
            float bh = Range(L.minH, L.maxH);

            var rt = NewRect("Bldg", layerRt);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // 가로는 화면 중앙, 세로는 바닥 기준
            rt.pivot     = Vector2.zero;
            rt.sizeDelta = new Vector2(bw, bh);
            BuildBuilding(L, rt, bw, bh);

            rts.Add(rt);
            xs.Add(cursor - STRIP_W * 0.5f);
            cursor += bw + Range(L.gapMin, L.gapMax);
        }
        L.rts   = rts.ToArray();
        L.baseX = xs.ToArray();
    }

    // 본체 + 그늘진 오른쪽 면 + 창 + 옥상 장식(평평·단차·첨탑·경사).
    // 밤에는 반투명 흰색이라 조각이 겹치면 그 부분만 진해진다. 그래서 옥상 장식은 본체 위로
    // 닿기만 하게 쌓고, 옆면은 본체를 덮지 않도록 옥상 장식 쪽엔 두지 않는다.
    void BuildBuilding(Layer L, RectTransform parent, float w, float h)
    {
        L.bodies.Add(AddPart(parent, 0f, 0f, w, h));
        L.sides.Add(AddPart(parent, w * 0.78f, 0f, w * 0.22f, h));

        if (L.cell > 0f) AddWindows(L, parent, w, h);

        switch (_rnd.Next(4))
        {
            case 0: break;   // 평평한 옥상
            case 1:          // 단차
            {
                float sw = w * Range(0.45f, 0.70f);
                L.bodies.Add(AddPart(parent, (w - sw) * 0.5f, h, sw, Range(50f, 130f)));
                break;
            }
            case 2:          // 첨탑
            {
                float sw = w * Range(0.40f, 0.60f), sh = Range(40f, 90f);
                L.bodies.Add(AddPart(parent, (w - sw) * 0.5f, h, sw, sh));
                float mw = Range(8f, 16f);
                L.bodies.Add(AddPart(parent, (w - mw) * 0.5f, h + sh, mw, Range(90f, 200f)));
                break;
            }
            default:         // 경사진 옥상
            {
                var rt = NewRect("Roof", parent);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot            = new Vector2(0.5f, 0f);   // 가로 중앙이라 좌우 반전해도 제자리
                rt.sizeDelta        = new Vector2(w, Range(70f, 170f));
                rt.anchoredPosition = new Vector2(w * 0.5f, h);
                if (_rnd.NextDouble() < 0.5) rt.localScale = new Vector3(-1f, 1f, 1f);
                var img = AddImage(rt.gameObject, Color.white);
                img.sprite = _slantSprite;
                L.bodies.Add(img);
                break;
            }
        }
    }

    void AddWindows(Layer L, RectTransform parent, float w, float h)
    {
        int cols = Mathf.FloorToInt((w * 0.78f - w * 0.12f) / L.cell);
        int rows = Mathf.FloorToInt((h - L.cell * 1.4f) / L.cell);
        if (cols <= 0 || rows <= 0) return;

        var size = new Vector2(cols * L.cell, rows * L.cell);
        var pos  = new Vector2(w * 0.12f, h - L.cell * 1.2f - size.y);

        L.glass.Add(AddRaw(parent, pos, size, _glassTex, new Rect(0f, 0f, cols, rows)));
        // 켜진 창 무늬는 건물마다 다른 칸에서 잘라 쓴다. 칸 단위로 맞춰야 창 자리와 어긋나지 않는다.
        var uv = new Rect(_rnd.Next(PAT_COLS) / (float)PAT_COLS, _rnd.Next(PAT_ROWS) / (float)PAT_ROWS,
                          cols / (float)PAT_COLS, rows / (float)PAT_ROWS);
        L.lit.Add(AddRaw(parent, pos, size, _litTex, uv));
    }

    // ── 가로등·도로·자동차 ──────────────────────────────────────

    void BuildPosts()
    {
        var layerRt = NewRect("Posts", _sceneryGo.transform);
        Stretch(layerRt);
        int n = Mathf.CeilToInt(STRIP_W / POST_SPACING);
        _postStrip = n * POST_SPACING;
        _postRts   = new RectTransform[n];
        _postBaseX = new float[n];
        for (int i = 0; i < n; i++)
        {
            var rt = NewRect("Post", layerRt);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = Vector2.zero;
            rt.sizeDelta = new Vector2(60f, POST_H);
            _posts.Add(AddPart(rt, 0f, 0f, 8f, POST_H));               // 기둥
            _posts.Add(AddPart(rt, 0f, POST_H - 8f, 46f, 8f));         // 팔
            var lamp = NewRect("Lamp", rt);
            lamp.anchorMin = lamp.anchorMax = Vector2.zero;
            lamp.anchoredPosition = new Vector2(42f, POST_H - 10f);
            lamp.sizeDelta        = new Vector2(90f, 90f);
            var li = AddImage(lamp.gameObject, Color.white);
            li.sprite = _softDot;
            _postLamps.Add(li);

            _postRts[i]   = rt;
            _postBaseX[i] = i * POST_SPACING - _postStrip * 0.5f;
        }
    }

    void BuildGround()
    {
        _groundImg = AddImage(RoadBand("Ground", 0f, GROUND_H).gameObject, Color.white);

        // 아래로 갈수록 어두워지는 막 — 노면이 앞으로 기울어진 것처럼 보인다.
        _roadShade = AddImage(RoadBand("RoadShade", 0f, GROUND_H).gameObject, Color.white);
        _roadShade.sprite = MakeGradient(64, up => new Color(1f, 1f, 1f, 1f - Smooth(up)));

        _curbShade = AddImage(RoadBand("CurbShade", GROUND_H - CURB_H - CURB_SHADE_H, CURB_SHADE_H).gameObject, Color.white);
        _curb      = AddImage(RoadBand("Curb", GROUND_H - CURB_H, CURB_H).gameObject, Color.white);
        _edge      = AddImage(RoadBand("EdgeLine", EDGE_Y, EDGE_H).gameObject, Color.white);
    }

    // 화면 폭으로 늘어나는 가로 띠 하나 (바닥에서 y 높이, 두께 h).
    RectTransform RoadBand(string name, float y, float h)
    {
        var rt = NewRect(name, _root);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, h);
        rt.anchoredPosition = new Vector2(0f, y);
        return rt;
    }

    void BuildLanes()
    {
        // 이음매가 먼저 — 점선이 그 위를 지나가야 한다.
        int nj = Mathf.CeilToInt((CanvasMetrics.REF_W + JOINT_GAP * 2f) / JOINT_GAP);
        _jointStrip = nj * JOINT_GAP;
        _jointRts   = new RectTransform[nj];
        _jointBaseX = new float[nj];
        for (int i = 0; i < nj; i++)
        {
            var rt = NewRect("Joint", _root);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(JOINT_W, GROUND_H - CURB_H - CURB_SHADE_H);
            _joints.Add(AddImage(rt.gameObject, Color.white));
            _jointRts[i]   = rt;
            _jointBaseX[i] = i * JOINT_GAP - _jointStrip * 0.5f;
        }

        float step = LANE_DASH_W + LANE_GAP;
        int n = Mathf.CeilToInt((CanvasMetrics.REF_W + step * 2f) / step);
        _laneStrip = n * step;
        _laneRts   = new RectTransform[n];
        _laneBaseX = new float[n];
        for (int i = 0; i < n; i++)
        {
            var rt = NewRect("Lane", _root);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(LANE_DASH_W, LANE_DASH_H);
            _lanes.Add(AddImage(rt.gameObject, Color.white));
            _laneRts[i]   = rt;
            _laneBaseX[i] = i * step - _laneStrip * 0.5f;
        }

        // 차 그림자. 연석 위에 납작하게 깔려 차가 떠 보이지 않게 붙잡아 준다.
        _carShadowRt = NewRect("CarShadow", _root);
        _carShadowRt.anchorMin = _carShadowRt.anchorMax = new Vector2(0.5f, 0f);
        _carShadowRt.anchoredPosition = new Vector2(0f, CAR_Y + 4f);
        _carShadowRt.sizeDelta        = new Vector2(SHADOW_W, SHADOW_H);
        _carShadow = AddImage(_carShadowRt.gameObject, Color.black);
        _carShadow.sprite = _softDot;
    }

    bool   _carIsArt;   // car.png 를 쓰는지. 없으면 예전 실루엣 도형으로 대신한다
    Sprite _carDay, _carSunset, _carNight;

    void BuildCar()
    {
        _carRt = NewRect("Car", _root);
        _carRt.anchorMin = _carRt.anchorMax = new Vector2(0.5f, 0f);
        _carRt.pivot            = new Vector2(0.5f, 0f);
        _carRt.sizeDelta        = new Vector2(CAR_W, CAR_H);
        _carRt.anchoredPosition = new Vector2(0f, CAR_Y);

        // Texture2D 로 읽어 직접 스프라이트를 만든다. 임포트 설정(Sprite 타입 여부)에 매이지 않는다.
        _carDay    = LoadSprite("Sprites/Effects/car");
        _carSunset = LoadSprite("Sprites/Effects/car_night");   // 파일 이름은 그린 사람 기준 — 주황빛 "밤" 도시용
        _carNight  = LoadSprite("Sprites/Effects/car_white");
        _carIsArt  = _carDay != null;
        _carImg = AddImage(_carRt.gameObject, Color.white);
        _carImg.sprite = _carIsArt ? _carDay : MakeCarSprite(512, 256);
        _carImg.preserveAspect = true;

        // 미등 번짐과 헤드라이트 빛줄기는 차의 자식이라 박자에 튈 때 같이 움직인다.
        // 그림에 램프가 이미 그려져 있으니 여기선 그 둘레에 빛만 더한다.
        var tl = NewRect("Taillight", _carRt);
        tl.anchorMin = tl.anchorMax = TAILLIGHT_UV;
        tl.anchoredPosition = Vector2.zero;
        tl.sizeDelta        = new Vector2(56f, 56f);
        _taillight = AddImage(tl.gameObject, Color.clear);
        _taillight.sprite = _softDot;

        var hl = NewRect("Headlight", _carRt);
        hl.anchorMin = hl.anchorMax = HEADLIGHT_UV;
        hl.pivot            = new Vector2(0f, 0.5f);
        hl.anchoredPosition = new Vector2(-6f, 0f);   // 램프 안쪽에서 시작해야 빛이 램프에서 나온다
        hl.sizeDelta        = new Vector2(460f, 170f);
        _headlight = AddImage(hl.gameObject, Color.clear);
        _headlight.sprite = MakeHeadlightSprite(256, 128);
    }

    // ── 매 프레임 ───────────────────────────────────────────────

    void Update()
    {
        if (_bt == null && !_preview) return;
        double t = NowSec();

        // 자기 구간을 벗어나면 스스로 정리한다. 되감기나 트랙 루프로 앞으로 돌아간 경우도 포함.
        // (CityLightBackground와 같은 규칙 — 얼어붙은 채 화면에 남지 않게)
        if (!_preview && (t < APPEAR_START || t >= DISAPPEAR_AT))
        {
            Destroy(gameObject);
            return;
        }

        // 페이즈 즉시 교체. 바뀌는 프레임에만 건드려 매 프레임 그래픽을 더럽히지 않는다.
        ScenePhase want = t >= COLORFUL_START ? ScenePhase.Colorful
                        : t >= NIGHT_START    ? ScenePhase.Night
                        : t >= MORNING_START  ? ScenePhase.Morning
                        :                       ScenePhase.Sunset;
        if (want != _phase) ApplyPhase(want);

        // BLACKOUT_START부터 배경 전체가 엷어진다. 그 아래에는 InGameUI가 같은 램프로 검게 만든
        // _bgImage가 깔려 있어, 도로·자동차가 검정에 잠기듯 사라진다.
        // (CanvasGroup은 중첩되면 곱해지므로 아래 sceneryAlpha와 서로를 덮어쓰지 않는다.)
        float rootAlpha = t < BLACKOUT_START
            ? 1f
            : 1f - Mathf.Clamp01((float)((t - BLACKOUT_START) / (DISAPPEAR_AT - BLACKOUT_START)));
        if (!Mathf.Approximately(_canvasGroup.alpha, rootAlpha))
            _canvasGroup.alpha = rootAlpha;

        float dt = Time.deltaTime;

        // 도로와 차는 Colorful 이후에도 남으니 그룹 페이드와 상관없이 움직인다.
        _laneScroll -= SCROLL_SPEED * LANE_SPEED * dt;
        if (_laneScroll <= -_laneStrip) _laneScroll += _laneStrip;
        for (int i = 0; i < _laneRts.Length; i++)
            _laneRts[i].anchoredPosition = new Vector2(Wrap(_laneBaseX[i] + _laneScroll, _laneStrip), LANE_Y);
        for (int i = 0; i < _jointRts.Length; i++)
            _jointRts[i].anchoredPosition = new Vector2(Wrap(_jointBaseX[i] + _laneScroll, _jointStrip), 0f);

        // 차는 박자 시작에 살짝 떴다가 내려앉는다.
        float beat = 1f - NowBeatPhase(t);
        float lift = CAR_BOB * beat * beat * beat;
        _carRt.anchoredPosition = new Vector2(0f, CAR_Y + lift);
        // 차가 뜨면 그림자는 살짝 작아진다.
        _carShadowRt.sizeDelta = new Vector2(SHADOW_W * (1f - lift / CAR_BOB * 0.08f), SHADOW_H);

        // Colorful로 넘어가면 하늘·건물만 서서히 사라진다. 페이드 도중에도 건물은 계속 흘러야 자연스럽다.
        float sceneryAlpha = _phase != ScenePhase.Colorful
            ? 1f
            : 1f - Mathf.Clamp01((float)((t - COLORFUL_START) / COLORFUL_FADE_SEC));

        bool sceneryOn = sceneryAlpha > 0f;
        if (_sceneryGo.activeSelf != sceneryOn) _sceneryGo.SetActive(sceneryOn);
        if (!sceneryOn) return;   // 다 사라진 뒤에는 렌더도 스크롤도 멈춘다
        if (!Mathf.Approximately(_sceneryGroup.alpha, sceneryAlpha))
            _sceneryGroup.alpha = sceneryAlpha;

        foreach (var L in _layers)
        {
            L.scrollX -= SCROLL_SPEED * L.speed * dt;
            if (L.scrollX <= -STRIP_W) L.scrollX += STRIP_W;   // 오래 돌아도 정밀도가 새지 않게
        }
        _postScroll -= SCROLL_SPEED * POST_SPEED * dt;
        if (_postScroll <= -_postStrip) _postScroll += _postStrip;
        ApplyAllPositions();
    }

    void ApplyAllPositions()
    {
        foreach (var L in _layers)
            for (int i = 0; i < L.rts.Length; i++)
                L.rts[i].anchoredPosition = new Vector2(Wrap(L.baseX[i] + L.scrollX, STRIP_W), GROUND_H);
        for (int i = 0; i < _postRts.Length; i++)
            _postRts[i].anchoredPosition = new Vector2(Wrap(_postBaseX[i] + _postScroll, _postStrip), GROUND_H);
    }

    // 주기 strip 으로 감는다. 왼쪽으로 빠진 것이 오른쪽 끝에서 다시 들어온다.
    static float Wrap(float x, float strip) => Mathf.Repeat(x + strip * 0.5f, strip) - strip * 0.5f;

    // Night부터는 명암을 뒤집는다: 검은 하늘 위에 흰 땅·자동차, 반투명 흰 건물에 검게 뚫린 창.
    // Colorful은 Night와 같은 색을 유지한 채로 하늘·건물 그룹이 페이드아웃한다(Update가 알파를 몬다).
    // 색을 그대로 두는 게 중요하다 — 사라지는 도중에 색까지 바뀌면 페이드가 아니라 교체로 보인다.
    void ApplyPhase(ScenePhase p)
    {
        _phase = p;
        bool inverted = p == ScenePhase.Night || p == ScenePhase.Colorful;
        int  pi       = inverted ? 2 : p == ScenePhase.Morning ? 1 : 0;

        _skyImg.sprite = inverted ? _nightSky : pi == 1 ? _morningSky : _sunsetSky;
        _sunsetSun.SetActive(pi == 0);
        _morningSun.SetActive(pi == 1);
        _stars.SetActive(inverted);

        foreach (var L in _layers)
        {
            var c = L.pal[pi];
            foreach (var img in L.bodies) img.color = c.body;
            foreach (var img in L.sides)  img.color = c.side;
            foreach (var raw in L.glass)  raw.color = c.glass;
            foreach (var raw in L.lit)    raw.color = c.lit;
        }

        // 가로등은 가까운 건물과 같은 색. 불은 노을에만 켠다 — 반전된 밤에 번지는 빛은 얼룩처럼 보인다.
        // 반전된 밤에는 가까운 건물(반투명 흰색)보다 또렷하게 — 가로등이 휙휙 지나가는 게 속도감이다.
        foreach (var img in _posts) img.color = inverted ? new Color(1f, 1f, 1f, 0.8f) : NEAR_PAL[pi].body;
        foreach (var img in _postLamps) img.color = pi == 0 ? new Color(1f, 0.78f, 0.45f, 0.75f) : Color.clear;

        var road = ROAD_PAL[pi];
        _groundImg.color = road.road;
        _roadShade.color = road.shade;
        _curb.color      = road.curb;
        _curbShade.color = road.curbShade;
        _edge.color      = road.edge;
        _carShadow.color = road.shadow;
        foreach (var img in _lanes)  img.color = road.lane;
        foreach (var img in _joints) img.color = road.joint;
        if (_carIsArt)
        {
            // 페이즈 그림이 없으면 기본 그림으로 대신한다.
            var spr = inverted ? _carNight : pi == 0 ? _carSunset : null;
            _carImg.sprite = spr != null ? spr : _carDay;
            _carImg.color  = Color.white;   // 그림 색 그대로
        }
        else
        {
            // 그림이 없을 때의 실루엣 도형은 예전처럼 검정/흰색으로 칠한다.
            _carImg.color = inverted ? Color.white : Color.black;
        }

        // 헤드라이트·미등은 어두운 하늘에서만. 아침에 켜 두면 흐릿한 얼룩일 뿐이다.
        _headlight.color = pi == 0 ? new Color(1f, 0.86f, 0.62f, 0.40f)
                         : inverted ? new Color(1f, 1f, 1f, 0.30f) : Color.clear;
        _taillight.color = pi == 1 ? Color.clear : new Color(1f, 0.2f, 0.2f, 0.55f);
    }

    // ═══════════════════════════════════════════════════════════
    // 도우미
    // ═══════════════════════════════════════════════════════════

    static Sprite LoadSprite(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f));
    }

    float Range(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);
    static float Smooth(float t) => t * t * (3f - 2f * t);

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    /// <summary>스프라이트 없는 Image 는 단색 사각형이 된다.</summary>
    static Image AddImage(GameObject go, Color col)
    {
        var img = go.AddComponent<Image>();
        img.color         = col;
        img.raycastTarget = false;
        return img;
    }

    // 좌하단을 원점으로 하는 사각 조각 하나.
    static Image AddPart(RectTransform parent, float x, float y, float w, float h)
    {
        var rt = NewRect("Part", parent);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot            = Vector2.zero;
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        return AddImage(rt.gameObject, Color.white);
    }

    static RawImage AddRaw(RectTransform parent, Vector2 pos, Vector2 size, Texture tex, Rect uv)
    {
        var rt = NewRect("Windows", parent);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot            = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture       = tex;
        raw.uvRect        = uv;
        raw.raycastTarget = false;
        return raw;
    }

    // 화면 아래 가운데 기준으로 놓는 원. 하늘 장식(해·별)에 쓴다.
    static void AddBottomDot(Transform parent, Vector2 pos, float size, Sprite spr, Color col)
    {
        var rt = NewRect("Dot", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(size, size);
        var img = AddImage(rt.gameObject, col);
        img.sprite = spr;
    }

    // ── 굽기 ────────────────────────────────────────────────────
    // Texture2D 는 y=0 이 아래쪽이다.

    /// <summary>창 한 칸(6x6 px). 가운데 창만 칠하고 나머지는 비운다. Point 로 늘려 또렷하게.</summary>
    static Texture2D MakeGlassTile()
    {
        var px = new Color32[CELL_PX * CELL_PX];
        for (int y = 0; y < CELL_PX; y++)
            for (int x = 0; x < CELL_PX; x++)
            {
                bool inWin = x >= WIN_PX0 && x < WIN_PX1 && y >= WIN_PX0 && y < WIN_PX1;
                px[y * CELL_PX + x] = inWin ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        return Bake(px, CELL_PX, CELL_PX, TextureWrapMode.Repeat);
    }

    /// <summary>켜진 창 무늬. PAT_COLS x PAT_ROWS 칸 중 LIT_RATE 만큼을 창 자리에 칠한다.</summary>
    Texture2D MakeLitPattern()
    {
        int w = PAT_COLS * CELL_PX, h = PAT_ROWS * CELL_PX;
        var px = new Color32[w * h];
        for (int cy = 0; cy < PAT_ROWS; cy++)
            for (int cx = 0; cx < PAT_COLS; cx++)
            {
                if (_rnd.NextDouble() >= LIT_RATE) continue;
                for (int y = WIN_PX0; y < WIN_PX1; y++)
                    for (int x = WIN_PX0; x < WIN_PX1; x++)
                        px[(cy * CELL_PX + y) * w + cx * CELL_PX + x] = new Color32(255, 255, 255, 255);
            }
        return Bake(px, w, h, TextureWrapMode.Repeat);
    }

    static Texture2D Bake(Color32[] px, int w, int h, TextureWrapMode wrap)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = wrap;
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>세로로만 변하는 1px 폭 그라데이션. up 은 0 = 아래, 1 = 위.</summary>
    static Sprite MakeGradient(int h, System.Func<float, Color> colorAt)
    {
        var tex = new Texture2D(1, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[h];
        for (int y = 0; y < h; y++) px[y] = colorAt(h > 1 ? y / (h - 1f) : 0f);
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeDotSprite(int size, bool soft)
    {
        var px = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d  = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a  = soft ? Smooth(Mathf.Clamp01(1f - d)) : Mathf.Clamp01((1f - d) * r);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>왼쪽(램프)이 좁고 오른쪽으로 퍼지며 옅어지는 빛줄기.</summary>
    static Sprite MakeHeadlightSprite(int w, int h)
    {
        var px = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            float u    = x / (w - 1f);
            float edge = 0.12f + 0.38f * u;                  // 세로 반폭 (높이 비율)
            float len  = Mathf.Pow(1f - u, 1.6f);
            for (int y = 0; y < h; y++)
            {
                float dy = Mathf.Abs((y + 0.5f) / h - 0.5f);
                float a  = Smooth(Mathf.Clamp01(1f - dy / edge)) * len;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f));
    }

    // 오른쪽으로 낮아지는 직각삼각형(아래변·왼쪽변이 직각, 빗변이 좌상단 → 우하단).
    static Sprite MakeSlantSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px  = new Color32[w * h];
        var on  = new Color32(255, 255, 255, 255);
        var off = new Color32(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
                px[y * w + x] = (float)x / (w - 1) <= 1f - ny ? on : off;
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // 간단한 측면 자동차 실루엣: 차체 + 앞유리가 기운 캐빈 + 바퀴 두 개.
    static Sprite MakeCarSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px  = new Color32[w * h];
        var on  = new Color32(255, 255, 255, 255);
        var off = new Color32(0, 0, 0, 0);

        float bodyX0 = w * 0.03f, bodyX1 = w * 0.97f;
        float bodyY0 = h * 0.20f, bodyY1 = h * 0.56f;
        float cabX0  = w * 0.26f, cabX1  = w * 0.74f;
        float cabY1  = h * 0.95f;
        float cabSlant = w * 0.10f;
        float wheelR = h * 0.19f, wheelY = h * 0.20f;
        float wheelLX = w * 0.24f, wheelRX = w * 0.76f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool hit = x >= bodyX0 && x <= bodyX1 && y >= bodyY0 && y <= bodyY1;
                if (!hit && y > bodyY1 && y <= cabY1)
                {
                    float k = (y - bodyY1) / (cabY1 - bodyY1);
                    hit = x >= cabX0 + cabSlant * k && x <= cabX1 - cabSlant * k;
                }
                if (!hit)
                    hit = InCircle(x, y, wheelLX, wheelY, wheelR) || InCircle(x, y, wheelRX, wheelY, wheelR);
                px[y * w + x] = hit ? on : off;
            }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    static bool InCircle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    }
}
