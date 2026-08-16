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
    public const int DISCO_LINES_PER_RAINBOW = 4;

    public int[,] Board { get; private set; }   // 0=빈칸, 1~6=색상 인덱스+1, 7=스페셜 블럭, 8=무지개 블럭
    public int Score     { get; private set; }
    public int HighScore { get; private set; }

    // 토글 모드: 현재 활성 색상 (0 = 화이트 모드, 1 = 블랙 모드)
    public int ToggleCurrentColor { get; private set; } = 0;

    // 토글 모드: 스페셜 블럭 게이지 (0~1, 2가 되면 스페셜 블럭 생성 후 리셋)
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

    /// <summary>이어하기로 시작했는지. false면 이번이 새 판이다.
    /// 디스코 모드가 BGM을 어디서부터 틀지 판단하는 데 쓴다(InGameUI.Start).
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
        int cells = 0;

        for (int r = 0; r < shape.Length; r++)
            for (int c = 0; c < shape[r].Length; c++)
            {
                if (shape[r][c] == 0) continue;
                Board[row + r, col + c] = piece.colorIndex + 1;
                cells++;
            }

        // 기본 점수: 칸당 10점
        Score += cells * 10;
        CurrentPieces[idx].placed = true;

        // 줄 클리어 + 콤보
        int lines = ClearFullLines();
        if (lines > 0)
        {
            _combo++;
            Score += lines * 100 * _combo;

            // 토글 모드: 클리어 시 색상 토글 + 미배치 조각 색상 갱신 + 게이지 증가
            if (ModeSession.SelectedMode == 2)
            {
                ToggleCurrentColor = 1 - ToggleCurrentColor;
                int newColorIdx = ToggleCurrentColor == 0 ? TOGGLE_WHITE_IDX : TOGGLE_BLACK_IDX;
                for (int i = 0; i < 3; i++)
                    if (!CurrentPieces[i].placed)
                    {
                        var p = CurrentPieces[i];
                        p.colorIndex = newColorIdx;
                        CurrentPieces[i] = p;
                    }

                SpecialGauge++;
                if (SpecialGauge >= 2)
                {
                    SpawnSpecialBlock();
                    SpecialGauge = 0;
                }
            }

            // 디스코 모드: 클리어된 줄 수를 누적해 DISCO_LINES_PER_RAINBOW줄마다 무지개 블럭을 하나 놓는다
            if (ModeSession.SelectedMode == 3)
            {
                DiscoLineGauge += lines;
                AwardRainbowBlocks();
            }
        }
        else
        {
            _combo = 0;
        }

        if (Score > HighScore)
        {
            HighScore = Score;
            PlayerPrefs.SetInt(_mk + "HighScore", HighScore);
        }

        // 3개 모두 배치됐으면 새 세트 생성
        bool allDone = true;
        for (int i = 0; i < 3; i++)
            if (!CurrentPieces[i].placed) { allDone = false; break; }
        if (allDone)
            GenerateNewPieces();

        SaveGame();
        OnStateChanged?.Invoke();
        return true;
    }

    // ── 줄 클리어 ───────────────────────────────────────────────
    int ClearFullLines()
    {
        LastClearedRows.Clear();
        LastClearedCols.Clear();

        bool toggleMode = ModeSession.SelectedMode == 2;
        int  activeVal  = toggleMode
            ? (ToggleCurrentColor == 0 ? TOGGLE_WHITE_IDX : TOGGLE_BLACK_IDX) + 1
            : -1;

        var rows = new List<int>();
        var cols = new List<int>();

        for (int r = 0; r < SIZE; r++)
        {
            bool clearable = true;
            for (int c = 0; c < SIZE; c++)
            {
                int v = Board[r, c];
                if (toggleMode ? (v != activeVal && v != SPECIAL_BLOCK_VAL) : v == 0) { clearable = false; break; }
            }
            if (clearable) rows.Add(r);
        }
        for (int c = 0; c < SIZE; c++)
        {
            bool clearable = true;
            for (int r = 0; r < SIZE; r++)
            {
                int v = Board[r, c];
                if (toggleMode ? (v != activeVal && v != SPECIAL_BLOCK_VAL) : v == 0) { clearable = false; break; }
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
        _combo++;
        Score += 2 * 100 * _combo;
        if (Score > HighScore)
        {
            HighScore = Score;
            PlayerPrefs.SetInt(_mk + "HighScore", HighScore);
        }

        SaveGame();
        OnStateChanged?.Invoke();
        return true;
    }

    // ── 스페셜 블럭 사용: 색상 일괄 변환 ───────────────────────
    public void ApplyColorSwap(bool blackToWhite)
    {
        int from = blackToWhite ? TOGGLE_BLACK_IDX + 1 : TOGGLE_WHITE_IDX + 1;
        int to   = blackToWhite ? TOGGLE_WHITE_IDX + 1 : TOGGLE_BLACK_IDX + 1;

        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
            {
                if      (Board[r, c] == from)              Board[r, c] = to;
                else if (Board[r, c] == SPECIAL_BLOCK_VAL) Board[r, c] = 0;
            }

        // 색 변환 후 가득 찬 줄 클리어 체크
        int lines = ClearFullLines();
        if (lines > 0)
        {
            _combo++;
            Score += lines * 100 * _combo;

            ToggleCurrentColor = 1 - ToggleCurrentColor;
            int newColorIdx = ToggleCurrentColor == 0 ? TOGGLE_WHITE_IDX : TOGGLE_BLACK_IDX;
            for (int i = 0; i < 3; i++)
                if (!CurrentPieces[i].placed)
                {
                    var p = CurrentPieces[i];
                    p.colorIndex = newColorIdx;
                    CurrentPieces[i] = p;
                }

            SpecialGauge++;
            if (SpecialGauge >= 2)
            {
                SpawnSpecialBlock();
                SpecialGauge = 0;
            }

            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(_mk + "HighScore", HighScore);
            }
        }

        SaveGame();
        OnStateChanged?.Invoke();
    }

    // ── 새 조각 3개 생성 ────────────────────────────────────────
    void GenerateNewPieces()
    {
        var excluded        = ModeConfig.Current.excludedShapes;
        var weightOverrides = ModeConfig.Current.shapeWeightOverrides;

        bool toggleMode    = ModeSession.SelectedMode == 2;
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
        _combo = 0;
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
            _combo++;
            Score += lines * 100 * _combo;
            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(_mk + "HighScore", HighScore);
            }
        }
        return lines;
    }

    // ── 놓을 수 있는 수가 있는지 확인 (게임오버 판정) ───────────
    public bool HasAnyValidMove()
    {
        // 토글 모드: 스페셜 블럭이 남아있으면 색상 변환으로 판도가 바뀔 수 있으므로 게임오버 아님
        if (ModeSession.SelectedMode == 2)
        {
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (Board[r, c] == SPECIAL_BLOCK_VAL) return true;
        }

        // 디스코 모드: 무지개 블럭을 탭하면 가로세로가 비므로 아직 수가 남아있다
        if (ModeSession.SelectedMode == 3)
        {
            for (int r = 0; r < SIZE; r++)
                for (int c = 0; c < SIZE; c++)
                    if (Board[r, c] == RAINBOW_BLOCK_VAL) return true;
        }

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
        ToggleCurrentColor = 0;
        SpecialGauge       = 0;
        DiscoLineGauge     = 0;
        GenerateNewPieces();
        ClearSave();
        OnStateChanged?.Invoke();
    }

    // ── 자동 저장 ────────────────────────────────────────────────
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
        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.SetInt(_mk + $"save_p{i}_shape",  CurrentPieces[i].shapeIndex);
            PlayerPrefs.SetInt(_mk + $"save_p{i}_color",  CurrentPieces[i].colorIndex);
            PlayerPrefs.SetInt(_mk + $"save_p{i}_placed", CurrentPieces[i].placed ? 1 : 0);
        }
        // 토글 모드: 현재 색상 및 게이지 저장
        if (ModeSession.SelectedMode == 2)
        {
            PlayerPrefs.SetInt(_mk + "save_toggle_color",   ToggleCurrentColor);
            PlayerPrefs.SetInt(_mk + "save_special_gauge",  SpecialGauge);
        }
        // 디스코 모드: 무지개 블럭 게이지 + 곡 재생 위치 저장.
        // 디스코는 재생 위치가 곧 연출 타임라인이라, 판만 되돌리고 곡을 안 되돌리면
        // 밤거리에서 나갔다가 터널에서 이어받는 식이 된다.
        if (ModeSession.SelectedMode == 3)
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
        if (ModeSession.SelectedMode == 2)
        {
            ToggleCurrentColor = PlayerPrefs.GetInt(_mk + "save_toggle_color",  0);
            // 게이지 임계값을 3→2로 줄였으므로 옛 저장값(2)이 새 시스템에선 즉시 스폰 트리거.
            // 로드 시점엔 [0, 1]로 클램프해서 UI/로직과 일관되게 유지.
            SpecialGauge       = Mathf.Clamp(PlayerPrefs.GetInt(_mk + "save_special_gauge", 0), 0, 1);
        }

        // 디스코 모드: 무지개 블럭 게이지 + 곡 재생 위치 복원.
        // 키가 없는 옛 저장은 0 — 곡을 처음부터 트는 셈이라 그대로 두어도 무해하다.
        if (ModeSession.SelectedMode == 3)
        {
            DiscoLineGauge = Mathf.Clamp(
                PlayerPrefs.GetInt(_mk + "save_disco_gauge", 0), 0, DISCO_LINES_PER_RAINBOW - 1);
            SavedBgmSec = PlayerPrefs.GetFloat(_mk + "save_disco_bgm", 0f);
        }

        Score  = PlayerPrefs.GetInt(_mk + "save_score", 0);
        _combo = PlayerPrefs.GetInt(_mk + "save_combo", 0);
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
        PlayerPrefs.DeleteKey(_mk + "save_toggle_color");
        PlayerPrefs.DeleteKey(_mk + "save_special_gauge");
        PlayerPrefs.DeleteKey(_mk + "save_disco_gauge");
        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.DeleteKey(_mk + $"save_p{i}_shape");
            PlayerPrefs.DeleteKey(_mk + $"save_p{i}_color");
            PlayerPrefs.DeleteKey(_mk + $"save_p{i}_placed");
        }
        PlayerPrefs.Save();
    }
}
