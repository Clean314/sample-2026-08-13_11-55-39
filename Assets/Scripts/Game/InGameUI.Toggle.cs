using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// InGameUI 의 토글 모드 전용 부분 — 스페셜 블럭을 끌어 한 줄을 고르는 조작,
// 조준 하이라이트, 드래그 안내 손, 모드 색에 맞춘 배경·게이지 갱신.
public partial class InGameUI
{

    // 토글 모드 배경/텍스트 색상 갱신
    void RefreshToggleModeBackground()
    {
        bool blackMode = _gm.ToggleCurrentColor == 1;

        // 색상이 실제로 전환됐을 때만 효과음 재생
        int curColor = _gm.ToggleCurrentColor;
        if (_prevToggleColor != curColor && _prevToggleColor != -1)
        {
            if (_audioSource != null && _sfxToggle != null)
                _audioSource.PlayOneShot(_sfxToggle);
        }
        _prevToggleColor = curColor;

        if (_bgImage != null)
            _bgImage.color = blackMode ? BG_LIGHT : BG_DARK;

        if (_scoreText != null)
            _scoreText.color = blackMode ? new Color(0.10f, 0.08f, 0.20f) : Color.white;

        if (_highScoreText != null)
            _highScoreText.color = blackMode ? new Color(0.60f, 0.40f, 0.00f) : GOLD;

    }

    // 게이지 원 색상 갱신.
    // 채워진 원은 지금 모드의 색(화이트 모드=흰색, 블랙 모드=검정)으로 칠해
    // 게이지만 봐도 어느 색을 지워야 하는지 알 수 있게 한다.
    void RefreshGauge()
    {
        if (_gaugeCircles == null || _gaugeCircles[0] == null) return;
        bool blackMode   = _gm.ToggleCurrentColor == 1;
        Color fullColor  = blackMode
            ? new Color(0.12f, 0.12f, 0.16f)   // 밝은 배경 위의 검정 원
            : new Color(0.92f, 0.92f, 0.94f);  // 어두운 배경 위의 흰 원
        Color emptyColor = blackMode
            ? new Color(0.55f, 0.55f, 0.65f)
            : new Color(0.30f, 0.30f, 0.40f);

        for (int i = 0; i < _gaugeCircles.Length; i++)
            _gaugeCircles[i].color = i < _gm.SpecialGauge ? fullColor : emptyColor;
    }

    // ── 토글 모드 스페셜 블럭: 끌어서 한 줄 색상 맞추기 ─────────
    // 스페셜 블럭에 손을 얹고 판 위를 쓸면 그 줄이 조준된다. 떼면 적용, 판 밖에서 떼면 취소.
    // 그리드 폭이 960이라 화면 좌우 여백이 45씩밖에 없어서, 줄마다 손잡이를 다는 방식은
    // 애초에 들어가지 않는다. 쓸어내는 손짓 자체가 "이 줄을 맞춘다"를 그대로 그린다.
    public void BeginSpecialDrag(int row, int col, Vector2 screenPos)
    {
        if (_gm == null || _dragging || _busy || _specDrag)      return;
        if (!ModeSession.IsToggle)                       return;
        if (_gm.Board[row, col] != GameManager.SPECIAL_BLOCK_VAL) return;

        _dragStartTime  = Time.unscaledTime;
        _specDrag       = true;
        _specRow        = row;
        _specCol        = col;
        _specAxisSet    = false;
        _specHorizontal = true;
        _specLine       = -1;
        _specAnchor     = screenPos;

        // 진짜로 끌기 시작했으면 안내는 제 할 일을 다 했다.
        StopDragGuide();

        _lineHiRow = MakeLineHighlight("LineHi_Row", new Vector2(960, 120));
        _lineHiCol = MakeLineHighlight("LineHi_Col", new Vector2(120, 960));

        // 집어 든 블럭은 살짝 키워 둔다 — 무엇을 끌고 있는지가 보여야 한다.
        if (_blockOverlays[row, col] != null)
            _blockOverlays[row, col].rectTransform.localScale = Vector3.one * 1.14f;

        UpdateSpecialDrag(screenPos);
    }

