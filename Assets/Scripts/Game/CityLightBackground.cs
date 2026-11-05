using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 디스코 모드 곡 후반의 도시 야경. 화이트아웃이 걷히면서 WHITE_OUT_END 시각에 완전히 드러난다.
///   WHITE_IN_START  화면 전체가 흰색으로 페이드인 (InGameUI의 WhiteFlashOverlay가 담당)
///   WHITE_FULL      완전 흰색 도달, WHITE_HOLD_SEC 동안 유지
///   APPEAR_START    도시를 즉시 완전히 노출 (아직 흰색에 완전히 가려진 상태)
///   WHITE_OUT_END   흰색 완전히 사라짐 = 도시 완전 노출
///   FADE_OUT_START~FADE_OUT_END  드라이빙 배경과 크로스페이드 후 자기 자신 파괴
///
/// 예전엔 city_light.png 한 장을 화면에 늘려 붙였다. 화면비에 따라 늘어나고 아래에 검은 띠가
/// 남았으며, 움직이는 건 창문 몇 개뿐이라 아이스 배경 옆에 두면 초라했다. 지금은 아이스와 같은
/// 방식(사각형 위주로 직접 그리기)으로 다시 그렸다. 차이는 디스코답게 밤이고, 곡에 맞춰 움직인다는 것.
///   · 깊은 남보라 밤하늘, 반짝이는 별, 건물 뒤에서 흔들리는 서치라이트 두 줄기
///   · 세 겹 스카이라인 — 계단식 옥상, 피뢰침, 옥상 네온 띠
///   · 네온 띠와 네온색 창은 박자마다 밝게 튄다. 피뢰침 불은 한 박씩 엇갈려 깜박인다
///   · 화면에 드러나는 창 몇 개가 가끔 꺼졌다 켜진다
/// </summary>
public class CityLightBackground : MonoBehaviour
{
    // ── 타이밍 (InGameUI·DrivingBackground 가 참조한다) ──────────
    // 양 끝점을 고정하고 그 사이를 페이드인 → 유지 → 페이드아웃으로 채운다.
    // 화이트아웃을 뒤로 미루고 싶으면 WHITE_IN_START만 올리면 되고,
    // 그만큼 마지막 페이드아웃(도시가 드러나는 구간)이 짧아진다.
    public const double WHITE_IN_START = 147.8;  // 화이트아웃 시작 (미루려면 이 값을 올린다)
    public const double WHITE_OUT_END  = 150;    // 도시 완전 노출 (음악 큐에 맞춘 고정점)
    const double WHITE_IN_SEC   = 0.5;           // 흰색 페이드인 길이
    const double WHITE_HOLD_SEC = 0.25;          // 완전 흰색 유지 길이

    public const double WHITE_FULL   = WHITE_IN_START + WHITE_IN_SEC;  // 완전 흰색 도달
    public const double APPEAR_START = WHITE_FULL + WHITE_HOLD_SEC;    // 도시 스폰 + 흰색 페이드아웃 시작

    // 드라이빙 배경이 FADE_OUT_START에 도시 뒤에서 함께 등장하므로, 이 구간 전체가
    // "도시가 엷어지며 드라이빙이 드러나는" 크로스페이드가 된다.
    public const double FADE_OUT_START = 180.6;
    const double FADE_OUT_SEC = 3.0;            // ── 조정 손잡이: 크로스페이드 길이 ──
    public const double FADE_OUT_END   = FADE_OUT_START + FADE_OUT_SEC;

    // ── 하늘 ────────────────────────────────────────────────────
    // 위는 거의 검은 남색, 판 아래쪽으로 갈수록 도시 불빛이 번진 보라.
    static readonly Color32 SKY_TOP     = new Color32(  8,   9,  30, 255);
    static readonly Color32 SKY_HORIZON = new Color32( 62,  30,  96, 255);
    const float SKY_HORIZON_Y = -400f;   // 그리드 아랫변

