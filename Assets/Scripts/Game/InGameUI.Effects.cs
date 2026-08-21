using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// InGameUI 의 연출 부분 — 줄 클리어 이펙트, 콤보 문구, 무지개 폭발, 화면 흔들림,
// 아이스 슬라이드 체인. 보드 상태는 GameManager 가 이미 확정한 뒤이고,
// 여기서는 그 결과를 눈에 보이게 만드는 일만 한다.
public partial class InGameUI
{

    // ── 연속 클리어 콤보 안내 ──────────────────────────────────
    // 줄을 지우는 순간 화면 가운데 위쪽에 커졌다 떠오르며 사라지는 문구. 소리는 붙이지 않는다 —
    // 클리어음·배치음이 이미 울리는 자리라 하나 더 얹으면 소리가 뭉개진다.
    void ShowComboPopup(int combo)
    {
        if (combo < COMBO_MIN) return;

        // 한 세트 안에서 여러 번 지우면 같은 값으로 계속 불린다(무지개 연쇄는 0.15초 간격).
        // 그때마다 다시 띄우면 같은 숫자가 제자리에서 깜빡인다. 값이 그대로면 두던 걸 둔다.
        if (_comboPopup != null && _comboPopupValue == combo) return;
        if (_comboPopup != null) Destroy(_comboPopup);

        // 색·크기 단계는 COMBO_MAX까지. 그보다 높은 콤보는 마지막 단계를 계속 쓴다.
        int tier = Mathf.Min(combo, COMBO_MAX) - COMBO_MIN;

        // 상한에 닿으면 숫자 대신 MAX!. 그 위로는 보너스도 안 오르므로 숫자만 커지면 거짓말이 된다.
        bool  atMax = combo >= COMBO_MAX;
        Color color = atMax ? COMBO_COLOR_MAX : COMBO_COLOR;
        int   size  = COMBO_FONT_BASE + tier * COMBO_FONT_STEP;

        // 루트는 그래픽 없는 빈 컨테이너다. UGUI는 부모를 먼저 그리고 자식을 그 위에 얹으므로,
        // 배경판을 글자 뒤에 두려면 글자도 자식이어야 한다(루트에 Text를 달면 판이 위로 온다).
        // 자리는 고정이다 — 떠오르는 움직임 없이 그 자리에서 커졌다 옅어지기만 한다.
        var go = new GameObject("ComboPopup");
        go.transform.SetParent(_canvas.transform, false);
        go.transform.SetAsLastSibling();
        _comboPopup      = go;
        _comboPopupValue = combo;

        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(900, 220);
        rt.anchoredPosition = COMBO_POS;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var txt           = labelGo.AddComponent<Text>();
        txt.font          = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
        txt.fontSize      = size;   // 콤보가 커질수록 큼직하게
        txt.fontStyle     = FontStyle.BoldAndItalic;
        txt.alignment     = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        txt.text          = atMax ? "MAX!" : $"x{combo}";
        txt.color         = color;

        var labelRt              = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin        = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
        labelRt.pivot            = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta        = new Vector2(900, 220);
        labelRt.anchoredPosition = Vector2.zero;

        // 배경판. 어떤 배경 위에서도 글자가 읽히게 검정을 깐다.
        // 글자 실제 폭(preferredWidth)에 여백을 더해 감싼다 — x2와 MAX!는 폭이 꽤 다르다.
        float plateW = txt.preferredWidth + COMBO_PLATE_PAD_X * 2f;
        float plateH = size * 1.16f + COMBO_PLATE_PAD_Y * 2f;

        var plateGo = new GameObject("Plate");
        plateGo.transform.SetParent(go.transform, false);
        plateGo.transform.SetAsFirstSibling();   // 글자·선보다 먼저 = 뒤에 깔린다
        var plateImg           = plateGo.AddComponent<Image>();
        plateImg.sprite        = _spr110;   // 9-slice 보더가 있어 늘려도 모서리가 유지된다
        plateImg.type          = Image.Type.Sliced;
        plateImg.color         = new Color(0.02f, 0.02f, 0.04f, COMBO_PLATE_ALPHA);
        plateImg.raycastTarget = false;

        var plateRt              = plateGo.GetComponent<RectTransform>();
        plateRt.anchorMin        = plateRt.anchorMax = new Vector2(0.5f, 0.5f);
        plateRt.pivot            = new Vector2(0.5f, 0.5f);
        plateRt.sizeDelta        = new Vector2(plateW, plateH);
        plateRt.anchoredPosition = Vector2.zero;   // 글자가 가운데 정렬이라 판도 중앙

        // 알파는 CanvasGroup 하나로 몬다. 판과 글자에 따로 걸면 둘이 어긋난다.
        var cg   = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        StartCoroutine(ComboPopupRoutine(rt, cg));
    }