    public void UpdateSpecialDrag(Vector2 screenPos)
    {
        if (!_specDrag) return;

        // 축은 방금 민 방향으로 잡는다. SPEC_AXIS_STEP만큼 움직일 때마다 다시 본다.
        Vector2 d   = screenPos - _specAnchor;
        float   sf  = _canvas != null ? _canvas.scaleFactor : 1f;
        float   ax  = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        float   dom = Mathf.Max(ax, ay), sub = Mathf.Min(ax, ay);

        if (dom > SPEC_AXIS_STEP * sf)
        {
            bool wantH = ax > ay;
            // 이미 잡힌 축을 뒤집을 때만 뚜렷한 차이를 요구한다.
            // 대각선 근처에서 가로세로가 번갈아 깜빡이는 걸 막는다.
            if (!_specAxisSet || wantH == _specHorizontal || dom > sub * 1.5f)
            {
                _specHorizontal = wantH;
                _specAxisSet    = true;
            }
            _specAnchor = screenPos;
        }

        _specLine = ScreenToCell(screenPos, out int row, out int col)
            ? (_specHorizontal ? row : col)
            : -1;

        RefreshSpecialDrag();
    }

    public void EndSpecialDrag(Vector2 screenPos)
    {
        if (!_specDrag) return;
        UpdateSpecialDrag(screenPos);

        bool apply      = _specAxisSet && _specLine >= 0;
        bool horizontal = _specHorizontal;
        int  line       = _specLine;

        // 하이라이트와 확대를 먼저 걷어야 이어지는 클리어 연출을 덮지 않는다.
        FinishSpecialDrag();
        if (!apply) return;

        // 보드를 먼저 바꾼다. 그래야 RefreshGrid가 새 색을 그려 놓은 뒤에 뒤집기가 시작돼,
        // 넘어가는 순간 이미 바뀐 면이 드러난다.
        _gm.ApplyLineSwap(horizontal, line);

        if (_audioSource != null && _sfxToggleSpecial != null)
            _audioSource.PlayOneShot(_sfxToggleSpecial);

        PlayLineSwapFlip(horizontal, line);
    }

    // ── 한 줄이 넘어가는 연출 ────────────────────────────────────
    // 카드가 한 장씩 뒤집히듯 가로줄은 세로축, 세로줄은 가로축으로 눌렸다 펴진다.
    // 지워진 칸은 건드리지 않는다 — 그 칸은 클리어 연출이 따로 맡고 있어서,
    // 여기서 배율을 되돌리면 사라지던 블록이 도로 튀어나온다.
    void PlayLineSwapFlip(bool horizontal, int index)
    {
        if (_swapFlipCo != null) StopCoroutine(_swapFlipCo);
        _swapFlipCo = StartCoroutine(LineSwapFlip(horizontal, index));
    }

