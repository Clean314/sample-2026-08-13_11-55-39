using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// InGameUI 의 토글 모드 전용 부분 — 스페셜 블럭을 끌어 한 줄을 고르는 조작,
// 조준 하이라이트, 드래그 안내 손, 모드 색에 맞춘 배경·게이지 갱신.
public partial class InGameUI
{

    // ── 방 ──────────────────────────────────────────────────────
    // 1점 투시로 그린 방. 정면 벽 하나와 그것을 둘러싼 네 면(천장·바닥·좌우 벽)이다.
    //
    // 다섯 면을 따로 그려 이어 붙이지 않는다. 화면 픽셀마다 카메라에서 광선을 쏴 어느 면에
    // 먼저 닿는지 찾고, 닿은 3D 좌표로 무늬를 읽는다. 방 전체를 한 번에 푸는 방식이라
    // 모서리가 저절로 맞고, 무늬가 면을 넘어가는 자리에서도 어긋나지 않는다.
    //
    // 뒷벽이 화면의 ROOM_BACK_SCALE 이라는 건 방의 깊이까지 정한다. 화면 테두리가 닿는
    // 깊이가 D×ROOM_BACK_SCALE 이므로 0.8 이면 방은 z 0.8D~D 만 차지하는 얕은 상자가 된다.
    // "화면보다 약간 작은 벽면"이라는 조건이 곧 얕은 방을 뜻한다.
    const float ROOM_BACK_SCALE = 0.80f;  // ── 조정 손잡이: 뒷벽이 화면에서 차지하는 비율 ──
    const float ROOM_FOCAL      = 1200f;  // ── 조정 손잡이: 초점거리. 작을수록 원근이 세다 ──
    const float ROOM_NEAR_MUL   = 1.16f;  // ── 조정 손잡이: 가까운 쪽 밝기 배수 ──
    const float ROOM_FAR_MUL    = 0.64f;  // ── 조정 손잡이: 먼 쪽 밝기 배수 ──

    // 굽는 해상도. 무늬가 흐릿하고 대비가 낮아 늘려 써도 티가 안 난다. 기준 해상도로 구우면
    // 픽셀이 네 배라 시작할 때 눈에 띄게 걸린다.
    const int   ROOM_BAKE_W = 480;
    const int   ROOM_BAKE_H = 854;
    const float ROOM_REF_W  = 1080f;   // CanvasScaler.referenceResolution
    // 세로는 화면 비율에 따라 달라지므로 상수로 둘 수 없다 — CanvasMetrics.Height 를 쓴다.

    // 무늬. 뒷벽 깊이를 초점거리와 같게 뒀으므로 뒷벽에서는 월드 단위가 곧 화면 픽셀이다 —
    // 즉 TOGGLE_TILE 이 뒷벽에서 보이는 간격이고, 옆면으로 갈수록 원근이 늘리고 줄인다.
    // 틈을 4 까지 좁혀 타일 줄눈처럼 만들었다.
    const int TOGGLE_TILE   = 96;   // ── 조정 손잡이: 타일 간격 ──
    const int TOGGLE_SQUARE = 92;   // ── 조정 손잡이: 타일 크기(클수록 줄눈이 가늘어짐) ──
    const int TOGGLE_RADIUS = 16;   // ── 조정 손잡이: 모서리 반지름 ──

