using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// InGameUI 의 노말 모드 줄 클리어 연출.
//
// 예전엔 블록이 0.3초 동안 그냥 투명해졌다. 지운다는 손맛이 없어서, 블록이 그 자리에서
// 네 조각으로 갈라져 튀어 나가 떨어지게 했다. 조각은 블록 그림을 그대로 줄인 것이라
// 첫 프레임에는 블록이 2x2 로 쪼개진 것처럼 보인다.
//
// 흰 번쩍임이나 줄을 훑는 빛, 가운데서 번지는 순서는 일부러 넣지 않는다 — 써 봤더니 과했다.
// 모든 칸이 한꺼번에 부서진다.
//
// 막는 시간(입력이 잠기는 구간)은 LCFX_HOLD 뿐이다. 조각은 따로 돌아 다음 조작을 막지 않는다.
public partial class InGameUI
{
    // ── 조정 손잡이 ──
    const float LCFX_HOLD        = 0.12f;  // 입력을 막고 기다리는 시간
    const float LCFX_SHARD_LIFE  = 0.5f;
    const float LCFX_SHARD_SPEED = 260f;   // px/초 (±30% 흔든다)
    const float LCFX_GRAVITY     = 1800f;
    const float LCFX_SHARD_UP    = 0.45f;  // 위로 쏠리는 정도 — 살짝 튀었다 떨어지게
    const float LCFX_SPIN        = 420f;   // 최대 회전 속도(도/초)

    // 블록 스프라이트는 120 이고 보이는 몸통은 그 안쪽 110 이다. 그 몸통을 2x2 로 나눈다.
    const float LCFX_BODY = 110f;
    const float LCFX_GAP  = 2f;            // 조각 사이 틈 — 갈라진 금처럼 보인다

    const string LCFX_ROOT = "LineClearFX";  // ClearTransientEffects 가 이 이름으로 치운다

    /// <summary>노말 모드 줄 클리어. FlashAndFade 자리에 들어간다.</summary>
    IEnumerator NormalLineClearFX(HashSet<(int r, int c)> cells)
    {
        if (_audioSource != null && _audioSource.clip != null)
            _audioSource.Play();

        // 캔버스 바로 밑에 둔다. 그리드 밑이면 튀어 나간 조각이 트레이에 가린다.
        var rootGo = new GameObject(LCFX_ROOT);
        rootGo.transform.SetParent(_canvas.transform, false);
        var root = rootGo.AddComponent<RectTransform>();
        UIEffects.SubCanvas(rootGo);   // 조각이 매 프레임 움직인다 — 판과 따로 묶는다
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        float q    = LCFX_BODY * 0.25f;                    // 사분면 중심까지 거리
        float size = LCFX_BODY * 0.5f - LCFX_GAP;
        var shards = new List<LcfxShard>(cells.Count * 4);
        foreach (var (r, c) in cells)
        {
            var ov = _blockOverlays[r, c];
            if (ov == null || ov.sprite == null || ov.color.a <= 0f) continue;

            Vector3 center = ov.rectTransform.position;
            float   scale  = ov.rectTransform.lossyScale.x;
            for (int i = 0; i < 4; i++)
            {
                var offset = new Vector2(i % 2 == 0 ? -q : q, i < 2 ? q : -q);
                // 제 사분면 쪽으로 튀어 나간다. 방향을 조금 흔들어야 네 조각이 기계적으로 안 보인다.
                Vector2 dir = offset.normalized + Random.insideUnitCircle * 0.35f;
                dir = (dir.normalized + new Vector2(0f, LCFX_SHARD_UP)).normalized;
                float speed = LCFX_SHARD_SPEED * Random.Range(0.7f, 1.3f);

                var img = TakeFx(root);
                img.sprite = ov.sprite;
                img.color  = Color.white;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(size, size);
                rt.position  = center + (Vector3)(offset * scale);
                shards.Add(new LcfxShard
                {
                    img = img, rt = rt, pos = rt.anchoredPosition, vel = dir * speed,
                    spin = Random.Range(-LCFX_SPIN, LCFX_SPIN),
                });
            }

            // 원래 블록은 바로 숨긴다. 같은 자리에 조각이 떠 있으니 끊김 없이 이어진다.
            ov.color = Color.clear;
        }

        // 조각은 따로 돌아 다음 조작을 막지 않는다. 오브젝트는 풀에서 빌리고, 움직임은 코루틴 하나가 맡는다.
        StartCoroutine(AnimateShards(rootGo, shards));
        yield return new WaitForSeconds(LCFX_HOLD);
    }