    IEnumerator LineSwapFlip(bool horizontal, int index)
    {
        float total = SWAP_FLIP_SEC + SWAP_FLIP_STAGGER * (GameManager.SIZE - 1);

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < GameManager.SIZE; i++)
            {
                int r = horizontal ? index : i;
                int c = horizontal ? i : index;
                if (_gm.Board[r, c] == 0) continue;

                var img = _blockOverlays[r, c];
                if (img == null) continue;

                float k = (t - i * SWAP_FLIP_STAGGER) / SWAP_FLIP_SEC;
                // 1 → 0 → 1. 가운데에서 두께가 0이 되면서 면이 넘어간 것처럼 보인다.
                float s = (k <= 0f || k >= 1f) ? 1f : Mathf.Abs(Mathf.Cos(k * Mathf.PI));

                img.rectTransform.localScale = horizontal
                    ? new Vector3(s, 1f, 1f)
                    : new Vector3(1f, s, 1f);
            }
            yield return null;
        }

        for (int i = 0; i < GameManager.SIZE; i++)
        {
            int r = horizontal ? index : i;
            int c = horizontal ? i : index;
            if (_gm.Board[r, c] == 0) continue;
            if (_blockOverlays[r, c] != null)
                _blockOverlays[r, c].rectTransform.localScale = Vector3.one;
        }
        _swapFlipCo = null;
    }

    void FinishSpecialDrag()
    {
        if (_specRow >= 0 && _blockOverlays[_specRow, _specCol] != null)
            _blockOverlays[_specRow, _specCol].rectTransform.localScale = Vector3.one;

        _specDrag    = false;
        _specAxisSet = false;
        _specLine    = -1;
        _specRow     = _specCol = -1;
        _specEndTime = Time.time;

        if (_lineHiRow != null) Destroy(_lineHiRow.gameObject);
        if (_lineHiCol != null) Destroy(_lineHiCol.gameObject);
        _lineHiRow = _lineHiCol = null;
    }

    // 조준 줄 하이라이트 갱신. 어느 줄을 겨누고 있는지만 보여 준다 —
    // 그 줄이 바로 지워지는지는 판을 보면 알 수 있고, 색으로 갈라 놓으면
    // 고를 수 있는 줄과 없는 줄이 따로 있는 것처럼 읽힌다.
    void RefreshSpecialDrag()
    {
        bool aiming = _specAxisSet && _specLine >= 0;

        if (_lineHiRow != null)
        {
            if (aiming && _specHorizontal)
            {
                _lineHiRow.rectTransform.anchoredPosition = new Vector2(0, 420 - _specLine * 120);
                _lineHiRow.color = LineHighlightColor();
            }
            else _lineHiRow.color = Color.clear;
        }

        if (_lineHiCol != null)
        {
            if (aiming && !_specHorizontal)
            {
                _lineHiCol.rectTransform.anchoredPosition = new Vector2(-420 + _specLine * 120, 0);
                _lineHiCol.color = LineHighlightColor();
            }
            else _lineHiCol.color = Color.clear;
        }
    }

    /// <summary>
    /// 조준 줄 색. 같은 알파라도 어두운 바탕 위의 밝은 띠는 훨씬 진하고 불투명하게 읽힌다
    /// (밝은 바탕에서는 반대로 묻힌다). 화이트 모드는 배경이 검으므로 그만큼 옅게 깐다.
    /// </summary>
    Color LineHighlightColor()
    {
        Color c = LINE_HI;
        if (_gm != null && _gm.ToggleCurrentColor == 0) c.a *= LINE_HI_DARK_BG;
        return c;
    }

    /// <summary>
    /// 화면 좌표 → 보드 칸. 조각 드래그(GetGridCell)와 달리 손가락 위치 보정을 주지 않는다.
    /// 저쪽은 조각이 손가락에 가리지 않게 위로 띄우지만, 여기서는 짚은 칸이 곧 고른 줄이라
    /// 보정이 들어가면 눈에 보이는 것과 골라지는 줄이 어긋난다.
    /// 가장자리에서 한 칸까지는 넘어가도 그 줄로 쳐준다 — 8번째 줄을 쓸 때 손이 판을 살짝
    /// 벗어나는 건 흔한 일이고, 그때마다 취소되면 쓸 수가 없다.
    /// </summary>
    bool ScreenToCell(Vector2 screenPos, out int row, out int col)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _gridRt, screenPos, null, out Vector2 lp);

        float fr = (-lp.y + 480f) / 120f;
        float fc = ( lp.x + 480f) / 120f;

        row = Mathf.Clamp(Mathf.FloorToInt(fr), 0, GameManager.SIZE - 1);
        col = Mathf.Clamp(Mathf.FloorToInt(fc), 0, GameManager.SIZE - 1);

        return fr > -1f && fr < GameManager.SIZE + 1f
            && fc > -1f && fc < GameManager.SIZE + 1f;
    }

    Image MakeLineHighlight(string name, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_gridRt, false);
        go.transform.SetAsLastSibling();
        var img           = go.AddComponent<Image>();
        img.sprite        = _spr110;
        img.type          = Image.Type.Sliced;
        img.color         = Color.clear;
        img.raycastTarget = false;
        var rt            = go.GetComponent<RectTransform>();
        rt.anchorMin      = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot          = new Vector2(0.5f, 0.5f);
        rt.sizeDelta      = size;
        return img;
    }

    // ── 드래그 안내: 손가락이 한 번 쓸어 보인다 ─────────────────
    // 알려 줄 건 하나뿐이다 — "이건 눌러서 되는 게 아니라 끄는 것이다".
    // 어느 줄을 고를 수 있는지, 가로냐 세로냐는 한 번 끌어 보면 그 자리에서 보이므로
    // 여기서 미리 가르치지 않는다. 설명이 길어지면 아무도 안 본다.
    void ShowDragGuide(int row, int col)
    {
        if (_dragGuideSprite == null || _gridRt == null) return;
        StopDragGuide();
        _dragGuideCo = StartCoroutine(DragGuideLoop(row, col));
    }

    void StopDragGuide()
    {
        if (_dragGuideCo != null) { StopCoroutine(_dragGuideCo); _dragGuideCo = null; }
        if (_dragGuideGo != null) { Destroy(_dragGuideGo);       _dragGuideGo = null; }
    }

    IEnumerator DragGuideLoop(int row, int col)
    {
        var handGo = new GameObject("DragGuide");
        handGo.transform.SetParent(_gridRt, false);
        handGo.transform.SetAsLastSibling();
        var hand           = handGo.AddComponent<Image>();
        hand.sprite        = _dragGuideSprite;
        hand.color         = Color.clear;
        hand.raycastTarget = false;
        var handRt         = handGo.GetComponent<RectTransform>();
        handRt.anchorMin   = handRt.anchorMax = new Vector2(0.5f, 0.5f);
        handRt.pivot       = new Vector2(0.5f, 0.5f);
        handRt.sizeDelta   = new Vector2(GUIDE_HAND_SIZE, GUIDE_HAND_SIZE);
        _dragGuideGo       = handGo;

        // 판 안에 남는 쪽으로 쓴다. 스페셜 블럭이 가장자리에 있어도 손이 허공을 훑지 않게.
        Vector2 from = CellPos(row, col);
        Vector2 to   = CellPos(Step(row, GUIDE_SWEEP_ROWS), Step(col, GUIDE_SWEEP_COLS));
        Vector2 tip  = GUIDE_TIP_OFFSET * GUIDE_HAND_SIZE;

        for (int pass = 0; pass < 2; pass++)
        {
            for (float t = 0f; t < GUIDE_SWEEP_SEC; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / GUIDE_SWEEP_SEC);

                // 들어올 때 빠르게, 나갈 때 조금 여유 있게.
                float a = Mathf.Clamp01(Mathf.Min(k / 0.18f, (1f - k) / 0.28f));

                // 손은 등속으로 밀지 않는다. 붙었다 떨어지듯 움직여야 "민다"로 읽힌다.
                handRt.anchoredPosition = Vector2.Lerp(from, to, Smooth(k)) + tip;
                hand.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
        }

        _dragGuideCo = null;
        StopDragGuide();
    }

    /// <summary>보드 칸 → 그리드 로컬 좌표(칸 중앙).</summary>
    static Vector2 CellPos(int row, int col)
        => new Vector2(-420f + col * 120f, 420f - row * 120f);

    /// <summary>판 안에 남는 쪽으로 n칸 옮긴 자리. 여유가 있는 쪽을 고른다.</summary>
    static int Step(int idx, int n)
        => idx + n <= GameManager.SIZE - 1 ? idx + n : Mathf.Max(idx - n, 0);

    static float Smooth(float k)
    {
        k = Mathf.Clamp01(k);
        return k * k * (3f - 2f * k);
    }
}
