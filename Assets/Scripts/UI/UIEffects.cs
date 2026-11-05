using UnityEngine;

/// <summary>
/// 그라데이션과 그림자를 런타임에 텍스처로 구워 주는 도구 모음.
///
/// 유니티 기본 Shadow 컴포넌트는 같은 그림을 어긋나게 한 번 더 그릴 뿐이라 번지지 않는다.
/// 그래서 그림자는 알파를 흐려서 직접 만든다. 화면을 만들 때 한 번만 굽고, 결과는 부르는
/// 쪽이 들고 있는다 — 매 프레임 하는 일이 아니다.
/// </summary>
public static class UIEffects
{
    // ── 원본 이미지 읽기 ─────────────────────────────────────────
    /// <summary>
    /// 텍스처의 픽셀을 읽는다. 임포트 설정이 Read/Write 꺼짐에 압축까지 되어 있어
    /// GetPixels 를 바로 쓸 수 없으므로, GPU 로 한 번 그려서 되읽는다.
    /// 리니어 색 공간 프로젝트라 sRGB 로 받아야 원본과 같은 값이 나온다.
    /// </summary>
    public static Color32[] ReadPixels(Texture2D src)
    {
        int w = src.width, h = src.height;
        var rt   = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;

        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tmp.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = tmp.GetPixels32();
        Object.Destroy(tmp);
        return px;
    }