    // ── 별 ──────────────────────────────────────────────────────
    const int   STAR_COUNT  = 45;
    const float STAR_BOTTOM = 150f;      // 이보다 아래는 건물·불빛에 묻혀 안 보인다
    static readonly Color32 STAR_COLOR = new Color32(230, 225, 255, 255);

    // ── 서치라이트 ──────────────────────────────────────────────
    // 수정 전과 같은 구조다. 왼쪽 아래·오른쪽 아래 모서리 바깥에 광원을 두고, 건물들 위 레이어에서
    // 서로 반대 위상으로 부채꼴 왕복한다. 광원을 모서리 바깥으로 밀어 두면 빛이 이미 넓어진 채로
    // 화면에 들어온다. (건물 뒤에 깔았더니 뿌리가 전부 가려져 하늘에 뜬 띠처럼 보였다.)
    const float BEAM_ALPHA     = 0.16f;  // ── 조정 손잡이: 빛줄기 세기 ──
    const float BEAM_PERIOD    = 14f;    // 한 왕복에 걸리는 시간(초)
    const float BEAM_SWING_DEG = 22f;    // 중심 각에서 ±얼마나 흔들리는가
    static readonly Color32 BEAM_COLOR = new Color32(200, 180, 255, 255);
    const float BEAM_CENTER_DEG = 38f;   // 기본 기울기. 왼쪽은 오른쪽 위로, 오른쪽은 왼쪽 위로
    static readonly Vector2 BEAM_ORIGIN_OUT = new Vector2(220f, 160f);   // 모서리에서 바깥으로 민 거리

    // ── 건물 겹 ─────────────────────────────────────────────────
    struct Layer
    {
        public Color32 body, side, window;
        public int     cellW, cellH, winW, winH;
        public float   litRate;     // 불 켜진 창의 비율
        public float   litFade;     // 켜진 창 색을 몸통 쪽으로 누르는 정도 — 멀수록 흐리다
        public bool    flickers;    // 이 겹에서 깜박이는 창을 뽑을지
    }

    static readonly Layer FAR = new Layer {
        body = new Color32(36, 30, 74, 255), side = new Color32(31, 26, 66, 255), window = new Color32(31, 26, 64, 255),
        cellW = 24, cellH = 28, winW = 12, winH = 12, litRate = 0.10f, litFade = 0.55f, flickers = false,
    };
    static readonly Layer MID = new Layer {
        body = new Color32(22, 19, 52, 255), side = new Color32(17, 15, 43, 255), window = new Color32(16, 14, 41, 255),
        cellW = 32, cellH = 36, winW = 18, winH = 18, litRate = 0.12f, litFade = 0.30f, flickers = true,
    };
    static readonly Layer NEAR = new Layer {
        body = new Color32(11, 11, 30, 255), side = new Color32( 7,  7, 22, 255), window = new Color32( 8,  8, 23, 255),
        cellW = 44, cellH = 50, winW = 28, winH = 28, litRate = 0.10f, litFade = 0.10f, flickers = true,
    };

    // 켜진 창 색. 대부분 따뜻한 노랑이고, 일부가 푸른 흰색, 아주 일부가 네온.
    static readonly Color32 LIT_WARM = new Color32(255, 214, 130, 255);
    static readonly Color32 LIT_COOL = new Color32(180, 210, 255, 255);
    static readonly Color32 NEON_MAG = new Color32(255,  80, 200, 255);
    static readonly Color32 NEON_CYN = new Color32( 80, 230, 255, 255);
    const float LIT_WARM_RATE = 0.62f, LIT_COOL_RATE = 0.24f;   // 나머지 14% 가 네온

    // ── 건물 배치 ───────────────────────────────────────────────
    // 지붕 y 는 화면 중심 기준이고 몸통은 화면 아래끝까지 내려간다.
    // 판(-480..480, -400..560) 위로 솟는 건 양 끝 고층 둘과 먼 겹 몇 채뿐이다. 판 위쪽은
    // 점수·하트(위에서 120~350)와 버튼(양쪽 위 모서리)이 차지하므로 그 높이에는 아무것도
    // 세우지 않는다. 피뢰침 끝은 16:9 기준으로 위에서 250 아래에 온다.
    struct Bldg
    {
        public float x, w, top;
        public bool stepped, antenna, neon;
        public Bldg(float x, float w, float top, bool stepped = false, bool antenna = false, bool neon = false)
        { this.x = x; this.w = w; this.top = top; this.stepped = stepped; this.antenna = antenna; this.neon = neon; }
    }

