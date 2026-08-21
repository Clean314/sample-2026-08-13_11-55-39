using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임 로직 (보드 상태 / 점수 / 조각 생성)
/// 빈 GameObject에 붙이거나 InGameUI가 자동 생성합니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public const int SIZE = 8;

    // 토글 모드 색상 인덱스 상수
    public const int TOGGLE_WHITE_IDX = 5;  // PieceData.Colors[5] → 화이트 블럭
    public const int TOGGLE_BLACK_IDX = 0;  // PieceData.Colors[0] → 블랙 블럭 (토글 모드에서 검정으로 렌더링)
    public const int SPECIAL_BLOCK_VAL = 7; // 보드 특수값: 스페셜 블럭 (토글 모드)
    public const int RAINBOW_BLOCK_VAL = 8; // 보드 특수값: 무지개 블럭 (디스코 모드)

    // 디스코 모드: 이 줄 수만큼 클리어될 때마다 무지개 블럭이 하나 생긴다.
    // (UI의 하트 게이지 개수도 이 값을 따라간다)
    public const int DISCO_LINES_PER_RAINBOW = 3;

    // 토글 모드: 이 횟수만큼 클리어해야 화이트↔블랙이 한 번 바뀐다.
    // 색 전환과 스페셜 블럭 생성이 같은 시점에 일어난다.
    // (UI의 게이지 원 개수도 이 값을 따라간다)
    public const int TOGGLE_CLEARS_PER_SWITCH = 3;

    public int[,] Board { get; private set; }   // 0=빈칸, 1~6=색상 인덱스+1, 7=스페셜 블럭, 8=무지개 블럭
    public int Score     { get; private set; }
    public int HighScore { get; private set; }

    // 토글 모드: 현재 활성 색상 (0 = 화이트 모드, 1 = 블랙 모드)
    public int ToggleCurrentColor { get; private set; } = 0;

    // 토글 모드: 색 전환 게이지 (TOGGLE_CLEARS_PER_SWITCH가 되면 색 전환 + 스페셜 블럭 생성 후 리셋)
    public int SpecialGauge { get; private set; } = 0;

    // 디스코 모드: 누적 클리어 줄 수 (DISCO_LINES_PER_RAINBOW마다 무지개 블럭 생성 후 차감)
    public int DiscoLineGauge { get; private set; } = 0;

    // 가장 최근 클리어된 행/열 (이펙트용)
    public List<int> LastClearedRows { get; private set; } = new List<int>();
    public List<int> LastClearedCols { get; private set; } = new List<int>();

    public struct PieceInstance
    {
        public int  shapeIndex;
        public int  colorIndex;
        public bool placed;
    }

    public PieceInstance[] CurrentPieces { get; private set; } = new PieceInstance[3];

    // UI가 구독해서 갱신
    public System.Action OnStateChanged;

    int    _combo;
    string _mk;   // 모드별 PlayerPrefs 키 prefix (예: "m0_", "m1_")

    // ── 콤보 ─────────────────────────────────────────────────────
    // 규칙이 두 갈래다. 헷갈리기 쉬우니 나눠서 적는다.
    //   · 오르는 건 클리어마다.  한 세트 안에서 두 번 지우면 콤보도 두 번 오른다.
    //   · 끊기는 건 세트마다.    조각 세 개를 다 쓰도록 한 번도 못 지워야 0으로 돌아간다.
    // 즉 줄이 안 난 배치 하나로는 안 끊긴다. 세트를 통째로 흘려보내야 끊긴다.
    bool _clearedThisSet;    // 이번 세트에서 한 번이라도 지웠는지
    bool _comboArmed;        // 첫 클리어를 한 뒤로 콤보 유지 규칙이 살아 있는지

    /// <summary>연속 클리어 수. 클리어할 때마다 오르고, 조각 한 세트를 아무것도 못 지우고
    /// 넘겼을 때만 0으로 돌아간다. 1은 그냥 한 번 지운 것이고, 2부터가 "연속"이다.</summary>
    public int Combo => _combo;

    /// <summary>이번 세트에서 이미 줄을 지웠는지. UI가 디스코 콤보 상태등에 쓴다.</summary>
    public bool ClearedThisSet => _clearedThisSet;

    /// <summary>디스코 모드에서 콤보를 놓쳐 판이 끝났는지.
    /// 이 모드는 "리듬을 끊지 마라"가 규칙이라, 한 세트를 통째로 못 지우면 게임오버다.
    /// 봐주는 경우는 하나뿐이다 — 첫 클리어 전. 빈 판에서 조각 셋으로 8칸 줄을 맞추는 건
    /// 운이고 무지개도 아직 없다. CloseSet 참고.</summary>
    public bool ComboFailed { get; private set; }

    /// <summary>줄을 지울 때마다 발행. 인자는 갱신된 콤보 수.
    /// UI가 이걸 받아 콤보 문구를 띄운다 — 문구는 클리어 시점에 떠야 한다.</summary>
    public System.Action<int> OnComboCleared;

    // 콤보 보너스: 클리어할 때마다 기본 점수 위에 얹는 추가 점수. 콤보가 커질수록 가파르다.
    //   2 → 50,  3 → 150,  4 → 300,  5 이상 → 500
    // 상한을 5에 두는 건 화면 표시가 MAX!에서 멈추는 것과 맞추기 위해서다. 화면은 MAX!인데
    // 점수만 계속 커지면 무엇 때문에 올랐는지 읽히지 않는다. InGameUI가 이 상수를 참조한다.
    public const int COMBO_TIER_MAX   = 5;
    const int        COMBO_BONUS_STEP = 25;

    /// <summary>줄을 지웠을 때 콤보를 1 올리고 점수(기본 + 콤보 보너스)를 더합니다.
    /// 이번 세트를 "지운 세트"로 표시해 두어, 세트가 끝날 때 CloseSet이 안 끊게 한다.
    /// 클리어가 일어나는 지점이 네 군데(배치·색상변환·아이스 슬라이드·무지개)라 여기 하나로 모은다.</summary>
    void AddLineScore(int lines)
    {
        _clearedThisSet = true;
        _combo++;

        int tier = Mathf.Min(_combo, COMBO_TIER_MAX);
        Score += lines * 100 * _combo
               + (tier >= 2 ? COMBO_BONUS_STEP * tier * (tier - 1) : 0);

        OnComboCleared?.Invoke(_combo);
    }

    /// <summary>조각 세 개를 다 썼을 때 콤보가 살아남는지 판정합니다.
    /// 이번 세트에서 한 번이라도 지웠으면 그대로 이어지고, 한 벌을 통째로
    /// 흘려보냈으면 여기서 끊긴다. 콤보를 올리는 건 이 함수의 일이 아니다.</summary>
    void CloseSet()
    {
        if (_clearedThisSet)
        {
            _comboArmed = true;   // 첫 클리어를 해냈으므로 이제부터 규칙이 걸린다
        }
        else
        {
            // 무지개 블럭은 "쓰면" 이미 여기까지 오지 않는다. ActivateRainbowBlock이
            // AddLineScore를 거치므로 그 세트는 지운 세트가 되고, 콤보도 한 칸 오른다.
            // 줄을 못 지운 세트를 무지개로 메우는 예외는 그 경로 하나로 충분하다.
            //
            // 반대로 "쥐고만 있는 것"으로 봐주면 안 된다. 무지개는 3줄마다 계속 나오므로
            // 하나만 안 쓰고 남겨 두면 어떤 세트를 흘려보내도 끝나지 않는다 —
            // 콤보가 영영 안 끊기고 판이 죽지 않는다.
            if (ModeSession.IsDisco && _comboArmed)
                ComboFailed = true;

            _combo = 0;
        }

        _clearedThisSet = false;
    }

    /// <summary>보드에 아직 쓰지 않은 무지개 블럭이 있는지.</summary>
    bool HasRainbowBlock()
    {
        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
                if (Board[r, c] == RAINBOW_BLOCK_VAL) return true;
        return false;
    }

    /// <summary>이어하기로 시작했는지. false면 이번이 새 판이다.
    /// 디스코 모드가 BGM을 처음부터 틀지 판단하는 데 쓴다(InGameUI.Start).
    /// Awake에서 한 번만 정해지므로 이후 SaveGame이 불려도 값은 변하지 않는다.</summary>
    public bool LoadedFromSave { get; private set; }

    /// <summary>이어하기로 복원한 디스코 BGM 재생 위치(초). 새 판이거나 다른 모드면 0.</summary>
    public double SavedBgmSec { get; private set; }

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        _mk       = $"m{ModeSession.SelectedMode}_";
        Board     = new int[SIZE, SIZE];
        HighScore = PlayerPrefs.GetInt(_mk + "HighScore", 0);
        LoadedFromSave = LoadGame();
        if (!LoadedFromSave)
            GenerateNewPieces();
    }

    // ── 배치 가능 여부 ──────────────────────────────────────────
    public bool CanPlacePiece(int idx, int row, int col)
    {
        if (CurrentPieces[idx].placed) return false;

        var shape = PieceData.Shapes[CurrentPieces[idx].shapeIndex];
        for (int r = 0; r < shape.Length; r++)
            for (int c = 0; c < shape[r].Length; c++)
            {
                if (shape[r][c] == 0) continue;
                int br = row + r, bc = col + c;
                if (br < 0 || br >= SIZE || bc < 0 || bc >= SIZE) return false;
                if (Board[br, bc] != 0) return false;
            }
        return true;
    }

    // ── 배치 실행 ───────────────────────────────────────────────
    public bool TryPlacePiece(int idx, int row, int col)
    {
        if (!CanPlacePiece(idx, row, col)) return false;

        var piece = CurrentPieces[idx];
        var shape = PieceData.Shapes[piece.shapeIndex];
        for (int r = 0; r < shape.Length; r++)
            for (int c = 0; c < shape[r].Length; c++)
            {
                if (shape[r][c] == 0) continue;
                Board[row + r, col + c] = piece.colorIndex + 1;
            }

        // 조각을 놓는 것만으로는 점수가 없다. 점수는 줄을 지웠을 때만 들어온다(AddLineScore).
        CurrentPieces[idx].placed = true;

        // 줄 클리어 + 콤보
        int lines = ClearFullLines();
        if (lines > 0)
        {
            AddLineScore(lines);

            // 토글 모드: 게이지를 올리고, 다 차면 색 전환 + 스페셜 블럭
            if (ModeSession.IsToggle) AdvanceToggleGauge();

            // 디스코 모드: 클리어된 줄 수를 누적해 DISCO_LINES_PER_RAINBOW줄마다 무지개 블럭을 하나 놓는다
            if (ModeSession.IsDisco)
            {
                DiscoLineGauge += lines;
                AwardRainbowBlocks();
            }
        }

        // 3개 모두 배치됐으면 세트를 닫고 새 세트 생성.
        // 콤보가 끊기는지 보는 자리는 여기 하나뿐이다 — 줄이 안 난 배치 하나로는 안 끊긴다.
        bool allDone = true;
        for (int i = 0; i < 3; i++)
            if (!CurrentPieces[i].placed) { allDone = false; break; }
        if (allDone)
        {
            CloseSet();
            GenerateNewPieces();
        }

        if (Score > HighScore)
        {
            HighScore = Score;
            SaveHighScore();
        }

        SaveGame();
        OnStateChanged?.Invoke();
        return true;
    }

    // ── 줄 클리어 ───────────────────────────────────────────────
    int ClearFullLines()
    {
        LastClearedRows.Clear();
        LastClearedCols.Clear();

        bool toggleMode = ModeSession.IsToggle;
        int  activeVal  = toggleMode
            ? (ToggleCurrentColor == 0 ? TOGGLE_WHITE_IDX : TOGGLE_BLACK_IDX) + 1
            : -1;

        var rows = new List<int>();
        var cols = new List<int>();

        // 스페셜 블럭은 줄을 완성시켜 주지 않는다. 들고 있는 동안 가로세로 두 줄을 막는 게
        // 아껴 두는 값이고, 그 대가가 있어야 "언제 쓸까"가 질문이 된다.
        // 덕분에 줄 클리어에 휩쓸려 사라질 일도 없다 — 낀 줄은 애초에 지워지지 않으니까.

        for (int r = 0; r < SIZE; r++)
        {
            bool clearable = true;
            for (int c = 0; c < SIZE; c++)
            {
                int v = Board[r, c];
                if (toggleMode ? v != activeVal : v == 0) { clearable = false; break; }
            }
            if (clearable) rows.Add(r);
        }
        for (int c = 0; c < SIZE; c++)
        {
            bool clearable = true;
            for (int r = 0; r < SIZE; r++)
            {
                int v = Board[r, c];
                if (toggleMode ? v != activeVal : v == 0) { clearable = false; break; }
            }
            if (clearable) cols.Add(c);
        }

        // 무지개 블럭은 줄 클리어로 없어지지 않는다 — 탭했을 때만 사라진다.
        // (줄을 채우는 데는 쓰이므로 클리어 판정에는 그대로 포함된다.)
        foreach (int r in rows) for (int c = 0; c < SIZE; c++) if (Board[r, c] != RAINBOW_BLOCK_VAL) Board[r, c] = 0;
        foreach (int c in cols) for (int r = 0; r < SIZE; r++) if (Board[r, c] != RAINBOW_BLOCK_VAL) Board[r, c] = 0;

        LastClearedRows.AddRange(rows);
        LastClearedCols.AddRange(cols);

        return rows.Count + cols.Count;
    }

    // ── 토글 모드 게이지 ────────────────────────────────────────
    /// <summary>
    /// 클리어 한 번을 게이지에 적는다. TOGGLE_CLEARS_PER_SWITCH번이 모이면 그때
    /// 화이트↔블랙을 뒤집고(미배치 조각 색도 같이 갱신) 스페셜 블럭을 하나 놓는다.
    /// 색 전환과 스페셜 블럭이 같은 시점에 일어나야 판이 바뀌는 순간이 한 박자로 읽힌다.
    /// </summary>
    void AdvanceToggleGauge()
    {
        SpecialGauge++;
        if (SpecialGauge < TOGGLE_CLEARS_PER_SWITCH) return;
        SpecialGauge = 0;

        ToggleCurrentColor = 1 - ToggleCurrentColor;
        int newColorIdx = ToggleCurrentColor == 0 ? TOGGLE_WHITE_IDX : TOGGLE_BLACK_IDX;
        for (int i = 0; i < 3; i++)
            if (!CurrentPieces[i].placed)
            {
                var p = CurrentPieces[i];
                p.colorIndex = newColorIdx;
                CurrentPieces[i] = p;
            }

        SpawnSpecialBlock();
    }

    // ── 스페셜 블럭 생성 ────────────────────────────────────────
    void SpawnSpecialBlock()
    {
        // 이미 스페셜 블럭이 있으면 중복 생성하지 않음
        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
                if (Board[r, c] == SPECIAL_BLOCK_VAL) return;

        // 방금 클리어된 셀 중 빈 칸 후보 수집
        var candidates = new List<(int, int)>();
        foreach (int r in LastClearedRows)
            for (int c = 0; c < SIZE; c++)
                if (Board[r, c] == 0) candidates.Add((r, c));
        foreach (int c in LastClearedCols)
            for (int r = 0; r < SIZE; r++)
                if (Board[r, c] == 0 && !candidates.Contains((r, c)))
                    candidates.Add((r, c));

        if (candidates.Count == 0) return;
        var pick = candidates[Random.Range(0, candidates.Count)];
        Board[pick.Item1, pick.Item2] = SPECIAL_BLOCK_VAL;
    }

    // ── 디스코 무지개 블럭 ──────────────────────────────────────
    /// <summary>게이지가 찬 만큼 무지개 블럭을 놓는다. 놓을 자리가 없으면 게이지를 남겨 다음 기회로 넘긴다.</summary>
    void AwardRainbowBlocks()
    {
        while (DiscoLineGauge >= DISCO_LINES_PER_RAINBOW)
        {
            if (!SpawnRainbowBlock()) break;
            DiscoLineGauge -= DISCO_LINES_PER_RAINBOW;
        }
    }

    /// <summary>빈 칸 하나에 무지개 블럭을 놓는다. 자리가 없으면 false.</summary>
    bool SpawnRainbowBlock()
    {
        // 방금 클리어된 자리를 우선 후보로 삼아 "줄이 터진 곳에서 나타나는" 인상을 준다.
        var candidates = new List<(int, int)>();
        foreach (int r in LastClearedRows)
            for (int c = 0; c < SIZE; c++)
                if (Board[r, c] == 0) candidates.Add((r, c));
        foreach (int c in LastClearedCols)
            for (int r = 0; r < SIZE; r++)
                if (Board[r, c] == 0 && !candidates.Contains((r, c)))
                    candidates.Add((r, c));

        // 클리어 자리가 이미 다 찼으면 보드의 아무 빈 칸이나
        if (candidates.Count == 0)
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (Board[r, c] == 0) candidates.Add((r, c));

        if (candidates.Count == 0) return false;

        var pick = candidates[Random.Range(0, candidates.Count)];
        Board[pick.Item1, pick.Item2] = RAINBOW_BLOCK_VAL;
        return true;
    }

    /// <summary>
    /// 무지개 블럭 발동: 해당 칸의 가로·세로 한 줄을 통째로 비운다.
    /// 같은 줄 위의 다른 무지개 블럭은 남는다 — 여기서 같이 지우면 그 블럭 몫의 폭발 연출이
    /// 통째로 사라진다. 남은 블럭까지 이어 터뜨리는 건 InGameUI.PlayRainbowChain이 맡아,
    /// 하나씩 순서대로 이 함수를 다시 부른다.
    /// 이펙트용으로 LastClearedRows/Cols를 채워 두므로 호출 측에서 클리어 연출을 그대로 쓸 수 있다.
    /// </summary>
    public bool ActivateRainbowBlock(int row, int col)
    {
        if (row < 0 || row >= SIZE || col < 0 || col >= SIZE) return false;
        if (Board[row, col] != RAINBOW_BLOCK_VAL) return false;

        Board[row, col] = 0;   // 발동한 본인은 먼저 치워야 아래 루프에서 살아남지 않는다
        for (int c = 0; c < SIZE; c++) if (Board[row, c] != RAINBOW_BLOCK_VAL) Board[row, c] = 0;
        for (int r = 0; r < SIZE; r++) if (Board[r, col] != RAINBOW_BLOCK_VAL) Board[r, col] = 0;

        LastClearedRows.Clear();
        LastClearedCols.Clear();
        LastClearedRows.Add(row);
        LastClearedCols.Add(col);

        // 가로+세로 = 2줄로 쳐서 기존 콤보 점수 체계를 그대로 따른다.
        // 다만 이 2줄은 DiscoLineGauge에 넣지 않는다 — 넣으면 무지개 블럭이 스스로를
        // 다시 불러내는 되먹임이 생긴다(3줄 기준이면 탭 두 번에 새 블럭이 나온다).
        // 조각을 쓰지 않으므로 세트를 마감하지 않는다. 대신 이번 세트를 "지운 세트"로
        // 표시해 콤보를 살려 준다 — 무지개를 쓴 세트가 콤보를 끊으면 앞뒤가 안 맞는다.
        AddLineScore(2);
        if (Score > HighScore)
        {
            HighScore = Score;
            SaveHighScore();
        }

        SaveGame();
        OnStateChanged?.Invoke();
        return true;
    }

    // ── 스페셜 블럭 사용: 한 줄 색상 통일 ───────────────────────
    /// <summary>보드에 놓인 스페셜 블럭 위치를 찾는다. 없으면 false.</summary>
    public bool FindSpecialBlock(out int row, out int col)
    {
        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
                if (Board[r, c] == SPECIAL_BLOCK_VAL) { row = r; col = c; return true; }
        row = col = -1;
        return false;
    }

    /// <summary>스페셜 블럭이 보드에 있는지.</summary>
    public bool HasSpecialBlock => FindSpecialBlock(out _, out _);

    /// <summary>
    /// 스페셜 블럭을 써서 고른 한 줄(가로 또는 세로)의 블럭을 전부 활성 색으로 맞춘다.
    /// 판 전체를 뒤집던 예전 방식은 전환이 방금 죽인 것을 통째로 되살려서, 이 모드의
    /// 유일한 위협인 색 전환을 매번 없던 일로 만들었다. 한 줄로 묶으면 보상이 정해져 있어
    /// 전환은 그대로 아프고, 대신 "어느 줄을 풀 것인가"라는 판을 읽는 결정이 남는다.
    ///
    /// 빈 칸은 채우지 않는다. 채워 주면 "무조건 한 줄 지우기"가 되어 색을 맞춘다는 규칙이
    /// 사라진다 — 꽉 찼는데 색만 어긋난 줄을 찾아내는 게 이 도구의 값이다.
    /// </summary>
    public bool ApplyLineSwap(bool horizontal, int index)
    {
        if (!ModeSession.IsToggle)  return false;
        if (index < 0 || index >= SIZE)     return false;
        if (!FindSpecialBlock(out int sr, out int sc)) return false;

        int activeVal = (ToggleCurrentColor == 0 ? TOGGLE_WHITE_IDX : TOGGLE_BLACK_IDX) + 1;

        // 스페셜 블럭은 쓰는 순간 사라진다. 다만 고른 줄 위에 있었다면 그 칸을 활성 색으로
        // 메운다 — 빈칸으로 만들면 정작 자기가 낀 줄을 고를 때 구멍이 나서 안 지워진다.
        bool onLine = horizontal ? sr == index : sc == index;
        Board[sr, sc] = onLine ? activeVal : 0;

        for (int i = 0; i < SIZE; i++)
        {
            int r = horizontal ? index : i;
            int c = horizontal ? i : index;
            if (Board[r, c] != 0) Board[r, c] = activeVal;
        }

        // 줄 하나만 건드리므로 되먹임이 없다. 여기서 난 클리어도 평범한 클리어로 쳐서
        // 점수·콤보·전환 게이지에 그대로 넣는다 — "스페셜로 한 줄 지워 전환을 앞당긴다"가
        // 부가 전략이 된다.
        int lines = ClearFullLines();
        if (lines > 0)
        {
            AddLineScore(lines);
            AdvanceToggleGauge();

            if (Score > HighScore)
            {
                HighScore = Score;
                SaveHighScore();
            }
        }

        SaveGame();
        OnStateChanged?.Invoke();
        return true;
    }

    // ── 새 조각 3개 생성 ────────────────────────────────────────
    void GenerateNewPieces()
    {
        var excluded        = ModeConfig.Current.excludedShapes;
        var weightOverrides = ModeConfig.Current.shapeWeightOverrides;

        bool toggleMode    = ModeSession.IsToggle;
        int  toggleColorIdx = toggleMode
            ? (ToggleCurrentColor == 0 ? TOGGLE_WHITE_IDX : TOGGLE_BLACK_IDX)
            : -1;

        // 유효한 조각의 누적 가중치 테이블 빌드
        int n = PieceData.Shapes.Length;
        var validIdx   = new System.Collections.Generic.List<int>(n);
        var cumWeights = new System.Collections.Generic.List<int>(n);
        int total = 0;
        for (int i = 0; i < n; i++)
        {
            if (excluded != null && System.Array.IndexOf(excluded, i) >= 0) continue;
            int w = (weightOverrides != null && i < weightOverrides.Length && weightOverrides[i] > 0)
                    ? weightOverrides[i]
                    : PieceData.ShapeWeights[i];
            total += w;
            validIdx.Add(i);
            cumWeights.Add(total);
        }

        var usedShapes = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 3; i++)
        {
            int shapeIndex;
            int attempts = 0;
            do
            {
                int pick = Random.Range(0, total);
                shapeIndex = validIdx[cumWeights.Count - 1];
                for (int j = 0; j < cumWeights.Count; j++)
                {
                    if (pick < cumWeights[j]) { shapeIndex = validIdx[j]; break; }
                }
                attempts++;
            }
            while (usedShapes.Contains(shapeIndex) && attempts < 20);

            usedShapes.Add(shapeIndex);
            CurrentPieces[i] = new PieceInstance
            {
                shapeIndex = shapeIndex,
                colorIndex = toggleMode ? toggleColorIdx : Random.Range(0, PieceData.Colors.Length),
                placed     = false
            };
        }
        // 여기서 _combo를 건드리지 않는다. 예전엔 "세트 안에서만 유지되는 콤보"라 새 세트마다
        // 0으로 밀었는데, 지금은 콤보 단위가 세트 자체다. 밀어버리면 CloseSet이 올린 값이
        // 바로 다음 줄에서 사라진다. 판을 새로 시작하는 쪽(ResetGame/Revive)에서만 초기화한다.
    }

    // ── 아이스모드: 한 칸 슬라이드 ─────────────────────────────
    /// <summary>점유된 셀을 한 칸 아래로 슬라이드. 이동한 셀이 있으면 true 반환.</summary>
    public bool SlideDown()
    {
        bool moved = false;
        int[,] next = (int[,])Board.Clone();
        for (int r = SIZE - 2; r >= 0; r--)
            for (int c = 0; c < SIZE; c++)
                if (Board[r, c] != 0 && Board[r + 1, c] == 0)
                {
                    next[r + 1, c] = Board[r, c];
                    next[r, c]     = 0;
                    moved          = true;
                }
        if (moved) Board = next;
        return moved;
    }

    /// <summary>슬라이드 후 새로 완성된 줄을 제거하고 점수 반영. OnStateChanged는 호출하지 않음.</summary>
    public int CheckAndClearLinesQuiet()
    {
        int lines = ClearFullLines();
        if (lines > 0)
        {
            AddLineScore(lines);
            if (Score > HighScore)
            {
                HighScore = Score;
                SaveHighScore();
            }
        }
        return lines;
    }

    // ── 놓을 수 있는 수가 있는지 확인 (게임오버 판정) ───────────
    public bool HasAnyValidMove()
    {
        // 토글 모드: 스페셜 블럭이 남아있으면 색상 변환으로 판도가 바뀔 수 있으므로 게임오버 아님
        if (ModeSession.IsToggle)
        {
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (Board[r, c] == SPECIAL_BLOCK_VAL) return true;
        }

        // 디스코 모드: 무지개 블럭을 탭하면 가로세로가 비므로 아직 수가 남아있다.
        // 이건 콤보와 다른 이야기다 — 놓을 자리가 없는 것과 줄을 못 지운 것은 별개의 끝이다.
        if (ModeSession.IsDisco && HasRainbowBlock()) return true;

        for (int i = 0; i < 3; i++)
        {
            if (CurrentPieces[i].placed) continue;
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (CanPlacePiece(i, r, c)) return true;
        }
        return false;
    }

    // ── 부활 (광고 시청 보상) ────────────────────────────────────
    /// <summary>
    /// 하단 3행을 비워 공간을 확보하고 새 조각을 지급합니다.
    /// 점수·최고점수는 유지됩니다.
    /// </summary>
    public void Revive()
    {
        // 하단 3행 클리어 (무지개 블럭은 탭으로만 없어진다는 규칙을 여기서도 지킨다)
        for (int r = SIZE - 3; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
                if (Board[r, c] != RAINBOW_BLOCK_VAL) Board[r, c] = 0;

        _combo = 0;
        _clearedThisSet = false;
        // 부활은 판이 이어지는 것이므로 실패 표시를 걷고 유예도 처음 상태로 돌린다.
        // 하단 3행만 비워 준 상태라 다시 발판을 만들 시간을 주는 게 맞다.
        ComboFailed = false;
        _comboArmed = false;
        GenerateNewPieces();
        SaveGame();
        OnStateChanged?.Invoke();
    }

    // ── 게임 재시작 ─────────────────────────────────────────────
    public void ResetGame()
    {
        Board              = new int[SIZE, SIZE];
        Score              = 0;
        _combo             = 0;
        _clearedThisSet    = false;
        ComboFailed        = false;
        _comboArmed        = false;
        ToggleCurrentColor = 0;
        SpecialGauge       = 0;
        DiscoLineGauge     = 0;
        GenerateNewPieces();
        ClearSave();
        OnStateChanged?.Invoke();
    }

    /// <summary>최고 점수를 저장하고 순위표에도 올린다.
    /// 갱신 지점이 네 군데(배치·무지개·줄 색맞춤·아이스 슬라이드)라 여기 하나로 모은다 —
    /// 흩어져 있으면 새 경로를 만들 때마다 순위표 제출을 빠뜨린다.</summary>
    void SaveHighScore()
    {
        PlayerPrefs.SetInt(_mk + "HighScore", HighScore);
        Leaderboards.ReportHighScore(ModeSession.SelectedMode, HighScore);
    }

    // ── 자동 저장 ────────────────────────────────────────────────
    // 죽은 판도 그대로 저장한다. 앱이 튕겨서 게임오버 화면을 못 본 사람이 광고 부활 기회를
    // 잃으면 안 되므로, 다음에 켰을 때 그 화면을 다시 띄워 선택할 기회를 준다.
    public void SaveGame()
    {
        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
            {
                if (r > 0 || c > 0) sb.Append(',');
                sb.Append(Board[r, c]);
            }
        PlayerPrefs.SetString(_mk + "save_board", sb.ToString());
        PlayerPrefs.SetInt(_mk + "save_score", Score);
        PlayerPrefs.SetInt(_mk + "save_combo", _combo);
        // 세트 도중에 나갈 수 있으므로 "이번 세트에서 지웠는지"도 같이 남긴다.
        // 안 그러면 이어하기 때 세트가 초기화돼 다 된 콤보가 끊긴다.
        PlayerPrefs.SetInt(_mk + "save_cleared_set", _clearedThisSet ? 1 : 0);
        // 콤보 유지 규칙의 상태도 같이 남긴다. 없으면 이어하기 때 규칙이 처음 상태로
        // 되돌아가 만회 기회가 공짜로 하나 더 생긴다.
        PlayerPrefs.SetInt(_mk + "save_combo_armed", _comboArmed ? 1 : 0);
        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.SetInt(_mk + $"save_p{i}_shape",  CurrentPieces[i].shapeIndex);
            PlayerPrefs.SetInt(_mk + $"save_p{i}_color",  CurrentPieces[i].colorIndex);
            PlayerPrefs.SetInt(_mk + $"save_p{i}_placed", CurrentPieces[i].placed ? 1 : 0);
        }
        // 토글 모드: 현재 색상 및 게이지 저장
        if (ModeSession.IsToggle)
        {
            PlayerPrefs.SetInt(_mk + "save_toggle_color",   ToggleCurrentColor);
            PlayerPrefs.SetInt(_mk + "save_special_gauge",  SpecialGauge);
        }
        // 디스코 모드: 무지개 블럭 게이지 + 곡 재생 위치 저장.
        // 디스코는 재생 위치가 곧 연출 타임라인이라, 판만 되돌리고 곡을 안 되돌리면
        // 밤거리에서 나갔다가 터널에서 이어받는 식이 된다.
        if (ModeSession.IsDisco)
        {
            PlayerPrefs.SetInt(_mk + "save_disco_gauge", DiscoLineGauge);
            if (BGMManager.Instance != null)
                PlayerPrefs.SetFloat(_mk + "save_disco_bgm", (float)BGMManager.Instance.PositionSec);
        }

        PlayerPrefs.SetInt(_mk + "save_flag", 1);
        PlayerPrefs.Save();
    }

    bool LoadGame()
    {
        if (!PlayerPrefs.HasKey(_mk + "save_flag")) return false;

        var parts = PlayerPrefs.GetString(_mk + "save_board", "").Split(',');
        if (parts.Length != SIZE * SIZE) return false;

        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
                Board[r, c] = int.Parse(parts[r * SIZE + c]);

        // 토글 모드: 저장된 색상 및 게이지 복원
        if (ModeSession.IsToggle)
        {
            ToggleCurrentColor = PlayerPrefs.GetInt(_mk + "save_toggle_color",  0);
            // 임계값이 바뀐 옛 저장값이 그대로 들어오면 UI 원 개수를 넘거나 즉시 전환이 터진다.
            // 로드 시점에 [0, 임계값-1]로 클램프해서 UI/로직과 일관되게 유지.
            SpecialGauge       = Mathf.Clamp(PlayerPrefs.GetInt(_mk + "save_special_gauge", 0), 0, TOGGLE_CLEARS_PER_SWITCH - 1);
        }

        // 디스코 모드: 무지개 블럭 게이지 + 곡 재생 위치 복원.
        // 키가 없는 옛 저장은 0 — 곡을 처음부터 트는 셈이라 그대로 두어도 무해하다.
        if (ModeSession.IsDisco)
        {
            DiscoLineGauge = Mathf.Clamp(
                PlayerPrefs.GetInt(_mk + "save_disco_gauge", 0), 0, DISCO_LINES_PER_RAINBOW - 1);
            SavedBgmSec = PlayerPrefs.GetFloat(_mk + "save_disco_bgm", 0f);
        }

        Score  = PlayerPrefs.GetInt(_mk + "save_score", 0);
        _combo          = PlayerPrefs.GetInt(_mk + "save_combo", 0);
        _clearedThisSet  = PlayerPrefs.GetInt(_mk + "save_cleared_set", 0) == 1;
        _comboArmed      = PlayerPrefs.GetInt(_mk + "save_combo_armed", 0) == 1;
        for (int i = 0; i < 3; i++)
            CurrentPieces[i] = new PieceInstance
            {
                shapeIndex = PlayerPrefs.GetInt(_mk + $"save_p{i}_shape", 0),
                colorIndex = PlayerPrefs.GetInt(_mk + $"save_p{i}_color", 0),
                placed     = PlayerPrefs.GetInt(_mk + $"save_p{i}_placed", 0) == 1,
            };
        return true;
    }

    void ClearSave()
    {
        PlayerPrefs.DeleteKey(_mk + "save_flag");
        PlayerPrefs.DeleteKey(_mk + "save_board");
        PlayerPrefs.DeleteKey(_mk + "save_score");
        PlayerPrefs.DeleteKey(_mk + "save_combo");
        PlayerPrefs.DeleteKey(_mk + "save_cleared_set");
        PlayerPrefs.DeleteKey(_mk + "save_combo_armed");
        PlayerPrefs.DeleteKey(_mk + "save_toggle_color");
        PlayerPrefs.DeleteKey(_mk + "save_special_gauge");
        PlayerPrefs.DeleteKey(_mk + "save_disco_gauge");
        PlayerPrefs.DeleteKey(_mk + "save_disco_bgm");
        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.DeleteKey(_mk + $"save_p{i}_shape");
            PlayerPrefs.DeleteKey(_mk + $"save_p{i}_color");
            PlayerPrefs.DeleteKey(_mk + $"save_p{i}_placed");
        }
        PlayerPrefs.Save();
    }
}
