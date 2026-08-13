using UnityEngine;

/// <summary>
/// 퍼즐 조각의 색상과 모양 정의 (정적 데이터)
/// </summary>
public static class PieceData
{
    // sky / green / yellow / orange / pink / white 순서
    public static readonly Color[] Colors = new Color[]
    {
        new Color(0.40f, 0.80f, 1.00f),  // 0: sky
        new Color(0.30f, 0.85f, 0.35f),  // 1: green
        new Color(1.00f, 0.90f, 0.20f),  // 2: yellow
        new Color(1.00f, 0.60f, 0.15f),  // 3: orange
        new Color(1.00f, 0.45f, 0.72f),  // 4: pink
        new Color(0.90f, 0.90f, 0.90f),  // 5: white
    };

    // 각 조각의 출현 가중치 (Shapes와 1:1 대응)
    // 6=일반 / 3=L형2×3 / 2=5칸막대 / 1=대형(3×3코너·사각형·직사각형)
    public static readonly int[] ShapeWeights = new int[]
    {
        6,  // 0:  1칸
        6,  // 1:  가로 2칸
        6,  // 2:  가로 3칸
        6,  // 3:  가로 4칸
        2,  // 4:  가로 5칸 막대
        6,  // 5:  세로 2칸
        6,  // 6:  세로 3칸
        6,  // 7:  세로 4칸
        2,  // 8:  세로 5칸 막대
        6,  // 9:  대각 ↘
        6,  // 10: 대각 ↗
        6,  // 11: L 2×2 (↙)
        6,  // 12: L 2×2 (↖)
        6,  // 13: L 2×2 (↗)
        6,  // 14: L 2×2 (↘)
        6,  // 15: 2×2 사각형
        4,  // 16: L 2×3 (↙)
        4,  // 17: L 2×3 (↘)
        4,  // 18: L 2×3 (↖)
        4,  // 19: L 2×3 (↗)
        2,  // 20: L 3×3 코너 (↙)
        2,  // 21: L 3×3 코너 (↘)
        2,  // 22: L 3×3 코너 (↖)
        2,  // 23: L 3×3 코너 (↗)
        2,  // 24: 3×3 사각형
        2,  // 25: 2×3 직사각형
        2,  // 26: 3×2 직사각형
    };

    // 각 shape: int[행][열], 1=채움 / 0=빈칸 (퍼즐 종류.py 기준)
    public static readonly int[][][] Shapes = new int[][][]
    {
        // ── 1칸 ──────────────────────────────────────────────────
        new int[][] { new int[] {1} },

        // ── 가로 막대 ────────────────────────────────────────────
        new int[][] { new int[] {1,1} },
        new int[][] { new int[] {1,1,1} },
        new int[][] { new int[] {1,1,1,1} },
        new int[][] { new int[] {1,1,1,1,1} },

        // ── 세로 막대 ────────────────────────────────────────────
        new int[][] { new int[] {1}, new int[] {1} },
        new int[][] { new int[] {1}, new int[] {1}, new int[] {1} },
        new int[][] { new int[] {1}, new int[] {1}, new int[] {1}, new int[] {1} },
        new int[][] { new int[] {1}, new int[] {1}, new int[] {1}, new int[] {1}, new int[] {1} },

        // ── 대각 ─────────────────────────────────────────────────
        new int[][] { new int[] {1,0}, new int[] {0,1} },
        new int[][] { new int[] {0,1}, new int[] {1,0} },

        // ── L형 (2×2에서 한 칸 빠짐) ─────────────────────────────
        new int[][] { new int[] {1,0}, new int[] {1,1} },
        new int[][] { new int[] {1,1}, new int[] {1,0} },
        new int[][] { new int[] {0,1}, new int[] {1,1} },
        new int[][] { new int[] {1,1}, new int[] {0,1} },

        // ── 2×2 정사각형 ─────────────────────────────────────────
        new int[][] { new int[] {1,1}, new int[] {1,1} },

        // ── L형 (2행 × 3열) ──────────────────────────────────────
        new int[][] { new int[] {1,0,0}, new int[] {1,1,1} },
        new int[][] { new int[] {0,0,1}, new int[] {1,1,1} },
        new int[][] { new int[] {1,1,1}, new int[] {1,0,0} },
        new int[][] { new int[] {1,1,1}, new int[] {0,0,1} },

        // ── L형 (3행 × 3열 코너) ─────────────────────────────────
        new int[][] { new int[] {1,0,0}, new int[] {1,0,0}, new int[] {1,1,1} },
        new int[][] { new int[] {0,0,1}, new int[] {0,0,1}, new int[] {1,1,1} },
        new int[][] { new int[] {1,1,1}, new int[] {1,0,0}, new int[] {1,0,0} },
        new int[][] { new int[] {1,1,1}, new int[] {0,0,1}, new int[] {0,0,1} },

        // ── 3×3 정사각형 ─────────────────────────────────────────
        new int[][] { new int[] {1,1,1}, new int[] {1,1,1}, new int[] {1,1,1} },

        // ── 직사각형 ─────────────────────────────────────────────
        new int[][] { new int[] {1,1}, new int[] {1,1}, new int[] {1,1} },
        new int[][] { new int[] {1,1,1}, new int[] {1,1,1} },
    };
}