    static readonly Bldg[] FAR_BUILDINGS = {
        new Bldg(-400f, 150f, 420f),
        new Bldg(-250f, 120f, 150f),
        new Bldg(-100f, 160f, 500f, stepped: true),
        new Bldg(  60f, 130f, 250f),
        new Bldg( 220f, 170f, 470f, antenna: true),
        new Bldg( 400f, 150f, 330f, stepped: true),
    };
    static readonly Bldg[] MID_BUILDINGS = {
        new Bldg(-410f, 200f,  500f, stepped: true, antenna: true),
        new Bldg(-180f, 180f, -300f),
        new Bldg(  60f, 200f, -380f, neon: true),
        new Bldg( 300f, 180f, -250f),
        new Bldg( 455f, 170f,  620f, stepped: true),
    };
    static readonly Bldg[] NEAR_BUILDINGS = {
        new Bldg(-470f, 210f,  320f, neon: true),
        new Bldg(-210f, 240f, -600f),
        new Bldg( 110f, 260f, -690f, neon: true),
        new Bldg( 470f, 190f,  -90f),
    };

    const float SIDE_RATIO = 0.22f;   // 그늘진 오른쪽 면의 폭 비율
    const float WIN_MARGIN = 0.12f;   // 왼쪽 벽 두께 (폭 비율)
    const float STEP_W     = 0.6f;    // 계단식 옥상의 윗단 폭 (몸통 비율)
    const float STEP_H     = 90f;
    const float ANTENNA_H  = 110f;

    // ── 안개 ────────────────────────────────────────────────────
    // 먼 겹과 중간 겹 뒤에 한 장씩. 아래로 갈수록 지평선 보라가 짙어진다.
    const float FOG_START_Y = 200f;
    const float FOG_ALPHA   = 0.30f;

    // ── 박자 ────────────────────────────────────────────────────
    // 네온은 박자 시작에 가장 밝고 박자 안에서 가라앉는다. 바닥 밝기를 너무 낮추면
    // 매 박자 꺼졌다 켜지는 것처럼 보여 정신없다.
    const float NEON_FLOOR = 0.6f;    // ── 조정 손잡이: 박자 사이 네온 밝기 ──
    const float NEON_GLOW_ALPHA = 0.35f;
    // 피뢰침 불은 두 박에 한 번, 탑마다 한 박씩 엇갈려 깜박인다.
    static readonly Color32 BEACON = new Color32(240, 70, 80, 255);
    const float BEACON_ON_BEATS = 0.6f;   // 켜져 있는 길이(박자 단위)

    // ── 창문 깜박임 ─────────────────────────────────────────────
    const int   FLICKER_COUNT   = 10;
    const float FLICKER_SPACING = 110f;
    const float FLICKER_ON_MIN  = 1f,   FLICKER_ON_MAX  = 3.5f;
    const float FLICKER_OFF_MIN = 0.3f, FLICKER_OFF_MAX = 1.2f;
    const float FLICKER_FADE    = 28f;   // 켜지고 꺼지는 속도 (1/초)

    // 배경이 가려지는 곳. 판은 960x960 (y +80), 트레이는 화면 아래 360, 위쪽 UI 는 위에서 360.
    static readonly Rect GRID_AREA = new Rect(-480f, -400f, 960f, 960f);
    const float TRAY_H = 360f, TOP_UI_H = 360f;

    const int SEED = 11;

    // ═══════════════════════════════════════════════════════════

    public bool  IsActive => _phase != Phase.Done;
    public float CurrentAlpha => _canvasGroup != null ? _canvasGroup.alpha : 0f;

    enum Phase { Idle, Visible, FadingOut, Done }
    Phase _phase = Phase.Idle;

