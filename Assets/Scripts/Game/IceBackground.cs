using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이스 모드 배경. ice.png 를 그대로 붙이지 않고 같은 색·형태로 다시 그린다.
///
/// 그리는 쪽을 고른 이유:
///   · 해상도에 매이지 않는다. 어떤 화면비에서도 하늘이 끝까지 차고 요소만 제자리를 잡는다.
///   · 움직일 수 있다. 한 장짜리 그림은 빙산을 띄우거나 구름을 흘려보낼 수가 없다.
///   · 용량이 0이다. 텍스처는 시작할 때 구워서 쓰고 버린다.
///
/// 디스코 배경들과 달리 아이스는 곡 타임라인이 없다. 그래서 모든 움직임이 경과 시간 기반의
/// 무한 반복이고, 어디서 들어와도 어색한 지점이 없다.
/// </summary>
public class IceBackground : MonoBehaviour
{
    // ── 색 ──────────────────────────────────────────────────────
    // 빙산과 구름은 ice.png 에서 그대로 뽑았고, 하늘과 물결은 다시 잡았다.
    //
    // 원본의 하늘은 (0,132,255) 한 색이었다. 파랑 채널이 꽉 찬 원색이라 화면이 값싸
    // 보이는데, 명도만 떨어뜨려서는 안 고쳐진다 — 채널이 255 에 붙어 있어 어둡게 눌러도
    // 파랑이 그대로 남기 때문이다. 그래서 위아래로 색상 자체가 옮겨가게 했다.
    // 위는 깊은 남색, 수평선은 옅은 하늘. 낮이라는 설정은 그대로 두고 채도만 걷어낸 값이다.
    // ── 조정 손잡이: 풍경 전체의 채도 ──
    // 1.0이면 아래 적힌 값 그대로다. 올리면 파랑이 진해진다.
    //
    // 밝기는 건드리지 않고 회색축에서 밀어내기만 한다. 그래야 아래 주석들이 기대는
    // 밝기 관계가 안 깨진다 — 물결이 물막보다 밝아야 수면에 걸린 빛으로 읽히고,
    // 하늘 위쪽이 수평선보다 어두워야 점수 문구가 읽힌다. 단순히 파랑 채널만 올리면
    // 그 관계가 전부 어긋난다.
    //
    // 흰 것들(ICE_LIGHT, CLOUD_WHITE)은 일부러 통과시키지 않는다. 눈과 빙산의 밝은 면은
    // 희게 남아야 하고, 여길 물들이면 값을 조금만 올려도 배경이 통째로 파래진다.
    const float SATURATION = 1.25f;

    static Color32 Sat(byte r, byte g, byte b)
    {
        float gray = 0.299f * r + 0.587f * g + 0.114f * b;
        return new Color32(
            (byte)Mathf.Clamp(Mathf.Round(gray + (r - gray) * SATURATION), 0f, 255f),
            (byte)Mathf.Clamp(Mathf.Round(gray + (g - gray) * SATURATION), 0f, 255f),
            (byte)Mathf.Clamp(Mathf.Round(gray + (b - gray) * SATURATION), 0f, 255f),
            255);
    }

    public static readonly Color32 SKY         = Sat( 10,  44,  92);  // 화면 위
    static readonly Color32 SKY_HORIZON = Sat( 72, 134, 178);  // 수평선 부근
    static readonly Color32 ICE_LIGHT   = new Color32(238, 240, 249, 255);  // 빙산 밝은 면
    static readonly Color32 ICE_SHADE   = Sat(198, 216, 251);  // 빙산 그늘진 면
    static readonly Color32 CLOUD_WHITE = new Color32(251, 252, 252, 255);
    static readonly Color32 CLOUD_BLUE  = Sat(194, 225, 252);
    // 구름 그늘도 같이 낮췄다. 이것만 원색으로 남으면 하늘에서 걷어낸 채도가 그 자리에
    // 그대로 튀어나온다.
    static readonly Color32 CLOUD_DARK  = Sat( 74, 124, 170);

