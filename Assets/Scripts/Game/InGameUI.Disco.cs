using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// InGameUI 의 디스코 모드 전용 부분 — 배경 연출(터널·별·비·스팟), 곡 타임라인에 얹힌
// 레이어 전환, 광과민성 경고, 하트/콤보 상태등.
// 같은 클래스를 파일만 나눈 것이라(partial) 필드와 다른 메서드가 그대로 보인다.
public partial class InGameUI
{

    // 콤보 상태등 갱신. 매 프레임 불린다(경고가 깜빡여야 해서).
    //   · 콤보 0      아무것도 안 띄운다 — 첫 클리어 전이라 규칙이 아직 안 걸린다
    //   · 이번 세트 클리어함  흰 글자로 가만히
    //   · 아직 못 지움        빨갛게 깜빡. 마지막 조각 하나가 남으면 두 배로 빨라진다
    void UpdateComboStatus()
    {
        if (_comboStatusText == null || _gm == null) return;

        int combo = _gm.Combo;
        if (combo <= 0)
        {
            if (_comboStatusText.text.Length > 0) _comboStatusText.text = "";
            _comboStatusText.rectTransform.localScale = Vector3.one;
            _comboStatusShown = -1;
            return;
        }

        // 이 함수는 매 프레임 돈다. 문자열 보간은 결과를 비교하기 전에 이미 할당되므로,
        // 만들어 놓고 버리는 쓰레기가 프레임마다 쌓인다. 숫자가 바뀔 때만 만든다.
        // ♪는 U+266A. ♫(U+266B)는 폰트에 없다 — NOTE_GLYPHS 주석 참고.
        if (_comboStatusShown != combo)
        {
            _comboStatusShown     = combo;
            _comboStatusText.text = combo >= COMBO_MAX ? "MAX! ♪" : $"x{combo} ♪";
        }

        // 이번 세트를 이미 지웠으면 위험하지 않다. 무지개 블럭을 쥐고 있는 것만으로는
        // 안전하지 않다 — 쓰기 전까지는 세트를 놓치면 그대로 끝난다(GameManager.CloseSet).
        if (_gm.ClearedThisSet)
        {
            _comboStatusText.color = COMBO_STATUS_SAFE;
            _comboStatusText.rectTransform.localScale = Vector3.one;
            return;
        }

        int left = 0;
        for (int i = 0; i < 3; i++)
            if (!_gm.CurrentPieces[i].placed) left++;

        float speed = left <= 1 ? COMBO_RISK_PULSE_LAST : COMBO_RISK_PULSE;
        float wave  = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);

        _comboStatusText.color = new Color(
            COMBO_STATUS_RISK.r, COMBO_STATUS_RISK.g, COMBO_STATUS_RISK.b, 0.45f + 0.55f * wave);