    BeatTracker   _bt;
    CanvasGroup   _canvasGroup;
    System.Random _rnd;
    Sprite        _softDot, _disc, _beamSprite, _glowSprite;

    class Flicker { public Image img; public bool on; public float alpha, timer; }
    struct Pulse  { public Image img; public float maxAlpha; }        // 박자에 맞춰 밝아지는 것
    struct Beacon { public Image dot, glow; public int beatOffset; }
    struct Star   { public Image img; public float baseA, speed, phase; }

    readonly List<Flicker> _flickers = new List<Flicker>();
    readonly List<Pulse>   _pulses   = new List<Pulse>();
    readonly List<Beacon>  _beacons  = new List<Beacon>();
    readonly List<Star>    _stars    = new List<Star>();
    readonly List<(RectTransform body, Vector2 pos, Vector2 size, Vector2 center, Color32 col)> _spots
        = new List<(RectTransform, Vector2, Vector2, Vector2, Color32)>();
    RectTransform _beamL, _beamR;

    // ── 미리보기 (공유 카드) ─────────────────────────────────────
    // 곡 없이 한 구간을 그대로 되풀이한다. BeatTracker 대신 스스로 시간을 흘리고, 구간 끝에
    // 닿으면 처음으로 돌아간다. 구간 밖으로 나가 스스로 사라지는 일도 없다.
    bool   _preview;
    double _previewT, _previewLo, _previewHi;

    public static CityLightBackground SpawnPreview(Transform parent, double fromSec, double toSec)
    {
        var go = new GameObject("CityLightBackgroundPreview");
        go.transform.SetParent(parent, false);
        var comp = go.AddComponent<CityLightBackground>();
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

        var root = gameObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        // 최종 sibling 순서는 spawn 쪽(InGameUI)에서 _bgImage 위로 지정한다.

        _canvasGroup                = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        _rnd        = new System.Random(SEED);
        _softDot    = MakeDotSprite(32, true);
        _disc       = MakeDotSprite(32, false);
        _beamSprite = MakeBeamSprite(128, 512);
        _glowSprite = MakeGlowBarSprite(64);

        // 형제 순서가 곧 그리는 순서라 먼저 세운 것이 뒤에 깔린다.
        AddFullScreen("Sky", MakeSkySprite(256));
        AddStars();

        AddLayer(FAR, FAR_BUILDINGS);
        AddFullScreen("Fog", MakeFogSprite(256));
        AddLayer(MID, MID_BUILDINGS);
        AddFullScreen("Fog", MakeFogSprite(256));
        AddLayer(NEAR, NEAR_BUILDINGS);

        AddBeams();   // 건물 위 — 아래 모서리에서 솟는 뿌리가 가려지면 안 된다
        PickFlickers();
    }

    // ── 하늘 요소 ───────────────────────────────────────────────

    void AddStars()
    {
        float half = CanvasMetrics.HalfHeight;
        for (int i = 0; i < STAR_COUNT; i++)
        {
            float size = Range(2f, 5f);
            var rt = NewRect("Star", transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Range(-540f, 540f), Range(STAR_BOTTOM, half));
            rt.sizeDelta = new Vector2(size, size);
            var img = AddSolid(rt.gameObject, STAR_COLOR);
            img.sprite = _softDot;
            _stars.Add(new Star { img = img, baseA = Range(0.3f, 0.8f), speed = Range(0.6f, 1.6f), phase = Range(0f, 6.28f) });
        }
    }

    void AddBeams()
    {
        float halfW = CanvasMetrics.REF_W * 0.5f, half = CanvasMetrics.HalfHeight;
        _beamL = AddBeam(new Vector2(-halfW - BEAM_ORIGIN_OUT.x, -half - BEAM_ORIGIN_OUT.y));
        _beamR = AddBeam(new Vector2( halfW + BEAM_ORIGIN_OUT.x, -half - BEAM_ORIGIN_OUT.y));
    }