    // 바탕과 줄눈을 같은 색상 계열의 이웃한 두 톤으로 잡는다. 톤을 붙여 두면 무늬가 배경
    // 위에 얹힌 그래픽이 아니라 벽면 자체의 결로 보인다.
    //
    // 알파를 안 쓰고 결과 색을 그대로 적는다. Linear 색 공간이라 알파로 잡으면 밝은 벽과
    // 어두운 벽에서 필요한 값이 몇 배씩 달라져 감으로 맞춰야 한다. 지금은 sRGB 로 어두운
    // 쪽 16 단계, 밝은 쪽 21 단계 차이다 — 밝은 쪽이 더 벌어져 있어야 같은 정도로 보인다.
    //
    // R 이 G 보다 높으면 어두운 색은 보라로 기운다. 눈은 어두운 영역에서 색상 차이에
    // 특히 민감해서, 몇 단계 차이만으로도 남색이 아니라 자주색으로 읽힌다.
    // 그래서 R < G < B 순서를 지킨다.
    //
    // 두 방의 줄눈 폭을 sRGB 18 단계로 맞췄다. 한때 어두운 방만 두 배 가까이 벌려 놨는데,
    // 그때는 어두운 방에 흰 블록이 올라가서 격자가 세도 안 부딪혔다. 조합을 맞바꾼 뒤로는
    // 검은 블록이 올라가므로 같은 폭이면 격자가 판과 경쟁한다.
    //
    // 어두운 방 바탕. 조각 후보가 셀 없이 벽에 바로 얹혀서, 이 값은 취향이 아니라
    // 검은 조각(32,33,40)이 벽에서 읽히느냐로 정해진다.
    //
    // 벽은 원근 그라데이션을 타므로 바탕값이 아니라 조각이 놓이는 자리(0,-610)에서
    // 재야 한다. 거기서 바탕 × 약 0.73 이 나온다. 조각과의 대비비는 이렇다.
    //     #1A2231 → 벽(19,24,35)  1.11   조각이 벽보다 밝다
    //     #303B4A → 벽(35,43,54)  1.12   벽이 조각을 지나치는 지점. 형태가 사라진다
    //     #384456 → 벽(41,49,63)  1.23   ← 지금
    //     #475463 → 벽(52,61,72)  1.46
    // 흰 방은 같은 자리에서 1.84 다. 즉 밝기를 올릴수록 좋아지지만 방이 "검은 방"으로
    // 안 보이게 된다. 1.23 은 그 사이에서 고른 값이고, 더 어둡게 가려면 #303B4A 를
    // 건너뛰어 한참 아래로 내려가야 한다 — 그때는 조각이 벽보다 밝아지는 쪽으로 벌어진다.
    static readonly Color TOGGLE_BG_DARK    = new Color(0.220f, 0.267f, 0.337f);  // #384456
    static readonly Color TOGGLE_LINE_DARK  = new Color(0.290f, 0.337f, 0.408f);  // #4A5668
    static readonly Color TOGGLE_BG_LIGHT   = new Color(0.922f, 0.937f, 0.973f);  // #EBEFF8
    static readonly Color TOGGLE_LINE_LIGHT = new Color(0.835f, 0.863f, 0.918f);  // #D5DCEA

    // 빈 셀 — 벽에 파낸 홈. 가운데는 평평한 단색이고, 위 안쪽에 그림자가, 아래 안쪽에
    // 얇은 빛이 걸린다. 위에서 빛이 온다고 보면 파인 자리는 그렇게 보인다.
    //
    // 띠를 좁게 잡는 게 핵심이다. 넓게 번지면 조각 그래픽의 또렷한 인상과 부딪힌다 —
    // 안쪽 글로우를 넣어 봤다가 그 이유로 물렸다. 지금은 셀 높이 110 중 위 12, 아래 4 다.
    const int TOGGLE_CELL_SHADE_PX = 12;   // ── 조정 손잡이: 위 안쪽 그림자 두께 ──
    const int TOGGLE_CELL_LIP_PX   = 4;    // ── 조정 손잡이: 아래 안쪽 빛 두께 ──

    // 알파가 아니라 결과 색을 직접 적는다. Linear 색 공간이라 같은 알파가 밝은 셀과
    // 어두운 셀에서 전혀 다른 세기로 나온다. 지금은 양쪽 다 그림자가 sRGB 21 단계쯤
    // 내려가고 빛이 26~28 단계쯤 올라간다 — 눈에 같은 정도로 보이는 값이다.
    // 흰 방 쪽은 하늘색으로 기울였다. 회색일 때는 셀(191,191,209)이 백드롭 뒤 벽
    // (201,203,212)과 대비비 1.12 라 거의 안 보였다 — 밝기가 거의 같아서, 파낸 자국의
    // 그림자·빛만으로 버티고 있었다. 색을 벌리면 밝기 차이 없이도 자리가 읽힌다.
    // R−B 를 −18 에서 −46 으로 벌려 1.20 이 됐다. 더 벌리면 방보다 셀이 먼저 보인다.
    // 흰 방의 셀은 벽보다 밝은 쪽이 아니라 어두운 회색이다. 흰 블록(235~255)이 올라가는
    // 자리라, 밝게 두면 블록과 겨우 20 단계 벌어졌다. 어둡게 파면 블록이 확실히 뜨고
    // 판도 흰 방 안에서 한 덩어리로 또렷해진다.
    static readonly Color32 CELL_BASE_LIGHT  = new Color32( 72,  74,  82, 255);
    static readonly Color32 CELL_SHADE_LIGHT = new Color32( 51,  53,  61, 255);
    static readonly Color32 CELL_LIP_LIGHT   = new Color32( 98, 100, 108, 255);
    static readonly Color32 CELL_BASE_DARK   = new Color32( 43,  43,  61, 255);
    static readonly Color32 CELL_SHADE_DARK  = new Color32( 22,  22,  34, 255);
    static readonly Color32 CELL_LIP_DARK    = new Color32( 64,  65,  84, 255);