    // ── 그라데이션 ───────────────────────────────────────────────
    /// <summary>
    /// 색은 위→아래 그라데이션으로 갈아 끼우고 알파(모양)는 그대로 둔다.
    /// 한 가지 색으로 된 그림(로고)에만 쓴다. 여러 색이 섞인 그림에 쓰면 전부 한 색이 된다.
    /// </summary>
    public static void TintVertical(Color32[] px, int w, int h, Color top, Color bottom)
    {
        for (int y = 0; y < h; y++)
        {
            // 텍스처 y 는 아래가 0 이다
            Color32 c = Color.Lerp(bottom, top, h > 1 ? y / (float)(h - 1) : 0f);
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                byte a = px[row + x].a;
                px[row + x] = new Color32(c.r, c.g, c.b, a);
            }
        }
    }

    /// <summary>
    /// 화면 전체 배경용 위→아래 그라데이션. 어두운 색끼리는 단계가 적어 줄무늬가
    /// 지기 쉬워서, 1단계 이하의 잡음을 섞어(디더링) 경계를 흩뜨린다.
    /// 화면보다 작게 만들어 늘려 쓰므로 잡음도 필터링되어 눈에 띄지 않는다.
    /// </summary>
    public static Sprite VerticalGradient(int w, int h, Color top, Color bottom)
    {
        var px  = new Color32[w * h];
        var rng = new System.Random(1234);   // 매번 같은 결과가 나오게 고정
        for (int y = 0; y < h; y++)
        {
            Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
            for (int x = 0; x < w; x++)
            {
                float n = (float)(rng.NextDouble() - 0.5) / 255f;
                px[y * w + x] = new Color(c.r + n, c.g + n, c.b + n, 1f);
            }
        }
        return ToSprite(px, w, h);
    }

    // ── 둥근 테두리 (버튼) ───────────────────────────────────────
    /// <summary>
    /// 속이 빈 둥근 사각형 테두리. 색은 왼쪽 위(from)에서 오른쪽 아래(to)로 비스듬히 바뀐다.
    /// 버튼 크기 그대로 굽는다 — 9-슬라이스로 늘리면 그라데이션이 같이 늘어나기 때문이다.
    /// 그림자를 붙일 수 있게 스프라이트가 아니라 픽셀로 돌려준다.
    /// </summary>
    public static Color32[] RoundedRing(int w, int h, float radius, float border, Color from, Color to)
    {
        var px = new Color32[w * h];
        float hw = w * 0.5f, hh = h * 0.5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float d = RoundedRectSdf(x + 0.5f - hw, y + 0.5f - hh, hw, hh, radius);
                // 바깥 경계와 안쪽 경계(d = -border) 둘 다 1px 안티에일리어싱
                float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + border + 0.5f);

                // 가로 위주로 바뀌고 세로는 조금만 섞는다. 가운데 쪽에서만 넘어가고
                // 양 끝은 제 색으로 머문다(참고 이미지가 그렇다).
                float u = (x + 0.5f) / w * 0.75f + (1f - (y + 0.5f) / h) * 0.25f;
                Color c = Color.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, u)));
                c.a *= a;
                px[y * w + x] = c;
            }
        }
        return px;
    }

    // 둥근 사각형까지의 거리. 안쪽이 음수, 바깥이 양수.
    static float RoundedRectSdf(float x, float y, float halfW, float halfH, float r)
    {
        r = Mathf.Max(0f, Mathf.Min(r, Mathf.Min(halfW, halfH)));
        float qx = Mathf.Abs(x) - (halfW - r);
        float qy = Mathf.Abs(y) - (halfH - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // ── 그림 모양을 딴 그림자 ────────────────────────────────────
    /// <summary>
    /// 그림의 모양(알파)을 따서 흐린 그림자를 밑에 깔아 합친다.
    ///
    /// pad 만큼 사방을 넓혀서 그림자가 잘리지 않게 할 수 있다. 결과는 (w + 2·pad) 크기라,
    /// 화면에 같은 크기로 보이려면 부르는 쪽이 표시 크기도 그만큼 키워야 한다.
    /// pad 를 0 으로 주면 원본 크기 그대로 두고, 원본의 여백 안에서 그림자를 그린다.
    /// offset 은 텍스처 기준이라 y 가 음수면 아래로 떨어진다.
    ///
    /// castFrom 을 주면 그림자를 원본 대신 그 모양에서 떨어뜨리고, 그 모양 안쪽에는 그리지
    /// 않는다(CSS box-shadow 처럼). 속이 빈 얇은 테두리는 제 모양으로는 그림자가 거의
    /// 안 생기고, 꽉 찬 모양으로 떨어뜨리면 비어 있는 속까지 어두워지기 때문이다.
    /// </summary>
    public static Texture2D WithDropShadow(Color32[] src, int w, int h, int pad,
                                           Vector2Int offset, int blur, float opacity, Color shadowColor,
                                           Color32[] castFrom = null)
    {
        var caster = castFrom ?? src;
        int ow = w + pad * 2, oh = h + pad * 2;

        // 원본 알파를 그림자 자리로 옮겨 놓고 흐린다
        var a = new float[ow * oh];
        for (int y = 0; y < h; y++)
        {
            int ty = y + pad + offset.y;
            if (ty < 0 || ty >= oh) continue;
            for (int x = 0; x < w; x++)
            {
                int tx = x + pad + offset.x;
                if (tx < 0 || tx >= ow) continue;
                a[ty * ow + tx] = caster[y * w + x].a / 255f;
            }
        }
        // 상자 흐림을 세 번 겹치면 가우시안에 가까워진다
        int r = Mathf.Max(1, blur / 3);
        var tmp = new float[a.Length];
        for (int i = 0; i < 3; i++)
        {
            BoxBlur(a, tmp, ow, oh, r, true);
            BoxBlur(tmp, a, ow, oh, r, false);
        }

        // 그림자 위에 원본을 얹는다
        Color32 sc = shadowColor;
        var outPx = new Color32[ow * oh];
        for (int y = 0; y < oh; y++)
        {
            for (int x = 0; x < ow; x++)
            {
                int i  = y * ow + x;
                float sa = a[i] * opacity;

                int sx = x - pad, sy = y - pad;
                bool inSrc = sx >= 0 && sx < w && sy >= 0 && sy < h;
                Color32 s = inSrc ? src[sy * w + sx] : default;
                float fa = s.a / 255f;
                if (castFrom != null && inSrc) sa *= 1f - castFrom[sy * w + sx].a / 255f;

                float oa = fa + sa * (1f - fa);
                if (oa <= 0f) { outPx[i] = default; continue; }
                float k = sa * (1f - fa);
                outPx[i] = new Color32(
                    (byte)((s.r * fa + sc.r * k) / oa),
                    (byte)((s.g * fa + sc.g * k) / oa),
                    (byte)((s.b * fa + sc.b * k) / oa),
                    (byte)(oa * 255f));
            }
        }

        var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(outPx);
        tex.Apply(false, true);   // 다시 읽을 일이 없으니 CPU 사본은 버린다
        return tex;
    }

    static void BoxBlur(float[] src, float[] dst, int w, int h, int r, bool horizontal)
    {
        int len  = horizontal ? w : h;
        int rows = horizontal ? h : w;
        float inv = 1f / (r * 2 + 1);
        for (int j = 0; j < rows; j++)
        {
            float sum = 0f;
            for (int k = -r; k <= r; k++) sum += At(src, w, horizontal, j, k, len);
            for (int i = 0; i < len; i++)
            {
                dst[horizontal ? j * w + i : i * w + j] = sum * inv;
                sum += At(src, w, horizontal, j, i + r + 1, len) - At(src, w, horizontal, j, i - r, len);
            }
        }
    }

    // 범위 밖은 투명으로 본다
    static float At(float[] a, int w, bool horizontal, int row, int i, int len)
    {
        if (i < 0 || i >= len) return 0f;
        return horizontal ? a[row * w + i] : a[i * w + row];
    }

    // ── 공통 ─────────────────────────────────────────────────────
    public static Sprite ToSprite(Texture2D tex) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                      100f, 0, SpriteMeshType.FullRect);

    static Sprite ToSprite(Color32[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return ToSprite(tex);
    }
}
