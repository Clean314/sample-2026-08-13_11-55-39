using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이스 모드 배경. 눈 내리는 새벽 도시 — ice.png 를 참고해 사각형 위주로 다시 그린다.
///
/// 그리는 쪽을 고른 이유:
///   · 해상도에 매이지 않는다. 건물은 화면 아래끝까지 내려가고, 화면이 길어지면 하늘만 늘어난다.
///   · 움직일 수 있다. 눈이 내리고, 창문 몇 개가 가끔 깜박이고, 피뢰침 끝 붉은 점이 점멸한다.
///   · 용량이 거의 0이다. 창문 무늬 몇 장과 그라데이션 몇 줄만 시작할 때 굽는다.
///
/// 깊이는 색으로만 낸다. 건물을 먼·중간·가까운 세 겹으로 나누고, 먼 겹일수록 하늘색에
/// 가깝게 흐려 둔다. 겹 사이에는 아래로 짙어지는 안개를 한 장씩 깔아 거리감을 더한다.
///
/// 아이스는 곡 타임라인이 없다. 그래서 모든 움직임이 경과 시간 기반의 무한 반복이고,
/// 어디서 들어와도 어색한 지점이 없다.
/// </summary>
public class IceBackground : MonoBehaviour
{
    // ── 하늘 ────────────────────────────────────────────────────
    // 위는 짙은 남색, 아래로 갈수록 눈보라에 번진 푸른 회색.
    // 채도를 일부러 낮게 잡았다 — 삭막한 새벽이라 색이 살아 있으면 안 된다.
    public static readonly Color32 SKY  = new Color32( 32,  38,  73, 255);  // 화면 위
    static readonly Color32 SKY_HORIZON = new Color32( 81,  87, 128, 255);  // 판 아래쪽 부근
    // 하늘이 SKY_HORIZON 에 다다르는 높이. 그리드 아랫변(-400)에 맞췄다.
    const float SKY_HORIZON_Y = -400f;

    // ── 건물 겹 ─────────────────────────────────────────────────
    // 먼 겹일수록 하늘에 가깝게 밝고 대비가 낮다. 창문도 작고 촘촘해진다.
    struct Layer
    {
        public Color32 body, side, window, snow;
        public int     cellW, cellH, winW, winH;  // 창문 한 칸과 그 안의 창
        public float   litRate;                   // 불 켜진 창의 비율
        public bool    flickers;                  // 이 겹에서 깜박이는 창을 뽑을지
        public float   litDim;                    // 켜진 창 색을 몸통 쪽으로 누르는 정도
    }

    static readonly Layer FAR = new Layer {
        body = new Color32( 68,  75, 117, 255), side = new Color32( 62,  68, 110, 255),
        window = new Color32( 60,  66, 108, 255), snow = new Color32(111, 118, 159, 255),
        cellW = 26, cellH = 30, winW = 14, winH = 14,
        litRate = 0.03f, flickers = false, litDim = 0.45f,
    };
    static readonly Layer MID = new Layer {
        body = new Color32( 53,  60, 101, 255), side = new Color32( 45,  52,  92, 255),
        window = new Color32( 42,  48,  87, 255), snow = new Color32(140, 147, 185, 255),
        cellW = 34, cellH = 38, winW = 20, winH = 20,
        litRate = 0.035f, flickers = true, litDim = 0.2f,
    };
    static readonly Layer NEAR = new Layer {
        body = new Color32( 39,  44,  81, 255), side = new Color32( 30,  36,  71, 255),
        window = new Color32( 27,  33,  66, 255), snow = new Color32(172, 179, 212, 255),
        cellW = 46, cellH = 52, winW = 30, winH = 30,
        litRate = 0.035f, flickers = true, litDim = 0f,
    };

    // 켜진 창은 대부분 따뜻한 노랑, 일부는 형광등 같은 푸른 흰색.
    static readonly Color32 LIT_WARM = new Color32(236, 214, 150, 255);
    static readonly Color32 LIT_COOL = new Color32(196, 206, 228, 255);
    const float LIT_COOL_RATE = 0.3f;
    // 켜진 창을 몸통 색 쪽으로 한 번 더 누른다. 겹마다 다른 litDim 위에 똑같이 얹힌다.
    // 원색 그대로면 눈보라 속 새벽치고 너무 또렷하다.
    const float LIT_FADE = 0.35f;   // ── 조정 손잡이: 0 이면 원색, 올릴수록 흐려진다 ──