    // ── 백드롭 ──────────────────────────────────────────────────
    // 판 뒤에만 까는 어두운 판. 디스코와 같은 방식이다(_gridRt 의 첫 자식, 990×990).
    //
    // 양쪽 모드 다 어둡게 간다. 밝게 들어 올리면 방보다 백드롭이 먼저 눈에 들어와
    // 배경을 그린 의미가 없어진다. 어둡게 깔면 셀 사이 틈이 가라앉아 격자만 떠오른다.
    //
    // 알파가 세 배 차이 나는 건 Linear 색 공간 때문이다. 거의 흰 벽은 조금만 눌러도
    // 내려가지만 어두운 벽은 한참 눌러야 셀과 벌어진다.
    const float BACKDROP_SIZE = 990f;
    static readonly Color TOGGLE_BACKDROP_ON_DARK  = new Color(0f, 0f, 0f, 0.80f);
    static readonly Color TOGGLE_BACKDROP_ON_LIGHT = new Color(0f, 0f, 0f, 0.18f);

    Image  _roomImage;
    Image  _backdrop;
    Sprite _roomDark, _roomLight;
    Sprite _cellLight, _cellDark;
    Sprite _toggleCellSprite;   // 지금 방에 맞는 빈 셀. RefreshGrid 가 읽어 간다

