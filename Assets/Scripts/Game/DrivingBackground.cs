using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 도시 구간이 끝난 뒤 이어지는 드라이빙 배경.
///   하늘 + 오른쪽에서 왼쪽으로 끝없이 흐르는 건물 실루엣(반투명, 옥상에 단차·첨탑·경사)
///   + 고정된 바닥(도로) + 도로 위 한가운데에 놓인 자동차 도형
///
/// 하늘 교체는 페이드 없이 즉시 이뤄지고, 마지막 전환만 페이드다:
///   ~MORNING_START   노을진 어두운 주황. 건물·바닥·자동차는 검정
///   MORNING_START~   아침 하늘(위쪽 파랑 → 지평선 크림). 나머지는 그대로 검정
///   NIGHT_START~     완전 검은 하늘 + 명암 반전 — 바닥·자동차는 흰색, 건물은 반투명 흰색
///   COLORFUL_START~  하늘·건물 그룹(Scenery)이 COLORFUL_FADE_SEC 동안 엷어지며 아래에 깔린
///                    디스코 셀(알록달록한 사각 타일)이 서서히 드러난다. 색은 Night 그대로 유지하고
///                    알파만 내린다. 흰 바닥과 자동차는 그룹 밖이라 끝까지 남는다.
/// 창문 불빛과 스포트라이트는 이 구간에 없다.
///
/// 도시 배경과 달리 이미지 에셋을 쓰지 않고 전부 절차적으로 그린다. city_light.png는 하늘이
/// 불투명한 남색이라 검게 틴트하면 실루엣이 아니라 사각형이 되고, 이음매 없이 반복 스크롤할 수도 없다.
///
/// 캔버스는 referenceResolution 1080×1920 / matchWidth=0 이라 가로는 항상 1080 단위이고
/// 세로만 화면 비율에 따라 변한다. STRIP_W는 그 1080 보다 넉넉해야 건물이 끊기지 않는다.
/// </summary>
public class DrivingBackground : MonoBehaviour
{
    // 도시가 페이드아웃을 시작하는 순간 도시 "뒤에서" 함께 등장한다.
    // 도시가 천천히 엷어지는 동안 이쪽이 서서히 드러나 크로스페이드가 된다.
    public const double APPEAR_START = CityLightBackground.FADE_OUT_START;

    // ── 조정 손잡이 ──
    // 페이드인은 없다. 처음부터 완전 불투명하게 도시 "뒤에" 깔려 있다가, 도시가 엷어지면서
    // 그대로 드러난다. 여기서도 페이드인을 하면 두 배경이 동시에 반투명해지는 순간이 생겨
    // 그 아래 디스코 셀 레이어가 언뜻 비친다.
    const float GROUND_H       = 180f;    // 바닥(도로) 높이 — city_light.png 아래쪽 검은 띠와 같은 비율
    const float BUILDING_ALPHA = 0.55f;   // 건물 검은색 불투명도 (낮출수록 투명)
    const float SCROLL_SPEED   = 360f;    // 건물이 왼쪽으로 흐르는 속도 (units/sec)
    const float STRIP_W        = 2400f;   // 건물 배치 주기 = 이만큼 흐르면 처음 배치로 되돌아온다
    const float CAR_W          = 340f;
    const float CAR_H          = 170f;
    const float CAR_Y          = GROUND_H - 6f;  // 바퀴가 도로 표면에 살짝 물리도록

    const float BLDG_MIN_W = 130f, BLDG_MAX_W = 300f;
    const float BLDG_MIN_H = 320f, BLDG_MAX_H = 950f;
    const float GAP_MIN    = 35f,  GAP_MAX    = 100f;

    // 페이즈 전환 시각. 하늘 교체(노을→아침→밤)는 페이드 없이 즉시 바뀐다.
    public const double MORNING_START  = 196.6;   // 3분 16.6초 — 노을 → 아침
    public const double NIGHT_START    = 212.6;   // 3분 32.6초 — 아침 → 검은 하늘(명암 반전)
    public const double COLORFUL_START = 229.6;   // 3분 49.6초 — 하늘·건물이 걷히고 디스코 셀이 드러남