    // ── 건물 배치 ───────────────────────────────────────────────
    // (중심 x, 폭, 지붕 y, 피뢰침 길이). 지붕 y 는 화면 중심 기준이고 몸통은 화면 아래끝까지 내려간다.
    //
    // 보이는 곳은 사실상 두 군데다. 판(-480..480, -400..560) 위의 하늘, 그리고 판과 조각
    // 트레이 사이. 판 위로 솟는 건 왼쪽 끝 고층 하나와 오른쪽 피뢰침 탑 하나뿐이다 —
    // 하늘을 비워 둬야 삭막하고, 점수 문구(위에서 75~205)도 그래야 읽힌다.
    static readonly Vector4[] FAR_BUILDINGS = {
        new Vector4(-270f, 160f, -120f,  0f),
        new Vector4( -70f, 120f,   60f, 90f),
        new Vector4( 120f, 150f, -230f,  0f),
        new Vector4( 310f, 170f,  140f,  0f),
    };
    static readonly Vector4[] MID_BUILDINGS = {
        new Vector4(-330f, 190f, -400f,   0f),
        new Vector4(  60f, 180f, -330f,   0f),
        new Vector4( 340f, 190f,  600f,  90f),   // 붉은 점이 도움말 버튼(위에서 55~155) 밑으로 오게 낮췄다
    };
    static readonly Vector4[] NEAR_BUILDINGS = {
        new Vector4(-470f, 220f,  820f, 0f),
        new Vector4( 475f, 170f,  300f, 0f),
        new Vector4(-130f, 240f, -640f, 0f),
        new Vector4( 200f, 220f, -710f, 0f),
    };

    const float SIDE_RATIO   = 0.22f;  // 그늘진 옆면이 차지하는 폭 비율 (오른쪽)
    const float WIN_MARGIN   = 0.12f;  // 왼쪽 벽 두께 (폭 비율)
    const float SNOW_CAP_H   = 10f;    // 지붕에 쌓인 눈 두께
    const float SNOW_CAP_OUT = 4f;     // 지붕 밖으로 삐져나온 폭

    // ── 안개 ────────────────────────────────────────────────────
    // 먼 겹과 중간 겹 뒤에 한 장씩. 위는 투명, 화면 아래로 갈수록 SKY_HORIZON 이 짙어진다.
    const float FOG_START_Y = 200f;   // ── 조정 손잡이: 안개가 시작되는 높이 ──
    const float FOG_ALPHA   = 0.27f;  // ── 조정 손잡이: 화면 맨 아래 안개 진하기 ──

    // ── 창문 깜박임 ─────────────────────────────────────────────
    // 비율로 뽑으면 대부분 판이나 조각 트레이 뒤에 떨어져 하나도 안 보인다. 그래서 실제로
    // 드러나는 칸만 후보로 모은 뒤 정해진 개수를 고른다.
    const int   FLICKER_COUNT   = 6;     // ── 조정 손잡이: 깜박이는 창 수 ──
    const float FLICKER_SPACING = 120f;  // 서로 이만큼은 떨어뜨린다 — 몰리면 한 건물만 요란하다
    const float FLICKER_ON_MIN  = 3f,   FLICKER_ON_MAX  = 9f;   // 켜져 있는 시간
    const float FLICKER_OFF_MIN = 0.8f, FLICKER_OFF_MAX = 3f;   // 꺼져 있는 시간
    const float STUTTER_CHANCE  = 0.4f;  // 켜질 때 형광등처럼 몇 번 떨다 켜질 확률

    // 배경이 가려지는 곳. 판은 _gridRt(960x960, y +80), 트레이는 화면 아래 360.
    static readonly Rect GRID_AREA = new Rect(-480f, -400f, 960f, 960f);
    const float TRAY_H = 360f;
    const float FADE_SPEED      = 22f;   // 켜지고 꺼지는 속도 (1/초)

