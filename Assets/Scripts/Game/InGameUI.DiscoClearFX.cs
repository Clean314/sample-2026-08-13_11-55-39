using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// InGameUI 의 디스코 모드 줄 클리어 연출 — 불꽃놀이.
//
// 블록이 바깥으로 살짝 커지며 옅어지고, 그 자리에서 "팡" 모양이 퍼진다 —
// 가운데가 빈 채로 끝이 둥근 작대기들이 방사형으로 놓인 만화식 터짐 표시.
// 작대기는 안쪽 끝이 바깥 끝보다 빨리 밀려 나간다. 그래서 퍼지면서 바깥쪽으로 짧아지다
// 끝에 닿아 사라진다. 작대기 간격·길이를 조금씩 흔들어 손으로 그린 듯 살짝 삐뚤게 둔다.
//
// 한 칸의 팡은 한 색이다. 칸마다 디스코 7색을 자리 순서로 돌려서 판 위에 무지개로 깔린다.
//
// 흰 번쩍임, 가운데서 번지는 순서는 넣지 않는다(노말에서 과했던 것들). 모든 칸이 한꺼번에 터진다.
// 무지개 블럭 발동으로 들어온 클리어는 자체 폭발이 있어서 여기를 타지 않는다(PlayClearEffect).
public partial class InGameUI
{
    // ── 조정 손잡이 ──
    const float DCFX_HOLD      = 0.12f;  // 입력을 막고 기다리는 시간 (노말과 같다)
    // 블록은 줄어들지 않고 폭죽과 같은 쪽(바깥)으로 살짝 커지면서 옅어진다. 줄어들게 했더니
    // 안으로 빨려 드는 움직임이 밖으로 퍼지는 폭죽과 엇갈려 어지러웠다.
    const float DCFX_BLOCK_SEC   = 0.2f;   // 블록이 사라지기까지
    const float DCFX_BLOCK_SCALE = 1.2f;   // 사라질 때까지 커지는 배율
    const int   DCFX_SPARKS    = 8;      // 팡 하나의 작대기 수
    const float DCFX_SPARK_SEC = 0.42f;  // 퍼져서 사라지기까지
    const float DCFX_IN_FROM   = 30f;    // 작대기 안쪽 끝: 시작 반지름
    const float DCFX_OUT_FROM  = 70f;    //       바깥 끝: 시작 반지름 (시작 길이 = 40)
    const float DCFX_END_R     = 104f;   // 둘 다 여기서 만나며 사라진다
    const float DCFX_SPARK_W   = 9f;     // 작대기 두께 (끝은 반원)

    const string DCFX_ROOT = "DiscoClearFX";  // ClearTransientEffects 가 이 이름으로 치운다

    Sprite _dcfxCapsule;

    struct DcfxSpark { public Image img; public RectTransform rt; public Vector2 center, dir; public float size; }
    struct DcfxPop   { public Image img; public RectTransform rt; }