    // 마지막 전환만은 페이드다. 하늘·건물이 이 시간에 걸쳐 엷어지며 아래 셀 배경이 드러난다.
    const float COLORFUL_FADE_SEC = 2.5f;   // ── 조정 손잡이: 셀 배경으로 넘어가는 페이드 길이 ──

    // 화면이 검게 덮이기 시작하는 시각. ── 조정 손잡이: 2차 터널 진입 타이밍 ──
    // 이 배경은 sibling 순서상 InGameUI의 검정 배경(_bgImage, sibling 0)보다 위에 깔리므로,
    // 배경만 검게 만들어서는 바닥·자동차가 그대로 남는다. 여기서 루트 CanvasGroup을 같은 램프로
    // 내려 이미 검어진 배경 위로 녹아 없어지게 한다. InGameUI.BLACK2_START가 이 값을 참조하므로
    // 이 숫자 하나만 옮기면 배경 검정·디스코 셀·바닥·자동차가 다 같이 따라온다.
    public const double BLACKOUT_START = 242.0;

    // 완전히 검어져 배경을 통째로 정리하고 2차 터널에 자리를 넘기는 시각.
    public const double DISAPPEAR_AT = 245.2;   // 4분 5.2초

    enum ScenePhase { Sunset, Morning, Night, Colorful }


    static readonly Color SKY_TOP    = new Color(0.10f, 0.05f, 0.08f);  // 노을: 위쪽(어두움)
    static readonly Color SKY_BOTTOM = new Color(0.52f, 0.20f, 0.06f);  // 노을: 지평선 쪽 주황
    static readonly Color MORNING_TOP    = new Color(0.36f, 0.58f, 0.82f);  // 아침: 위쪽 파랑
    static readonly Color MORNING_BOTTOM = new Color(0.88f, 0.86f, 0.76f);  // 아침: 지평선 쪽 옅은 크림
    static readonly Color GROUND_COL = new Color(0.03f, 0.02f, 0.03f);  // 바닥(도로)

    RectTransform _root;
    CanvasGroup   _canvasGroup;
    Sprite        _solidSprite;
    Sprite        _slantSprite;   // 경사진 옥상용 직각삼각형
    BeatTracker   _bt;

    Image      _skyImg, _groundImg, _carImg;
    Sprite     _sunsetSky, _morningSky, _nightSky;
    ScenePhase _phase = ScenePhase.Sunset;

    // 페이즈마다 색을 갈아끼우려고 모아둔다. 한 건물의 조각(본체+옥상)은 색이 같아야 하므로
    // 평평한 리스트가 아니라 건물 단위로 묶어서 들고 있는다.
    readonly List<Image[]> _bldgParts = new List<Image[]>();
    readonly List<Image>   _partBuf   = new List<Image>();   // 건물 하나를 만드는 동안만 쓰는 버퍼

    // 하늘+건물 묶음. Colorful로 넘어갈 때 이 그룹만 페이드아웃해 아래 디스코 셀을 드러낸다.
    // 바닥·자동차는 이 그룹 밖에 있어 그대로 남는다.
    GameObject  _sceneryGo;
    CanvasGroup _sceneryGroup;

    /// Colorful 페이즈부터는 하늘·건물이 걷히며 아래 레이어가 드러나므로 기본 배경이 다시 필요하다.
    /// 페이드가 시작되는 순간부터 false — 아직 하늘이 불투명할 때 미리 아래를 준비시켜야
    /// 엷어지는 동안 셀이 이미 그 자리에 있다.
    public bool CoversScreen => _phase != ScenePhase.Colorful;

    RectTransform[] _bldgRts;
    float[]         _bldgBaseX;   // 스트립 안에서의 원래 x (좌측 끝 기준, 화면 중앙 오프셋 적용됨)
    float           _scrollX;