    // 물결은 반대로 밝게 간다. 원본에선 하늘보다 어두운 색이 물이었지만, 지금은 물결이
    // 수면 막(WATER_*) 아래에 깔려 62% 가 덮인다. 어두운 색을 쓰면 막에 먹혀 그늘과
    // 구분이 안 가고 물에 잠긴 잔해처럼 보인다. 수평선 색보다 밝아야 막을 뚫고 나와서
    // 수면에 걸린 빛으로 읽힌다 — 막을 통과한 뒤 물이 (68,120,161), 물결이 (109,148,174).
    static readonly Color32 WAVE_DARK   = Sat(168, 208, 230);
    static readonly Color32 WAVE_DEEP   = Sat(140, 186, 214);

    // ── 수면 ────────────────────────────────────────────────────
    // 빙산 위에 덮는 반투명 막. 이 선 아래는 물에 잠긴 것으로 읽힌다.
    //
    // 빙산 밑동이 둘 다 -875 라, 수면 하나로 큰 빙산은 34%·작은 빙산은 그보다 깊이
    // 잠긴다. 밑동을 맞춰 놓은 이상 이건 피할 수 없는데, 작은 얼음이 더 깊이 잠기는 건
    // 실제로도 그래서 어색하지 않다. -720 은 곰이 설 마른 얼음을 61px 남기는 선이다.
    //
    // 어둠막이 이 구간을 76% 눌러도 효과는 남는다. 막이 마른 얼음과 잠긴 얼음을 똑같이
    // 누르므로 둘 사이의 차이는 비율 그대로 살아 있기 때문이다.
    const float WATER_LINE_Y    = -720f;  // ── 조정 손잡이: 수면 높이 ──
    // 어둠막이 이미 이 구간을 76% 눌러 놔서, 물막을 어지간히 넣어서는 그늘 하나 더 진
    // 것으로밖에 안 보인다. 잠긴 걸로 읽히게 만드는 건 진하기보다 수면의 밝은 한 줄이다 —
    // 그 선이 있어야 "여기가 물 표면"이 되고, 없으면 그냥 어두운 사각형이 얹힌 게 된다.
    const float WATER_ALPHA_TOP = 0.58f;  // ── 조정 손잡이: 수면 바로 아래의 진하기 ──
    const float WATER_ALPHA_BOT = 0.82f;  // ── 조정 손잡이: 가장 깊은 곳의 진하기 ──
    const float WATER_EDGE_PX   = 10f;    // ── 조정 손잡이: 수면에 걸리는 빛의 두께 ──
    const float WATER_EDGE_A    = 0.92f;  // ── 조정 손잡이: 그 빛의 세기 ──
    static readonly Color32 WATER_TINT = Sat( 24,  70, 112);
    static readonly Color32 WATER_EDGE = Sat(150, 199, 226);

    // 하늘 그라데이션이 수평선 색에 다다르는 높이. 그리드 아랫변(-395)에 맞춰,
    // 판 뒤는 색이 거의 안 변하고 판 위쪽에서만 남색으로 깊어지게 한다.
    const float SKY_HORIZON_Y = -395f;

    // ── 움직임 (전부 "아주 천천히") ──────────────────────────────
    const float BERG_BOB_PX   = 16f;   // ── 조정 손잡이: 빙산이 오르내리는 높이 ──
    const float BERG_BOB_SEC  = 9f;    // ── 조정 손잡이: 한 번 오르내리는 데 걸리는 시간 ──
    const float CLOUD_SPEED   = 7f;    // ── 조정 손잡이: 구름이 흐르는 속도(px/초) ──
    const float WAVE_SPEED    = 3.5f;  // ── 조정 손잡이: 물결이 흐르는 속도(px/초) ──

    // 화면 밖으로 나간 뒤 반대편에서 다시 들어오는 폭. 캔버스 기준 해상도(1080)에
    // 요소 크기를 더해 넉넉히 잡는다 — 좁으면 사라지는 순간이 눈에 띈다.
    const float WRAP_W = 1080f + 400f;

