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
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        float q    = LCFX_BODY * 0.25f;                    // 사분면 중심까지 거리
        float size = LCFX_BODY * 0.5f - LCFX_GAP;
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
                StartCoroutine(Shard(root, ov.sprite, center + (Vector3)(offset * scale), size, dir * speed));
            }

            // 원래 블록은 바로 숨긴다. 같은 자리에 조각이 떠 있으니 끊김 없이 이어진다.
            ov.color = Color.clear;
        }

        yield return new WaitForSeconds(LCFX_HOLD);

        // 조각이 다 떨어질 때까지 루트는 남겨 두되, 기다리지는 않는다.
        StartCoroutine(DestroyLater(rootGo, LCFX_SHARD_LIFE));
    }

    IEnumerator Shard(Transform parent, Sprite spr, Vector3 worldPos, float size, Vector2 vel)
    {
        if (parent == null) yield break;

        var go = new GameObject("Shard");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite        = spr;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.position  = worldPos;

        Vector2 pos  = rt.anchoredPosition;
        float   spin = Random.Range(-LCFX_SPIN, LCFX_SPIN);
        float   rot  = 0f;   // 처음엔 반듯해야 블록이 쪼개진 모양으로 읽힌다

        float t = 0f;
        while (t < LCFX_SHARD_LIFE)
        {
            if (rt == null) yield break;
            float dt = Time.deltaTime;
            t += dt;
            vel.y -= LCFX_GRAVITY * dt;
            pos   += vel * dt;
            rot   += spin * dt;
            float k = Mathf.Clamp01(t / LCFX_SHARD_LIFE);
            rt.anchoredPosition = pos;
            rt.localRotation    = Quaternion.Euler(0f, 0f, rot);
            rt.localScale       = Vector3.one * Mathf.Lerp(1f, 0.5f, k * k);
            // 처음 40% 는 또렷하게, 그 뒤로 옅어진다
            img.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((k - 0.4f) / 0.6f));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    IEnumerator DestroyLater(GameObject go, float sec)
    {
        yield return new WaitForSeconds(sec);
        if (go != null) Destroy(go);
    }
}