    public static DrivingBackground Spawn(Canvas parentCanvas, BeatTracker bt)
    {
        var go = new GameObject("DrivingBackground");
        go.transform.SetParent(parentCanvas.transform, false);
        var comp = go.AddComponent<DrivingBackground>();
        comp._bt = bt;
        comp.Build();
        return comp;
    }

    void Build()
    {
        _root = gameObject.AddComponent<RectTransform>();
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;
        // 최종 sibling 순서는 spawn 쪽(InGameUI)에서 지정한다.

        _canvasGroup                = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 1f;   // 페이드인 없음 — 도시 뒤에서 처음부터 불투명
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        _solidSprite = MakeSolidSprite();
        _slantSprite = MakeSlantSprite(64, 64);

        // 하늘·건물은 한 그룹으로 묶어둔다. Colorful 전환 때 이 그룹만 통째로 페이드아웃하면
        // 바닥·자동차는 남은 채 아래 디스코 셀이 서서히 드러난다.
        _sceneryGo = new GameObject("Scenery");
        _sceneryGo.transform.SetParent(_root, false);
        var sceneryRt = _sceneryGo.AddComponent<RectTransform>();
        sceneryRt.anchorMin = Vector2.zero;
        sceneryRt.anchorMax = Vector2.one;
        sceneryRt.offsetMin = Vector2.zero;
        sceneryRt.offsetMax = Vector2.zero;
        _sceneryGroup = _sceneryGo.AddComponent<CanvasGroup>();
        _sceneryGroup.interactable   = false;
        _sceneryGroup.blocksRaycasts = false;

        // 그리는 순서 = 겹치는 순서. 하늘 → 건물 → 바닥 → 자동차.
        BuildSky();
        BuildBuildings();
        BuildGround();
        BuildCar();
    }

    void BuildSky()
    {
        _sunsetSky  = MakeVerticalGradientSprite(SKY_BOTTOM, SKY_TOP, 256);
        _morningSky = MakeVerticalGradientSprite(MORNING_BOTTOM, MORNING_TOP, 256);
        _nightSky   = MakeVerticalGradientSprite(Color.black, Color.black, 2);  // 완전 검정 단색

        var go = new GameObject("Sky");
        go.transform.SetParent(_sceneryGo.transform, false);
        var img = go.AddComponent<Image>();
        img.sprite        = _sunsetSky;
        img.raycastTarget = false;
        _skyImg = img;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 폭 STRIP_W 안에 건물을 빈틈 배치하고, 그 폭을 주기로 순환시켜 무한 반복을 만든다.
    // 경계를 걸치는 건물은 만들지 않아야 이어붙였을 때 잘린 건물이 안 보인다.
    void BuildBuildings()
    {
        var layer = new GameObject("Buildings");
        layer.transform.SetParent(_sceneryGo.transform, false);
        var layerRt = layer.AddComponent<RectTransform>();
        layerRt.anchorMin = Vector2.zero;
        layerRt.anchorMax = Vector2.one;
        layerRt.offsetMin = Vector2.zero;
        layerRt.offsetMax = Vector2.zero;

        var rts = new List<RectTransform>();
        var xs  = new List<float>();
        var col = new Color(0f, 0f, 0f, BUILDING_ALPHA);

        float cursor = 0f;
        while (true)
        {
            float bw = Random.Range(BLDG_MIN_W, BLDG_MAX_W);
            if (cursor + bw > STRIP_W) break;      // 경계를 넘으면 배치 중단
            float bh = Random.Range(BLDG_MIN_H, BLDG_MAX_H);

            // 건물 하나 = 본체 + 옥상 장식을 담는 그룹. 스크롤은 이 그룹만 옮기면 된다.
            var go = new GameObject($"Bldg_{rts.Count}");
            go.transform.SetParent(layer.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);  // 가로는 화면 중앙 기준, 세로는 바닥 기준
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0f, 0f);    // 좌하단 = 위치 기준점
            rt.sizeDelta = new Vector2(bw, bh);

            _bldgParts.Add(BuildSilhouette(rt, bw, bh, col));

            rts.Add(rt);
            xs.Add(cursor - STRIP_W * 0.5f);       // 스트립을 화면 중앙에 걸치도록 이동
            cursor += bw + Random.Range(GAP_MIN, GAP_MAX);
        }

        _bldgRts   = rts.ToArray();
        _bldgBaseX = xs.ToArray();
        ApplyBuildingPositions();
    }