    // 풍경 위에 까는 검은 막. 빙산이 얼음 블록과 같은 흰 계열이라 그냥 두면 판이 배경에
    // 묻힌다. 배경을 어둡게 눌러 두면 블록만 밝게 남아 대비가 산다.
    // 이 막은 IceBackground 의 마지막 자식이라 풍경 위·그리드 아래에 놓인다.
    //
    // 위는 고르게, 아래로 갈수록 짙어진다. 조각 트레이가 화면 아래 28% 를 쓰는데 거기
    // 하늘색이 그대로 밝아서 조각 후보가 배경에 묻혔다.
    //
    // 아래쪽은 검정이 아니라 남색으로 누른다. 하늘색은 파랑 채널이 255 라 검정을 겹쳐도
    // 밝기가 잘 안 떨어진다 — 같은 세기로 눌렀을 때 조각 높이의 배경 휘도가 검정은 0.152,
    // 남색은 0.098 이다. 남색 쪽이 빈 셀의 물색과도 같은 계열이라 화면 아래가 물로 이어진다.
    //
    // 램프는 트레이 위(0.276)보다 넉넉히 높은 데서 시작해야 한다. 트레이 경계에서 시작하면
    // 정작 조각이 놓인 높이(0.182)까지 짙어질 거리가 모자라 효과가 거의 없다.
    const float DIM_ALPHA        = 0f;   // ── 조정 손잡이: 위쪽을 누르는 정도 ──
    const float DIM_BOTTOM_ALPHA = 0.45f;  // ── 조정 손잡이: 화면 맨 아래를 누르는 정도 ──
    const float DIM_RAMP_H       = 0.45f;  // ── 조정 손잡이: 짙어지기 시작하는 높이(화면 비율) ──

    static readonly Color32 DIM_TOP_COL    = new Color32(0,  0,  0, 255);
    static readonly Color32 DIM_BOTTOM_COL = new Color32(2, 28, 62, 255);

    // 곰은 작은 빙산의 자식으로 붙인다. 그래야 빙산이 오르내릴 때 같이 움직인다 —
    // 따로 띄우면 둘의 위상을 맞춰 줘야 하고, 한쪽 주기를 바꾸는 순간 어긋난다.
    // y 는 빙산 윗면 높이에서 역산했다. 스프라이트 rect 위쪽에 여백이 있어 얼음 윗면이
    // 로컬 y 95 쯤이고, 곰 실제 높이가 103 이라 중심이 143 이면 발이 얼음에 닿는다.
    // 다만 얼음 윗면이 왼쪽으로 기울어 앞발 쪽이 더 낮다. 143 이면 거기서 1~2px 뜨므로
    // 138 까지 내려 앞발도 확실히 묻히게 했다.
    static readonly Vector2 BEAR_OFFSET = new Vector2(-9f, 138f);
    const float BEAR_SIZE = 208f;   // polarbear.png 는 200px 정사각에 여백이 있어 실제 곰은 약 162px

    struct Drifter
    {
        public RectTransform rt;
        public float speed;     // px/초. 음수면 왼쪽으로
        public float baseY;
        public float bobAmp;    // 0이면 위아래로 안 움직인다
        public float bobSec;
        public float bobPhase;
    }

    Drifter[] _items;

    /// <summary>배경 이미지 바로 위에 아이스 풍경을 세운다.</summary>
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
        var list = new System.Collections.Generic.List<Drifter>();

        // ── 하늘 ─────────────────────────────────────────────
        // 맨 먼저 세운다. 형제 순서가 곧 그리는 순서라 첫 자식이 제일 뒤에 깔린다.
        AddSky();

        // ── 구름 ─────────────────────────────────────────────
        // 셋 다 속도가 다르다. 같으면 한 덩어리가 통째로 미끄러지는 것처럼 보인다.
        var cloudW = MakeCloudSprite(300, 190, CLOUD_WHITE, CLOUD_BLUE);
        var cloudB = MakeCloudSprite(300, 180, CLOUD_DARK,  CLOUD_DARK);
        var cloudL = MakeCloudSprite(300, 165, CLOUD_BLUE,  CLOUD_WHITE);

        list.Add(Add(cloudW, new Vector2(-345, 580), new Vector2(300, 190), CLOUD_SPEED,        0f, 0f, 0f));
        list.Add(Add(cloudB, new Vector2( -13, 500), new Vector2(300, 180), CLOUD_SPEED * 0.62f, 0f, 0f, 0f));
        list.Add(Add(cloudL, new Vector2( 362, 685), new Vector2(300, 165), CLOUD_SPEED * 1.35f, 0f, 0f, 0f));