    // ── 피뢰침 붉은 점 ──────────────────────────────────────────
    static readonly Color32 BEACON = new Color32(235, 70, 70, 255);
    const float BEACON_PERIOD = 2.6f;   // ── 조정 손잡이: 한 번 깜박이는 주기 ──
    const float BEACON_ON     = 1.0f;   // 그중 켜져 있는 시간
    const float BEACON_SIZE   = 12f;
    const float BEACON_GLOW   = 44f;

    // ── 눈 ──────────────────────────────────────────────────────
    const int   SNOW_COUNT     = 80;     // ── 조정 손잡이: 눈송이 수 ──
    const float SNOW_SPEED_MIN = 40f,  SNOW_SPEED_MAX = 110f;   // px/초
    const float SNOW_SIZE_MIN  = 4f,   SNOW_SIZE_MAX  = 11f;
    static readonly Color32 SNOW_COLOR = new Color32(214, 218, 238, 255);

    // ── 아래쪽 어둠막 ───────────────────────────────────────────
    // 조각 트레이가 화면 아래 28% 를 쓰는데, 거기 건물 창문과 눈이 그대로 보이면 조각 후보가
    // 묻힌다. 아래로 갈수록 어두운 남색으로 눌러 둔다. 마지막 자식이라 풍경 위·그리드 아래다.
    //
    // 램프는 트레이 위(0.276)보다 넉넉히 높은 데서 시작해야 한다. 트레이 경계에서 시작하면
    // 정작 조각이 놓인 높이(0.182)까지 짙어질 거리가 모자라 효과가 거의 없다.
    const float DIM_BOTTOM_ALPHA = 0.45f;  // ── 조정 손잡이: 화면 맨 아래를 누르는 정도 ──
    const float DIM_RAMP_H       = 0.45f;  // ── 조정 손잡이: 짙어지기 시작하는 높이(화면 비율) ──
    static readonly Color32 DIM_COL = new Color32(8, 12, 32, 255);

    // 배치가 매번 같아야 한다. 판마다 불 켜진 창이 바뀌면 다른 동네처럼 보인다.
    const int SEED = 7;

    // ═══════════════════════════════════════════════════════════

    class Flicker
    {
        public Image img;
        public bool  on;
        public float alpha;
        public float timer;
        public int   stutter;   // 남은 떨림 횟수
    }

    struct FlickerSpot
    {
        public RectTransform body;
        public Vector2 pos, size, center;   // pos/size 는 몸통 기준, center 는 캔버스 기준
        public Color32 col;
    }

    struct Beacon
    {
        public Image dot, glow;
        public float phase;
    }

    struct Flake
    {
        public RectTransform rt;
        public Image img;
        public float x, y, speed, swayAmp, swaySec, swayPhase;
    }

    readonly List<Flicker> _flickers = new List<Flicker>();
    readonly List<Beacon>  _beacons  = new List<Beacon>();
    readonly List<FlickerSpot> _spots = new List<FlickerSpot>();
    Flake[] _flakes;
    System.Random _rnd;
    Sprite _softDot, _disc;

    /// <summary>배경 이미지 바로 위에 도시 풍경을 세운다.</summary>
    public static IceBackground Create(Transform canvas, int siblingIndex)
    {
        var go = new GameObject("IceBackground");
        go.transform.SetParent(canvas, false);
        go.transform.SetSiblingIndex(siblingIndex);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<IceBackground>();
        bg.Build();
        return bg;
    }

    void Build()
    {
        _rnd     = new System.Random(SEED);
        _softDot = MakeDotSprite(32, true);
        _disc    = MakeDotSprite(32, false);

        // 형제 순서가 곧 그리는 순서라 먼저 세운 것이 뒤에 깔린다.
        AddFullScreen("Sky", MakeSkySprite(256));

        AddLayer(FAR, FAR_BUILDINGS);
        AddFullScreen("Fog", MakeFogSprite(256));
        AddLayer(MID, MID_BUILDINGS);
        AddFullScreen("Fog", MakeFogSprite(256));
        AddLayer(NEAR, NEAR_BUILDINGS);
        PickFlickers();

        AddSnow();

        // 맨 마지막에 깔아야 풍경 전체를 덮는다.
        AddFullScreen("Dim", MakeDimSprite(256));
    }