    // 옥상에 단차·첨탑·경사를 얹어 밋밋한 사각형 대신 스카이라인 실루엣을 만든다.
    // 반투명이라 도형끼리 겹치면 그 부분만 진해지므로, 서로 닿기만 하고 겹치지 않게 위로 쌓는다.
    // 한 건물을 이루는 조각들을 반환한다(페이즈가 바뀔 때 건물 단위로 같은 색을 입히기 위해).
    Image[] BuildSilhouette(RectTransform parent, float w, float h, Color col)
    {
        _partBuf.Clear();
        AddPart(parent, 0f, 0f, w, h, col);   // 본체

        switch (Random.Range(0, 4))
        {
            case 0:  // 평평한 옥상
                break;

            case 1:  // 단차: 좁은 블록을 한 단 올림
            {
                float sw = w * Random.Range(0.45f, 0.70f);
                float sh = Random.Range(50f, 130f);
                AddPart(parent, (w - sw) * 0.5f, h, sw, sh, col);
                break;
            }

            case 2:  // 첨탑: 단차 위에 얇은 안테나
            {
                float sw = w * Random.Range(0.40f, 0.60f);
                float sh = Random.Range(40f, 90f);
                AddPart(parent, (w - sw) * 0.5f, h, sw, sh, col);

                float mw = Random.Range(10f, 22f);
                AddPart(parent, (w - mw) * 0.5f, h + sh, mw, Random.Range(90f, 220f), col);
                break;
            }

            default: // 경사진 옥상
            {
                var go = new GameObject("Roof");
                go.transform.SetParent(parent, false);
                var img = go.AddComponent<Image>();
                img.sprite        = _slantSprite;
                img.color         = col;
                img.raycastTarget = false;

                // pivot을 가로 중앙에 둬야 localScale로 좌우 반전해도 제자리에 남는다.
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin        = Vector2.zero;
                rt.anchorMax        = Vector2.zero;
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.sizeDelta        = new Vector2(w, Random.Range(70f, 170f));
                rt.anchoredPosition = new Vector2(w * 0.5f, h);
                if (Random.value < 0.5f) rt.localScale = new Vector3(-1f, 1f, 1f);
                _partBuf.Add(img);
                break;
            }
        }

        return _partBuf.ToArray();
    }