        // ── 빙산 ─────────────────────────────────────────────
        // 흐르지 않고(속도 0) 제자리에서 아주 느리게 오르내린다. 위상을 어긋나게 둬야
        // 둘이 한 몸처럼 같이 출렁이지 않는다.
        //
        // y 는 두 선 사이에 끼워 맞춘 값이다.
        //   위   그리드 아랫변이 y -395 다. _gridRt 가 +80 올라가 있으므로 셀 배치값
        //        (-420 - 55 = -475)에 그 80 을 더해야 실제 위치가 나온다.
        //        빙산도 곰도 오르내림 폭까지 더해 이 선을 안 넘는다.
        //   아래 배너가 화면 밑 150 쯤(y -810 아래)을 덮는다. 큰 빙산은 14%, 작은 빙산은
        //        29% 가 가리는데, 곰은 그보다 한참 위라 온전히 남는다.
        //
        // 둘의 밑동은 같은 높이(-875)에 둔다. 같은 바다에 떠 있는 것이라 수면선이 어긋나면
        // 한쪽이 가라앉은 것처럼 보인다. 그래서 큰 빙산을 내릴 때 작은 쪽도 같이 내렸다.
        // 이 조합에서 가장 높이 솟는 건 곰 머리이고, 그것도 -561 이라 판까지 166 남는다.
        var bergBig = MakeBergSprite(520, 450, BIG_BERG, BIG_BERG_SHADE);
        var bergSml = MakeBergSprite(260, 225, SML_BERG, SML_BERG_SHADE);

        list.Add(Add(bergBig, new Vector2(-218, -650), new Vector2(520, 450), 0f, BERG_BOB_PX,        BERG_BOB_SEC,        0f));

        var sml = Add(bergSml, new Vector2(347, -762), new Vector2(260, 225),
                      0f, BERG_BOB_PX * 0.7f, BERG_BOB_SEC * 1.3f, 2.1f);
        list.Add(sml);
        AddBear(sml.rt);

        // ── 물결 ─────────────────────────────────────────────
        // 바다를 그리지 않고 짧은 선 몇 개로만 암시한다. 원본이 그렇게 하고 있고,
        // 그 덕에 하늘색 한 장으로 하늘과 바다를 겸할 수 있다.
        //
        // 빙산 다음에 세운다. 형제 순서가 곧 그리는 순서라 물결이 빙산 앞을 지나간다.
        // 한때는 반대로 뒀었다 — 물결이 빙산 몸통을 가로지르는 게 이상해서였는데,
        // 수면(WATER_LINE_Y)이 생긴 뒤로 물결은 전부 그 아래에 있다. 가로지르는 건
        // 잠긴 부분뿐이고, 빙산 앞 물에 이는 잔물결은 원래 그렇게 보인다.
        //
        // 빙산을 내린 만큼 같이 내려 밑동 근처에 둔다. 여덟 중 다섯은 배너에 가리지만
        // 물결은 어차피 거들 뿐이고, 판 근처로 올리면 빈 셀 사이에서 선이 어른거려 더 나쁘다.
        var waveA = MakeBarSprite(140, 16, WAVE_DARK);
        var waveB = MakeBarSprite( 96, 16, WAVE_DEEP);
        float[,] waves = {
            { -400, -825, 140 }, { -330, -865, 96 }, {  -60, -795, 140 },
            {   70, -805,  96 }, {  240, -855, 140 }, {  360, -885, 96 },
            {  430, -805,  96 }, { -180, -895, 140 },
        };
        for (int i = 0; i < waves.GetLength(0); i++)
        {
            bool wide = waves[i, 2] > 100f;
            list.Add(Add(wide ? waveA : waveB,
                         new Vector2(waves[i, 0], waves[i, 1]),
                         new Vector2(waves[i, 2], 16),
                         WAVE_SPEED * (i % 2 == 0 ? 1f : -0.7f), 0f, 0f, 0f));
        }

        _items = list.ToArray();

        // 빙산 다음, 어둠막 앞. 빙산을 덮어야 잠긴 것으로 보인다.
        AddWater();