    /// <summary>디스코 일반 줄 클리어. FlashAndFade 자리에 들어간다.</summary>
    ///
    /// 두 줄이면 블록 16개 + 작대기 128개다. 예전엔 하나마다 오브젝트를 새로 만들고 코루틴을 따로
    /// 돌렸다가 지웠다. 지금은 오브젝트를 풀에서 빌려 쓰고(TakeFx/ReturnFx, InGameUI.ClearFX.cs)
    /// 움직임은 코루틴 하나가 한꺼번에 돌린다.
    IEnumerator DiscoLineClearFX(HashSet<(int r, int c)> cells)
    {
        if (_audioSource != null && _audioSource.clip != null)
            _audioSource.Play();

        if (_dcfxCapsule == null) _dcfxCapsule = MakeCapsuleSprite(32);

        // 캔버스 바로 밑. 그리드 밑이면 판 밖으로 튄 작대기가 트레이·점수에 가린다.
        // 작대기는 블록 위에 그린다(형제 순서 = 그리는 순서). 처음엔 작대기가 블록 가장자리
        // 안쪽에서 시작해서, 밑에 깔면 블록이 줄어들 때까지 가려진다.
        var rootGo = new GameObject(DCFX_ROOT);
        rootGo.transform.SetParent(_canvas.transform, false);
        var root = rootGo.AddComponent<RectTransform>();
        UIEffects.SubCanvas(rootGo);   // 작대기가 매 프레임 움직인다 — 판과 따로 묶는다
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        var blocks = NewLayer("Blocks", root);
        var sparkLayer = NewLayer("Sparks", root);

        var pops   = new List<DcfxPop>(cells.Count);
        var sparks = new List<DcfxSpark>(cells.Count * DCFX_SPARKS);

        foreach (var (r, c) in cells)
        {
            // 줄에 속해도 살아남는 칸(무지개 블럭)은 건드리지 않는다.
            if (_gm.Board[r, c] != 0) continue;
            var ov = _blockOverlays[r, c];
            if (ov == null || ov.sprite == null || ov.color.a <= 0f) continue;

            Vector3 pos = ov.rectTransform.position;

            var pop = TakeFx(blocks);
            pop.sprite = ov.sprite;
            pop.color  = Color.white;
            pop.rectTransform.sizeDelta = new Vector2(120f, 120f);   // 블록 스프라이트 크기 그대로
            pop.rectTransform.position  = pos;
            pops.Add(new DcfxPop { img = pop, rt = pop.rectTransform });

            // 한 칸 = 한 색. 대각선으로 색이 이어져 판 위에 무지개가 깔린다.
            Color col     = Color.Lerp(DISCO_COLORS[(r + c) % DISCO_COLORS.Length], Color.white, 0.15f);
            float baseAng = Random.Range(0f, 360f);
            for (int i = 0; i < DCFX_SPARKS; i++)
            {
                float ang = baseAng + i * (360f / DCFX_SPARKS) + Random.Range(-9f, 9f);
                var img = TakeFx(sparkLayer);
                img.sprite = _dcfxCapsule;
                img.type   = Image.Type.Sliced;   // 늘려도 양 끝 반원이 유지된다
                img.color  = col;
                var rt = img.rectTransform;
                rt.position      = pos;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);   // 가로로 누운 작대기를 바깥 방향으로
                sparks.Add(new DcfxSpark
                {
                    img = img, rt = rt, center = rt.anchoredPosition,
                    dir  = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)),
                    size = Random.Range(0.85f, 1.15f),
                });
            }

            // 원래 블록은 바로 숨긴다. 같은 자리에 복사본이 떠 있으니 끊김 없이 이어진다.
            ov.color = Color.clear;
        }

        StartCoroutine(AnimateDiscoFX(rootGo, pops, sparks));
        yield return new WaitForSeconds(DCFX_HOLD);
    }

    static RectTransform NewLayer(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    IEnumerator AnimateDiscoFX(GameObject rootGo, List<DcfxPop> pops, List<DcfxSpark> sparks)
    {
        float total = Mathf.Max(DCFX_SPARK_SEC, DCFX_BLOCK_SEC);
        float t = 0f;
        while (t < total)
        {
            if (rootGo == null) yield break;   // 게임오버 정리(ClearTransientEffects)가 통째로 치웠다
            t += Time.deltaTime;

            // 블록: 바깥으로 살짝 커지며 옅어진다 — 폭죽과 같은 결
            float kb = Mathf.Clamp01(t / DCFX_BLOCK_SEC);
            float eb = 1f - (1f - kb) * (1f - kb);
            var popScale = Vector3.one * Mathf.Lerp(1f, DCFX_BLOCK_SCALE, eb);
            var popCol   = new Color(1f, 1f, 1f, 1f - kb);
            foreach (var p in pops) { p.rt.localScale = popScale; p.img.color = popCol; }

            // 작대기: 바깥 끝은 처음에 확 나갔다 느려지고, 안쪽 끝은 조금 늦게 따라와 끝에서 만난다.
            float k  = Mathf.Clamp01(t / DCFX_SPARK_SEC);
            float eo = 1f - (1f - k) * (1f - k) * (1f - k);
            float ei = 1f - (1f - k) * (1f - k);
            float ppu = _dcfxCapsule.rect.height;
            foreach (var sp in sparks)
            {
                float rOut = Mathf.Lerp(DCFX_OUT_FROM, DCFX_END_R, eo) * sp.size;
                float rIn  = Mathf.Lerp(DCFX_IN_FROM,  DCFX_END_R, ei) * sp.size;
                float len  = Mathf.Max(rOut - rIn, 0f);
                sp.rt.anchoredPosition = sp.center + sp.dir * ((rIn + rOut) * 0.5f);
                // 길이가 두께보다 짧아지면 두께도 같이 줄여 동그란 점으로 작아지며 사라지게 한다.
                float w = Mathf.Min(DCFX_SPARK_W, len);
                sp.rt.sizeDelta = new Vector2(Mathf.Max(len, w), w);
                // 9-슬라이스 끝(텍스처 반지름 16)이 화면에서 두께의 절반이 되게 줄인다.
                // 안 그러면 두께 9 짜리 막대 끝이 폭 16 으로 그려져 납작한 타원이 된다.
                if (w > 0.01f) sp.img.pixelsPerUnitMultiplier = ppu / w;
            }
            yield return null;
        }

        foreach (var p in pops)    ReturnFx(p.img);
        foreach (var sp in sparks) ReturnFx(sp.img);
        Destroy(rootGo);
    }

    // 끝이 반원인 막대. 가운데 1px 을 늘려 쓰도록 양 끝을 9-슬라이스 테두리로 잡는다.
    static Sprite MakeCapsuleSprite(int h)
    {
        int w = h + 2;
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        float rad = h * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, rad, w - rad);
                float dx = x + 0.5f - cx, dy = y + 0.5f - rad;
                float a  = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        float b = rad;
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(b, 0f, b, 0f));
    }
}