    // ── 건물 ────────────────────────────────────────────────────

    void AddLayer(Layer layer, Vector4[] buildings)
    {
        // 창문 무늬는 겹마다 한 장. 건물마다 같은 걸 반복해 깐다.
        var tile = MakeWindowTile(layer);
        foreach (var b in buildings)
            AddBuilding(layer, tile, b.x, b.y, b.z, b.w);
    }

    void AddBuilding(Layer L, Texture2D tile, float cx, float w, float top, float antenna)
    {
        float half = CanvasMetrics.HalfHeight;
        float h    = top + half;          // 지붕에서 화면 아래끝까지
        if (h <= 1f) return;

        // 몸통. 피벗을 지붕 가운데에 둬서 자식들을 지붕 기준으로 놓는다.
        var body = NewRect("Building", transform);
        body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
        body.pivot            = new Vector2(0.5f, 1f);
        body.anchoredPosition = new Vector2(cx, top);
        body.sizeDelta        = new Vector2(w, h);
        AddSolid(body.gameObject, L.body);

        // 그늘진 오른쪽 면. 이 한 줄이 있어야 납작한 판이 아니라 기둥으로 읽힌다.
        var side = NewRect("Side", body);
        side.anchorMin = new Vector2(1f - SIDE_RATIO, 0f);
        side.anchorMax = Vector2.one;
        side.offsetMin = side.offsetMax = Vector2.zero;
        AddSolid(side.gameObject, L.side);

        // 창문. 칸 수를 정수로 맞춰 깔아야 무늬가 위아래 어디서도 잘리지 않는다.
        int   cols  = Mathf.FloorToInt((w * (1f - SIDE_RATIO) - w * WIN_MARGIN) / L.cellW);
        float winTop = L.cellH * 1.2f;                       // 지붕에서 첫 줄까지
        int   rows  = Mathf.FloorToInt((h - winTop) / L.cellH);
        float left  = -w * 0.5f + w * WIN_MARGIN;            // 몸통 피벗 기준
        if (cols > 0 && rows > 0)
        {
            var win = NewRect("Windows", body);
            win.anchorMin = win.anchorMax = new Vector2(0.5f, 1f);
            win.pivot            = new Vector2(0f, 1f);
            win.anchoredPosition = new Vector2(left, -winTop);
            win.sizeDelta        = new Vector2(cols * L.cellW, rows * L.cellH);
            var raw = win.gameObject.AddComponent<RawImage>();
            raw.texture       = tile;
            raw.uvRect        = new Rect(0f, 0f, cols, rows);
            raw.raycastTarget = false;

            // 켜진 창은 무늬 위에 칸 하나 크기로 덧댄다.
            // 꺼진 창 중 화면에 드러나는 것은 깜박임 후보로 모아 둔다 (PickFlickers).
            var size = new Vector2(L.winW, L.winH);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    bool lit = _rnd.NextDouble() < L.litRate;
                    Color32 col = _rnd.NextDouble() < LIT_COOL_RATE ? LIT_COOL : LIT_WARM;
                    col = Color32.Lerp(col, L.body, 1f - (1f - L.litDim) * (1f - LIT_FADE));

                    var pos = new Vector2(left + c * L.cellW + (L.cellW - L.winW) * 0.5f,
                                          -winTop - r * L.cellH - (L.cellH - L.winH) * 0.5f);
                    if (lit) { AddLitWindow(body, pos, size, col); continue; }

                    var center = new Vector2(cx + pos.x + L.winW * 0.5f, top + pos.y - L.winH * 0.5f);
                    if (L.flickers && IsExposed(center, size))
                        _spots.Add(new FlickerSpot { body = body, pos = pos, size = size, center = center, col = col });
                }
        }

        // 지붕에 쌓인 눈. 몸통보다 살짝 넓어야 처마에 얹힌 것처럼 보인다.
        var cap = NewRect("Snow", body);
        cap.anchorMin = cap.anchorMax = new Vector2(0.5f, 1f);
        cap.pivot            = new Vector2(0.5f, 0f);
        cap.anchoredPosition = new Vector2(0f, -2f);
        cap.sizeDelta        = new Vector2(w + SNOW_CAP_OUT * 2f, SNOW_CAP_H);
        AddSolid(cap.gameObject, L.snow);