        // 밝기만 흔들면 화면이 번쩍이는 디스코에서는 묻힌다. 크기까지 같이 뛰게 해
        // 배경이 무슨 짓을 하든 눈에 걸리게 한다.
        _comboStatusText.rectTransform.localScale =
            Vector3.one * (1f + COMBO_STATUS_PULSE_SCALE * wave);
    }

    // 하트 게이지 갱신: 채워진 하트는 분홍, 빈 하트는 흐릿하게
    void RefreshDiscoHearts()
    {
        if (_discoHearts == null) return;
        for (int i = 0; i < _discoHearts.Length; i++)
        {
            if (_discoHearts[i] == null) continue;
            bool filled = i < _gm.DiscoLineGauge;
            _discoHearts[i].color = filled ? HEART_FULL : HEART_EMPTY;
            _discoHearts[i].rectTransform.localScale = Vector3.one * (filled ? 1.12f : 1f);
        }
    }

    void BuildDiscoBall()
    {
        // 디스코 연출은 설계 비율(1920) 안에서만 그린다. 남는 위아래는 BuildLetterbox 가
        // 검게 덮는다 — 화면에 맞춰 늘리면 조명 간격이 벌어져 판과 따로 놀았다.
        int rows  = DISCO_ROWS;
        int cells = rows * DISCO_COLS;
        int total = cells * DISCO_SUBS;

        _spotImgs     = new DiscoTile[total];
        _spotRts      = new RectTransform[total];
        _spotPhase    = new float[total];
        _spotBaseX    = new float[total];
        _spotBaseY    = new float[total];
        _spotAmpX     = new float[total];
        _spotAmpY     = new float[total];
        _spotOffsetX  = new float[total];
        _spotOffsetY  = new float[total];
        _spotMaxAlpha = new float[total];
        _spotBaseW    = new float[total];
        _spotBaseH    = new float[total];

        var layerGo = new GameObject("DiscoSpotLayer");
        layerGo.transform.SetParent(_canvas.transform, false);
        layerGo.transform.SetSiblingIndex(1);
        _spotLayerGo = layerGo;
        var layerRt = layerGo.AddComponent<RectTransform>();
        layerRt.anchorMin = Vector2.zero;
        layerRt.anchorMax = Vector2.one;
        layerRt.offsetMin = layerRt.offsetMax = Vector2.zero;

        Sprite tileSpr = MakeTileSprite(64);

        var rng = new System.Random(20260425);

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            // 행 Y: 설계 높이를 균등 분할 (어긋남 없음)
            float rowY = -CanvasMetrics.REF_H * 0.5f + CanvasMetrics.REF_H * (r + 0.5f) / rows;

            for (int c = 0; c < DISCO_COLS; c++)
            {
                // 트랙 안에서 X도 균등 분할
                float baseX = -DISCO_TRACK_WIDTH * 0.5f + DISCO_TRACK_WIDTH * (c + 0.5f) / DISCO_COLS;

                // 타일은 부드러운 보케라 살짝 크게 잡아야 글로우가 잘 보임.
                float w = 120f + (float)rng.NextDouble() * 22f;
                float h = w * (0.92f + (float)rng.NextDouble() * 0.16f);
                float maxAlpha = 0.95f + (float)rng.NextDouble() * 0.10f;

                // 인접 색 분산
                Color col = DISCO_COLORS[(r * 5 + c * 3) % DISCO_COLORS.Length];

                // 셀 단위 깜빡임 (sub-tile 모두 공유 → 셀 통째로 같이 명멸)
                float blinkFreq  = 1.0f + (float)rng.NextDouble() * 1.6f;
                float blinkPhase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

                // 셀마다 sub들의 Y 오프셋에 작은 랜덤 변량을 추가해서 어긋남 패턴이 단조롭지 않게
                float jitterY = (float)((rng.NextDouble() - 0.5) * 4.0); // ±2px

                for (int s = 0; s < DISCO_SUBS; s++)
                {
                    AddSpot(layerGo.transform, tileSpr, idx, col,
                        bX: baseX, bY: rowY,
                        aX: blinkFreq, aY: DISCO_SUB_ALPHA[s],
                        phase: blinkPhase, w: w, h: h, alpha: maxAlpha);

                    _spotOffsetX[idx] = DISCO_SUB_OFFX[s];
                    _spotOffsetY[idx] = DISCO_SUB_OFFY[s] + jitterY;
                    idx++;
                }
            }
        }
    }

    void AddSpot(Transform parent, Sprite spr, int idx, Color col,
        float bX, float bY, float aX, float aY, float phase,
        float w, float h, float alpha)
    {
        var go  = new GameObject($"Spot_{idx}");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<DiscoTile>();
        img.sprite        = spr;
        img.type          = Image.Type.Simple;
        img.color         = new Color(col.r, col.g, col.b, 0f);
        img.raycastTarget = false;
        _spotImgs[idx] = img;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        _spotRts[idx] = rt;

        _spotPhase[idx]    = phase;
        _spotBaseX[idx]    = bX;
        _spotBaseY[idx]    = bY;
        _spotAmpX[idx]     = aX;
        _spotAmpY[idx]     = aY;
        _spotMaxAlpha[idx] = alpha;
        // 크기는 여기서 한 번만 정한다(rt.sizeDelta 위에서 설정). 매 프레임 다시 쓰면
        // 같은 값이어도 RectTransform이 더럽혀져 351개가 통째로 재계산된다.
        _spotBaseW[idx]    = w;
        _spotBaseH[idx]    = h;
    }

    // 디스코볼 거울 반사 타일: 사각형 등고선이지만 솔리드 영역 없이 중심에서 가장자리까지
    // 부드럽게 페이드되어 환상적인 보케 글로우처럼 보임.
    Sprite MakeTileSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px   = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - half + 0.5f) / half;
                float dy = Mathf.Abs(y - half + 0.5f) / half;
                // 체비셰프(사각)와 유클리드(둥근)를 섞어 모서리가 부드러운 사각 보케
                float dCheb = Mathf.Max(dx, dy);
                float dEucl = Mathf.Sqrt(dx * dx + dy * dy);
                float d     = Mathf.Lerp(dCheb, dEucl, 0.35f);
                float t     = 1f - Mathf.Clamp01(d);
                float a     = t * t * (3f - 2f * t);   // smoothstep
                a = Mathf.Pow(a, 1.6f);                 // 가장자리가 더 빨리 흐려짐 → 보케
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void UpdateDiscoVisuals()
    {
        float beatPhase = _beatTracker != null ? _beatTracker.BeatPhase : 0f;
        float beatDecay = Mathf.Max(0f, 1f - beatPhase * 3.5f);
        float beatPulseY = 1f + 0.14f * beatDecay * beatDecay;

        // 무지개 블럭은 배경 연출 구간과 무관하게 항상 맥동해야 하므로
        // 아래 레이어 on/off 및 early return보다 먼저 처리한다.
        UpdateRainbowBlocks(Time.time);

        // 48.5~49초 페이드아웃 → 49~100.1초 완전 블랙(이 동안 터널/흰색배경이 화면을 채움)
        // → 100.1~100.6초 페이드인으로 셀로 복귀.
        // 49~50초만은 터널도 꺼져 있어 화면 전체가 진짜 검게 비는 빈 캔버스가 된다.
        // 흰색 단색 배경: 96.7~97.2 페이드인, 97.2~100.1 풀, 100.1~100.6 페이드아웃(셀 페이드인과 크로스).
        // 2차 터널 진입도 같은 구조: BLACK2_START~BLACK2_FULL 검정 페이드인 → 이후 곡 끝까지 검정 유지,
        // TUNNEL2_START부터 터널이 그 위로 페이드인하며 검정이 걷히는 것처럼 보인다.
        float blackout = 0f;
        float whiteBg = 0f;
        float cityWhite = 0f;
        if (_beatTracker != null)
        {
            double t = _beatTracker.PlaybackSec;
            // 곡이 루프로 되감긴 직후. 곡 끝을 검정으로 마무리했으므로 여기서 검정을 걷어
            // 이어 붙인다. 이 분기가 맨 앞이라 아래 구간들과 겹칠 일이 없다(LOOP_FADE_SEC ≪ 48.5).
            if      (t < LOOP_FADE_SEC)      blackout = 1f - (float)(t / LOOP_FADE_SEC);
            else if (t >= 48.5 && t < 49.0)  blackout = (float)((t - 48.5) * 2.0);
            else if (t >= 49.0 && t < 100.1) blackout = 1f;
            else if (t >= 100.1 && t < 100.6) blackout = (float)((100.6 - t) * 2.0);
            // 2차 터널 진입: 검정으로 덮은 뒤 끝까지 검정 배경을 유지한다(터널이 그 위에 뜬다).
            else if (t >= BLACK2_START && t < BLACK2_FULL)
                blackout = (float)((t - BLACK2_START) / (BLACK2_FULL - BLACK2_START));
            else if (t >= BLACK2_FULL) blackout = 1f;

            if      (t >= 96.7 && t < 97.2)   whiteBg = (float)((t - 96.7) * 2.0);
            else if (t >= 97.2 && t < 100.1)  whiteBg = 1f;
            else if (t >= 100.1 && t < 100.6) whiteBg = (float)((100.6 - t) * 2.0);

            // 도시 등장 화이트아웃: 위 흰 배경과 같은 0.5초 램프.
            // 145.7~146.2 페이드인 → 146.2~146.7 풀 화이트(이 사이 146.7에 도시 스폰)
            // → 146.7~147.2 페이드아웃하며 도시가 드러남.
            double wIn   = CityLightBackground.WHITE_IN_START;
            double wFull = CityLightBackground.WHITE_FULL;
            double wOut  = CityLightBackground.APPEAR_START;
            double wEnd  = CityLightBackground.WHITE_OUT_END;
            if      (t >= wIn   && t < wFull) cityWhite = (float)((t - wIn) / (wFull - wIn));
            else if (t >= wFull && t < wOut)  cityWhite = 1f;
            else if (t >= wOut  && t < wEnd)  cityWhite = (float)((wEnd - t) / (wEnd - wOut));
        }
        float pulseY    = Mathf.Lerp(beatPulseY, 1f, blackout);
        float visibility = 1f - blackout;

        if (_bgImage != null)
        {
            // blackout으로 어두운 색 ↔ 검정을 보간한 뒤, whiteBg가 그 위에서 흰색을 덮어쓴다.
            Color baseBg = Color.Lerp(new Color(0.05f, 0.03f, 0.10f), Color.black, blackout);
            _bgImage.color = Color.Lerp(baseBg, Color.white, whiteBg);
        }

        // 도시 화이트아웃은 도시를 완전히 가려야 하므로 풀 알파를 쓴다(흰 배경 구간은 기존대로 0.7).
        // 알파가 0인 구간에는 아예 꺼둔다 — 투명해도 전체화면 쿼드는 계속 그려지기 때문.
        if (_whiteFlashOverlay != null)
        {
            float flashAlpha = Mathf.Max(whiteBg * 0.7f, cityWhite);
            bool  flashOn    = flashAlpha > 0.001f;
            if (_whiteFlashOverlay.enabled != flashOn) _whiteFlashOverlay.enabled = flashOn;
            if (flashOn) _whiteFlashOverlay.color = new Color(1f, 1f, 1f, flashAlpha);
        }

        // 그리드 백드롭은 배경이 밝아질수록 같이 진해진다.
        // 빈 셀이 밝은 유리라 바닥이 어두워야 읽힌다 — 배경이 하얘지는 구간에서 고정 알파로는
        // 유리가 배경에 그대로 묻힌다. 바닥이 배경을 흡수해 주면 격자는 늘 같은 대비를 갖는다.
        if (_discoGridBackdrop != null)
        {
            float bright = Mathf.Max(Mathf.Max(whiteBg, cityWhite),
                                     SceneBackgroundActive ? DISCO_BACKDROP_SCENE : 0f);
            float want   = Mathf.Lerp(DISCO_BACKDROP_ALPHA, DISCO_BACKDROP_ALPHA_BRIGHT, bright);
            if (!Mathf.Approximately(_discoGridBackdrop.color.a, want))
                _discoGridBackdrop.color = new Color(0f, 0f, 0f, want);
        }

        // 보드 블록 세로 펄스. 값이 지난 프레임과 같으면 64칸을 아예 건드리지 않는다 —
        // 블랙아웃 구간(49~100.1초)에서는 pulseY가 정확히 1로 고정이라 그동안 통째로 쉰다.
        // 그 구간에 새로 놓인 블록도 기본 배율이 1이라 어긋나지 않는다.
        if (pulseY != _lastPulseY)
        {
            _lastPulseY = pulseY;
            var pulseScale = new Vector3(1f, pulseY, 1f);
            for (int r = 0; r < GameManager.SIZE; r++)
                for (int c = 0; c < GameManager.SIZE; c++)
                    if (_blockOverlays[r, c] != null && _gm.Board[r, c] != 0)
                        _blockOverlays[r, c].rectTransform.localScale = pulseScale;
        }

        // 트레이 조각 펄스 (드래그 중 제외)
        for (int i = 0; i < 3; i++)
            if (_previewContainers[i] != null && i != _dragIdx)
                _previewContainers[i].transform.localScale = new Vector3(1f, pulseY, 1f);

        double playbackSec = _beatTracker != null ? _beatTracker.PlaybackSec : 0.0;
        float  now         = Time.time;

        // 전용 배경(도시/드라이빙) 진입 판정을 먼저 돌려야 아래 가시성 판단이 같은 프레임 기준이 된다.
        UpdateSceneBackground(playbackSec);

        // ── 레이어 on/off ────────────────────────────────────────────────
        // 지금 화면에 실제로 보이는 레이어만 켜둔다. 꺼진 레이어는 렌더도 갱신도 하지 않는다.
        // 알파만 0으로 두면 UI 이미지는 계속 메시를 만들고 그려지므로 GameObject 자체를 끈다.
        //
        //   셀   blackout(49~100.1초)에는 알파 0, 도시/드라이빙이 덮는 동안에는 배경 뒤에 가려짐
        //   터널 1차 50~96.7초, 2차 TUNNEL2_START~곡 끝. 그 밖에서는 intensity가 0이라 아무것도 안 보임
        //   별   100.6초부터 스폰 시작, 그 전에는 전부 비활성 슬롯
        bool cellsVisible  = visibility > 0f && !SceneBackgroundActive;
        bool tunnelVisible = (playbackSec >= 50.0 && playbackSec < 96.7)
                          || playbackSec >= TUNNEL2_START;
        // 별은 검정 램프를 따라 같이 옅어지다가(visibility) 완전 검정에서 끊는다.
        bool starsVisible  = playbackSec >= 100.6 && playbackSec < BLACK2_FULL;

        // 터널은 켜지는 순간 굽는 방향을 새로 뽑는다. 1차는 일직선, 2차만 굽는다.
        if (SetLayerActive(_tunnelLayerRt != null ? _tunnelLayerRt.gameObject : null, tunnelVisible)
            && tunnelVisible)
            RerollTunnelCurve(playbackSec >= TUNNEL2_START);
        if (tunnelVisible) UpdateTunnel(now, playbackSec);

        // 별은 꺼질 때 살아있던 슬롯을 비워둬야 다시 켤 때 옛 별이 그 자리에 남아있지 않는다.
        // (알파도 같이 0으로 — 갱신 루프는 비활성 슬롯을 건너뛰므로 스스로 지워지지 않는다)
        if (SetLayerActive(_starLayerGo, starsVisible) && !starsVisible && _starBornTime != null)
            for (int i = 0; i < STAR_POOL; i++)
            {
                _starBornTime[i] = -1f;
                if (_starImgs[i] != null) _starImgs[i].color = new Color(1f, 1f, 1f, 0f);
            }
        if (starsVisible) UpdateStarSparkles(now, visibility, playbackSec);

        SetLayerActive(_spotLayerGo, cellsVisible);
        if (!cellsVisible || _spotRts == null) return;

        // _discoBallAngle를 "누적 스크롤 픽셀"로 사용 (이름은 그대로지만 의미만 px)
        //
        // 방향·속도는 구간에 따라 다르다.
        //   ~COLORFUL_START  기존 그대로: 좌→우, 280 px/sec (1080px를 약 3.9초에 가로지름)
        //   COLORFUL_START~  자동차 이후 셀만 보이는 구간: 우→좌, 440 px/sec (약 2.5초)
        //                    드라이빙 배경의 건물 진행 방향과 맞추고 더 빠르게.
        // 전환 시점에는 셀이 드라이빙 배경에 완전히 가려져 있어 방향이 꺾이는 게 보이지 않는다.
        bool  carSection = playbackSec >= DrivingBackground.COLORFUL_START;
        float dir        = carSection ? -1f : 1f;
        float scrollSpeed = carSection ? 440f + 180f * beatDecay * beatDecay
                                       : 280f + 120f * beatDecay * beatDecay;
        _discoBallAngle += dir * scrollSpeed * Time.deltaTime;
        float scrollX = _discoBallAngle;

        // sub-tile 잔향은 항상 진행 방향의 "뒤쪽"에 깔려야 하므로 방향이 뒤집히면 부호도 뒤집는다.
        float trailSign = -dir;

        float beatBoost = 0.10f * beatDecay * beatDecay;

        for (int i = 0; i < _spotRts.Length; i++)
        {
            if (_spotRts[i] == null) continue;

            // 트랙 안에서 wrap-around 스크롤. 화면 밖으로 나간 타일은 그냥 안 보이는 상태.
            // sub-tile 잔향: 메인 뒤쪽에 깔리도록 offset을 더함. Y는 작게 어긋나서 한 장 느낌을 깸.
            float x = Mathf.Repeat(_spotBaseX[i] + _spotOffsetX[i] * trailSign + scrollX + DISCO_TRACK_WIDTH * 0.5f,
                                   DISCO_TRACK_WIDTH) - DISCO_TRACK_WIDTH * 0.5f;
            float y = _spotBaseY[i] + _spotOffsetY[i];

            _spotRts[i].anchoredPosition = new Vector2(x, y);

            // 셀마다 다른 빈도/위상의 깜빡임. 0.0~1.0 사이로 진동 → 또렷한 플리커.
            // 움직임이 있으니 잠깐 꺼져도 "제자리에서 사라지는" 느낌은 안 남.
            float blink = 0.5f + 0.5f * Mathf.Sin(now * _spotAmpX[i] + _spotPhase[i]);

            // _spotAmpY: sub-tile별 알파 배율 (메인=1.0, 잔향=0.55/0.28).
            float alpha = (_spotMaxAlpha[i] * blink + beatBoost) * _spotAmpY[i] * visibility;
            alpha = Mathf.Min(alpha, 1f);

            var col = _spotImgs[i].color;
            _spotImgs[i].color = new Color(col.r, col.g, col.b, alpha);
        }
    }

    // 레이어 GameObject를 켜고 끈다. 상태가 실제로 바뀐 프레임에만 SetActive를 부른다.
    // 반환값: 이번 프레임에 상태가 바뀌었는지 (전환 시점에만 할 정리 작업용)
    static bool SetLayerActive(GameObject go, bool active)
    {
        if (go == null || go.activeSelf == active) return false;
        go.SetActive(active);
        return true;
    }

    // 곡 후반의 전용 배경(도시 야경 → 드라이빙)을 시간에 맞춰 띄우고 내린다.
    // 각 배경이 스스로 Destroy하므로 여기서는 진입 트리거와 기본 배경 on/off만 본다.
    void UpdateSceneBackground(double playbackSec)
    {
        if (_cityLight == null
            && playbackSec >= CityLightBackground.APPEAR_START
            && playbackSec <  CityLightBackground.FADE_OUT_END)
        {
            _cityLight = CityLightBackground.Spawn(_canvas, _beatTracker);
            PlaceSceneBackground(_cityLight.transform);
        }

        // 도시가 페이드아웃을 시작할 때 드라이빙 배경이 그 뒤에서 함께 뜬다(크로스페이드).
        // 상한을 같이 봐야 한다 — 드라이빙이 스스로 사라진 뒤에도 조건이 참이면 매 프레임 다시 스폰된다.
        if (_driving == null
            && playbackSec >= DrivingBackground.APPEAR_START
            && playbackSec <  DrivingBackground.DISAPPEAR_AT)
        {
            _driving = DrivingBackground.Spawn(_canvas, _beatTracker);
            PlaceSceneBackground(_driving.transform);
        }

        // 전용 배경(도시·드라이빙)이 떠 있는 동안에는 기본 전체 배경을 감춘다.
        // (Unity의 fake-null 체크로 파괴 여부까지 함께 판정됨)
        //
        // 그리드 백드롭은 예전에 여기서 같이 껐다. 빈 셀이 반투명 채움이던 시절에는
        // 검은 판과 겹쳐 탁해졌기 때문이다. 지금 빈 셀은 가장자리만 있는 유리라
        // 뒤를 거의 다 통과시켜서, 백드롭이 없으면 밝은 도시나 터널 위에서 격자가 사라진다.
        // 그래서 백드롭은 구간과 무관하게 계속 켜 둔다.
        bool sceneActive = SceneBackgroundActive;
        if (_bgImage != null && _bgImage.enabled == sceneActive)
        {
            _bgImage.enabled = !sceneActive;
            RefreshGrid(); // 빈 셀 표현을 즉시 갱신
        }
    }

    // 전용 배경은 DiscoSpotLayer(1) / DiscoTunnelLayer(2) 위, 그리드·트레이·스코어 아래에 둔다.
    // 나중에 스폰된 쪽이 항상 같은 idx로 "끼어들어" 먼저 있던 배경을 한 칸 위로 밀어낸다.
    // 덕분에 도시→드라이빙 크로스페이드 동안 드라이빙이 도시 뒤에 깔린다.
    void PlaceSceneBackground(Transform t)
    {
        int idx = _bgImage != null ? _bgImage.transform.GetSiblingIndex() + 1 : 1;
        if (_tunnelLayerRt != null)
            idx = Mathf.Max(idx, _tunnelLayerRt.GetSiblingIndex() + 1);
        t.SetSiblingIndex(idx);
    }

    // ── 별 스프라이트: 4점 십자 별(긴 빔) + 둥근 코어 ────────────
    Sprite MakeStarSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half + 0.5f) / half;
                float dy = (y - half + 0.5f) / half;
                float ax = Mathf.Abs(dx);
                float ay = Mathf.Abs(dy);
                // 가로/세로 빔: 빔 두께(반대축)는 얇고 길이축은 가장자리까지 페이드 (십자 모양, 중심 원 없음)
                float horiz = Mathf.Clamp01(1f - ay * 14f) * Mathf.Clamp01(1f - ax);
                float vert  = Mathf.Clamp01(1f - ax * 14f) * Mathf.Clamp01(1f - ay);
                float a = Mathf.Clamp01(horiz + vert);
                a = a * a * (3f - 2f * a);   // smoothstep
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // 디스코 모드 전용: 화려한 벽 위에 그리드가 뚜렷하게 보이도록 그리드 뒤에 어두운 패널을 깔아둔다.
    // _gridRt의 첫 번째 자식이라 모든 셀보다 먼저 그려져 → 셀 뒤에 깔림.
    void BuildDiscoGridBackdrop()
    {
        var go = new GameObject("DiscoGridBackdrop");
        go.transform.SetParent(_gridRt, false);
        go.transform.SetAsFirstSibling();

        var img = go.AddComponent<Image>();
        img.sprite        = MakeRoundedSprite(120, 120, 32);
        img.type          = Image.Type.Sliced;
        img.color         = new Color(0f, 0f, 0f, DISCO_BACKDROP_ALPHA);
        img.raycastTarget = false;
        _discoGridBackdrop = img;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(990, 990);
    }

    void BuildWhiteFlashOverlay()
    {
        var go = new GameObject("WhiteFlashOverlay");
        go.transform.SetParent(_canvas.transform, false);

        _whiteFlashOverlay                = go.AddComponent<Image>();
        _whiteFlashOverlay.color          = new Color(1f, 1f, 1f, 0f);
        _whiteFlashOverlay.raycastTarget  = false;

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.one;
        rt.offsetMin    = Vector2.zero;
        rt.offsetMax    = Vector2.zero;

        // 배경 레이어 바로 위, 보드·점수보다는 아래에 둔다.
        // 맨 위에 두면 화이트아웃 3초 동안 보드까지 하얗게 덮여 아무것도 안 보인다.
        // 여기 두면 하얘지는 건 배경뿐이고, 도시가 스폰되는 순간을 가리는 역할은 그대로다
        // (도시는 이 판 뒤에서 바뀐다).
        if (_tunnelLayerRt != null)
            go.transform.SetSiblingIndex(_tunnelLayerRt.GetSiblingIndex() + 1);
    }

    /// <summary>
    /// 디스코 연출을 설계 비율(1080x1920) 안에 가두고, 남는 위아래를 검게 덮는다.
    /// 긴 화면에 맞춰 늘리면 조명과 격자 간격이 벌어져 판과 따로 놀았다. 영화처럼
    /// 띠를 두는 편이 원래 그림을 그대로 지킨다.
    /// </summary>
    void BuildLetterbox()
    {
        float bar = (CanvasMetrics.Height - CanvasMetrics.REF_H) * 0.5f;
        if (bar <= 1f) return;   // 16:9 화면이면 덮을 자리가 없다

        AddBar("LetterboxTop",    1f, bar);
        AddBar("LetterboxBottom", 0f, bar);

        void AddBar(string name, float anchorY, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            go.transform.SetAsLastSibling();

            var img = go.AddComponent<Image>();
            img.color         = Color.black;
            img.raycastTarget = false;   // 띠가 입력을 먹으면 안 된다

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, anchorY);
            rt.anchorMax        = new Vector2(1f, anchorY);
            rt.pivot            = new Vector2(0.5f, anchorY);
            rt.sizeDelta        = new Vector2(0f, h);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    void BuildPhotoWarning()
    {
        var loc = LocalizationManager.Instance;

        var go = new GameObject("PhotoWarning");
        go.transform.SetParent(_canvas.transform, false);
        go.transform.SetAsLastSibling();   // sibling 순서 = 그리는 순서. 맨 위여야 전부 덮는다.

        var backdrop   = go.AddComponent<Image>();
        backdrop.color = new Color(0.04f, 0.03f, 0.07f, 1f);

        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 아이콘·문구만 담는 자식. 판과 알파를 따로 굴리려고 한 겹 더 둔다.
        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(go.transform, false);
        var contentRt       = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;
        var contentCg   = contentGo.AddComponent<CanvasGroup>();
        contentCg.alpha = 0f;

        // caution.png는 흰 실루엣 + 투명 배경이라 어두운 backdrop 위에서 그대로 읽힌다.
        var iconSprite = LoadSpriteFromPath("Sprites/Logo/caution");
        if (iconSprite != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(contentGo.transform, false);
            var img            = iconGo.AddComponent<Image>();
            img.sprite         = iconSprite;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            var irt              = iconGo.GetComponent<RectTransform>();
            irt.anchorMin        = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot            = new Vector2(0.5f, 0.5f);
            irt.sizeDelta        = new Vector2(340, 340);
            irt.anchoredPosition = new Vector2(0, 210);
        }

        AddText(contentGo.transform, loc.Get("photo_warn_title"), 62, Color.white,
            new Vector2(0, -60), new Vector2(940, 100));
        AddText(contentGo.transform, loc.Get("photo_warn_desc"), 38, new Color(0.80f, 0.80f, 0.86f),
            new Vector2(0, -190), new Vector2(900, 180));

        // 경고가 떠 있는 동안 뒤쪽 보드로 터치가 새지 않게 막는다.
        // 게임 자체는 이미 시작돼 있어서, 막지 않으면 경고를 읽다가 조각이 놓인다.
        // alpha는 1로 시작한다 — 첫 프레임부터 화면을 가려야 한다.
        var cg            = go.AddComponent<CanvasGroup>();
        cg.alpha          = 1f;
        cg.blocksRaycasts = true;

        StartCoroutine(PhotoWarningRoutine(cg, contentCg));
    }

    IEnumerator PhotoWarningRoutine(CanvasGroup panelCg, CanvasGroup contentCg)
    {
        // 검정 판은 이미 화면을 덮고 있고, 여기서는 문구만 떠오른다.
        for (float e = 0f; e < WARN_FADE_IN; e += Time.deltaTime)
        {
            contentCg.alpha = e / WARN_FADE_IN;
            yield return null;
        }
        contentCg.alpha = 1f;

        yield return new WaitForSeconds(WARN_HOLD);

        // 사라지는 동안에는 밑을 다시 만질 수 있게 풀어 준다.
        // 나갈 때는 판째로 걷는다. CanvasGroup은 중첩되면 알파가 곱해지므로
        // contentCg를 1로 둔 채 판만 내려도 문구가 같이 옅어진다.
        panelCg.blocksRaycasts = false;
        for (float e = 0f; e < WARN_FADE_OUT; e += Time.deltaTime)
        {
            panelCg.alpha = 1f - e / WARN_FADE_OUT;
            yield return null;
        }

        // 사라진 뒤에는 남겨 둘 이유가 없다.
        Destroy(panelCg.gameObject);

        // 여기가 곡의 실질적인 0초다. 경고가 완전히 걷힌 뒤에 시작해야
        // 연출 타임라인의 시작과 플레이어가 화면을 보기 시작하는 순간이 맞아떨어진다.
        BGMManager.Instance?.Resume();
    }

    // 동심원 링 (속이 빈 원, 부드러운 경계)
    Sprite MakeRingSprite(int size, float strokeRatio)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float half   = size * 0.5f;
        float outerR = half - 1f;
        float innerR = outerR * (1f - strokeRatio);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float r  = Mathf.Sqrt(dx * dx + dy * dy);
                float aOut = Mathf.Clamp01(outerR - r);  // 바깥 경계 부드럽게
                float aIn  = Mathf.Clamp01(r - innerR);  // 안쪽 경계 부드럽게
                float a    = Mathf.Min(aOut, aIn);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // 방사 스포크 (왼쪽 끝=중심, 오른쪽 끝=화면 가장자리. 안쪽으로 갈수록 투명).
    Sprite MakeSpokeSprite(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[width * height];
        for (int x = 0; x < width; x++)
        {
            float tx = x / (float)(width - 1);     // 0(중심쪽) → 1(가장자리쪽)
            // 끝부분도 살짝 페이드아웃해 가장자리에서 자연스럽게 사라지게
            float lenFade = tx < 0.85f ? tx / 0.85f : 1f - (tx - 0.85f) / 0.15f;
            for (int y = 0; y < height; y++)
            {
                float dy   = Mathf.Abs(y + 0.5f - height * 0.5f) / (height * 0.5f);
                float yfade = Mathf.Clamp01(1f - dy * dy);   // 두께 부드럽게
                float a    = Mathf.Clamp01(lenFade) * yfade;
                px[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        // pivot (0, 0.5): 회전축이 안쪽 끝(중심) → anchoredPosition을 (0,0)에 두고 회전만 주면 방사 배치
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0f, 0.5f));
    }

    // 카메라가 원기둥 안쪽으로 빨려들어가는 1인칭 터널.
    // 링 18개를 zNear~zFar에 균등 분포시켜 매 프레임 z를 줄이고, zNear 통과 시 zFar로 재활용.
    // 화면 사이즈는 size = FOCAL / z 원근 공식. 방사 스포크 12개는 소실점을 강조한다.
    // 링은 동심원이 아니다 — TunnelOffset이 깊이별로 옆으로 밀어 굽은 터널을 만든다.
    void BuildTunnelLayer()
    {
        _ringSprite  = MakeRingSprite(192, 0.06f);   // 6% 두께의 얇은 링
        _spokeSprite = MakeSpokeSprite(1200, 6);     // 길이 1200, 두께 6px

        _ringRts  = new RectTransform[TUNNEL_RINGS];
        _ringImgs = new Image[TUNNEL_RINGS];
        _ringZ    = new float[TUNNEL_RINGS];
        _spokeImgs = new Image[TUNNEL_SPOKES];

        var layerGo = new GameObject("DiscoTunnelLayer");
        layerGo.transform.SetParent(_canvas.transform, false);
        layerGo.transform.SetSiblingIndex(2); // Background(0), DiscoSpotLayer(1) 위, 그리드/트레이 아래
        var layerRt = layerGo.AddComponent<RectTransform>();
        layerRt.anchorMin = Vector2.zero;
        layerRt.anchorMax = Vector2.one;
        layerRt.offsetMin = layerRt.offsetMax = Vector2.zero;
        _tunnelLayerRt = layerRt;

        // 스포크는 한 그룹으로 묶는다. 이 그룹만 회전시키고, 터널이 굽은 만큼 위치도 옮긴다
        // (스포크가 모이는 지점 = 화면에서 터널 끝이 보이는 자리).
        var spokeGroupGo = new GameObject("TunnelSpokeGroup");
        spokeGroupGo.transform.SetParent(layerGo.transform, false);
        _spokeGroupRt = spokeGroupGo.AddComponent<RectTransform>();
        _spokeGroupRt.anchorMin        = new Vector2(0.5f, 0.5f);
        _spokeGroupRt.anchorMax        = new Vector2(0.5f, 0.5f);
        _spokeGroupRt.pivot            = new Vector2(0.5f, 0.5f);
        _spokeGroupRt.sizeDelta        = Vector2.zero;
        _spokeGroupRt.anchoredPosition = Vector2.zero;

        // 스포크: 그룹 중심에서 12방향으로 30도씩 배치
        for (int i = 0; i < TUNNEL_SPOKES; i++)
        {
            var go  = new GameObject($"TunnelSpoke_{i}");
            go.transform.SetParent(spokeGroupGo.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite        = _spokeSprite;
            img.color         = new Color(TUNNEL_COLOR.r, TUNNEL_COLOR.g, TUNNEL_COLOR.b, 0f);
            img.raycastTarget = false;
            _spokeImgs[i] = img;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0f, 0.5f);   // 안쪽 끝(중심) 기준 회전
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(1200f, 6f);
            rt.localRotation    = Quaternion.Euler(0f, 0f, 360f / TUNNEL_SPOKES * i);
        }

        // 동심원 링: zFar~zNear 사이 균등 분포로 초기화
        for (int i = 0; i < TUNNEL_RINGS; i++)
        {
            var go  = new GameObject($"TunnelRing_{i}");
            go.transform.SetParent(layerGo.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite        = _ringSprite;
            img.color         = new Color(TUNNEL_COLOR.r, TUNNEL_COLOR.g, TUNNEL_COLOR.b, 0f);
            img.raycastTarget = false;
            _ringImgs[i] = img;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            _ringRts[i] = rt;

            _ringZ[i] = Mathf.Lerp(TUNNEL_Z_NEAR, TUNNEL_Z_FAR, (i + 0.5f) / TUNNEL_RINGS);
        }

        BuildRainLayer();
    }

    // 빗방울은 그리드 안, CellLayer와 BlockLayer 사이에 낀다.
    //   백드롭 · 빈 셀  <  빗방울  <  놓인 블록  <  별
    // 터널 레이어에 두면 판 전체(불투명한 빈 셀)에 가려 존재감이 없고, 캔버스 최상단에 두면
    // 점이 UI 앞에 떠다녀 터널 "안"이라는 깊이감이 깨진다. 그 사이가 맞는 자리다.
    void BuildRainLayer()
    {
        _rainDotSprite = MakeDotSprite(32);

        _rainRts   = new RectTransform[RAIN_COUNT];
        _rainImgs  = new Image[RAIN_COUNT];
        _rainZ     = new float[RAIN_COUNT];
        _rainX     = new float[RAIN_COUNT];
        _rainY     = new float[RAIN_COUNT];
        _rainHue   = new float[RAIN_COUNT];
        _rainPhase = new float[RAIN_COUNT];

        var go = new GameObject("TunnelRainLayer");
        go.transform.SetParent(_gridRt, false);
        go.transform.SetSiblingIndex(_blockLayerRt.GetSiblingIndex());   // 블록 바로 아래

        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta    = Vector2.zero;
        // 빗방울 좌표는 화면 중앙 기준으로 계산된다(TunnelOffset). 그리드는 (0, 80)만큼
        // 올라가 있으므로 그만큼 되돌려 원점을 화면 중앙으로 맞춘다.
        // 그리드에는 마스크가 없어서 960×960 밖으로 나가는 점도 그대로 그려진다.
        rt.anchoredPosition = -_gridRt.anchoredPosition;
        _rainLayerRt    = rt;
        go.SetActive(false);   // RAIN_START 전에는 갱신도 렌더도 하지 않는다

        for (int i = 0; i < RAIN_COUNT; i++)
        {
            var dot = new GameObject($"Rain_{i}");
            dot.transform.SetParent(go.transform, false);
            var img           = dot.AddComponent<Image>();
            img.sprite        = _rainDotSprite;
            img.raycastTarget = false;
            _rainImgs[i]      = img;

            var drt       = dot.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot     = new Vector2(0.5f, 0.5f);
            _rainRts[i]   = drt;

            // z를 깊이 방향으로 고르게 흩어 둔다. 전부 zFar에서 시작하면 켜지는 순간
            // 한 겹이 통째로 몰려와 "벽이 다가오는" 것처럼 보인다.
            SpawnRainDot(i, Mathf.Lerp(TUNNEL_Z_NEAR, TUNNEL_Z_FAR, (i + 0.5f) / RAIN_COUNT));
        }
    }

    // 점 하나를 깊이 z의 단면 어딘가에 새로 놓는다.
    void SpawnRainDot(int i, float z)
    {
        // 터널 벽(반지름 0.5) 안쪽에 뿌리되 반지름에 sqrt를 씌운다.
        // 안 씌우면 넓이가 아니라 반지름 기준으로 균등해져 정중앙만 빽빽해진다.
        float ang = Random.Range(0f, Mathf.PI * 2f);
        float rad = Mathf.Sqrt(Random.value) * 0.46f;

        // 낙하 보정 ── 알갱이는 다가오는 내내 아래로 흘러서, 화면에서 크게 보이는
        // 가까운 것들은 이미 낙하량을 거의 다 쓴 뒤다. 그래서 눈에 띄는 알갱이가
        // 죄다 아래쪽에 몰린다. 앞으로 흘러내릴 만큼을 미리 위에서 시작시켜 상쇄한다.
        // 남은 거리를 속도로 나눈 게 남은 시간이므로, 상수를 만져도 자동으로 따라온다.
        float travelSec = (z - TUNNEL_Z_NEAR) / (TUNNEL_SPEED * TUNNEL2_SPEED_MUL * RAIN_SPEED_MUL);

        _rainX[i]     = Mathf.Cos(ang) * rad;
        _rainY[i]     = Mathf.Sin(ang) * rad + RAIN_FALL * travelSec * RAIN_RISE_BIAS;
        _rainZ[i]     = z;
        _rainHue[i]   = Random.value;
        _rainPhase[i] = Random.Range(0f, Mathf.PI * 2f);
    }

    // 터널 링과 같은 원근·곡률을 쓴다. 다른 건 점이 축에서 떨어진 만큼 옆으로 밀린다는 것뿐:
    //     화면 = TunnelOffset(z) + 월드오프셋 × (FOCAL / z)
    // 링이 지름 1의 원이라 sizeDelta가 FOCAL/z인 것과 같은 식이다.
    void UpdateTunnelRain(float now, double playbackSec, float intensity)
    {
        if (_rainLayerRt == null) return;

        bool on = playbackSec >= RAIN_START && intensity > 0f;
        if (_rainLayerRt.gameObject.activeSelf != on) _rainLayerRt.gameObject.SetActive(on);
        if (!on) return;

        float dz = TUNNEL_SPEED * TUNNEL2_SPEED_MUL * RAIN_SPEED_MUL * Time.deltaTime;

        for (int i = 0; i < RAIN_COUNT; i++)
        {
            _rainZ[i] -= dz;
            _rainY[i] -= RAIN_FALL * Time.deltaTime;   // 비처럼 아래로 흘러내린다
            if (_rainZ[i] <= TUNNEL_Z_NEAR) SpawnRainDot(i, TUNNEL_Z_FAR);

            float z     = _rainZ[i];
            float scale = TUNNEL_FOCAL / z;
            float sway  = Mathf.Sin(now * 1.7f + _rainPhase[i]) * RAIN_SWAY;

            _rainRts[i].anchoredPosition =
                TunnelOffset(z) + new Vector2((_rainX[i] + sway) * scale, _rainY[i] * scale);

            float px = Mathf.Max(RAIN_MIN_PX, RAIN_WORLD_SIZE * scale);
            _rainRts[i].sizeDelta = new Vector2(px, px);

            // 멀리서 들어올 때 페이드인, 카메라를 스칠 때 페이드아웃.
            // 스치는 쪽을 안 지우면 화면을 덮는 큰 원반이 되어 눈에 거슬린다.
            float fadeIn  = Mathf.InverseLerp(TUNNEL_Z_FAR, TUNNEL_Z_FAR - 2f, z);
            float fadeOut = Mathf.InverseLerp(TUNNEL_Z_NEAR, RAIN_FADE_Z, z);
            float a       = Mathf.Clamp01(fadeIn) * Mathf.Clamp01(fadeOut) * intensity;

            // 점마다 색상환 위치가 달라 알록달록하고, 시간 항이 더해져 천천히 변색된다.
            var c = Color.HSVToRGB(Mathf.Repeat(_rainHue[i] + now * 0.12f, 1f), 0.75f, 1f);
            _rainImgs[i].color = new Color(c.r, c.g, c.b, a);
        }
    }

    // 가장자리를 알파로 깎은 원. MakeRoundedSprite는 경계가 딱 떨어져서
    // 몇 px짜리로 줄이면 각져 보이므로 파티클용은 따로 굽는다.
    Sprite MakeDotSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var   px  = new Color32[size * size];
        float ctr = (size - 1) * 0.5f;
        float rad = size * 0.5f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - ctr, dy = y - ctr;
                float a  = Mathf.Clamp01((rad - Mathf.Sqrt(dx * dx + dy * dy)) / 1.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // 구간 i가 가진 곡률 벡터. 방향은 좌/우로 번갈아 뒤집히므로 "한쪽으로 길게 감다가
    // 반대쪽으로 홱" 넘어간다. 세기와 약간의 각도 지터만 랜덤이고, 세로로 눕지 않도록
    // 좌우 기준 ±34도 안에서만 흔든다(화면이 세로로 길어 위아래로 굽으면 금방 화면 밖으로 나간다).
    Vector2 SegmentCurvature(int i)
    {
        float angle = (((i & 1) == 0) ? 0f : Mathf.PI) + (CurveHash(i * 2) - 0.5f) * 1.2f;
        float mag   = Mathf.Lerp(0.55f, 1f, CurveHash(i * 2 + 1)) * TUNNEL_CURVE_AMP;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * mag;
    }

    // 결정적 해시 [0,1). 적분 중에 같은 구간을 여러 번 물어보므로 s와 무관하게 값이 고정돼야 한다.
    float CurveHash(int n)
    {
        float h = Mathf.Sin(n * 12.9898f + _tunnelCurveSeed) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    // 진행 거리 s에서의 곡률. 구간 안에서는 계속 같은 값을 유지하다가
    // 구간 앞쪽 TUNNEL_CURVE_TURN 비율 동안만 이전 구간 값에서 부드럽게 넘어간다.
    Vector2 TunnelCurvature(float s)
    {
        float seg = s / TUNNEL_CURVE_SEG;
        int   i   = Mathf.FloorToInt(seg);
        float t   = Mathf.Clamp01((seg - i) / TUNNEL_CURVE_TURN);
        t = t * t * (3f - 2f * t);   // smoothstep — 꺾이는 순간이 각지지 않게
        return Vector2.Lerp(SegmentCurvature(i - 1), SegmentCurvature(i), t);
    }

    // 카메라 위치에서 봤을 때, 깊이 z에 있는 단면이 화면에서 밀려나는 양.
    // 카메라는 직진이 아니라 길을 따라 앞을 보고 있으므로, 눈앞의 단면은 항상 화면 중앙이고
    // 밀려나는 양은 오직 그 사이에 쌓인 곡률에서 나온다:
    //     offset(z) = (FOCAL / z) · ∫₀^z (z - u)·K(travel + u) du
    // 곡률이 일정하면 정확히 K·z·FOCAL/2 — 깊이에 비례해 한쪽으로 감기는 미끄럼틀이 된다.
    // 1차 터널은 _curveOn이 false라 전부 정중앙 = 일직선.
    Vector2 TunnelOffset(float z)
    {
        if (!_curveOn) return Vector2.zero;

        Vector2 acc = Vector2.zero;
        float   du  = z / TUNNEL_CURVE_STEPS;
        for (int k = 0; k <= TUNNEL_CURVE_STEPS; k++)
        {
            float u = du * k;
            float w = (z - u) * ((k == 0 || k == TUNNEL_CURVE_STEPS) ? 0.5f : 1f);  // 사다리꼴
            acc += TunnelCurvature(_tunnelTravel + u) * w;
        }
        return acc * (du * TUNNEL_FOCAL / z);
    }

    // 터널이 켜질 때마다 굽는 방향을 새로 뽑는다.
    // 1차 터널은 일직선이라 curved=false로 들어오고, 2차만 매번 다른 방향으로 굽는다.
    void RerollTunnelCurve(bool curved)
    {
        _curveOn         = curved;
        _tunnelCurveSeed = Random.Range(0f, 1000f);
        // 첫 구간을 반쯤 지난 지점에서 시작 → 등장하자마자 곡률이 이미 붙어 있다.
        _tunnelTravel    = TUNNEL_CURVE_SEG * (Random.value < 0.5f ? 0.5f : 1.5f);
    }

    // 1차 터널
    //   50.0~50.5    파랑 페이드인
    //   50.5~64.70   파랑 풀 알파, 정지
    //   64.70~80.65  흰색, +TUNNEL_SPIN_DEG (시계방향) — 색은 즉시 전환
    //   80.65~96.7   빨강, -TUNNEL_SPIN_DEG (반시계방향) — 색/방향 모두 즉시 반전
    //   96.7~        터널 숨김 (흰색 단색 배경이 화면을 덮음)
    // 1차는 축이 곧은 일직선 터널이다(_curveOn = false).
    // 2차 터널 (곡 마지막)
    //   TUNNEL2_START~        검정이 걷히며 흰색 터널 페이드인.
    //                         링·회전 모두 1차보다 빠르고(TUNNEL2_*_MUL), 축이 랜덤 방향으로 굽는다.
    //   TUNNEL_RAINBOW_START~ 흰색 → 알록달록로 즉시 전환. 곡 끝까지 유지.
    // 알파 > 0 동안만 z 진행 (페이드인 중에도 링이 흘러야 자연스러움).
    void UpdateTunnel(float now, double playbackSec)
    {
        if (_ringRts == null) return;

        bool  second    = playbackSec >= TUNNEL2_START;
        float intensity = 0f;
        if      (playbackSec >= 50.0 && playbackSec < 50.5) intensity = (float)((playbackSec - 50.0) * 2.0);
        else if (playbackSec >= 50.5 && playbackSec < 96.7) intensity = 1f;
        else if (second) intensity = Mathf.Clamp01((float)((playbackSec - TUNNEL2_START) / TUNNEL2_FADE_SEC));

        // 곡 마무리 암전. 배경은 BLACK2_FULL부터 이미 검정이므로 그 위의 터널·빗방울만
        // 지우면 화면이 통째로 검게 남는다. 빗방울도 이 intensity를 그대로 받는다.
        if (playbackSec >= ENDING_BLACK_START)
            intensity *= 1f - Mathf.Clamp01((float)((playbackSec - ENDING_BLACK_START) / ENDING_FADE_SEC));

        // 페이즈별 색/회전속도 (즉시 전환, 그라데이션 없음)
        Color baseColor   = TUNNEL_COLOR;
        float angularDeg  = 0f;
        float speedMul    = 1f;
        if (second)
        {
            // 앞 구간(검은 하늘 + 흰 바닥/자동차)의 흑백 팔레트를 그대로 이어받는다.
            baseColor  = Color.white;
            angularDeg = -TUNNEL_SPIN_DEG * TUNNEL2_SPIN_MUL;
            speedMul   = TUNNEL2_SPEED_MUL;
        }
        else if (playbackSec >= 64.70 && playbackSec < 80.65)
        {
            baseColor  = Color.white;
            angularDeg = -TUNNEL_SPIN_DEG;     // 시계방향 (Unity Z축 음수가 CW)
        }
        else if (playbackSec >= 80.65 && playbackSec < 96.7)
        {
            baseColor  = TUNNEL_COLOR_RED;
            angularDeg = TUNNEL_SPIN_DEG;      // 반시계방향
        }

        _tunnelSpin += angularDeg * Time.deltaTime;

        if (intensity > 0f)
        {
            float dz = TUNNEL_SPEED * speedMul * Time.deltaTime;
            float zRange = TUNNEL_Z_FAR - TUNNEL_Z_NEAR;
            for (int i = 0; i < TUNNEL_RINGS; i++)
            {
                _ringZ[i] -= dz;
                if (_ringZ[i] < TUNNEL_Z_NEAR) _ringZ[i] += zRange;
            }
            // 카메라도 축을 따라 같은 거리만큼 전진한다. 링의 절대 위치(travel + z)는 그대로 유지되고,
            // 카메라만 움직이므로 각 링이 화면에서 서서히 옆으로 흘러간다.
            _tunnelTravel += dz;
        }

        // 스포크 그룹: 터널 끝(zFar)이 보이는 자리로 옮긴 뒤 그 점을 축으로 회전.
        if (_spokeGroupRt != null)
        {
            _spokeGroupRt.anchoredPosition = TunnelOffset(TUNNEL_Z_FAR);
            _spokeGroupRt.localRotation    = Quaternion.Euler(0f, 0f, _tunnelSpin);
        }

        bool rainbow = playbackSec >= TUNNEL_RAINBOW_START;

        // 링 갱신
        for (int i = 0; i < TUNNEL_RINGS; i++)
        {
            float z    = _ringZ[i];
            float size = TUNNEL_FOCAL / z;
            _ringRts[i].sizeDelta        = new Vector2(size, size);
            _ringRts[i].anchoredPosition = TunnelOffset(z);

            // 멀리(zFar 근처)에서 페이드인. 이후엔 풀 알파.
            float fadeIn = Mathf.InverseLerp(TUNNEL_Z_FAR, TUNNEL_Z_FAR - 1.5f, z);
            float alpha  = Mathf.Clamp01(fadeIn) * intensity * TUNNEL_RING_ALPHA;
            Color c      = rainbow ? RainbowAt(z, now) : baseColor;
            _ringImgs[i].color = new Color(c.r, c.g, c.b, alpha);
        }

        // 스포크: intensity만 반영 (정적). 살짝 옅게 깔아 링이 주연이 되게.
        // 알록달록 구간에는 터널 끝(zFar)의 색을 따라가 스포크가 모이는 자리와 색이 맞는다.
        if (_spokeImgs != null)
        {
            float spokeAlpha = TUNNEL_SPOKE_ALPHA * intensity;
            Color c = rainbow ? RainbowAt(TUNNEL_Z_FAR, now) : baseColor;
            for (int i = 0; i < _spokeImgs.Length; i++)
                _spokeImgs[i].color = new Color(c.r, c.g, c.b, spokeAlpha);
        }

        UpdateTunnelRain(now, playbackSec, intensity);
    }

    // 깊이 z에 있는 단면의 색. 터널 축 위의 절대 위치(travel + z)로 색상을 정하므로
    // 색 띠가 링에 붙어 카메라 쪽으로 같이 밀려오고, 거기에 시간 항이 더해져 계속 변색된다.
    Color RainbowAt(float z, float now)
    {
        float hue = Mathf.Repeat((_tunnelTravel + z) * TUNNEL_RAINBOW_PER_Z
                                 + now * TUNNEL_RAINBOW_SPEED, 1f);
        return Color.HSVToRGB(hue, 0.85f, 1f);
    }

    void BuildStarLayer()
    {
        _starSprite   = MakeStarSprite(96);
        _starRts      = new RectTransform[STAR_POOL];
        _starImgs     = new Image[STAR_POOL];
        _starBornTime = new float[STAR_POOL];
        _starLifetime = new float[STAR_POOL];
        _starBaseSize = new float[STAR_POOL];
        _starColor    = new Color[STAR_POOL];

        // 그리드의 마지막 자식으로 두면 셀/블록 위에 그려짐.
        var layerGo = new GameObject("StarLayer");
        layerGo.transform.SetParent(_gridRt, false);
        _starLayerGo = layerGo;
        var layerRt = layerGo.AddComponent<RectTransform>();
        layerRt.anchorMin = Vector2.zero;
        layerRt.anchorMax = Vector2.one;
        layerRt.offsetMin = layerRt.offsetMax = Vector2.zero;

        for (int i = 0; i < STAR_POOL; i++)
        {
            var go  = new GameObject($"Star_{i}");
            go.transform.SetParent(layerGo.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite        = _starSprite;
            img.raycastTarget = false;
            img.color         = new Color(1f, 1f, 1f, 0f);
            _starImgs[i] = img;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            _starRts[i] = rt;

            _starBornTime[i] = -1f;
        }
        _nextStarTime = Time.time + 0.4f;
    }

    // 반짝이는 흰 배경 페이드아웃이 끝난 뒤(>= 100.6s)부터만 스폰된다.
    // 곡 초반 디스코 인트로에는 별이 나오지 않는다.
    void UpdateStarSparkles(float now, float visibility, double playbackSec)
    {
        if (_starRts == null) return;

        // 새 별 스폰: 비활성 슬롯 하나에 그리드 셀 위치 ±지터로 배치
        if (playbackSec >= 100.6 && now >= _nextStarTime)
        {
            for (int i = 0; i < STAR_POOL; i++)
            {
                if (_starBornTime[i] >= 0f) continue;
                int gr = Random.Range(0, GameManager.SIZE);
                int gc = Random.Range(0, GameManager.SIZE);
                float px = -420f + gc * 120f + Random.Range(-35f, 35f);
                float py =  420f - gr * 120f + Random.Range(-35f, 35f);
                _starRts[i].anchoredPosition = new Vector2(px, py);

                _starBaseSize[i] = Random.Range(40f, 80f);
                _starLifetime[i] = Random.Range(0.25f, 0.45f);
                _starColor[i]    = STAR_COLORS[Random.Range(0, STAR_COLORS.Length)];
                _starBornTime[i] = now;
                break;
            }
            _nextStarTime = now + Random.Range(0.04f, 0.12f);
        }

        // 활성 별 갱신: 페이드인 15% / 페이드아웃 85%(파바박 튀는 느낌), 펄스 스케일
        for (int i = 0; i < STAR_POOL; i++)
        {
            if (_starBornTime[i] < 0f) continue;
            float age = (now - _starBornTime[i]) / _starLifetime[i];
            if (age >= 1f)
            {
                _starImgs[i].color = new Color(1f, 1f, 1f, 0f);
                _starBornTime[i]   = -1f;
                continue;
            }
            float alpha = age < 0.15f ? age / 0.15f : 1f - (age - 0.15f) / 0.85f;
            alpha = alpha * alpha * (3f - 2f * alpha);
            alpha *= 0.45f * visibility;   // 완전 불투명 대신 은은하게

            var c = _starColor[i];
            _starImgs[i].color = new Color(c.r, c.g, c.b, alpha);

            // 펄스 스케일
            float scale = 0.7f + 0.3f * (alpha + 0.4f);
            float sz    = _starBaseSize[i] * scale;
            _starRts[i].sizeDelta = new Vector2(sz, sz);
        }
    }
}