    IEnumerator ComboPopupRoutine(RectTransform rt, CanvasGroup cg)
    {
        float elapsed = 0f;

        while (elapsed < COMBO_SEC)
        {
            if (rt == null) yield break;   // 다음 콤보가 이미 치웠다
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / COMBO_SEC);

            // 튀어나오듯 1.35배까지 커졌다가 제자리로. 앞뒤 절반씩 나눠 쓴다.
            float half  = COMBO_POP_SEC * 0.5f;
            float scale = elapsed < half
                ? Mathf.Lerp(0.55f, 1.35f, elapsed / half)
                : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((elapsed - half) / half));
            rt.localScale = Vector3.one * scale;

            cg.alpha = t < COMBO_HOLD ? 1f : 1f - (t - COMBO_HOLD) / (1f - COMBO_HOLD);

            yield return null;
        }

        if (rt != null)
        {
            if (_comboPopup == rt.gameObject) _comboPopup = null;
            Destroy(rt.gameObject);
        }
    }

    // ── 디스코 무지개 블럭: 탭 → 가로세로 클리어 + 파티클 폭발 ──
    // 보드 반영(ActivateRainbowBlock)이 OnStateChanged를 거쳐 기존 줄 클리어 연출을 띄우고,
    // 이 코루틴은 그 위에 화면 플래시 / 링 / 십자 빔 / 파티클을 얹는다.
    //
    // FX 루트는 그리드가 아니라 캔버스 최상단 자식으로 단다. 그리드 안에 두면 나중에 생성된
    // 캔버스 자식(트레이·점수·흰 플래시 오버레이)에 가려질 수 있어서, 무엇에도 안 가리게 못박는다.
    // 좌표는 그리드 로컬 → 캔버스로 옮겨야 하는데, 둘 다 화면 중앙 앵커라 그리드 오프셋만 더하면 된다.
    IEnumerator PlayRainbowBurst(int row, int col)
    {
        Vector2 gridOff = _gridRt != null ? _gridRt.anchoredPosition : Vector2.zero;
        Vector2 origin  = gridOff + new Vector2(-420f + col * 120f, 420f - row * 120f);

        // 이 호출 안에서 OnStateChanged → PlayClearEffect까지 동기로 돌아간다.
        // 그 사이에만 표시를 세워 두면 음표 파티클이 자기 폭발과 겹치지 않는다.
        _rainbowActivating = true;
        bool activated     = _gm.ActivateRainbowBlock(row, col);
        _rainbowActivating = false;
        if (!activated) yield break;

        // 클리어음은 위 호출 안에서 _rainbowActivating 표시를 보고 건너뛴다(FlashAndFade).
        // 그래서 여기서 우는 발동음 하나만 들린다.
        if (_audioSource != null && _sfxRainbow != null)
            _audioSource.PlayOneShot(_sfxRainbow);

        // 화면 흔들림은 무지개 블럭 발동에만 붙인다. 일반 줄 클리어는 자주 일어나서
        // 매번 흔들면 금방 피로해지고, 특수 블럭의 "한 방"이 특별해 보이지 않는다.
        // (ActivateRainbowBlock이 이미 OnStateChanged를 거쳐 줄 클리어 연출을 띄운 뒤다)
        StartBurstShake();

        var fxRoot = new GameObject("RainbowBurstFX");
        fxRoot.transform.SetParent(_canvas.transform, false);
        var rootRt       = fxRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        fxRoot.transform.SetAsLastSibling();

        // 1) 화면 전체 순간 플래시 — "터졌다"는 신호를 가장 먼저 준다
        var flashGo = new GameObject("BurstFlash");
        flashGo.transform.SetParent(fxRoot.transform, false);
        var flashImg           = flashGo.AddComponent<Image>();
        flashImg.color         = new Color(1f, 1f, 1f, 0f);
        flashImg.raycastTarget = false;
        var flashRt      = flashGo.GetComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;

        // 2) 십자 빔 — 가로세로가 통째로 날아간다는 걸 보여 준다
        var beamH = MakeBurstBeam(fxRoot.transform, new Vector2(gridOff.x, origin.y));
        var beamV = MakeBurstBeam(fxRoot.transform, new Vector2(origin.x, gridOff.y));

        // 3) 충격파 링 2개. 색을 돌리면 촌스러워져서 흰색으로만 얇게 빠르게 지나가게 한다.
        //    색은 파티클이 충분히 내고 있으므로 링은 "확 퍼지는 압력"만 담당한다.
        var ringRts  = new RectTransform[RING_COUNT];
        var ringImgs = new Image[RING_COUNT];
        if (_burstRingSprite != null)
            for (int i = 0; i < RING_COUNT; i++)
            {
                var ringGo = new GameObject($"BurstRing_{i}");
                ringGo.transform.SetParent(fxRoot.transform, false);
                var rImg              = ringGo.AddComponent<Image>();
                rImg.sprite           = _burstRingSprite;
                rImg.raycastTarget    = false;
                rImg.color            = Color.clear;
                var rRt               = ringGo.GetComponent<RectTransform>();
                rRt.anchorMin         = rRt.anchorMax = new Vector2(0.5f, 0.5f);
                rRt.pivot             = new Vector2(0.5f, 0.5f);
                rRt.anchoredPosition  = origin;
                ringRts[i]  = rRt;
                ringImgs[i] = rImg;
            }

        // 4) 클리어되는 칸마다 터지는 잔파티클 — 중심에서 바깥으로 파도처럼 번진다
        for (int c = 0; c < GameManager.SIZE; c++)
            StartCoroutine(RainbowCellPop(fxRoot.transform,
                gridOff + new Vector2(-420f + c * 120f, 420f - row * 120f),
                Mathf.Abs(c - col) * 0.035f));
        for (int r = 0; r < GameManager.SIZE; r++)
            if (r != row)
                StartCoroutine(RainbowCellPop(fxRoot.transform,
                    gridOff + new Vector2(-420f + col * 120f, 420f - r * 120f),
                    Mathf.Abs(r - row) * 0.035f));

        // 5) 파티클: 원점에서 사방으로 튀며 중력에 끌려 흩어진다
        var rts   = new RectTransform[RAINBOW_BURST_COUNT];
        var imgs  = new Image[RAINBOW_BURST_COUNT];
        var pos   = new Vector2[RAINBOW_BURST_COUNT];
        var vel   = new Vector2[RAINBOW_BURST_COUNT];
        var cols  = new Color[RAINBOW_BURST_COUNT];
        var size0 = new float[RAINBOW_BURST_COUNT];
        var aspect= new float[RAINBOW_BURST_COUNT];
        var spin  = new float[RAINBOW_BURST_COUNT];
        var life  = new float[RAINBOW_BURST_COUNT];

        var particleSpr = _burstChipSprite != null ? _burstChipSprite : _spr110;

        for (int i = 0; i < RAINBOW_BURST_COUNT; i++)
        {
            var go = new GameObject($"burst_{i}");
            go.transform.SetParent(fxRoot.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite        = particleSpr;
            img.raycastTarget = false;

            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            // 색상환을 고르게 나눠 가지되 살짝 흔들어 줄 세운 티를 없앤다
            float hue = (i / (float)RAINBOW_BURST_COUNT + Random.Range(-0.04f, 0.04f) + 1f) % 1f;
            float ang = i * (Mathf.PI * 2f / RAINBOW_BURST_COUNT) + Random.Range(-0.3f, 0.3f);
            float spd = Random.Range(700f, 2100f);

            rts[i]   = rt;
            imgs[i]  = img;
            pos[i]   = origin;
            vel[i]   = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
            cols[i]   = Color.HSVToRGB(hue, 0.70f, 1f);
            size0[i]  = Random.Range(18f, 40f);          // 작은 색종이 조각
            aspect[i] = Random.Range(0.5f, 1f);           // 살짝 납작하게 → 조각처럼 보임
            spin[i]   = Random.Range(-540f, 540f);
            life[i]   = RAINBOW_BURST_SEC * Random.Range(0.6f, 1f);

            rt.anchoredPosition = origin;
            rt.sizeDelta        = new Vector2(size0[i], size0[i] * aspect[i]);
            img.color           = cols[i];
        }

        const float GRAVITY = 1500f;   // px/s² — 살짝 떨어지며 흩어지는 느낌
        const float DRAG    = 2.6f;    // 초기 폭발 속도를 빠르게 죽인다

        float elapsed = 0f;
        while (elapsed < RAINBOW_BURST_SEC)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            for (int i = 0; i < RAINBOW_BURST_COUNT; i++)
            {
                if (imgs[i] == null) continue;
                float t = elapsed / life[i];
                if (t >= 1f)
                {
                    imgs[i].color = Color.clear;
                    continue;
                }

                vel[i] *= Mathf.Max(0f, 1f - DRAG * dt);
                vel[i]  = new Vector2(vel[i].x, vel[i].y - GRAVITY * dt);
                pos[i] += vel[i] * dt;

                rts[i].anchoredPosition = pos[i];
                rts[i].localRotation    = Quaternion.Euler(0f, 0f, spin[i] * elapsed);

                // 조각은 크기를 크게 흔들지 않는다. 튀어나올 때만 살짝 커지고 이후 유지 —
                // 색종이 조각은 부풀었다 줄어들면 오히려 어색하다.
                float scale = t < 0.12f ? Mathf.Lerp(0.6f, 1f, t / 0.12f) : 1f;
                float sz    = size0[i] * scale;
                rts[i].sizeDelta = new Vector2(sz, sz * aspect[i]);

                float alpha = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
                imgs[i].color = new Color(cols[i].r, cols[i].g, cols[i].b, Mathf.Clamp01(alpha));
            }

            // 화면 플래시: 0.18초 안에 확 밝아졌다가 꺼진다
            float fl = Mathf.Clamp01(elapsed / 0.18f);
            flashImg.color = new Color(1f, 1f, 1f, (1f - fl) * 0.55f);

            // 십자 빔: 0.3초 동안 그리드 폭까지 뻗으며 옅어진다
            float bt = Mathf.Clamp01(elapsed / 0.30f);
            float bw = Mathf.Lerp(0f, 980f, bt * (2f - bt));   // ease-out
            float ba = (1f - bt) * 0.9f;
            beamH.rt.sizeDelta = new Vector2(bw, 118f);
            beamH.img.color    = new Color(1f, 1f, 1f, ba);
            beamV.rt.sizeDelta = new Vector2(118f, bw);
            beamV.img.color    = new Color(1f, 1f, 1f, ba);

            // 충격파 링: 흰색으로 얇게, 0.08초 시간차를 두고 빠르게 지나간다.
            // 알파를 (1-t)³로 떨어뜨려 초반에만 살짝 보이고 금방 사라지게 한다.
            for (int i = 0; i < RING_COUNT; i++)
            {
                if (ringRts[i] == null) continue;
                float rt01 = (elapsed - i * 0.08f) / 0.38f;
                if (rt01 < 0f || rt01 > 1f) { ringImgs[i].color = Color.clear; continue; }
                float sz  = Mathf.Lerp(120f, 980f, rt01 * (2f - rt01));   // ease-out
                float fade = (1f - rt01) * (1f - rt01) * (1f - rt01);
                ringRts[i].sizeDelta = new Vector2(sz, sz);
                ringImgs[i].color    = new Color(1f, 1f, 1f, fade * 0.5f);
            }

            yield return null;
        }

        Destroy(fxRoot);
    }

    // 십자 빔 한 줄 (가로/세로 공용). 크기는 호출 측이 매 프레임 늘린다.
    (RectTransform rt, Image img) MakeBurstBeam(Transform parent, Vector2 pos)
    {
        var go = new GameObject("BurstBeam");
        go.transform.SetParent(parent, false);
        var img           = go.AddComponent<Image>();
        img.sprite        = _spr110;
        img.type          = Image.Type.Sliced;
        img.color         = Color.clear;
        img.raycastTarget = false;

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = Vector2.zero;
        return (rt, img);
    }

    // 클리어되는 칸 하나에서 튀는 작은 조각 몇 개. delay만큼 늦게 터져 파도처럼 번진다.
    IEnumerator RainbowCellPop(Transform parent, Vector2 pos, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (parent == null) yield break;

        const int   N   = 5;
        const float DUR = 0.4f;

        var rts  = new RectTransform[N];
        var imgs = new Image[N];
        var vel  = new Vector2[N];
        var cols = new Color[N];
        var cur  = new Vector2[N];
        var sz0  = new float[N];

        var spr = _burstChipSprite != null ? _burstChipSprite : _spr110;

        for (int i = 0; i < N; i++)
        {
            var go = new GameObject("pop");
            go.transform.SetParent(parent, false);
            var img           = go.AddComponent<Image>();
            img.sprite        = spr;
            img.raycastTarget = false;

            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            float ang = Random.Range(0f, Mathf.PI * 2f);
            rts[i]  = rt;
            imgs[i] = img;
            cur[i]  = pos;
            vel[i]  = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(180f, 520f);
            cols[i] = Color.HSVToRGB(Random.value, 0.6f, 1f);
            sz0[i]  = Random.Range(14f, 26f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(sz0[i], sz0[i] * Random.Range(0.5f, 1f));
            img.color           = cols[i];
        }

        float t = 0f;
        while (t < DUR)
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = t / DUR;

            for (int i = 0; i < N; i++)
            {
                if (imgs[i] == null) continue;
                vel[i] *= Mathf.Max(0f, 1f - 3.2f * dt);
                cur[i] += vel[i] * dt;
                rts[i].anchoredPosition = cur[i];

                imgs[i].color = new Color(cols[i].r, cols[i].g, cols[i].b, 1f - k);
            }
            yield return null;
        }
    }

    // 무지개 블럭이 살아있는 동안 심장박동처럼 맥동시킨다.
    // RefreshGrid는 드래그 중 매 프레임 불려 스케일을 덮어쓸 수 있어 여기서만 건드린다.
    void UpdateRainbowBlocks(float now)
    {
        if (_gm == null || _blockOverlays[0, 0] == null) return;

        // 크기와 밝기를 같이 흔들어 "눌러 달라"는 신호를 준다
        float wave  = Mathf.Sin(now * 7f);
        float pulse = 1f + 0.16f * wave;
        var   tint  = Color.Lerp(new Color(0.80f, 0.80f, 0.80f), Color.white, wave * 0.5f + 0.5f);

        for (int r = 0; r < GameManager.SIZE; r++)
            for (int c = 0; c < GameManager.SIZE; c++)
            {
                var img = _blockOverlays[r, c];
                if (img == null) continue;

                if (_gm.Board[r, c] == GameManager.RAINBOW_BLOCK_VAL)
                {
                    img.rectTransform.localScale = Vector3.one * pulse;
                    img.color = tint;
                }
                else if (img.rectTransform.localScale != Vector3.one)
                {
                    // 무지개 블럭이 사라진 칸의 스케일을 되돌린다 (색은 RefreshGrid가 관리)
                    img.rectTransform.localScale = Vector3.one;
                }
            }
    }

    // 번쩍임(80ms) → 페이드아웃(300ms) → 그리드 갱신 → [아이스] 슬라이드 체인
    // ── 디스코 모드: 무지개 블럭 발동 화면 흔들림 ──────────────────
    // Overlay 캔버스의 RectTransform은 캔버스 시스템이 매 프레임 덮어쓰므로 루트를 통째로 흔들 수 없다.
    // 대신 배경 위에 얹힌 "내용물"만 같은 오프셋으로 민다. 전체화면을 채우는 배경·터널·스팟 레이어는
    // 일부러 제외했다 — 화면을 꽉 채우는 레이어를 흔들면 밀린 쪽 가장자리에 빈 띠가 드러난다.
    void CollectShakeTargets()
    {
        var candidates = new RectTransform[]
        {
            _gridRt, _trayRt, _heartRootRt,
            _scoreText     != null ? _scoreText.rectTransform     : null,
            _highScoreText != null ? _highScoreText.rectTransform : null,
        };

        int n = 0;
        for (int i = 0; i < candidates.Length; i++) if (candidates[i] != null) n++;

        _shakeTargets = new RectTransform[n];
        _shakeHome    = new Vector2[n];
        int k = 0;
        for (int i = 0; i < candidates.Length; i++)
            if (candidates[i] != null)
            {
                _shakeTargets[k] = candidates[i];
                // 제자리는 여기서 한 번만 기록한다. 이 다섯 개는 빌드 후 anchoredPosition이
                // 바뀌지 않으므로, 흔들림이 겹쳐 들어와도 복귀 지점이 흔들린 좌표로 밀리지 않는다.
                _shakeHome[k]    = candidates[i].anchoredPosition;
                k++;
            }
    }

    void StartBurstShake()
    {
        if (_shakeTargets == null || _shakeTargets.Length == 0) return;

        // 무지개 블럭 두 개를 연달아 터뜨리면 이전 흔들림을 끊고 새로 시작한다.
        // 코루틴을 하나 더 얹으면 둘이 같은 anchoredPosition을 매 프레임 서로 덮어써서
        // 먼저 끝난 쪽이 제자리로 돌려놔도 남은 쪽이 다시 밀어버린다.
        if (_shakeCo != null) StopCoroutine(_shakeCo);
        _shakeCo = StartCoroutine(BurstShake());
    }

    IEnumerator BurstShake()
    {
        float elapsed = 0f;
        while (elapsed < SHAKE_SEC)
        {
            elapsed += Time.deltaTime;

            // 진폭은 제곱으로 잦아들어 초반만 세게 치고 금방 가라앉는다.
            float decay = 1f - Mathf.Clamp01(elapsed / SHAKE_SEC);
            float amp   = SHAKE_AMP * decay * decay;

            // 매 프레임 난수로 방향을 뽑으면 지글거리기만 해서 "충격"으로 읽히지 않는다.
            // 주기가 서로 안 맞는 사인 두 개를 쓰면 x/y가 같은 리듬을 반복하지 않아
            // 짧은 시간에도 흔들림이 규칙적으로 보이지 않는다. 세로는 60%만 흔든다.
            var off = new Vector2(
                Mathf.Sin(elapsed * SHAKE_FREQ) * amp,
                Mathf.Sin(elapsed * SHAKE_FREQ * 0.73f + 1.3f) * amp * 0.6f);

            for (int i = 0; i < _shakeTargets.Length; i++)
                if (_shakeTargets[i] != null)
                    _shakeTargets[i].anchoredPosition = _shakeHome[i] + off;

            yield return null;
        }

        for (int i = 0; i < _shakeTargets.Length; i++)
            if (_shakeTargets[i] != null)
                _shakeTargets[i].anchoredPosition = _shakeHome[i];

        _shakeCo = null;
    }

    IEnumerator PlayClearEffect()
    {
        var cells = CollectClearedCells(_gm.LastClearedRows, _gm.LastClearedCols);

        // 디스코 일반 클리어에만 음표를 남긴다. 무지개 블럭 발동은 자체 파티클이 있어 제외.
        // 음표는 블록이 사라진 뒤에도 한참 남아 있으므로 기다리지 않고 따로 돌린다.
        if (ModeSession.IsDisco && !_rainbowActivating)
            StartCoroutine(PlayClearNotes(cells));

        yield return StartCoroutine(FlashAndFade(cells));
        RefreshGrid();

        if (ModeSession.IsIce)
            yield return StartCoroutine(IceSlideAndChain());
    }

    // ── 디스코 일반 클리어: 비워진 칸에 남는 음표 ────────────────────
    // 지워진 칸 자리에 음표가 그대로 떠서 잠깐 머물다 옅어진다.
    //
    // FX 루트는 무지개 폭발과 같은 이유로 캔버스 최상단 자식에 단다 — 그리드 아래에 두면
    // 나중에 생성된 캔버스 자식(트레이·점수)에 가려지고, 음표는 그리드 위로 올라가므로 특히 겹친다.
    // 좌표는 그리드 로컬 → 캔버스인데 둘 다 화면 중앙 앵커라 그리드 오프셋만 더하면 된다.
    // 지워진 칸마다 음표를 하나 남긴다. 칸 자리에 그대로 떠서 옅어지므로 격자를 안 벗어난다.
    // 셀 목록은 다음 클리어에 덮어써지는 LastClearedRows/Cols에서 이미 뽑아 온 것을 받는다.
    IEnumerator PlayClearNotes(System.Collections.Generic.HashSet<(int r, int c)> cells)
    {
        if (_canvas == null || _gm == null || cells == null || cells.Count == 0) yield break;

        // 줄에 속해 있어도 살아남는 칸(무지개 블럭)에는 음표를 얹지 않는다 — 비지 않았으니까.
        // 보드는 ClearFullLines에서 이미 갱신된 뒤라 지금 값이 맞다.
        var spots = new System.Collections.Generic.List<(int r, int c)>(cells.Count);
        foreach (var (r, c) in cells)
            if (_gm.Board[r, c] == 0) spots.Add((r, c));
        if (spots.Count == 0) yield break;

        Vector2 gridOff = _gridRt != null ? _gridRt.anchoredPosition : Vector2.zero;
        int n = spots.Count;

        var root = new GameObject("ClearNoteFX");
        root.transform.SetParent(_canvas.transform, false);
        var rootRt       = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        root.transform.SetAsLastSibling();

        var txts   = new Text[n];
        var delays = new float[n];

        // 글자는 칸마다 따로 뽑지 않고 번갈아 넣는다. 독립으로 뽑으면 한 줄이 전부
        // 같은 글자로 나오는 경우가 생겨 두 종류가 보이지 않는다.
        // 시작 글자만 무작위로 굴려 매번 같은 배열(♪♬♪♬)로 고정되지도 않게 한다.
        int glyphOffset = Random.Range(0, NOTE_GLYPHS.Length);

        for (int i = 0; i < n; i++)
        {
            var (r, c) = spots[i];

            var go = new GameObject($"Note_{r}_{c}");
            go.transform.SetParent(root.transform, false);

            var t           = go.AddComponent<Text>();
            t.font          = Font4();
            t.fontSize      = NOTE_SIZE;
            t.alignment     = TextAnchor.MiddleCenter;
            t.text          = NOTE_GLYPHS[(i + glyphOffset) % NOTE_GLYPHS.Length];
            t.color         = new Color(1f, 1f, 1f, 0f);
            t.raycastTarget = false;

            // 셀과 같은 자리, 셀 크기(110)에 맞춘 칸. 그리드가 (0, 80)만큼 올라가 있으므로 더한다.
            var rt              = go.GetComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(110f, 110f);
            rt.anchoredPosition = gridOff + new Vector2(-420f + c * 120f, 420f - r * 120f);

            txts[i]   = t;
            delays[i] = Random.Range(0f, NOTE_STAGGER);   // 한꺼번에 켜지지 않게 조금씩 어긋내기
        }

        float elapsed = 0f;
        while (elapsed < NOTE_STAGGER + NOTE_SEC)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Clamp01((elapsed - delays[i]) / NOTE_SEC);

                // 켜지고 → NOTE_HOLD까지 그대로 → 남은 구간에 옅어진다.
                // 가장 진할 때도 NOTE_ALPHA라 뒤가 훤히 비쳐 잔상처럼 읽힌다.
                float a = t < 0.10f ? t / 0.10f
                        : t < NOTE_HOLD ? 1f
                        : 1f - (t - NOTE_HOLD) / (1f - NOTE_HOLD);
                txts[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(a) * NOTE_ALPHA);
            }
            yield return null;
        }

        Destroy(root);
    }

    System.Collections.Generic.HashSet<(int r, int c)> CollectClearedCells(
        System.Collections.Generic.List<int> rows,
        System.Collections.Generic.List<int> cols)
    {
        var cells = new System.Collections.Generic.HashSet<(int r, int c)>();
        foreach (int r in rows) for (int c = 0; c < GameManager.SIZE; c++) cells.Add((r, c));
        foreach (int c in cols) for (int r = 0; r < GameManager.SIZE; r++) cells.Add((r, c));
        return cells;
    }

    // 페이드아웃
    IEnumerator FlashAndFade(System.Collections.Generic.HashSet<(int r, int c)> cells)
    {
        // 무지개 블럭 발동으로 들어온 클리어는 발동음(disco_special) 하나만 울린다.
        // 클리어음까지 겹치면 두 소리가 뭉개진다. (PlayRainbowBurst가 표시를 세워 준다)
        if (_audioSource != null && _audioSource.clip != null && !_rainbowActivating)
            _audioSource.Play();

        float elapsed = 0f, duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            foreach (var (r, c) in cells)
                // 줄에 포함됐어도 살아남는 칸(디스코 무지개 블럭)은 옅어지면 안 된다.
                // 다른 모드는 클리어된 칸이 전부 0이라 동작이 그대로다.
                if (_gm.Board[r, c] == 0)
                    _blockOverlays[r, c].color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }
    }

    // ── 토글 모드: 반전 플래시 + 리플 수축 소멸 ──────────────────
    // 임시 오브젝트로 애니메이션 → 보드/입력은 즉시 갱신
    IEnumerator PlayToggleClearEffect()
    {
        if (_audioSource != null && _audioSource.clip != null)
            _audioSource.Play();

        var cells      = CollectClearedCells(_gm.LastClearedRows, _gm.LastClearedCols);
        bool blackMode = _gm.ToggleCurrentColor == 1;
        // 반전색: 화이트 모드→블랙, 블랙 모드→화이트
        Color flashColor = blackMode ? Color.black : Color.white;

        // 중심 좌표 + 거리 계산
        float centerR = 0f, centerC = 0f;
        var cellList = new System.Collections.Generic.List<(int r, int c, float dist)>();
        foreach (var (r, c) in cells) { centerR += r; centerC += c; }
        centerR /= cells.Count;
        centerC /= cells.Count;
        float maxDist = 0f;
        foreach (var (r, c) in cells)
        {
            float dist = Mathf.Abs(r - centerR) + Mathf.Abs(c - centerC);
            if (dist > maxDist) maxDist = dist;
            cellList.Add((r, c, dist));
        }
        if (maxDist < 1f) maxDist = 1f;

        // 임시 오버레이 생성 (그리드 위에 float)
        var tempRoot = new GameObject("ToggleClearFX");
        tempRoot.transform.SetParent(_gridRt, false);
        var tempRootRt        = tempRoot.AddComponent<RectTransform>();
        tempRootRt.anchorMin  = Vector2.zero;
        tempRootRt.anchorMax  = Vector2.one;
        tempRootRt.offsetMin  = tempRootRt.offsetMax = Vector2.zero;

        var temps = new System.Collections.Generic.List<(RectTransform rt, Image img)>();
        foreach (var (r, c, _) in cellList)
        {
            var go  = new GameObject($"fx_{r}_{c}");
            go.transform.SetParent(tempRoot.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite          = _spr110;
            img.type            = Image.Type.Sliced;
            img.color           = flashColor;
            img.raycastTarget   = false;
            var rt              = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-420 + c * 120, 420 - r * 120);
            rt.sizeDelta        = new Vector2(110, 110);
            temps.Add((rt, img));
        }

        // 보드·입력 즉시 갱신 (유저 대기 없음)
        RefreshGrid();

        // 1단계: 반전 플래시 홀드 (0.05s)
        yield return new WaitForSeconds(0.05f);

        // 2단계: 리플 수축 소멸
        float rippleDuration = 0.20f;
        float cellAnimDur    = 0.13f;
        float maxDelay       = rippleDuration - cellAnimDur;

        int idx = 0;
        foreach (var (r, c, dist) in cellList)
        {
            float delay = (dist / maxDist) * maxDelay;
            var   cap   = temps[idx++];
            StartCoroutine(ShrinkFX(cap.rt, cap.img, delay, cellAnimDur, flashColor));
        }

        yield return new WaitForSeconds(rippleDuration + 0.02f);
        Destroy(tempRoot);
    }

    IEnumerator ShrinkFX(RectTransform rt, Image img, float delay, float duration, Color startColor)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float ease  = 1f - t * t;                       // ease-in
            float scale = Mathf.Lerp(1.3f, 0f, 1f - ease); // 팽창 → 수축
            float alpha = 1f - t;
            if (rt  != null) rt.localScale = Vector3.one * scale;
            if (img != null) img.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
    }

    // ── 아이스모드: 슬라이드 → 연쇄 클리어 루프 ─────────────────
    IEnumerator IceSlideAndChain()
    {
        _busy = true;

        while (true)
        {
            var movers = CollectSliders();
            if (movers.Count == 0) break;

            _gm.SlideDown();
            yield return StartCoroutine(AnimateSlideDown(movers));
            RefreshGrid();

            int newLines = _gm.CheckAndClearLinesQuiet();
            if (newLines == 0) break;

            _highScoreText.text = $"BEST  {_gm.HighScore}";
            _scoreText.text     = _gm.Score.ToString();

            var cells = CollectClearedCells(_gm.LastClearedRows, _gm.LastClearedCols);
            yield return StartCoroutine(FlashAndFade(cells));
            RefreshGrid();
        }

        _gm.SaveGame();

        // _busy를 내리면 다음 프레임에 CheckGameOver가 판단한다.
        _busy = false;
    }

    // (출발행, 열, 값, 도착행) — 한 칸씩 내려오므로 toR = fromR + 1
    System.Collections.Generic.List<(int fromR, int c, int val, int toR)> CollectSliders()
    {
        var list = new System.Collections.Generic.List<(int, int, int, int)>();
        for (int r = GameManager.SIZE - 2; r >= 0; r--)
            for (int c = 0; c < GameManager.SIZE; c++)
                if (_gm.Board[r, c] != 0 && _gm.Board[r + 1, c] == 0)
                    list.Add((r, c, _gm.Board[r, c], r + 1));
        return list;
    }

    IEnumerator AnimateSlideDown(System.Collections.Generic.List<(int fromR, int c, int val, int toR)> movers)
    {
        int count = movers.Count;
        var gos  = new GameObject[count];
        var rts  = new RectTransform[count];
        var curR = new int[count];
        var toRs = new int[count];
        var cs   = new int[count];

        for (int i = 0; i < count; i++)
        {
            var (fromR, c, v, toR) = movers[i];
            curR[i] = fromR;
            toRs[i] = toR;
            cs[i]   = c;

            _blockOverlays[fromR, c].color = Color.clear;

            var ghost = new GameObject("SlideGhost");
            ghost.transform.SetParent(_gridRt, false);

            var img = ghost.AddComponent<Image>();
            var spr = _colorSprites[v - 1];
            if (spr != null) { img.sprite = spr; img.type = Image.Type.Simple; }
            else             { img.sprite = _spr110; img.type = Image.Type.Sliced; }
            img.color         = Color.white;
            img.raycastTarget = false;

            var rt = ghost.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(110, 110);
            rt.anchoredPosition = new Vector2(-420 + c * 120, 420 - fromR * 120);

            gos[i] = ghost;
            rts[i] = rt;
        }

        // 한 칸씩 스텝 애니메이션 – 각 행을 지날 때마다 멜팅 흔적 생성
        const float rowDuration = 0.06f;

        while (true)
        {
            bool anyActive = false;
            for (int i = 0; i < count; i++)
            {
                if (curR[i] >= toRs[i]) continue;
                anyActive = true;
                if (_meltingSprite != null)
                    SpawnMeltingTrail(curR[i], cs[i]);
            }
            if (!anyActive) break;

            float elapsed = 0f;
            while (elapsed < rowDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / rowDuration));
                for (int i = 0; i < count; i++)
                {
                    if (curR[i] >= toRs[i]) continue;
                    rts[i].anchoredPosition = new Vector2(
                        -420 + cs[i] * 120,
                        Mathf.Lerp(420 - curR[i] * 120, 420 - (curR[i] + 1) * 120, t));
                }
                yield return null;
            }

            for (int i = 0; i < count; i++)
                if (curR[i] < toRs[i]) curR[i]++;
        }

        for (int i = 0; i < count; i++)
            Destroy(gos[i]);
    }

    void SpawnMeltingTrail(int r, int c)
    {
        var maskGo  = new GameObject("MeltTrail");
        maskGo.transform.SetParent(_gridRt, false);

        var maskImg    = maskGo.AddComponent<Image>();
        maskImg.sprite = _spr110;
        maskImg.type   = Image.Type.Sliced;

        var mask = maskGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var maskRt = maskGo.GetComponent<RectTransform>();
        maskRt.anchorMin        = new Vector2(0.5f, 0.5f);
        maskRt.anchorMax        = new Vector2(0.5f, 0.5f);
        maskRt.pivot            = new Vector2(0.5f, 0.5f);
        maskRt.anchoredPosition = new Vector2(-420 + c * 120, 420 - r * 120);
        maskRt.sizeDelta        = new Vector2(110, 110);

        var meltGo  = new GameObject("Melting");
        meltGo.transform.SetParent(maskGo.transform, false);

        var meltImg    = meltGo.AddComponent<Image>();
        meltImg.sprite = _meltingSprite;
        meltImg.preserveAspect = true;
        meltImg.raycastTarget  = false;

        var meltRt = meltGo.GetComponent<RectTransform>();
        meltRt.anchorMin = Vector2.zero;
        meltRt.anchorMax = Vector2.one;
        meltRt.offsetMin = Vector2.zero;
        meltRt.offsetMax = Vector2.zero;

        StartCoroutine(FadeMeltTrail(maskGo, meltImg));
    }

    IEnumerator FadeMeltTrail(GameObject parent, Image img)
    {
        float elapsed = 0f, duration = 0.45f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            img.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        Destroy(parent);
    }
}
