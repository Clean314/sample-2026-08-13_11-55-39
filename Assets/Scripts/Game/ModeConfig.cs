/// <summary>
/// 모드별 디자인/오디오 설정 정의
/// puzzleSprite: null이면 개별 색상 스프라이트 사용 (노말), 경로 지정 시 단일 텍스처 사용 (아이스)
/// </summary>
public static class ModeConfig
{
    public struct Config
    {
        public string   bgmClip;             // Resources 경로
        public string   puzzleSprite;        // null = 색상별 개별 스프라이트, 경로 = 단일 텍스처
        public string[] colorSpriteNames;    // null = 기본값, 지정 시 해당 인덱스 스프라이트 이름 오버라이드
        public string   sfxSelect;           // 조각 선택 효과음
        public string   sfxDecide;           // 조각 배치 효과음
        public string   sfxClear;            // 줄 클리어 효과음
        public int[]    excludedShapes;      // 이 모드에서 제외할 PieceData.Shapes 인덱스 (null = 제한 없음)
        public int[]    shapeWeightOverrides;// PieceData.ShapeWeights 오버라이드 (null 또는 0 = 기본값 사용)
    }

    public static readonly Config[] Modes =
    {
        new Config { bgmClip = "Audio/BGM/normal_bgm",   puzzleSprite = null,                        sfxSelect = "Audio/SFX/선택",     sfxDecide = "Audio/SFX/결정",     sfxClear = "Audio/SFX/normal", excludedShapes = null           },  // 0: 노말
        new Config { bgmClip = "Audio/BGM/ice_mode_bgm", puzzleSprite = "Sprites/Puzzles/ice",       sfxSelect = "Audio/SFX/ice_선택", sfxDecide = "Audio/SFX/ice_결정", sfxClear = "Audio/SFX/ice",    excludedShapes = new int[]{9,10},
                     shapeWeightOverrides = new int[27] { 8,8,8, 0,0, 0,0,0,0, 0,0, 0,0,0,0, 0, 0,0,0,0, 0,0,0,0, 0, 0,0 }},  // 1: 아이스 (대각 제외, 소형 조각 가중치↑)
        new Config { bgmClip = "Audio/BGM/toggle", puzzleSprite = null,        colorSpriteNames = new string[]{ "black", null, null, null, null, null },
                     // FIXME: sfxClear가 오디오가 아니라 모드 아이콘 PNG를 가리킨다. Resources.Load<AudioClip>이
                     //        타입 불일치로 null을 돌려주므로 토글 모드에는 줄 클리어 효과음이 나지 않는다.
                     //        (경로 정리 전부터 있던 문제 — 의도한 클립을 정해서 교체 필요)
                     sfxSelect = "Audio/SFX/toggle_선택", sfxDecide = "Audio/SFX/toggle_결정", sfxClear = "Sprites/Modes/toggle_mode", excludedShapes = null,
                     shapeWeightOverrides = new int[27] {
                         12, 12, 10,  4,  1,   // 0:1칸  1:가로2  2:가로3  3:가로4  4:가로5
                         12, 10,  4,  1,        // 5:세로2  6:세로3  7:세로4  8:세로5
                         10, 10,                // 9:대각↘  10:대각↗
                         10, 10, 10, 10,        // 11~14: L 2×2 (3칸)
                          6,                    // 15: 2×2 사각 (4칸)
                          2,  2,  2,  2,        // 16~19: L 2×3 (4칸)
                          1,  1,  1,  1,        // 20~23: L 3×3 코너 (5칸)
                          1,                    // 24: 3×3 사각 (9칸)
                          1,  1                 // 25~26: 직사각형 (6칸)
                     } },  // 2: 토글
        new Config { bgmClip = "Audio/BGM/disco", puzzleSprite = null,
                     colorSpriteNames = new string[]{ "skyblue_disco", "green_disco", "yellow_disco", "orange_disco", "pink_disco", "white_disco" },
                     sfxSelect = "Audio/SFX/disco_선택", sfxDecide = "Audio/SFX/disco_결정", sfxClear = "Audio/SFX/normal", excludedShapes = null },  // 3: 디스코
    };

    public static Config Current => Modes[ModeSession.SelectedMode];
}