    /// <summary>
    /// 배경 바로 위에 세우는 방. 두 모드를 시작할 때 다 구워 두고 전환 때는 스프라이트만
    /// 바꾼다 — 색을 바꿔 끼우려면 다시 구워야 하는데, 판이 도는 중에 굽으면 끊긴다.
    /// </summary>
    void BuildToggleScene()
    {
        _roomDark  = MakeRoomSprite(TOGGLE_BG_DARK,  TOGGLE_LINE_DARK);
        _roomLight = MakeRoomSprite(TOGGLE_BG_LIGHT, TOGGLE_LINE_LIGHT);
        _cellLight = MakeToggleCellSprite(CELL_BASE_LIGHT, CELL_SHADE_LIGHT, CELL_LIP_LIGHT);
        _cellDark  = MakeToggleCellSprite(CELL_BASE_DARK,  CELL_SHADE_DARK,  CELL_LIP_DARK);
        _toggleCellSprite = _cellLight;   // 시작은 화이트 모드 = 흰 방

        var go = new GameObject("ToggleRoom");
        go.transform.SetParent(_canvas.transform, false);
        go.transform.SetSiblingIndex(_bgImage.transform.GetSiblingIndex() + 1);

        _roomImage               = go.AddComponent<Image>();
        _roomImage.sprite        = _roomLight;   // 시작은 화이트 모드 = 흰 방
        _roomImage.type          = Image.Type.Simple;
        _roomImage.color         = Color.white;   // 색은 스프라이트가 통째로 들고 있다
        _roomImage.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 판 뒤에 까는 어두운 판. 그리드의 첫 자식이라 셀보다는 아래, 방보다는 위다.
    /// BuildGrid 가 끝난 뒤에 불러야 한다 — _gridRt 에 붙기 때문이다.
    /// </summary>
    void BuildToggleGridBackdrop()
    {
        var go = new GameObject("ToggleGridBackdrop");
        go.transform.SetParent(_gridRt, false);
        go.transform.SetAsFirstSibling();

        _backdrop = go.AddComponent<Image>();
        // 모서리 반지름을 border 로 들고 있는 작은 스프라이트를 Sliced 로 늘린다.
        _backdrop.sprite        = MakeRoundedSprite(120, 120, 32);
        _backdrop.type          = Image.Type.Sliced;
        _backdrop.color         = TOGGLE_BACKDROP_ON_LIGHT;   // 시작은 화이트 모드 = 흰 방
        _backdrop.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(BACKDROP_SIZE, BACKDROP_SIZE);
    }

    /// <summary>
    /// 빈 셀 한 장 — 벽에 파낸 홈.
    ///
    /// 위 안쪽에 그림자, 아래 안쪽에 얇은 빛. 둘 다 셀 높이에서 재는 가로 띠라 둥근
    /// 모서리에서는 알파 마스크에 잘려 자연스럽게 곡선을 따라간다.
    /// Texture2D 는 y=0 이 아래쪽이라 위에서부터의 거리는 뒤집어 세야 한다.
    /// </summary>
    Sprite MakeToggleCellSprite(Color32 baseCol, Color32 shade, Color32 lip)
    {
        const int SIZE = 110, R = 30;

        var tex = new Texture2D(SIZE, SIZE, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[SIZE * SIZE];

        float half  = SIZE * 0.5f;
        float inner = half - R;   // 모서리 원의 중심이 놓이는 사각형의 반너비

        for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                float ax = Mathf.Abs(x + 0.5f - half) - inner;
                float ay = Mathf.Abs(y + 0.5f - half) - inner;
                float ox = Mathf.Max(ax, 0f);
                float oy = Mathf.Max(ay, 0f);
                float d = R - Mathf.Sqrt(ox * ox + oy * oy);
                if (ax < 0f && ay < 0f) d = R - Mathf.Max(ax, ay);

                if (d <= 0f) { px[y * SIZE + x] = new Color32(0, 0, 0, 0); continue; }

                Color32 c = baseCol;

                float fromTop = SIZE - 1 - y;
                if (fromTop < TOGGLE_CELL_SHADE_PX)
                {
                    float k = fromTop / TOGGLE_CELL_SHADE_PX;
                    c = Color32.Lerp(shade, baseCol, k * k * (3f - 2f * k));
                }

                // 아래 빛이 나중에 온다. 셀이 아주 낮아 두 띠가 겹치면 빛이 이기는 게 맞다.
                if (y < TOGGLE_CELL_LIP_PX)
                {
                    float k = y / (float)TOGGLE_CELL_LIP_PX;
                    c = Color32.Lerp(lip, baseCol, k * k * (3f - 2f * k));
                }

                px[y * SIZE + x] = new Color32(c.r, c.g, c.b,
                                               (byte)(Mathf.Clamp01(d) * 255f));
            }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// 방 한 장을 굽는다. 카메라는 원점에서 +z 를 보고, 방은 x∈[-hx,hx] y∈[-hy,hy] 인
    /// 상자다. 뒷벽 깊이 D 를 초점거리와 같게 둬서 뒷벽에서 월드 단위 = 화면 픽셀이 된다.
    /// </summary>
    Sprite MakeRoomSprite(Color bg, Color line)
    {
        const float D = ROOM_FOCAL;
        float hx = ROOM_BACK_SCALE * ROOM_REF_W * 0.5f;
        float refH = CanvasMetrics.Height;
        float hy = ROOM_BACK_SCALE * refH * 0.5f;

        // 그라데이션의 양 끝. 화면 테두리가 닿는 깊이가 가장 가깝고, 뒷벽 구석이 가장 멀다.
        float dNear = D * ROOM_BACK_SCALE;
        float dFar  = Mathf.Sqrt(hx * hx + hy * hy + D * D);

        // 굽는 픽셀 하나가 기준 해상도로 몇 픽셀인지. 경계 흐림 폭을 여기 맞춰야
        // 굽는 해상도를 바꿔도 무늬가 같은 정도로 부드럽다.
        float pixScale = ROOM_REF_W / ROOM_BAKE_W;

        var tex = new Texture2D(ROOM_BAKE_W, ROOM_BAKE_H, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[ROOM_BAKE_W * ROOM_BAKE_H];

        for (int y = 0; y < ROOM_BAKE_H; y++)
            for (int x = 0; x < ROOM_BAKE_W; x++)
            {
                // 굽는 좌표를 기준 해상도의 화면 좌표로. Texture2D 는 y=0 이 아래쪽이고
                // 화면도 아래가 -y 라 그대로 두면 위아래가 맞는다.
                float sx = ((x + 0.5f) / ROOM_BAKE_W - 0.5f) * ROOM_REF_W;
                float sy = ((y + 0.5f) / ROOM_BAKE_H - 0.5f) * refH;

                // 광선 (sx, sy, f) 가 다섯 면 중 어디에 먼저 닿는가. t 가 작을수록 가깝다.
                float t    = D / ROOM_FOCAL;   // 뒷벽
                int   face = 0;
                float ax = Mathf.Abs(sx), ay = Mathf.Abs(sy);
                if (ax > 1e-4f) { float tx = hx / ax; if (tx < t) { t = tx; face = 1; } }
                if (ay > 1e-4f) { float ty = hy / ay; if (ty < t) { t = ty; face = 2; } }

                float wx = sx * t, wy = sy * t, wz = ROOM_FOCAL * t;
                float dist = Mathf.Sqrt(wx * wx + wy * wy + wz * wz);

                // 면마다 무늬를 읽는 두 축이 다르다. 옆면은 깊이 축이 비스듬해서 한 픽셀이
                // 훨씬 넓은 범위를 덮으므로 흐림 폭을 키운다 — 안 그러면 줄눈이 지글거린다.
                float u, v, aa = dist / ROOM_FOCAL * pixScale;
                if      (face == 0) { u = wx; v = wy; }
                else if (face == 1) { u = wz; v = wy; aa *= 2.5f; }
                else                { u = wx; v = wz; aa *= 2.5f; }

                float cover = RoomJointCoverage(u, v, Mathf.Max(aa, 0.5f));

                float g = Mathf.Clamp01((dist - dNear) / (dFar - dNear));
                g = g * g * (3f - 2f * g);
                float mul = Mathf.Lerp(ROOM_NEAR_MUL, ROOM_FAR_MUL, g);

                Color c = Color.Lerp(bg, line, cover) * mul;
                px[y * ROOM_BAKE_W + x] = new Color32(
                    (byte)(Mathf.Clamp01(c.r) * 255f),
                    (byte)(Mathf.Clamp01(c.g) * 255f),
                    (byte)(Mathf.Clamp01(c.b) * 255f), 255);
            }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, ROOM_BAKE_W, ROOM_BAKE_H),
                             new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// 벽면 위 한 점이 줄눈에 얼마나 걸치는지. 타일 안이면 0, 줄눈이면 1 이다.
    /// aa 는 그 지점에서 화면 한 픽셀이 덮는 월드 길이 — 경계를 그만큼 흐려 계단을 없앤다.
    /// </summary>
    static float RoomJointCoverage(float u, float v, float aa)
    {
        float inner = TOGGLE_SQUARE * 0.5f - TOGGLE_RADIUS;
        float cu = Mathf.Repeat(u, TOGGLE_TILE) - TOGGLE_TILE * 0.5f;
        float cv = Mathf.Repeat(v, TOGGLE_TILE) - TOGGLE_TILE * 0.5f;

        float dx = Mathf.Abs(cu) - inner;
        float dy = Mathf.Abs(cv) - inner;
        float ox = Mathf.Max(dx, 0f);
        float oy = Mathf.Max(dy, 0f);
        float dist = TOGGLE_RADIUS - Mathf.Sqrt(ox * ox + oy * oy);
        if (dx < 0f && dy < 0f) dist = TOGGLE_RADIUS - Mathf.Max(dx, dy);

        // dist > 0 이 타일 안. 경계(0)에서 0.5 가 되도록 2aa 폭으로 넘긴다.
        return Mathf.Clamp01(0.5f - dist / (2f * aa));
    }

    // 토글 모드 배경/텍스트 색상 갱신
    void RefreshToggleModeBackground()
    {
        bool blackMode = _gm.ToggleCurrentColor == 1;

        // 방은 지금 지울 색과 같은 쪽으로 간다 — 화이트 모드면 흰 방, 블랙 모드면 검은 방.
        // 배경·셀·게이지·글자가 전부 이 하나를 따라가므로, 조합을 뒤집고 싶으면
        // 여기 한 줄만 뒤집으면 된다.
        bool lightRoom = !blackMode;

        // 색상이 실제로 전환됐을 때만 효과음 재생
        int curColor = _gm.ToggleCurrentColor;
        if (_prevToggleColor != curColor && _prevToggleColor != -1)
        {
            if (_audioSource != null && _sfxToggle != null)
                _audioSource.PlayOneShot(_sfxToggle);
        }
        _prevToggleColor = curColor;

        // 방이 화면을 덮으므로 배경은 화면비가 안 맞을 때 드러나는 자리를 메우는 몫이다.
        if (_bgImage != null)
            _bgImage.color = lightRoom ? TOGGLE_BG_LIGHT : TOGGLE_BG_DARK;

        if (_roomImage != null)
            _roomImage.sprite = lightRoom ? _roomLight : _roomDark;

        if (_backdrop != null)
            _backdrop.color = lightRoom ? TOGGLE_BACKDROP_ON_LIGHT : TOGGLE_BACKDROP_ON_DARK;

        _toggleCellSprite = lightRoom ? _cellLight : _cellDark;

        if (_scoreText != null)
            _scoreText.color = lightRoom ? new Color(0.10f, 0.08f, 0.20f) : Color.white;

        if (_highScoreText != null)
            _highScoreText.color = lightRoom ? new Color(0.60f, 0.40f, 0.00f) : GOLD;

    }

    // 게이지 원 색상 갱신.
    //
    // 예전엔 채운 원을 지금 지울 색으로 칠해 게이지가 모드까지 알려 줬다. 이제는 방 자체가
    // 그 색이라 게이지가 같은 말을 반복할 필요가 없고, 오히려 같은 색이면 방에 묻힌다.
    // 그래서 방 밝기의 반대로 칠해 눈에 걸리게만 한다.
    void RefreshGauge()
    {
        if (_gaugeCircles == null || _gaugeCircles[0] == null) return;
        bool lightRoom   = _gm.ToggleCurrentColor == 0;
        Color fullColor  = lightRoom
            ? new Color(0.12f, 0.12f, 0.16f)   // 밝은 방 위의 검정 원
            : new Color(0.92f, 0.92f, 0.94f);  // 어두운 방 위의 흰 원
        Color emptyColor = lightRoom
            ? new Color(0.55f, 0.55f, 0.65f)
            : new Color(0.30f, 0.30f, 0.40f);

        for (int i = 0; i < _gaugeCircles.Length; i++)
            _gaugeCircles[i].color = i < _gm.SpecialGauge ? fullColor : emptyColor;
    }

    // ── 토글 모드 스페셜 블럭: 끌어서 한 줄 색상 맞추기 ─────────
    // 스페셜 블럭에 손을 얹고 판 위를 쓸면 그 줄이 조준된다. 떼면 적용, 판 밖에서 떼면 취소.
    // 그리드 폭이 960이라 화면 좌우 여백이 45씩밖에 없어서, 줄마다 손잡이를 다는 방식은
    // 애초에 들어가지 않는다. 쓸어내는 손짓 자체가 "이 줄을 맞춘다"를 그대로 그린다.
    public void BeginSpecialDrag(int row, int col, Vector2 screenPos)
    {
        if (_gm == null || _dragging || _busy || _specDrag)      return;
        if (!ModeSession.IsToggle)                       return;
        if (_gm.Board[row, col] != GameManager.SPECIAL_BLOCK_VAL) return;

        _dragStartTime  = Time.unscaledTime;
        _specDrag       = true;
        _specRow        = row;
        _specCol        = col;
        _specAxisSet    = false;
        _specHorizontal = true;
        _specLine       = -1;
        _specAnchor     = screenPos;

        // 진짜로 끌기 시작했으면 안내는 제 할 일을 다 했다.
        StopDragGuide();

        _lineHiRow = MakeLineHighlight("LineHi_Row", new Vector2(960, 120));
        _lineHiCol = MakeLineHighlight("LineHi_Col", new Vector2(120, 960));

        // 집어 든 블럭은 살짝 키워 둔다 — 무엇을 끌고 있는지가 보여야 한다.
        if (_blockOverlays[row, col] != null)
            _blockOverlays[row, col].rectTransform.localScale = Vector3.one * 1.14f;

        UpdateSpecialDrag(screenPos);
    }

    public void UpdateSpecialDrag(Vector2 screenPos)
    {
        if (!_specDrag) return;

        // 축은 방금 민 방향으로 잡는다. SPEC_AXIS_STEP만큼 움직일 때마다 다시 본다.
        Vector2 d   = screenPos - _specAnchor;
        float   sf  = _canvas != null ? _canvas.scaleFactor : 1f;
        float   ax  = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        float   dom = Mathf.Max(ax, ay), sub = Mathf.Min(ax, ay);

        if (dom > SPEC_AXIS_STEP * sf)
        {
            bool wantH = ax > ay;
            // 이미 잡힌 축을 뒤집을 때만 뚜렷한 차이를 요구한다.
            // 대각선 근처에서 가로세로가 번갈아 깜빡이는 걸 막는다.
            if (!_specAxisSet || wantH == _specHorizontal || dom > sub * 1.5f)
            {
                _specHorizontal = wantH;
                _specAxisSet    = true;
            }
            _specAnchor = screenPos;
        }

        _specLine = ScreenToCell(screenPos, out int row, out int col)
            ? (_specHorizontal ? row : col)
            : -1;

        RefreshSpecialDrag();
    }

    public void EndSpecialDrag(Vector2 screenPos)
    {
        if (!_specDrag) return;
        UpdateSpecialDrag(screenPos);

        bool apply      = _specAxisSet && _specLine >= 0;
        bool horizontal = _specHorizontal;
        int  line       = _specLine;

        // 하이라이트와 확대를 먼저 걷어야 이어지는 클리어 연출을 덮지 않는다.
        FinishSpecialDrag();
        if (!apply) return;

        // 보드를 먼저 바꾼다. 그래야 RefreshGrid가 새 색을 그려 놓은 뒤에 뒤집기가 시작돼,
        // 넘어가는 순간 이미 바뀐 면이 드러난다.
        _gm.ApplyLineSwap(horizontal, line);

        if (_audioSource != null && _sfxToggleSpecial != null)
            _audioSource.PlayOneShot(_sfxToggleSpecial);

        PlayLineSwapFlip(horizontal, line);
    }

    // ── 한 줄이 넘어가는 연출 ────────────────────────────────────
    // 카드가 한 장씩 뒤집히듯 가로줄은 세로축, 세로줄은 가로축으로 눌렸다 펴진다.
    // 지워진 칸은 건드리지 않는다 — 그 칸은 클리어 연출이 따로 맡고 있어서,
    // 여기서 배율을 되돌리면 사라지던 블록이 도로 튀어나온다.
    void PlayLineSwapFlip(bool horizontal, int index)
    {
        if (_swapFlipCo != null) StopCoroutine(_swapFlipCo);
        _swapFlipCo = StartCoroutine(LineSwapFlip(horizontal, index));
    }

    IEnumerator LineSwapFlip(bool horizontal, int index)
    {
        float total = SWAP_FLIP_SEC + SWAP_FLIP_STAGGER * (GameManager.SIZE - 1);

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < GameManager.SIZE; i++)
            {
                int r = horizontal ? index : i;
                int c = horizontal ? i : index;
                if (_gm.Board[r, c] == 0) continue;

                var img = _blockOverlays[r, c];
                if (img == null) continue;

                float k = (t - i * SWAP_FLIP_STAGGER) / SWAP_FLIP_SEC;
                // 1 → 0 → 1. 가운데에서 두께가 0이 되면서 면이 넘어간 것처럼 보인다.
                float s = (k <= 0f || k >= 1f) ? 1f : Mathf.Abs(Mathf.Cos(k * Mathf.PI));

                img.rectTransform.localScale = horizontal
                    ? new Vector3(s, 1f, 1f)
                    : new Vector3(1f, s, 1f);
            }
            yield return null;
        }

        for (int i = 0; i < GameManager.SIZE; i++)
        {
            int r = horizontal ? index : i;
            int c = horizontal ? i : index;
            if (_gm.Board[r, c] == 0) continue;
            if (_blockOverlays[r, c] != null)
                _blockOverlays[r, c].rectTransform.localScale = Vector3.one;
        }
        _swapFlipCo = null;
    }

    void FinishSpecialDrag()
    {
        if (_specRow >= 0 && _blockOverlays[_specRow, _specCol] != null)
            _blockOverlays[_specRow, _specCol].rectTransform.localScale = Vector3.one;

        _specDrag    = false;
        _specAxisSet = false;
        _specLine    = -1;
        _specRow     = _specCol = -1;
        _specEndTime = Time.time;

        if (_lineHiRow != null) Destroy(_lineHiRow.gameObject);
        if (_lineHiCol != null) Destroy(_lineHiCol.gameObject);
        _lineHiRow = _lineHiCol = null;
    }

    // 조준 줄 하이라이트 갱신. 어느 줄을 겨누고 있는지만 보여 준다 —
    // 그 줄이 바로 지워지는지는 판을 보면 알 수 있고, 색으로 갈라 놓으면
    // 고를 수 있는 줄과 없는 줄이 따로 있는 것처럼 읽힌다.
    void RefreshSpecialDrag()
    {
        bool aiming = _specAxisSet && _specLine >= 0;

        if (_lineHiRow != null)
        {
            if (aiming && _specHorizontal)
            {
                _lineHiRow.rectTransform.anchoredPosition = new Vector2(0, 420 - _specLine * 120);
                _lineHiRow.color = LineHighlightColor();
            }
            else _lineHiRow.color = Color.clear;
        }

        if (_lineHiCol != null)
        {
            if (aiming && !_specHorizontal)
            {
                _lineHiCol.rectTransform.anchoredPosition = new Vector2(-420 + _specLine * 120, 0);
                _lineHiCol.color = LineHighlightColor();
            }
            else _lineHiCol.color = Color.clear;
        }
    }

    /// <summary>
    /// 조준 줄 색. 같은 알파라도 어두운 바탕 위의 밝은 띠는 훨씬 진하고 불투명하게 읽힌다
    /// (밝은 바탕에서는 반대로 묻힌다). 화이트 모드는 배경이 검으므로 그만큼 옅게 깐다.
    /// </summary>
    Color LineHighlightColor()
    {
        Color c = LINE_HI;
        if (_gm != null && _gm.ToggleCurrentColor == 0) c.a *= LINE_HI_DARK_BG;
        return c;
    }

    /// <summary>
    /// 화면 좌표 → 보드 칸. 조각 드래그(GetGridCell)와 달리 손가락 위치 보정을 주지 않는다.
    /// 저쪽은 조각이 손가락에 가리지 않게 위로 띄우지만, 여기서는 짚은 칸이 곧 고른 줄이라
    /// 보정이 들어가면 눈에 보이는 것과 골라지는 줄이 어긋난다.
    /// 가장자리에서 한 칸까지는 넘어가도 그 줄로 쳐준다 — 8번째 줄을 쓸 때 손이 판을 살짝
    /// 벗어나는 건 흔한 일이고, 그때마다 취소되면 쓸 수가 없다.
    /// </summary>
    bool ScreenToCell(Vector2 screenPos, out int row, out int col)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _gridRt, screenPos, null, out Vector2 lp);