    struct LcfxShard { public Image img; public RectTransform rt; public Vector2 pos, vel; public float spin, rot; }

    IEnumerator AnimateShards(GameObject rootGo, List<LcfxShard> shards)
    {
        float t = 0f;
        while (t < LCFX_SHARD_LIFE)
        {
            if (rootGo == null) yield break;   // 게임오버 정리가 통째로 치웠다
            float dt = Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / LCFX_SHARD_LIFE);
            var scale = Vector3.one * Mathf.Lerp(1f, 0.5f, k * k);
            // 처음 40% 는 또렷하게, 그 뒤로 옅어진다
            var col = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((k - 0.4f) / 0.6f));
            for (int i = 0; i < shards.Count; i++)
            {
                var sh = shards[i];
                sh.vel.y -= LCFX_GRAVITY * dt;
                sh.pos   += sh.vel * dt;
                sh.rot   += sh.spin * dt;   // 처음엔 반듯해야 블록이 쪼개진 모양으로 읽힌다
                sh.rt.anchoredPosition = sh.pos;
                sh.rt.localRotation    = Quaternion.Euler(0f, 0f, sh.rot);
                sh.rt.localScale       = scale;
                sh.img.color           = col;
                shards[i] = sh;
            }
            yield return null;
        }
        foreach (var sh in shards) ReturnFx(sh.img);
        Destroy(rootGo);
    }

    // ── 이펙트용 이미지 풀 ────────────────────────────────────────
    // 줄을 지울 때마다 수십~백여 개의 이미지를 만들었다 지우면 그때마다 할당·정리 비용이 몰린다.
    // 다 쓴 것은 꺼진 보관함(FxPool) 밑에 모아 두었다가 다음 클리어에 다시 쓴다.
    // 게임오버 정리가 이펙트 루트를 통째로 지우면 그 안에 있던 것은 같이 사라지는데, 그건 그대로 둔다 —
    // 풀에서 꺼낼 때 사라진 것은 건너뛰고 모자라면 새로 만든다.
    readonly Stack<Image> _fxPool = new Stack<Image>();
    GameObject _fxPoolRoot;

    Image TakeFx(Transform parent)
    {
        while (_fxPool.Count > 0)
        {
            var img = _fxPool.Pop();
            if (img == null) continue;
            var rt = img.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;
            rt.localScale    = Vector3.one;
            img.type = Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }
        var go = new GameObject("Fx");
        go.transform.SetParent(parent, false);
        var n = go.AddComponent<Image>();
        n.raycastTarget = false;
        return n;
    }

    void ReturnFx(Image img)
    {
        if (img == null) return;
        if (_fxPoolRoot == null)
        {
            _fxPoolRoot = new GameObject("FxPool");
            _fxPoolRoot.transform.SetParent(_canvas.transform, false);
            _fxPoolRoot.SetActive(false);   // 꺼진 부모 밑이라 그려지지도 갱신되지도 않는다
        }
        img.rectTransform.SetParent(_fxPoolRoot.transform, false);
        _fxPool.Push(img);
    }

    IEnumerator DestroyLater(GameObject go, float sec)
    {
        yield return new WaitForSeconds(sec);
        if (go != null) Destroy(go);
    }
}
