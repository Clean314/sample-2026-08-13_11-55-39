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

    /// <summary>디스코 일반 줄 클리어. FlashAndFade 자리에 들어간다.</summary>
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
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        var blocks = NewLayer("Blocks", root);
        var sparks = NewLayer("Sparks", root);

        foreach (var (r, c) in cells)
        {
            // 줄에 속해도 살아남는 칸(무지개 블럭)은 건드리지 않는다.
            if (_gm.Board[r, c] != 0) continue;
            var ov = _blockOverlays[r, c];
            if (ov == null || ov.sprite == null || ov.color.a <= 0f) continue;

            Vector3 pos = ov.rectTransform.position;
            StartCoroutine(DiscoBlockPop(blocks, ov.sprite, pos));

            // 한 칸 = 한 색. 대각선으로 색이 이어져 판 위에 무지개가 깔린다.
            Color col     = Color.Lerp(DISCO_COLORS[(r + c) % DISCO_COLORS.Length], Color.white, 0.15f);
            float baseAng = Random.Range(0f, 360f);
            for (int i = 0; i < DCFX_SPARKS; i++)
            {
                float ang = baseAng + i * (360f / DCFX_SPARKS) + Random.Range(-9f, 9f);
                StartCoroutine(DiscoSpark(sparks, pos, ang, col, Random.Range(0.85f, 1.15f)));
            }

            // 원래 블록은 바로 숨긴다. 같은 자리에 복사본이 떠 있으니 끊김 없이 이어진다.
            ov.color = Color.clear;
        }

        yield return new WaitForSeconds(DCFX_HOLD);
        StartCoroutine(DestroyLater(rootGo, Mathf.Max(DCFX_SPARK_SEC, DCFX_BLOCK_SEC)));
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

    IEnumerator DiscoBlockPop(Transform parent, Sprite spr, Vector3 worldPos)
    {
        var go = new GameObject("Pop");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite        = spr;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(120f, 120f);   // 블록 스프라이트 크기 그대로
        rt.position  = worldPos;

        float t = 0f;
        while (t < DCFX_BLOCK_SEC)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DCFX_BLOCK_SEC);
            float e = 1f - (1f - k) * (1f - k);                  // 빨리 커지다 멈춘다 — 폭죽과 같은 결
            rt.localScale = Vector3.one * Mathf.Lerp(1f, DCFX_BLOCK_SCALE, e);
            img.color     = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // 방사형 작대기 하나. 안쪽 끝이 바깥 끝을 따라잡으며 짧아진다. size: 길이·거리 흔들기 배율.
    IEnumerator DiscoSpark(Transform parent, Vector3 worldPos, float angDeg, Color col, float size)
    {
        var go = new GameObject("Spark");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite        = _dcfxCapsule;
        img.type          = Image.Type.Sliced;   // 늘려도 양 끝 반원이 유지된다
        img.color         = col;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.position      = worldPos;
        rt.localRotation = Quaternion.Euler(0f, 0f, angDeg);   // 가로로 누운 작대기를 바깥 방향으로
        Vector2 center = rt.anchoredPosition;
        Vector2 dir    = new Vector2(Mathf.Cos(angDeg * Mathf.Deg2Rad), Mathf.Sin(angDeg * Mathf.Deg2Rad));

        float t = 0f;
        while (t < DCFX_SPARK_SEC)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / DCFX_SPARK_SEC);
            // 바깥 끝은 처음에 확 나갔다 느려지고, 안쪽 끝은 조금 늦게 따라와 끝에서 만난다.
            float eo = 1f - (1f - k) * (1f - k) * (1f - k);
            float ei = 1f - (1f - k) * (1f - k);
            float rOut = Mathf.Lerp(DCFX_OUT_FROM, DCFX_END_R, eo) * size;
            float rIn  = Mathf.Lerp(DCFX_IN_FROM,  DCFX_END_R, ei) * size;
            float len  = Mathf.Max(rOut - rIn, 0f);

            rt.anchoredPosition = center + dir * ((rIn + rOut) * 0.5f);
            // 길이가 두께보다 짧아지면 두께도 같이 줄여 동그란 점으로 작아지며 사라지게 한다.
            float w = Mathf.Min(DCFX_SPARK_W, len);
            rt.sizeDelta = new Vector2(Mathf.Max(len, w), w);
            // 9-슬라이스 끝(텍스처 반지름 16)이 화면에서 두께의 절반이 되게 줄인다.
            // 안 그러면 두께 9 짜리 막대 끝이 폭 16 으로 그려져 납작한 타원이 된다.
            if (w > 0.01f) img.pixelsPerUnitMultiplier = _dcfxCapsule.rect.height / w;
            yield return null;
        }
        if (go != null) Destroy(go);
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