        if (antenna > 0f) AddAntenna(body, L, antenna);
    }

    /// <summary>창이 화면 안에 온전히 있고 판·트레이에 가리지 않는지.</summary>
    static bool IsExposed(Vector2 c, Vector2 size)
    {
        float halfW = CanvasMetrics.REF_W * 0.5f;
        if (c.x - size.x * 0.5f < -halfW || c.x + size.x * 0.5f > halfW) return false;
        if (c.y + size.y * 0.5f > CanvasMetrics.HalfHeight) return false;
        if (c.y - size.y * 0.5f < -CanvasMetrics.HalfHeight + TRAY_H) return false;
        return !GRID_AREA.Contains(c);
    }

    /// <summary>모아 둔 후보 중 서로 떨어진 것들로 FLICKER_COUNT 개를 깜박이는 창으로 만든다.</summary>
    void PickFlickers()
    {
        // 섞어서 앞에서부터 고르되, 이미 고른 창과 너무 가까우면 건너뛴다.
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

            var img = AddLitWindow(s.body, s.pos, s.size, s.col);
            bool on = _rnd.NextDouble() < 0.5;
            // 첫 전환은 일찍 오게 한다 — 들어오자마자 몇 초씩 아무 일도 없으면 정지 화면 같다.
            var f = new Flicker { img = img, on = on, alpha = on ? 1f : 0f, timer = Range(0.3f, 3f) };
            SetAlpha(img, f.alpha);
            _flickers.Add(f);
        }
        _spots.Clear();
    }

    void AddAntenna(RectTransform body, Layer L, float len)
    {
        var rod = NewRect("Antenna", body);
        rod.anchorMin = rod.anchorMax = new Vector2(0.5f, 1f);
        rod.pivot            = new Vector2(0.5f, 0f);
        rod.anchoredPosition = Vector2.zero;
        rod.sizeDelta        = new Vector2(6f, len);
        AddSolid(rod.gameObject, L.body);

        // 번짐을 먼저 깔고 점을 위에 얹는다.
        var glow = AddDot(body, new Vector2(0f, len), BEACON_GLOW, _softDot, BEACON);
        var dot  = AddDot(body, new Vector2(0f, len), BEACON_SIZE, _disc,    BEACON);
        // 탑마다 박자를 어긋나게 둬야 두 점이 한꺼번에 깜박이지 않는다.
        _beacons.Add(new Beacon { dot = dot, glow = glow, phase = Range(0f, BEACON_PERIOD) });
    }

    Image AddLitWindow(RectTransform parent, Vector2 topLeft, Vector2 size, Color32 col)
    {
        var rt = NewRect("Lit", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta        = size;
        return AddSolid(rt.gameObject, col);
    }

    Image AddDot(RectTransform parent, Vector2 pos, float size, Sprite spr, Color32 col)
    {
        var rt = NewRect("Beacon", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(size, size);
        var img = AddSolid(rt.gameObject, col);
        img.sprite = spr;
        return img;
    }

    // ── 눈 ──────────────────────────────────────────────────────

    void AddSnow()
    {
        float half = CanvasMetrics.HalfHeight;
        _flakes = new Flake[SNOW_COUNT];
        for (int i = 0; i < SNOW_COUNT; i++)
        {
            // 작은 송이일수록 멀리 있는 것 — 느리고 흐리다.
            float t    = Range(0f, 1f);
            float size = Mathf.Lerp(SNOW_SIZE_MIN, SNOW_SIZE_MAX, t * t);
            var rt  = NewRect("Flake", transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = AddSolid(rt.gameObject, SNOW_COLOR);
            img.sprite = _softDot;
            SetAlpha(img, Mathf.Lerp(0.35f, 0.85f, t));

            _flakes[i] = new Flake {
                rt = rt, img = img,
                x = Range(-CanvasMetrics.REF_W * 0.5f, CanvasMetrics.REF_W * 0.5f),
                y = Range(-half, half),
                speed     = Mathf.Lerp(SNOW_SPEED_MIN, SNOW_SPEED_MAX, t),
                swayAmp   = Range(8f, 28f),
                swaySec   = Range(3f, 7f),
                swayPhase = Range(0f, 1f),
            };
        }
    }

    // ── 움직임 ──────────────────────────────────────────────────

    void Update()
    {
        float dt = Time.deltaTime;
        float tm = Time.time;

        // 창문
        for (int i = 0; i < _flickers.Count; i++)
        {
            var f = _flickers[i];
            f.timer -= dt;
            if (f.timer <= 0f)
            {
                f.on = !f.on;
                if (f.stutter > 0)
                {
                    // 떨리는 중 — 짧게 켜졌다 꺼졌다를 되풀이하다 마지막에 켜진 채로 남는다.
                    f.stutter--;
                    f.timer = f.stutter == 0 ? Range(FLICKER_ON_MIN, FLICKER_ON_MAX) : Range(0.06f, 0.16f);
                    if (f.stutter == 0) f.on = true;
                }
                else if (f.on && Range(0f, 1f) < STUTTER_CHANCE)
                {
                    f.stutter = _rnd.Next(2, 5);
                    f.timer   = Range(0.06f, 0.16f);
                }
                else
                {
                    f.timer = f.on ? Range(FLICKER_ON_MIN, FLICKER_ON_MAX)
                                   : Range(FLICKER_OFF_MIN, FLICKER_OFF_MAX);
                }
            }
            f.alpha = Mathf.MoveTowards(f.alpha, f.on ? 1f : 0f, FADE_SPEED * dt);
            SetAlpha(f.img, f.alpha);
        }

        // 피뢰침. 켜질 때와 꺼질 때 살짝 번지게 — 뚝 끊기면 화면 오류처럼 보인다.
        for (int i = 0; i < _beacons.Count; i++)
        {
            var b = _beacons[i];
            float p = Mathf.Repeat(tm + b.phase, BEACON_PERIOD);
            float a = Mathf.Clamp01(Mathf.Min(p, BEACON_ON - p) / 0.15f);
            SetAlpha(b.dot,  Mathf.Lerp(0.25f, 1f, a));
            SetAlpha(b.glow, 0.35f * a);
        }

        // 눈
        if (_flakes == null) return;
        float half = CanvasMetrics.HalfHeight;
        for (int i = 0; i < _flakes.Length; i++)
        {
            var f = _flakes[i];
            f.y -= f.speed * dt;
            if (f.y < -half - 20f)
            {
                f.y = half + 20f;
                f.x = Range(-CanvasMetrics.REF_W * 0.5f, CanvasMetrics.REF_W * 0.5f);
            }
            float sway = f.swayAmp * Mathf.Sin((tm / f.swaySec + f.swayPhase) * Mathf.PI * 2f);
            f.rt.anchoredPosition = new Vector2(f.x + sway, f.y);
            _flakes[i] = f;
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
    // 세로 그라데이션들은 가로로 변하지 않으므로 1px 폭이면 되고, 늘려서 쓴다.
    // Texture2D 는 y=0 이 아래쪽이다.

    /// <summary>창문 한 칸. 가운데 창만 칠하고 나머지는 비워 몸통 색이 비치게 한다.</summary>
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
            return Color32.Lerp(SKY, SKY_HORIZON, t);
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

    static Sprite MakeDimSprite(int h)
    {
        return MakeVertical(h, up =>
        {
            float t = Smooth(Mathf.Clamp01(1f - up / DIM_RAMP_H));
            var c = DIM_COL;
            c.a = (byte)(DIM_BOTTOM_ALPHA * t * 255f);
            return c;
        });
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);   // 선형이면 중간에 띠가 보인다

    /// <summary>up 은 0 = 화면 아래, 1 = 위.</summary>
    static Sprite MakeVertical(int h, System.Func<float, Color32> colorAt)
    {
        var tex = new Texture2D(1, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;   // 없으면 위아래 끝이 반대쪽 색을 물어 온다
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
                float d  = Mathf.Sqrt(dx * dx + dy * dy) / r;   // 0 = 가운데, 1 = 테두리
                float a  = soft ? Smooth(Mathf.Clamp01(1f - d))
                                : Mathf.Clamp01((1f - d) * r);  // 테두리 1px 만 번진다
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
