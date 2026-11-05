using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// InGameUI 의 스페셜 블럭 등장 연출 — 토글 모드의 스페셜 블럭, 디스코 모드의 무지개 블럭.
//
// 판에 "새로" 생긴 순간을 잡는다. 두 블럭 다 생긴 뒤엔 움직이지 않으므로, RefreshGrid 때마다
// 칸별로 지난번에 특수 블럭이 있었는지와 비교하면 등장만 정확히 걸러진다. 첫 갱신(이어 하기로
// 판을 불러온 직후 등)은 기준만 잡고 연출하지 않는다 — 원래 있던 블럭이 갑자기 터지면 이상하다.
//
// 연출은 얇은 고리 두 개가 블럭에서 퍼져 나가며 사라지는 것뿐이다. 블럭 그림 자체는 건드리지
// 않는다 — 디스코는 블럭 크기를 박자에 맞춰 매 프레임 바꾸고 있어서, 여기서 크기를 만지면
// 서로 덮어쓴다. 흰 막·반짝이·글자는 넣지 않는다.
public partial class InGameUI
{
    // ── 조정 손잡이 ──
    const float SPFX_DUR        = 0.55f;   // 고리 하나가 퍼져서 사라지는 시간
    const float SPFX_GAP        = 0.14f;   // 두 번째 고리가 뒤따르는 간격
    const float SPFX_FROM       = 110f;    // 시작 지름 (칸 크기)
    const float SPFX_TO         = 300f;    // 끝 지름
    const float SPFX_ALPHA      = 0.9f;    // 첫 고리의 시작 불투명도 (둘째는 60%)

    const string SPFX_ROOT = "SpecialAppearFX";  // ClearTransientEffects 가 이 이름으로 치운다

    readonly bool[,] _specialSeen = new bool[GameManager.SIZE, GameManager.SIZE];
    bool   _specialBaseline;
    Sprite _spfxRing;

    static bool IsSpecialVal(int v) => v == GameManager.SPECIAL_BLOCK_VAL || v == GameManager.RAINBOW_BLOCK_VAL;

    void DetectSpecialAppear()
    {
        if (_gm == null || !(ModeSession.IsToggle || ModeSession.IsDisco)) return;

        for (int r = 0; r < GameManager.SIZE; r++)
            for (int c = 0; c < GameManager.SIZE; c++)
            {
                bool now = IsSpecialVal(_gm.Board[r, c]);
                if (now && !_specialSeen[r, c] && _specialBaseline)
                    PlaySpecialAppear(r, c);
                _specialSeen[r, c] = now;
            }
        _specialBaseline = true;
    }

    void PlaySpecialAppear(int r, int c)
    {
        var ov = _blockOverlays[r, c];
        if (ov == null) return;

        if (_spfxRing == null) _spfxRing = MakeRingSprite(192, 0.05f);

        // 토글은 방 색에 따라 반대색 고리 — 흰 방에 흰 고리는 안 보인다. 디스코는 어두운 무대라 흰색.
        Color col = ModeSession.IsToggle && _gm.ToggleCurrentColor == 0
            ? new Color(0.10f, 0.10f, 0.14f) : Color.white;

        // 캔버스 바로 밑에 둔다. 그리드 밑이면 판 밖으로 퍼지는 부분이 트레이·점수에 가린다.
        var rootGo = new GameObject(SPFX_ROOT);
        rootGo.transform.SetParent(_canvas.transform, false);
        var root = rootGo.AddComponent<RectTransform>();
        UIEffects.SubCanvas(rootGo);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.position  = ov.rectTransform.position;

        StartCoroutine(SpecialRing(root, col, 0f,       SPFX_ALPHA));
        StartCoroutine(SpecialRing(root, col, SPFX_GAP, SPFX_ALPHA * 0.6f));
        StartCoroutine(DestroyLater(rootGo, SPFX_GAP + SPFX_DUR + 0.05f));
    }

    IEnumerator SpecialRing(RectTransform root, Color col, float delay, float alpha)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (root == null) yield break;

        var go = new GameObject("Ring");
        go.transform.SetParent(root, false);
        var img = go.AddComponent<Image>();
        img.sprite        = _spfxRing;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        float t = 0f;
        while (t < SPFX_DUR)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / SPFX_DUR);
            float e = 1f - (1f - k) * (1f - k) * (1f - k);      // ease-out: 빨리 퍼지다 느려진다
            float d = Mathf.Lerp(SPFX_FROM, SPFX_TO, e);
            rt.sizeDelta = new Vector2(d, d);
            img.color    = new Color(col.r, col.g, col.b, alpha * (1f - k) * (1f - k));
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