    // 건물 그룹의 좌하단을 원점으로 하는 사각 조각 하나.
    void AddPart(RectTransform parent, float x, float y, float w, float h, Color col)
    {
        var go = new GameObject("Part");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite        = _solidSprite;
        img.color         = col;
        img.raycastTarget = false;
        _partBuf.Add(img);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.zero;
        rt.pivot            = Vector2.zero;
        rt.sizeDelta        = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    void BuildGround()
    {
        var go = new GameObject("Ground");
        go.transform.SetParent(_root, false);
        var img = go.AddComponent<Image>();
        img.sprite        = _solidSprite;
        img.color         = GROUND_COL;
        img.raycastTarget = false;
        _groundImg = img;

        // 가로는 화면 폭에 맞춰 늘어나고(sizeDelta.x = 0), 세로만 GROUND_H로 고정.
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.sizeDelta        = new Vector2(0f, GROUND_H);
        rt.anchoredPosition = Vector2.zero;
    }

    void BuildCar()
    {
        var go = new GameObject("Car");
        go.transform.SetParent(_root, false);
        var img = go.AddComponent<Image>();
        img.sprite        = MakeCarSprite(512, 256);
        img.color         = Color.black;
        img.raycastTarget = false;
        _carImg = img;

        // 가로는 화면 정가운데, 세로는 도로 위에 올려둔다(바닥 기준 앵커 + 아래쪽 pivot).
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.sizeDelta        = new Vector2(CAR_W, CAR_H);
        rt.anchoredPosition = new Vector2(0f, CAR_Y);
    }

    void Update()
    {
        if (_bt == null) return;
        double t = _bt.PlaybackSec;

        // 자기 구간을 벗어나면 스스로 정리한다. 되감기나 트랙 루프로 앞으로 돌아간 경우도 포함.
        // (CityLightBackground와 같은 규칙 — 얼어붙은 채 화면에 남지 않게)
        if (t < APPEAR_START || t >= DISAPPEAR_AT)
        {
            Destroy(gameObject);
            return;
        }

        // 페이즈 즉시 교체. 바뀌는 프레임에만 건드려 매 프레임 그래픽을 더럽히지 않는다.
        // 되감아도 이전 페이즈로 정상 복귀한다.
        ScenePhase want = t >= COLORFUL_START ? ScenePhase.Colorful
                        : t >= NIGHT_START    ? ScenePhase.Night
                        : t >= MORNING_START  ? ScenePhase.Morning
                        :                       ScenePhase.Sunset;
        if (want != _phase) ApplyPhase(want);

        // BLACKOUT_START부터 배경 전체가 엷어진다. 그 아래에는 InGameUI가 같은 램프로 검게 만든
        // _bgImage가 깔려 있어, 바닥·자동차가 검정에 잠기듯 사라진다.
        // (아래 sceneryAlpha와 달리 이건 루트 그룹이라 하늘·건물까지 전부 포함한다.
        //  CanvasGroup은 중첩되면 곱해지므로 두 페이드가 서로를 덮어쓰지 않는다.)
        float rootAlpha = t < BLACKOUT_START
            ? 1f
            : 1f - Mathf.Clamp01((float)((t - BLACKOUT_START) / (DISAPPEAR_AT - BLACKOUT_START)));
        if (!Mathf.Approximately(_canvasGroup.alpha, rootAlpha))
            _canvasGroup.alpha = rootAlpha;

        // Colorful로 넘어가면 하늘·건물만 서서히 사라진다. 바닥·자동차는 그룹 밖이라 그대로 남고,
        // 그 사이 아래 디스코 셀 레이어가 드러난다. 페이드 도중에도 건물은 계속 흘러야 자연스럽다.
        float sceneryAlpha = _phase != ScenePhase.Colorful
            ? 1f
            : 1f - Mathf.Clamp01((float)((t - COLORFUL_START) / COLORFUL_FADE_SEC));

        bool sceneryOn = sceneryAlpha > 0f;
        if (_sceneryGo.activeSelf != sceneryOn) _sceneryGo.SetActive(sceneryOn);
        if (!sceneryOn) return;   // 다 사라진 뒤에는 렌더도 스크롤도 멈춘다
        if (!Mathf.Approximately(_sceneryGroup.alpha, sceneryAlpha))
            _sceneryGroup.alpha = sceneryAlpha;   // 값이 바뀔 때만 — 캔버스를 괜히 더럽히지 않게

        _scrollX -= SCROLL_SPEED * Time.deltaTime;
        if (_scrollX <= -STRIP_W) _scrollX += STRIP_W;   // 오래 돌아도 정밀도가 새지 않게
        ApplyBuildingPositions();
    }

    // Night부터는 명암을 뒤집는다: 검은 하늘 위에 흰 땅·자동차.
    // Colorful은 Night와 같은 색을 유지한 채로 하늘·건물 그룹이 페이드아웃한다(Update가 알파를 몬다).
    // 색을 그대로 두는 게 중요하다 — 사라지는 도중에 색까지 바뀌면 페이드가 아니라 교체로 보인다.
    void ApplyPhase(ScenePhase p)
    {
        _phase = p;

        bool inverted = p == ScenePhase.Night || p == ScenePhase.Colorful;

        Color solid = inverted ? Color.white : Color.black;
        _groundImg.color = inverted ? Color.white : GROUND_COL;
        _carImg.color    = solid;

        _skyImg.sprite = inverted                ? _nightSky
                       : p == ScenePhase.Morning ? _morningSky
                       :                           _sunsetSky;

        var mono = new Color(solid.r, solid.g, solid.b, BUILDING_ALPHA);
        for (int b = 0; b < _bldgParts.Count; b++)
        {
            var parts = _bldgParts[b];
            for (int i = 0; i < parts.Length; i++)
                parts[i].color = mono;
        }
    }

    // STRIP_W를 주기로 감아 배치. 왼쪽으로 빠진 건물이 오른쪽 끝에서 다시 들어온다.
    void ApplyBuildingPositions()
    {
        if (_bldgRts == null) return;
        for (int i = 0; i < _bldgRts.Length; i++)
        {
            float x = Mathf.Repeat(_bldgBaseX[i] + _scrollX + STRIP_W * 0.5f, STRIP_W) - STRIP_W * 0.5f;
            _bldgRts[i].anchoredPosition = new Vector2(x, GROUND_H);
        }
    }

    // ── 스프라이트 생성 ─────────────────────────────────────────
    Sprite MakeSolidSprite()
    {
        var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        var w = new Color32(255, 255, 255, 255);
        tex.SetPixels32(new[] { w, w, w, w });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
    }

    // 아래(지평선) → 위 방향 세로 그라데이션. 가로로는 늘어나므로 폭 2면 충분하다.
    Sprite MakeVerticalGradientSprite(Color bottom, Color top, int h)
    {
        var tex = new Texture2D(2, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var px = new Color32[2 * h];
        for (int y = 0; y < h; y++)
        {
            Color32 c = Color.Lerp(bottom, top, (float)y / (h - 1));
            px[y * 2]     = c;
            px[y * 2 + 1] = c;
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f));
    }

    // 오른쪽으로 낮아지는 직각삼각형(아래변·왼쪽변이 직각, 빗변이 좌상단 → 우하단).
    Sprite MakeSlantSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px  = new Color32[w * h];
        var on  = new Color32(255, 255, 255, 255);
        var off = new Color32(0, 0, 0, 0);

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / (w - 1);
                px[y * w + x] = nx <= 1f - ny ? on : off;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // 간단한 측면 자동차 실루엣: 차체 + 앞유리가 기운 캐빈 + 바퀴 두 개.
    Sprite MakeCarSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px  = new Color32[w * h];
        var on  = new Color32(255, 255, 255, 255);
        var off = new Color32(0, 0, 0, 0);

        float bodyX0 = w * 0.03f, bodyX1 = w * 0.97f;
        float bodyY0 = h * 0.20f, bodyY1 = h * 0.56f;
        float cabX0  = w * 0.26f, cabX1  = w * 0.74f;
        float cabY1  = h * 0.95f;
        float cabSlant = w * 0.10f;                    // 위로 갈수록 캐빈이 좁아짐 = 앞유리 경사
        float wheelR = h * 0.19f, wheelY = h * 0.20f;
        float wheelLX = w * 0.24f, wheelRX = w * 0.76f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool hit = x >= bodyX0 && x <= bodyX1 && y >= bodyY0 && y <= bodyY1;

                if (!hit && y > bodyY1 && y <= cabY1)
                {
                    float k = (y - bodyY1) / (cabY1 - bodyY1);
                    hit = x >= cabX0 + cabSlant * k && x <= cabX1 - cabSlant * k;
                }
                if (!hit)
                    hit = InCircle(x, y, wheelLX, wheelY, wheelR)
                       || InCircle(x, y, wheelRX, wheelY, wheelR);

                px[y * w + x] = hit ? on : off;
            }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    static bool InCircle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    }
}