        float fr = (-lp.y + 480f) / 120f;
        float fc = ( lp.x + 480f) / 120f;

        row = Mathf.Clamp(Mathf.FloorToInt(fr), 0, GameManager.SIZE - 1);
        col = Mathf.Clamp(Mathf.FloorToInt(fc), 0, GameManager.SIZE - 1);

        return fr > -1f && fr < GameManager.SIZE + 1f
            && fc > -1f && fc < GameManager.SIZE + 1f;
    }

    Image MakeLineHighlight(string name, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_gridRt, false);
        go.transform.SetAsLastSibling();
        var img           = go.AddComponent<Image>();
        img.sprite        = _spr110;
        img.type          = Image.Type.Sliced;
        img.color         = Color.clear;
        img.raycastTarget = false;
        var rt            = go.GetComponent<RectTransform>();
        rt.anchorMin      = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot          = new Vector2(0.5f, 0.5f);
        rt.sizeDelta      = size;
        return img;
    }

    // ── 드래그 안내: 손가락이 한 번 쓸어 보인다 ─────────────────
    // 알려 줄 건 하나뿐이다 — "이건 눌러서 되는 게 아니라 끄는 것이다".
    // 어느 줄을 고를 수 있는지, 가로냐 세로냐는 한 번 끌어 보면 그 자리에서 보이므로
    // 여기서 미리 가르치지 않는다. 설명이 길어지면 아무도 안 본다.
    void ShowDragGuide(int row, int col)
    {
        if (_dragGuideSprite == null || _gridRt == null) return;
        StopDragGuide();
        _dragGuideCo = StartCoroutine(DragGuideLoop(row, col));
    }

    void StopDragGuide()
    {
        if (_dragGuideCo != null) { StopCoroutine(_dragGuideCo); _dragGuideCo = null; }
        if (_dragGuideGo != null) { Destroy(_dragGuideGo);       _dragGuideGo = null; }
    }

    IEnumerator DragGuideLoop(int row, int col)
    {
        var handGo = new GameObject("DragGuide");
        handGo.transform.SetParent(_gridRt, false);
        handGo.transform.SetAsLastSibling();
        var hand           = handGo.AddComponent<Image>();
        hand.sprite        = _dragGuideSprite;
        hand.color         = Color.clear;
        hand.raycastTarget = false;
        var handRt         = handGo.GetComponent<RectTransform>();
        handRt.anchorMin   = handRt.anchorMax = new Vector2(0.5f, 0.5f);
        handRt.pivot       = new Vector2(0.5f, 0.5f);
        handRt.sizeDelta   = new Vector2(GUIDE_HAND_SIZE, GUIDE_HAND_SIZE);
        _dragGuideGo       = handGo;

        // 판 안에 남는 쪽으로 쓴다. 스페셜 블럭이 가장자리에 있어도 손이 허공을 훑지 않게.
        Vector2 from = CellPos(row, col);
        Vector2 to   = CellPos(Step(row, GUIDE_SWEEP_ROWS), Step(col, GUIDE_SWEEP_COLS));
        Vector2 tip  = GUIDE_TIP_OFFSET * GUIDE_HAND_SIZE;

        for (int pass = 0; pass < 2; pass++)
        {
            for (float t = 0f; t < GUIDE_SWEEP_SEC; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / GUIDE_SWEEP_SEC);

                // 들어올 때 빠르게, 나갈 때 조금 여유 있게.
                float a = Mathf.Clamp01(Mathf.Min(k / 0.18f, (1f - k) / 0.28f));

                // 손은 등속으로 밀지 않는다. 붙었다 떨어지듯 움직여야 "민다"로 읽힌다.
                handRt.anchoredPosition = Vector2.Lerp(from, to, Smooth(k)) + tip;
                hand.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
        }

        _dragGuideCo = null;
        StopDragGuide();
    }

    /// <summary>보드 칸 → 그리드 로컬 좌표(칸 중앙).</summary>
    static Vector2 CellPos(int row, int col)
        => new Vector2(-420f + col * 120f, 420f - row * 120f);

    /// <summary>판 안에 남는 쪽으로 n칸 옮긴 자리. 여유가 있는 쪽을 고른다.</summary>
    static int Step(int idx, int n)
        => idx + n <= GameManager.SIZE - 1 ? idx + n : Mathf.Max(idx - n, 0);

    static float Smooth(float k)
    {
        k = Mathf.Clamp01(k);
        return k * k * (3f - 2f * k);
    }
}
