using UnityEngine;
using UnityEngine.UI;

// InGameUI 의 디스코 터널 뒤에 까는 별. 우주 속을 지나는 느낌만 살짝 더한다.
//
// 터널 레이어의 첫 자식이라 링·스포크보다 뒤에 깔린다. 움직이지 않고 제자리에서 천천히
// 반짝이기만 한다 — 터널 자체가 앞으로 빨려 들어가는 움직임이 있으니, 별까지 흐르면 정신없다.
// 밝기는 터널의 intensity 를 그대로 따라서, 터널이 켜지고 꺼질 때 함께 나타나고 사라진다.
public partial class InGameUI
{
    // ── 조정 손잡이 ──
    const int   SPACE_STAR_COUNT  = 22;     // 별 수 — 너무 많으면 터널 링과 섞여 지저분하다
    const int   SPACE_STAR_BRIGHT = 4;      // 그중 크고 밝은 별 수
    const float SPACE_STAR_ALPHA  = 0.85f;  // 가장 밝을 때의 불투명도

    RectTransform _spaceStarRoot;
    Image[]       _spaceStars;
    float[]       _spaceStarBase, _spaceStarSpeed, _spaceStarPhase;

    void BuildTunnelStars(Transform tunnelLayer)
    {
        var rootGo = new GameObject("TunnelSpaceStars");
        rootGo.transform.SetParent(tunnelLayer, false);
        rootGo.transform.SetAsFirstSibling();            // 링·스포크보다 뒤
        _spaceStarRoot = rootGo.AddComponent<RectTransform>();
        _spaceStarRoot.anchorMin = Vector2.zero;
        _spaceStarRoot.anchorMax = Vector2.one;
        _spaceStarRoot.offsetMin = _spaceStarRoot.offsetMax = Vector2.zero;

        var dot = MakeDotSprite(32);
        _spaceStars     = new Image[SPACE_STAR_COUNT];
        _spaceStarBase  = new float[SPACE_STAR_COUNT];
        _spaceStarSpeed = new float[SPACE_STAR_COUNT];
        _spaceStarPhase = new float[SPACE_STAR_COUNT];

        // 배치가 판마다 같아야 한다 — 별자리가 매번 바뀌면 같은 곡이 다른 곳처럼 보인다.
        var rnd  = new System.Random(5);
        float hw = CanvasMetrics.REF_W * 0.5f, hh = CanvasMetrics.HalfHeight;
        for (int i = 0; i < SPACE_STAR_COUNT; i++)
        {
            bool  bright = i < SPACE_STAR_BRIGHT;
            float size   = bright ? Lerp(rnd, 9f, 12f) : Lerp(rnd, 3f, 6f);

            var go = new GameObject("SpaceStar");
            go.transform.SetParent(_spaceStarRoot, false);
            var img = go.AddComponent<Image>();
            img.sprite        = dot;
            img.color         = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Lerp(rnd, -hw, hw), Lerp(rnd, -hh, hh));
            rt.sizeDelta        = new Vector2(size, size);

            _spaceStars[i]     = img;
            _spaceStarBase[i]  = bright ? 1f : Lerp(rnd, 0.45f, 0.85f);
            _spaceStarSpeed[i] = Lerp(rnd, 0.5f, 1.4f);
            _spaceStarPhase[i] = Lerp(rnd, 0f, Mathf.PI * 2f);
        }
    }

    void UpdateTunnelStars(float now, float intensity)
    {
        if (_spaceStars == null) return;
        for (int i = 0; i < _spaceStars.Length; i++)
        {
            float twinkle = 0.6f + 0.4f * Mathf.Sin(now * _spaceStarSpeed[i] + _spaceStarPhase[i]);
            _spaceStars[i].color = new Color(1f, 1f, 1f, SPACE_STAR_ALPHA * _spaceStarBase[i] * twinkle * intensity);
        }
    }

    static float Lerp(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);
}
