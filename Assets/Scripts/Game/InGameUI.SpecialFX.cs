using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    // ── 무지개 블럭 표지 (디스코) ──
    // 무지개 블럭은 판의 다른 블록과 같은 박자로 튀어서 눈에 덜 띈다. 그래서 남아 있는 동안
    // 두 박마다 등장 때와 같은 고리를 하나씩, 더 작고 옅게 내보낸다 — "아직 여기 있다"는 표시.
    const int   SPFX_BEACON_BEATS = 2;      // ── 조정 손잡이: 몇 박마다 고리를 낼지 ──
    const float SPFX_BEACON_TO    = 210f;   // 고리가 퍼지는 끝 지름 (등장 때는 300)
    const float SPFX_BEACON_ALPHA = 0.5f;   // 고리 시작 불투명도 (등장 때는 0.9)
    const float SPFX_BEACON_DUR   = 0.6f;
    long _lastBeaconBeat = -1;

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

    // 표지 고리는 1초마다 무지개 블럭 수만큼 생긴다. 그때마다 오브젝트(하위 캔버스 포함)를
    // 만들었다 지우지 않도록, 늘 있는 층 하나(_beaconRoot)에 풀(TakeFx/ReturnFx)에서 빌린 고리를
    // 얹고, 움직임도 코루틴 없이 여기서 한꺼번에 돌린다.
    struct Beacon { public Image img; public float t; }
    readonly List<Beacon> _beacons = new List<Beacon>();
    RectTransform _beaconRoot;

    /// <summary>디스코: 박자를 보고 남아 있는 무지개 블럭마다 표지 고리를 하나씩 낸다. 매 프레임 불린다.</summary>
    void UpdateRainbowBeacons(double playbackSec)
    {
        // 떠 있는 고리 움직이기 — 퍼지며 옅어진다(등장 고리와 같은 곡선)
        float dt = Time.deltaTime;
        for (int i = _beacons.Count - 1; i >= 0; i--)
        {
            var b = _beacons[i];
            b.t += dt;
            if (b.img == null || b.t >= SPFX_BEACON_DUR)
            {
                ReturnFx(b.img);
                _beacons.RemoveAt(i);
                continue;
            }
            float k = b.t / SPFX_BEACON_DUR;
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            float d = Mathf.Lerp(SPFX_FROM, SPFX_BEACON_TO, e);
            b.img.rectTransform.sizeDelta = new Vector2(d, d);
            b.img.color = new Color(1f, 1f, 1f, SPFX_BEACON_ALPHA * (1f - k) * (1f - k));
            _beacons[i] = b;
        }

        long beat = (long)System.Math.Floor(playbackSec * BeatTracker.BPM / 60.0);
        if (beat == _lastBeaconBeat) return;
        _lastBeaconBeat = beat;
        if (beat % SPFX_BEACON_BEATS != 0) return;

        for (int r = 0; r < GameManager.SIZE; r++)
            for (int c = 0; c < GameManager.SIZE; c++)
                if (_gm.Board[r, c] == GameManager.RAINBOW_BLOCK_VAL)
                    PlayRainbowBeacon(r, c);
    }

    void PlayRainbowBeacon(int r, int c)
    {
        var ov = _blockOverlays[r, c];
        if (ov == null) return;
        if (_spfxRing == null) _spfxRing = MakeRingSprite(192, 0.05f);

        if (_beaconRoot == null)
        {
            // 캔버스 바로 밑에 한 번만 세운다. 그리드 밑이면 판 밖으로 퍼지는 부분이 가린다.
            var go = new GameObject("RainbowBeacons");
            go.transform.SetParent(_canvas.transform, false);
            _beaconRoot = go.AddComponent<RectTransform>();
            UIEffects.SubCanvas(go);   // 매 프레임 움직인다 — 판과 따로 묶는다
            _beaconRoot.anchorMin = _beaconRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _beaconRoot.sizeDelta = Vector2.zero;
        }

        var img = TakeFx(_beaconRoot);
        img.sprite = _spfxRing;
        img.color  = new Color(1f, 1f, 1f, SPFX_BEACON_ALPHA);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(SPFX_FROM, SPFX_FROM);
        rt.position  = ov.rectTransform.position;
        _beacons.Add(new Beacon { img = img, t = 0f });
    }

    IEnumerator SpecialRing(RectTransform root, Color col, float delay, float alpha)
        => SpecialRing(root, col, delay, alpha, SPFX_TO, SPFX_DUR);

    IEnumerator SpecialRing(RectTransform root, Color col, float delay, float alpha, float toSize, float dur)
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
        while (t < dur)
        {
            if (rt == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - k) * (1f - k) * (1f - k);      // ease-out: 빨리 퍼지다 느려진다
            float d = Mathf.Lerp(SPFX_FROM, toSize, e);
            rt.sizeDelta = new Vector2(d, d);
            img.color    = new Color(col.r, col.g, col.b, alpha * (1f - k) * (1f - k));
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