    RectTransform AddBeam(Vector2 origin)
    {
        var rt = NewRect("Beam", transform);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0f);   // 광원이 회전 중심
        rt.anchoredPosition = origin;
        rt.sizeDelta        = new Vector2(900f, 3400f);   // 긴 화면에서도 모서리 바깥에서 반대편 위끝까지 닿게
        var img = AddSolid(rt.gameObject, BEAM_COLOR);
        img.sprite = _beamSprite;
        SetAlpha(img, BEAM_ALPHA);
        return rt;
    }

    // ── 건물 ────────────────────────────────────────────────────

    void AddLayer(Layer L, Bldg[] buildings)
    {
        var tile = MakeWindowTile(L);
        foreach (var b in buildings) AddBuilding(L, tile, b);
    }

    void AddBuilding(Layer L, Texture2D tile, Bldg b)
    {
        float half = CanvasMetrics.HalfHeight;
        float h    = b.top + half;
        if (h <= 1f) return;

        // 몸통. 피벗을 지붕 가운데에 둬서 자식들을 지붕 기준으로 놓는다.
        var body = NewRect("Building", transform);
        body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
        body.pivot            = new Vector2(0.5f, 1f);
        body.anchoredPosition = new Vector2(b.x, b.top);
        body.sizeDelta        = new Vector2(b.w, h);
        AddSolid(body.gameObject, L.body);
        AddSide(body, L.side);

        float roof = 0f;   // 몸통 피벗 기준 옥상 높이
        if (b.stepped)
        {
            var step = NewRect("Step", body);
            step.anchorMin = step.anchorMax = new Vector2(0.5f, 1f);
            step.pivot            = new Vector2(0.5f, 0f);
            step.anchoredPosition = Vector2.zero;
            step.sizeDelta        = new Vector2(b.w * STEP_W, STEP_H);
            AddSolid(step.gameObject, L.body);
            AddSide(step, L.side);
            roof = STEP_H;
        }

        AddWindows(L, tile, body, b, h);

        if (b.neon)    AddNeon(body, b.w, b.x < 0f ? NEON_MAG : NEON_CYN);
        if (b.antenna) AddAntenna(body, L, roof);
    }

    static void AddSide(RectTransform parent, Color32 col)
    {
        // 그늘진 오른쪽 면. 이 한 줄이 있어야 납작한 판이 아니라 기둥으로 읽힌다.
        var side = NewRect("Side", parent);
        side.anchorMin = new Vector2(1f - SIDE_RATIO, 0f);
        side.anchorMax = Vector2.one;
        side.offsetMin = side.offsetMax = Vector2.zero;
        AddSolid(side.gameObject, col);
    }

    void AddWindows(Layer L, Texture2D tile, RectTransform body, Bldg b, float h)
    {
        // 칸 수를 정수로 맞춰 깔아야 무늬가 위아래 어디서도 잘리지 않는다.
        int   cols   = Mathf.FloorToInt((b.w * (1f - SIDE_RATIO) - b.w * WIN_MARGIN) / L.cellW);
        float winTop = L.cellH * 1.2f;
        int   rows   = Mathf.FloorToInt((h - winTop) / L.cellH);
        float left   = -b.w * 0.5f + b.w * WIN_MARGIN;
        if (cols <= 0 || rows <= 0) return;

        var win = NewRect("Windows", body);
        win.anchorMin = win.anchorMax = new Vector2(0.5f, 1f);
        win.pivot            = new Vector2(0f, 1f);
        win.anchoredPosition = new Vector2(left, -winTop);
        win.sizeDelta        = new Vector2(cols * L.cellW, rows * L.cellH);
        var raw = win.gameObject.AddComponent<RawImage>();
        raw.texture       = tile;
        raw.uvRect        = new Rect(0f, 0f, cols, rows);
        raw.raycastTarget = false;

        var size = new Vector2(L.winW, L.winH);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                bool   lit  = _rnd.NextDouble() < L.litRate;
                double kind = _rnd.NextDouble();
                bool   neon = kind >= LIT_WARM_RATE + LIT_COOL_RATE;
                Color32 col = kind < LIT_WARM_RATE ? LIT_WARM
                            : !neon               ? LIT_COOL
                            : (_rnd.NextDouble() < 0.5 ? NEON_MAG : NEON_CYN);
                col = Color32.Lerp(col, L.body, L.litFade);

                var pos = new Vector2(left + c * L.cellW + (L.cellW - L.winW) * 0.5f,
                                      -winTop - r * L.cellH - (L.cellH - L.winH) * 0.5f);
                if (lit)
                {
                    var img = AddRectAt(body, pos, size, col);
                    // 네온색 창은 박자에 맞춰 같이 숨 쉰다 — 건물 속에서도 파티가 열린 것처럼.
                    if (neon) _pulses.Add(new Pulse { img = img, maxAlpha = 1f });
                    continue;
                }

                // 꺼진 창 중 화면에 드러나는 것은 깜박임 후보로 모아 둔다.
                // 깜박이는 창은 따뜻한 노랑으로만 켠다 — 네온이 꺼졌다 켜지면 고장 난 간판 같다.
                var center = new Vector2(b.x + pos.x + L.winW * 0.5f, b.top + pos.y - L.winH * 0.5f);
                if (L.flickers && IsExposed(center, size))
                    _spots.Add((body, pos, size, center, Color32.Lerp(LIT_WARM, L.body, L.litFade)));
            }
    }

    /// <summary>옥상 가장자리의 네온 띠. 가는 선 + 위아래로 번지는 빛 두 겹이고, 박자에 맞춰 밝아진다.</summary>
    void AddNeon(RectTransform body, float w, Color32 col)
    {
        var glow = NewRect("NeonGlow", body);
        glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 1f);
        glow.pivot            = new Vector2(0.5f, 0.5f);
        glow.anchoredPosition = Vector2.zero;
        glow.sizeDelta        = new Vector2(w, 36f);
        var gImg = AddSolid(glow.gameObject, col);
        gImg.sprite = _glowSprite;
        _pulses.Add(new Pulse { img = gImg, maxAlpha = NEON_GLOW_ALPHA });

        var line = NewRect("Neon", body);
        line.anchorMin = line.anchorMax = new Vector2(0.5f, 1f);
        line.pivot            = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta        = new Vector2(w, 6f);
        _pulses.Add(new Pulse { img = AddSolid(line.gameObject, col), maxAlpha = 1f });
    }

    void AddAntenna(RectTransform body, Layer L, float roof)
    {
        var rod = NewRect("Antenna", body);
        rod.anchorMin = rod.anchorMax = new Vector2(0.5f, 1f);
        rod.pivot            = new Vector2(0.5f, 0f);
        rod.anchoredPosition = new Vector2(0f, roof);
        rod.sizeDelta        = new Vector2(6f, ANTENNA_H);
        AddSolid(rod.gameObject, L.body);

        var tip  = new Vector2(0f, roof + ANTENNA_H);
        var glow = AddDot(body, tip, 44f, _softDot, BEACON);
        var dot  = AddDot(body, tip, 12f, _disc,    BEACON);
        _beacons.Add(new Beacon { dot = dot, glow = glow, beatOffset = _beacons.Count });
    }

    Image AddDot(RectTransform parent, Vector2 pos, float size, Sprite spr, Color32 col)
    {
        var rt = NewRect("Beacon", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(size, size);
        var img = AddSolid(rt.gameObject, col);
        img.sprite = spr;
        return img;
    }

    static Image AddRectAt(RectTransform parent, Vector2 topLeft, Vector2 size, Color32 col)
    {
        var rt = NewRect("Lit", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta        = size;
        return AddSolid(rt.gameObject, col);
    }

    static bool IsExposed(Vector2 c, Vector2 size)
    {
        float halfW = CanvasMetrics.REF_W * 0.5f, half = CanvasMetrics.HalfHeight;
        if (c.x - size.x * 0.5f < -halfW || c.x + size.x * 0.5f > halfW) return false;
        if (c.y + size.y * 0.5f > half - TOP_UI_H) return false;
        if (c.y - size.y * 0.5f < -half + TRAY_H) return false;
        return !GRID_AREA.Contains(c);
    }

    /// <summary>모아 둔 후보 중 서로 떨어진 것들로 FLICKER_COUNT 개를 깜박이는 창으로 만든다.</summary>
    void PickFlickers()
    {
        for (int i = _spots.Count - 1; i > 0; i--)
        {
            int j = _rnd.Next(i + 1);
            var t = _spots[i]; _spots[i] = _spots[j]; _spots[j] = t;
        }

        var picked = new List<Vector2>();
        foreach (var s in _spots)
        {
            if (picked.Count >= FLICKER_COUNT) break;
            bool near = false;
            foreach (var p in picked)
                if ((p - s.center).sqrMagnitude < FLICKER_SPACING * FLICKER_SPACING) { near = true; break; }
            if (near) continue;
            picked.Add(s.center);

            var img = AddRectAt(s.body, s.pos, s.size, s.col);
            bool on = _rnd.NextDouble() < 0.5;
            var f = new Flicker { img = img, on = on, alpha = on ? 1f : 0f, timer = Range(0.2f, 1.2f) };
            SetAlpha(img, f.alpha);
            _flickers.Add(f);
        }
        _spots.Clear();
    }

    // ── 움직임 ──────────────────────────────────────────────────

    void Update()
    {
        if (_bt == null && !_preview) return;
        double t = NowSec();

        // 등장 구간 밖으로 되감긴 경우(에디터 시크 등): 화면을 덮은 채 얼어붙지 않도록 스스로 정리한다.
        // 파괴되면 InGameUI가 _bgImage/그리드 배경을 복원하고, 다시 앞으로 시크하면 새로 스폰된다.
        if (!_preview && (t < APPEAR_START || t >= FADE_OUT_END))
        {
            _phase = Phase.Done;
            Destroy(gameObject);
            return;
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

        float dt = Time.deltaTime;
        float tm = Time.time;

        // 박자: 박 시작에 1, 박 끝으로 갈수록 NEON_FLOOR 로 가라앉는다.
        float beat  = 1f - NowBeatPhase(t);
        float pulse = Mathf.Lerp(NEON_FLOOR, 1f, beat * beat);
        foreach (var p in _pulses) SetAlpha(p.img, p.maxAlpha * pulse);

        // 피뢰침: 두 박에 한 번, 탑마다 한 박씩 엇갈려.
        double beats = t * BeatTracker.BPM / 60.0;
        foreach (var b in _beacons)
        {
            float k = (float)((beats + b.beatOffset) % 2.0);              // 0~2 박
            float a = k < BEACON_ON_BEATS ? 1f - k / BEACON_ON_BEATS * 0.4f : 0f;
            SetAlpha(b.dot,  Mathf.Lerp(0.25f, 1f, a));
            SetAlpha(b.glow, 0.4f * a);
        }

        // 서치라이트
        float swing = BEAM_SWING_DEG * Mathf.Sin((float)((t - APPEAR_START) * (Mathf.PI * 2.0 / BEAM_PERIOD)));
        if (_beamL != null) _beamL.localRotation = Quaternion.Euler(0f, 0f, -BEAM_CENTER_DEG + swing);
        if (_beamR != null) _beamR.localRotation = Quaternion.Euler(0f, 0f,  BEAM_CENTER_DEG - swing);   // 반대로 흔들어 두 줄기가 엇갈린다

        // 별
        foreach (var s in _stars)
            SetAlpha(s.img, s.baseA * (0.55f + 0.45f * Mathf.Sin(tm * s.speed + s.phase)));

        // 깜박이는 창
        foreach (var f in _flickers)
        {
            f.timer -= dt;
            if (f.timer <= 0f)
            {
                f.on    = !f.on;
                f.timer = f.on ? Range(FLICKER_ON_MIN, FLICKER_ON_MAX) : Range(FLICKER_OFF_MIN, FLICKER_OFF_MAX);
            }
            f.alpha = Mathf.MoveTowards(f.alpha, f.on ? 1f : 0f, FLICKER_FADE * dt);
            SetAlpha(f.img, f.alpha);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 도우미
    // ═══════════════════════════════════════════════════════════

    float Range(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

    static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    /// <summary>스프라이트 없는 Image 는 단색 사각형이 된다. 건물은 거의 전부 이걸로 그린다.</summary>
    static Image AddSolid(GameObject go, Color32 col)
    {
        var img = go.AddComponent<Image>();
        img.color         = col;
        img.raycastTarget = false;
        return img;
    }

    void AddFullScreen(string name, Sprite spr)
    {
        var rt = NewRect(name, transform);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = AddSolid(rt.gameObject, Color.white);   // 색과 세기는 스프라이트가 들고 있다
        img.sprite = spr;
    }

    // ── 굽기 ────────────────────────────────────────────────────
    // Texture2D 는 y=0 이 아래쪽이다.

    static Texture2D MakeWindowTile(Layer L)
    {
        int w = L.cellW, h = L.cellH;
        var px = new Color32[w * h];
        int x0 = (w - L.winW) / 2, y0 = (h - L.winH) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inWin = x >= x0 && x < x0 + L.winW && y >= y0 && y < y0 + L.winH;
                px[y * w + x] = inWin ? L.window : new Color32(0, 0, 0, 0);
            }
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Repeat;   // uvRect 로 칸 수만큼 되풀이한다
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Sprite MakeSkySprite(int h)
    {
        float half = CanvasMetrics.HalfHeight;
        return MakeVertical(h, up =>
        {
            float cy = -half + CanvasMetrics.Height * up;
            float t  = Smooth(Mathf.Clamp01((half - cy) / (half - SKY_HORIZON_Y)));
            return Color32.Lerp(SKY_TOP, SKY_HORIZON, t);
        });
    }

    static Sprite MakeFogSprite(int h)
    {
        float half = CanvasMetrics.HalfHeight;
        return MakeVertical(h, up =>
        {
            float cy = -half + CanvasMetrics.Height * up;
            float t  = Mathf.Clamp01((FOG_START_Y - cy) / (FOG_START_Y + half));
            var c = SKY_HORIZON;
            c.a = (byte)(FOG_ALPHA * t * 255f);
            return c;
        });
    }

    /// <summary>가운데가 진하고 위아래로 옅어지는 띠. 네온 번짐에 쓴다.</summary>
    static Sprite MakeGlowBarSprite(int h)
    {
        return MakeVertical(h, up =>
        {
            float a = Smooth(1f - Mathf.Abs(up - 0.5f) * 2f);
            return new Color32(255, 255, 255, (byte)(a * 255f));
        });
    }

    /// <summary>
    /// 서치라이트 빛줄기. 아래(광원)가 좁고 위로 퍼지는 부채꼴이고, 가장자리와 끝이 부드럽게 옅어진다.
    /// 예전 것은 경계가 칼같이 잘린 단색이라 투명도를 0.01까지 내려야 했고, 그러면 안 보였다.
    /// </summary>
    static Sprite MakeBeamSprite(int w, int h)
    {
        const float HALF_SPREAD = 0.2f;    // 끝에서의 반폭 (스프라이트 폭 비율)
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v    = y / (h - 1f);                       // 0 = 광원, 1 = 끝
            float edge = Mathf.Max(0.01f, v * HALF_SPREAD + 0.01f);
            float len  = Smooth(1f - v) * 0.6f + 0.4f;       // 위로 갈수록 옅어진다
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / w - 0.5f);
                float a  = Smooth(Mathf.Clamp01(1f - dx / edge)) * len;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f));
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    /// <summary>세로로만 변하는 1px 폭 그라데이션. up 은 0 = 아래, 1 = 위.</summary>
    static Sprite MakeVertical(int h, System.Func<float, Color32> colorAt)
    {
        var tex = new Texture2D(1, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[h];
        for (int y = 0; y < h; y++) px[y] = colorAt(y / (h - 1f));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>흰 원. soft 면 가운데서 바깥으로 옅어지고, 아니면 가장자리만 부드럽다.</summary>
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
}