        // 맨 마지막에 깔아야 풍경 전체를 덮는다.
        AddDim();
    }

    /// <summary>작은 빙산 위에 곰을 세운다. 빙산의 자식이라 오르내림을 그대로 따라간다.</summary>
    void AddBear(RectTransform berg)
    {
        var tex = Resources.Load<Texture2D>("Sprites/Effects/polarbear");
        if (tex == null) return;   // 없으면 곰만 빠지고 나머지 풍경은 그대로 선다

        var go = new GameObject("PolarBear");
        go.transform.SetParent(berg, false);

        var img = go.AddComponent<Image>();
        img.sprite         = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f));
        img.preserveAspect = true;
        img.raycastTarget  = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = BEAR_OFFSET;
        rt.sizeDelta        = new Vector2(BEAR_SIZE, BEAR_SIZE);
    }

    /// <summary>
    /// 수면 아래를 덮는 반투명 막. 빙산 위에 오므로 밑동이 물에 잠긴 것으로 읽힌다.
    /// 화면 전체가 아니라 수면 아래만 차지한다 — 위쪽은 어차피 투명이라 쓸 데가 없다.
    /// </summary>
    void AddWater()
    {
        float h = WATER_LINE_Y + CanvasMetrics.HalfHeight;   // 수면에서 화면 밑까지
        if (h <= 1f) return;             // 수면을 화면 밖으로 내리면 그릴 게 없다

        var go = new GameObject("Water");
        go.transform.SetParent(transform, false);

        var img = go.AddComponent<Image>();
        img.sprite        = MakeWaterSprite(Mathf.RoundToInt(h));
        img.type          = Image.Type.Simple;
        img.color         = Color.white;   // 색과 세기는 스프라이트가 들고 있다
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, h);
    }

    /// <summary>
    /// 수면 막. 얕은 곳은 옅고 깊을수록 짙어지며, 맨 위 몇 px 은 수면에 걸린 빛으로 밝다.
    /// 그 한 줄이 없으면 그냥 어두운 사각형이라 "잠겼다"가 아니라 "가려졌다"로 보인다.
    /// Texture2D 는 y=0 이 아래쪽이라 깊은 쪽이 낮은 y 다.
    /// </summary>
    static Sprite MakeWaterSprite(int h)
    {
        var tex = new Texture2D(1, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        var px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            float down = h - 1 - y;                  // 수면에서 내려온 거리
            float t    = h > 1 ? down / (h - 1f) : 0f;
            float a    = Mathf.Lerp(WATER_ALPHA_TOP, WATER_ALPHA_BOT, t);
            Color32 col = WATER_TINT;

            float edge = Mathf.Exp(-(down / WATER_EDGE_PX) * (down / WATER_EDGE_PX));
            if (edge > 0.02f)
            {
                col = Color32.Lerp(WATER_TINT, WATER_EDGE, edge);
                a   = Mathf.Lerp(a, WATER_EDGE_A, edge);
            }

            px[y] = new Color32(col.r, col.g, col.b, (byte)(Mathf.Clamp01(a) * 255f));
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>화면 전체를 덮는 하늘. 위에서 수평선으로 색이 옮겨간다.</summary>
    void AddSky()
    {
        var go = new GameObject("Sky");
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();

        var img = go.AddComponent<Image>();
        img.sprite        = MakeSkySprite(256);
        img.type          = Image.Type.Simple;
        img.color         = Color.white;   // 색은 스프라이트가 들고 있다
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 세로로 색이 옮겨가는 하늘. 가로로는 변하지 않으므로 1px 폭이면 되고, 늘려서 쓴다.
    /// Texture2D 는 y=0 이 아래쪽이라 수평선 쪽이 낮은 y 다.
    /// </summary>
    static Sprite MakeSkySprite(int h)
    {
        var tex = new Texture2D(1, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;   // 없으면 위아래 끝이 반대쪽 색을 물어 온다

        var px = new Color32[h];
        float half = CanvasMetrics.HalfHeight;
        for (int y = 0; y < h; y++)
        {
            // 캔버스 좌표(위가 +half, 아래가 -half)로 되돌려서 수평선까지의 비율을 잡는다.
            float cy = -half + CanvasMetrics.Height * (y / (h - 1f));
            float t  = Mathf.Clamp01((half - cy) / (half - SKY_HORIZON_Y));
            t = t * t * (3f - 2f * t);   // 선형이면 중간에 띠가 보인다
            px[y] = Color32.Lerp(SKY, SKY_HORIZON, t);
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// 풍경 위에 까는 검은 막. 마지막 자식이라 풍경만 덮고 그리드는 못 덮는다.
    /// 아래로 갈수록 짙어지므로 색이 아니라 스프라이트가 세기를 들고 있다.
    /// </summary>
    void AddDim()
    {
        var go = new GameObject("Dim");
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        var img = go.AddComponent<Image>();
        img.sprite        = MakeDimSprite(256);
        img.type          = Image.Type.Simple;
        img.color         = Color.white;   // 세기는 스프라이트가 들고 있다
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    Drifter Add(Sprite spr, Vector2 pos, Vector2 size, float speed,
                float bobAmp, float bobSec, float bobPhase)
    {
        var go = new GameObject("IceItem");
        go.transform.SetParent(transform, false);

        var img = go.AddComponent<Image>();
        img.sprite        = spr;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;

        return new Drifter {
            rt = rt, speed = speed, baseY = pos.y,
            bobAmp = bobAmp, bobSec = bobSec <= 0f ? 1f : bobSec, bobPhase = bobPhase
        };
    }

    void Update()
    {
        if (_items == null) return;

        float dt = Time.deltaTime;
        float t  = Time.time;

        for (int i = 0; i < _items.Length; i++)
        {
            var d = _items[i];
            if (d.rt == null) continue;

            var p = d.rt.anchoredPosition;

            if (d.speed != 0f)
            {
                p.x += d.speed * dt;
                // 한쪽 끝을 넘어가면 반대편으로 되돌린다. 폭을 넉넉히 잡아서
                // 되돌아오는 순간이 화면 안에서 보이지 않는다.
                if (p.x >  WRAP_W * 0.5f) p.x -= WRAP_W;
                if (p.x < -WRAP_W * 0.5f) p.x += WRAP_W;
            }

            p.y = d.bobAmp > 0f
                ? d.baseY + d.bobAmp * Mathf.Sin((t / d.bobSec + d.bobPhase) * Mathf.PI * 2f)
                : d.baseY;

            d.rt.anchoredPosition = p;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 스프라이트 굽기
    // ═══════════════════════════════════════════════════════════

    // 빙산 실루엣. 좌표는 0~1 비율이라 크기를 바꿔도 형태가 유지된다.
    // (x, y) 에서 y 는 위가 0 이다 — 텍스처를 채울 때 뒤집는다.
    static readonly Vector2[] BIG_BERG = {
        new Vector2(0.34f, 0.00f), new Vector2(0.10f, 0.36f), new Vector2(0.00f, 1.00f),
        new Vector2(1.00f, 1.00f), new Vector2(1.00f, 0.52f), new Vector2(0.74f, 0.52f),
        new Vector2(0.72f, 0.24f),
    };
    // 오른쪽으로 향한 면. 같은 얼음이지만 빛을 덜 받아 푸르게 가라앉는다.
    static readonly Vector2[] BIG_BERG_SHADE = {
        new Vector2(0.34f, 0.00f), new Vector2(0.72f, 0.24f), new Vector2(0.74f, 0.52f),
        new Vector2(1.00f, 0.52f), new Vector2(1.00f, 1.00f), new Vector2(0.66f, 1.00f),
    };
    static readonly Vector2[] SML_BERG = {
        new Vector2(0.22f, 0.10f), new Vector2(0.95f, 0.04f), new Vector2(1.00f, 1.00f),
        new Vector2(0.00f, 1.00f),
    };
    static readonly Vector2[] SML_BERG_SHADE = {
        new Vector2(0.22f, 0.10f), new Vector2(0.34f, 0.10f), new Vector2(0.16f, 1.00f),
        new Vector2(0.00f, 1.00f),
    };

    static Sprite MakeBergSprite(int w, int h, Vector2[] body, Vector2[] shade)
    {
        var px = NewClear(w, h);
        FillPoly(px, w, h, body,  ICE_LIGHT);
        FillPoly(px, w, h, shade, ICE_SHADE);
        return Bake(px, w, h);
    }

    // 구름은 원 여러 개를 겹쳐 만든다. 원본도 같은 방식으로 그려져 있다.
    // 밝은 덩어리를 먼저 깔고 그 위에 조금 어두운 덩어리를 얹으면 부피가 생긴다.
    static Sprite MakeCloudSprite(int w, int h, Color32 main, Color32 sub)
    {
        var px = NewClear(w, h);

        // 아래를 평평하게 잘라 주는 받침. 원만 겹치면 밑이 울퉁불퉁해진다.
        FillRect(px, w, h, 0.10f, 0.62f, 0.92f, 0.98f, main);

        FillCircle(px, w, h, 0.28f, 0.46f, 0.26f, main);
        FillCircle(px, w, h, 0.46f, 0.30f, 0.22f, main);
        FillCircle(px, w, h, 0.14f, 0.66f, 0.17f, main);

        FillCircle(px, w, h, 0.68f, 0.55f, 0.24f, sub);
        FillCircle(px, w, h, 0.86f, 0.70f, 0.16f, sub);
        FillRect(px, w, h, 0.55f, 0.70f, 0.94f, 0.98f, sub);

        return Bake(px, w, h);
    }

    /// <summary>
    /// 세로로 짙어지는 막. 가로로는 변하지 않으므로 1px 폭이면 되고, 늘려서 쓴다.
    /// 색과 세기가 같이 움직인다 — 위는 옅은 검정, 아래는 짙은 남색.
    /// Texture2D 는 y=0 이 아래쪽이라 짙은 쪽이 낮은 y 다.
    /// </summary>
    static Sprite MakeDimSprite(int h)
    {
        var tex = new Texture2D(1, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;   // 없으면 위아래 끝이 반대쪽 색을 물어 온다

        var px = new Color32[h];
        for (int y = 0; y < h; y++)
        {
            float up = y / (h - 1f);                        // 0 = 화면 아래, 1 = 위
            float t  = Mathf.Clamp01(1f - up / DIM_RAMP_H); // 램프 구간에서만 1 → 0
            t = t * t * (3f - 2f * t);                      // 선형이면 램프 끝에 띠가 보인다
            float   a   = Mathf.Lerp(DIM_ALPHA, DIM_BOTTOM_ALPHA, t);
            Color32 col = Color32.Lerp(DIM_TOP_COL, DIM_BOTTOM_COL, t);
            px[y] = new Color32(col.r, col.g, col.b, (byte)(a * 255f));
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
    }

    static Sprite MakeBarSprite(int w, int h, Color32 col)
    {
        var px = NewClear(w, h);
        float r = h * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, w - r);
                float dx = x + 0.5f - cx, dy = y + 0.5f - r;
                if (dx * dx + dy * dy <= r * r) px[y * w + x] = col;
            }
        return Bake(px, w, h);
    }

    // ── 픽셀 도우미 ─────────────────────────────────────────────
    static Color32[] NewClear(int w, int h)
    {
        var px = new Color32[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
        return px;
    }

    static Sprite Bake(Color32[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>비율 좌표 다각형을 채운다. 홀짝 규칙(ray casting).</summary>
    static void FillPoly(Color32[] px, int w, int h, Vector2[] poly, Color32 col)
    {
        for (int y = 0; y < h; y++)
        {
            // 텍스처는 아래가 0, 다각형 좌표는 위가 0 이라 뒤집는다.
            float fy = 1f - (y + 0.5f) / h;
            for (int x = 0; x < w; x++)
            {
                float fx = (x + 0.5f) / w;
                bool inside = false;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                {
                    if ((poly[i].y > fy) != (poly[j].y > fy) &&
                        fx < (poly[j].x - poly[i].x) * (fy - poly[i].y)
                             / (poly[j].y - poly[i].y) + poly[i].x)
                        inside = !inside;
                }
                if (inside) px[y * w + x] = col;
            }
        }
    }

    static void FillCircle(Color32[] px, int w, int h, float cx, float cy, float r, Color32 col)
    {
        float pcx = cx * w, pcy = (1f - cy) * h, pr = r * w;
        int x0 = Mathf.Max(0, (int)(pcx - pr)), x1 = Mathf.Min(w - 1, (int)(pcx + pr));
        int y0 = Mathf.Max(0, (int)(pcy - pr)), y1 = Mathf.Min(h - 1, (int)(pcy + pr));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - pcx, dy = y + 0.5f - pcy;
                if (dx * dx + dy * dy <= pr * pr) px[y * w + x] = col;
            }
    }

    static void FillRect(Color32[] px, int w, int h, float x0, float y0, float x1, float y1, Color32 col)
    {
        int px0 = Mathf.Max(0, (int)(x0 * w)),        px1 = Mathf.Min(w - 1, (int)(x1 * w));
        int py0 = Mathf.Max(0, (int)((1f - y1) * h)), py1 = Mathf.Min(h - 1, (int)((1f - y0) * h));
        for (int y = py0; y <= py1; y++)
            for (int x = px0; x <= px1; x++)
                px[y * w + x] = col;
    }
}
