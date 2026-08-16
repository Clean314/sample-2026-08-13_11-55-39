using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 인게임 화면 자동 생성 스크립트
/// 빈 씬에 빈 GameObject 하나 만들고 이 스크립트만 붙이면 됩니다.
/// </summary>
public class InGameUI : MonoBehaviour
{
    // ── 참조 ────────────────────────────────────────────────────
    GameManager  _gm;
    Canvas       _canvas;
    RectTransform _canvasRt;

    Text  _highScoreText;
    Text  _scoreText;
    Image _bgImage;

    Image[,]    _cellImages        = new Image[8, 8];  // 빈 셀 배경 레이어
    Image[,]    _blockOverlays    = new Image[8, 8];  // 블록 스프라이트 레이어 (비활성 반투명 처리용)
    Image[]     _gaugeCircles     = new Image[2];     // 스페셜 블럭 게이지 표시 (토글 모드)
    RectTransform _gridRt;
    RectTransform _blockLayerRt;   // 놓인 블록만 담는 그리드 자식. 빗방울을 이 바로 아래에 끼운다.
    RectTransform _trayRt;
    RectTransform _heartRootRt;

    // 무지개 블럭 발동 화면 흔들림 (디스코 모드 전용). 대상 선정은 CollectShakeTargets 참고.
    RectTransform[] _shakeTargets;
    Vector2[]       _shakeHome;
    Coroutine       _shakeCo;
    const float SHAKE_SEC  = 0.22f;   // ── 조정 손잡이: 흔들림 길이(초) ──
    const float SHAKE_AMP  = 14f;     // ── 조정 손잡이: 시작 진폭(px, 1080 기준 해상도) ──
    const float SHAKE_FREQ = 34f;     // ── 조정 손잡이: 진동 각속도(클수록 잘게 떨린다) ──

    // 일반 줄 클리어 음표 파티클 (디스코 모드 전용). PlayClearNotes 참고.
    // SCDream4/8에는 ♫(U+266B)가 빠져 있어 ♪(U+266A)와 ♬(U+266C)만 쓴다(cmap 확인).
    // 없는 글자를 넣으면 에디터에서는 OS 폰트로 대체돼 멀쩡히 보이다가 모바일 빌드에서 빈칸이 된다.
    static readonly string[] NOTE_GLYPHS = { "♪", "♬" };
    // 지워진 칸 자리에 음표를 하나씩 남겼다가 옅어지게 한다. 떠오르는 파티클보다
    // "여기가 방금 비었다"가 또렷하게 읽히고, 격자를 벗어나지 않아 화면이 깔끔하다.
    const float NOTE_SEC      = 0.55f;  // ── 조정 손잡이: 음표가 남아 있는 시간(초) ──
    const int   NOTE_SIZE     = 62;     // ── 조정 손잡이: 글자 크기(px, 셀은 110) ──
    const float NOTE_ALPHA    = 0.32f;  // ── 조정 손잡이: 가장 진할 때의 불투명도 ──
    const float NOTE_HOLD     = 0.55f;  // ── 조정 손잡이: 이 비율까지 NOTE_ALPHA 유지, 이후 사라짐 ──
    const float NOTE_STAGGER  = 0.07f;  // ── 조정 손잡이: 칸마다 어긋나는 시작 시각(초) ──

    // 무지개 블럭 발동으로 들어온 클리어인지. ActivateRainbowBlock이 OnStateChanged를 동기로
    // 부르며 일반 줄 클리어 연출을 그대로 태우기 때문에, 그쪽만 골라내려면 표시가 필요하다.
    bool _rainbowActivating;

    GameObject[]  _pieceSlots          = new GameObject[3];
    GameObject[]  _previewContainers   = new GameObject[3]; // 슬롯별 조각 컨테이너 참조
    float[]       _previewCellSizes    = new float[3];      // 슬롯별 셀 크기(cs)

    // ── 부활 ────────────────────────────────────────────────────
    bool _reviveUsed = false;   // 한 게임에 한 번만 부활 허용

    // 떠 있는 게임오버 오버레이. 판정 지점이 여러 곳(조각 놓기, 클리어 연출, 아이스 슬라이드,
    // 무지개 연쇄)이라 겹쳐 불릴 수 있어서, 두 장이 쌓이지 않게 붙잡아 둔다.
    GameObject _gameOverOverlay;

    // ── 드래그 상태 ─────────────────────────────────────────────
    int           _dragIdx      = -1;
    bool          _dragging     = false;
    bool          _busy         = false;   // 아이스 슬라이드 체인 중 입력 차단
    RectTransform _dragContainer;   // 집어든 조각 컨테이너 (트레이에서 분리됨)
    int           _previewRow   = -1;
    int           _previewCol   = -1;


    // ── 오디오 ──────────────────────────────────────────────────
    AudioSource _audioSource;
    AudioClip   _sfxSelect;   // 선택: 조각을 집었을 때
    AudioClip   _sfxDecide;   // 결정: 조각을 그리드에 놓았을 때
    AudioClip   _sfxToggle;   // 토글 모드: 화이트↔블랙 전환 효과음
    AudioClip   _sfxRainbow;  // 디스코 모드: 무지개 블럭을 탭해 발동시킬 때

    // ── 연속 클리어 콤보 (전 모드 공통) ─────────────────────────
    // 조각을 놓을 때마다 줄이 나면 콤보가 쌓인다. 1은 그냥 한 번 지운 것이라 아무것도 띄우지 않고,
    // 2부터 전용 효과음과 문구가 붙어 "연속으로 해내고 있다"를 알려 준다.
    const int   COMBO_MIN  = 2;      // 이 값부터 연출이 붙는다
    // 표시·색·크기가 모두 여기서 멈춘다. 콤보 보너스 점수의 상한과 같은 상수를 써서
    // "x5인데 점수만 더 오르는" 어긋남이 생기지 않게 한다.
    const int   COMBO_MAX  = GameManager.COMBO_TIER_MAX;
    const float COMBO_SEC  = 0.85f;  // ── 조정 손잡이: 문구가 떠 있는 시간(초) ──
    const float COMBO_HOLD = 0.55f;  // ── 조정 손잡이: 이 비율까지 또렷, 이후 옅어짐 ──
    const float COMBO_POP_SEC = 0.14f;   // 커졌다가(1.35배) 제자리로 돌아오는 데 걸리는 시간
    static readonly Vector2 COMBO_POS = new Vector2(0, 300);

    // 글자 크기: 단계마다 조금씩 커진다 (x2 → 62, x3 → 71, x4 → 80, MAX! → 89)
    const int   COMBO_FONT_BASE = 62;    // ── 조정 손잡이: x2일 때의 글자 크기 ──
    const int   COMBO_FONT_STEP = 9;     // ── 조정 손잡이: 단계마다 커지는 폭 ──
    // 검정 배경판. 디스코 화이트아웃·토글 블랙모드처럼 밝은 배경 위에서도 글자가 읽히게 한다.
    // 판이 대비를 만들어 주므로 그림자(Shadow)는 따로 안 깐다.
    const float COMBO_PLATE_ALPHA = 0.85f; // ── 조정 손잡이: 배경판 불투명도 ──
    const float COMBO_PLATE_PAD_X = 38f;   // ── 조정 손잡이: 좌우 여백(px) ──
    const float COMBO_PLATE_PAD_Y = 20f;   // ── 조정 손잡이: 위아래 여백(px) ──

    // x2~x4는 흰 글자, MAX!만 파랑. 색을 한 군데만 쓰면 상한에 닿았다는 게 확실히 도드라진다.
    // 단계별로 색을 바꾸면 네 가지 색이 번갈아 뜨면서 오히려 산만해진다.
    static readonly Color COMBO_COLOR     = Color.white;
    static readonly Color COMBO_COLOR_MAX = new Color(0.35f, 0.70f, 1.00f);

    GameObject  _comboPopup;      // 떠 있는 팝업 하나. 값이 바뀌면 이전 것을 치우고 새로 띄운다.
    int         _comboPopupValue; // 그 팝업이 보여 주고 있는 콤보 수

    // ── 공통 스프라이트 캐시 ────────────────────────────────────
    Sprite _spr110;   // 110×110 r=30  (그리드 셀용)
    Sprite _spr200;   // 200×100 r=36  (버튼용)
    Sprite _sprCellOutline;  // 110×110 r=30 테두리만 (도시 구간 빈 셀용)
    Sprite[] _colorSprites = new Sprite[6];  // 색상별 PNG 스프라이트
    Sprite _meltingSprite;                   // 아이스모드 슬라이드 흔적

    int    _prevToggleColor = -1; // 토글 모드 이전 색상 (변환 감지용)

    // ── 디스코 모드 ─────────────────────────────────────────────────
    BeatTracker         _beatTracker;
    CityLightBackground _cityLight;      // 곡 후반에만 살아있는 도시 야경 배경
    DrivingBackground   _driving;        // 도시가 끝난 뒤 이어받는 드라이빙 배경
    float               _discoBallAngle;

    // 전용 배경(도시/드라이빙)이 화면을 덮고 있는지. 빈 셀을 내곽선으로 그릴지 판단하는 데도 쓴다.
    // 드라이빙 배경은 마지막 페이즈에서 하늘·건물을 감추고 디스코 셀을 드러내므로,
    // 그동안은 덮고 있지 않은 것으로 본다(기본 배경과 그리드 백드롭이 다시 필요하다).
    bool SceneBackgroundActive => _cityLight != null || (_driving != null && _driving.CoversScreen);
    Image           _whiteFlashOverlay;

    DiscoTile[]     _spotImgs;
    RectTransform[] _spotRts;
    float[]         _spotPhase;     // 깜빡임 시작 위상 (셀 내 sub-tile들끼리 공유)
    float[]         _spotBaseX;     // 셀의 X 기준 위치 (스크롤 적용 전)
    float[]         _spotBaseY;     // 셀의 Y 기준 위치
    float[]         _spotAmpX;      // 깜빡임 각속도(rad/sec)
    float[]         _spotAmpY;      // sub-tile 알파 배율 (메인=1.0, 잔향=0.5/0.25 …)
    float[]         _spotOffsetX;   // sub-tile별 X 오프셋 (잔향 = 메인 뒤쪽)
    float[]         _spotOffsetY;   // sub-tile별 Y 오프셋 (살짝 어긋남)
    float[]         _spotMaxAlpha;
    float[]         _spotBaseW;
    float[]         _spotBaseH;

    static readonly Color[] DISCO_COLORS =
    {
        new Color(1.00f, 0.12f, 0.12f),  // 빨강
        new Color(0.12f, 0.90f, 0.12f),  // 초록
        new Color(0.12f, 0.35f, 1.00f),  // 파랑
        new Color(0.10f, 0.95f, 0.95f),  // 시안
        new Color(0.95f, 0.10f, 0.95f),  // 마젠타
        new Color(1.00f, 0.55f, 0.10f),  // 오렌지
        new Color(0.65f, 0.10f, 1.00f),  // 보라
    };

    // ── 별 파티클 (디스코 모드: 그리드 위에 반짝) ────────────────
    const int STAR_POOL = 24;
    RectTransform[] _starRts;
    Image[]         _starImgs;
    float[]         _starBornTime;   // 음수 = 비활성 슬롯
    float[]         _starLifetime;
    float[]         _starBaseSize;
    Color[]         _starColor;
    Sprite          _starSprite;
    float           _nextStarTime;

    static readonly Color[] STAR_COLORS =
    {
        new Color(1.00f, 0.95f, 0.78f),  // 따뜻한 흰색
        new Color(1.00f, 0.85f, 0.55f),  // 골드
        new Color(1.00f, 0.78f, 0.88f),  // 핑크
        new Color(0.85f, 0.95f, 1.00f),  // 시안 흰
    };

    // ── 디스코 모드 터널 이펙트 (50~96.7초) ─────────────────────
    // 동심원 링이 z축으로 균등 분포된 채 카메라 쪽으로 흘러옴 (원근: size = FOCAL / z).
    // 가장자리로 뻗는 방사 스포크가 원기둥 내벽의 길이 방향 표면을 암시한다.
    // 색 전환은 음악 큐(반박)에 맞춰 즉시 전환된다:
    //   50.0~64.70   파랑, 정지
    //   64.70~80.65  흰색, 시계방향 회전 (즉시 전환)
    //   80.65~96.7   빨강, 반시계방향 회전 (색/방향 즉시 반전)
    //   96.7~100.1   배경만 흰색 단색 (터널/셀 숨김)
    //   100.1~100.6  배경 페이드 + 셀 페이드인 (크로스, 0.5초)
    const int   TUNNEL_RINGS  = 18;
    const int   TUNNEL_SPOKES = 12;
    const float TUNNEL_Z_FAR  = 14f;
    const float TUNNEL_Z_NEAR = 0.45f;
    const float TUNNEL_SPEED  = 1.2f;     // z-units / sec (작을수록 천천히 빨려들어감)
    const float TUNNEL_FOCAL  = 1700f;    // 원근 초점 거리(px) — z=zNear에서 ~3700px (충분히 화면 밖)
    const float TUNNEL_SPIN_DEG = 45f;    // 회전 페이즈 각속도 (deg/sec)

    // ── 구부러진 터널 (2차 터널 전용, 1차는 일직선) ──────────────────
    // 사인 곡선을 겹치는 대신 "곡률"을 직접 구간별로 정한다. 한 구간 동안은 곡률이 일정해서
    // 미끄럼틀처럼 한쪽으로 계속 감기고, 구간이 바뀔 때만 짧게 반대 방향으로 넘어간다.
    // (사인으로 만들면 화면에 보이는 휘어짐이 곡률 = 진폭×주파수²에 비례해서,
    //  주파수를 낮춰 구간을 길게 하면 휘어짐이 제곱으로 죽어 거의 직선이 된다.)
    //
    // 곡률 K가 일정할 때 깊이 z의 단면이 밀려나는 양은 K·z·FOCAL/2 —
    // 즉 터널 끝(z=zFar)의 화면 이동량 ≈ TUNNEL_CURVE_AMP × 11900 px.
    const float TUNNEL_CURVE_AMP  = 0.035f;  // ── 조정 손잡이: 휘는 정도(곡률) ──
    const float TUNNEL_CURVE_SEG  = 22f;     // ── 조정 손잡이: 한 방향을 유지하는 거리(z단위) ──
                                             //    2차 터널은 초당 약 2.16 z → 22면 약 10초
    const float TUNNEL_CURVE_TURN = 0.18f;   // ── 조정 손잡이: 구간 중 방향이 넘어가는 비율 ──
                                             //    0.18이면 세그먼트의 18%(≈1.8초)만 전환에 쓴다
    const int   TUNNEL_CURVE_STEPS = 8;      // 곡률 적분 샘플 수 (구간이 길어 8이면 충분)

    // ── 2차 터널 (곡 마지막) ─────────────────────────────────────────
    // 244.5초에 화면이 검게 덮이고(1차 터널 진입과 같은 blackout 램프), 검정이 걷히면서
    // 터널이 드러난다. 1차보다 링이 빠르게 밀려오고 회전도 빠르다.
    // 검정 램프는 배경(_bgImage, sibling 0)과 디스코 셀만 어둡게 한다. 그 위에 깔린 드라이빙 배경의
    // 바닥·자동차는 DrivingBackground가 같은 구간에 자기 CanvasGroup을 내려 함께 사라진다.
    // 두 램프가 어긋나면 차만 남거나 먼저 사라지므로 시각을 그쪽 상수에서 한 번만 정한다.
    const double BLACK2_START     = DrivingBackground.BLACKOUT_START; // ── 조정은 DrivingBackground에서 ──
    const double BLACK2_FULL      = DrivingBackground.DISAPPEAR_AT;   // 완전 검정 (드라이빙 배경 정리)
    const double TUNNEL2_START    = 246;                            // 검정이 걷히며 터널 페이드인
    const double TUNNEL2_FADE_SEC = 0.7;
    const float  TUNNEL2_SPEED_MUL = 2f;   // ── 조정 손잡이: 1차 대비 링 속도 ──
    const float  TUNNEL2_SPIN_MUL  = 0.3f;   // ── 조정 손잡이: 1차 대비 회전 속도 ──

    // 4분 36.5초부터 흰 터널이 알록달록해진다(즉시 전환). 곡 길이가 5:24.19라 약 47.7초 유지된다.
    // 색은 터널 축 위의 절대 위치로 정하므로 색 띠가 링에 붙어 같이 밀려오고,
    // 동시에 시간에 따라 색상환 전체가 계속 돌아 가만히 있어도 색이 변한다.
    const double TUNNEL_RAINBOW_START = 276.5;   
    const float  TUNNEL_RAINBOW_PER_Z = 0.12f;   // ── 조정 손잡이: z 1당 색상환 회전 비율(띠 촘촘함) ──
    const float  TUNNEL_RAINBOW_SPEED = 0.80f;   // ── 조정 손잡이: 초당 색상환 회전수(변색 속도) ──

    // ── 터널 빗방울 (곡 마지막) ──────────────────────────────────────
    // 4분 52.6초부터 곡 끝까지, 터널 안을 알록달록한 작은 점이 스쳐 지나간다.
    // 링과 같은 원근식(FOCAL/z)과 같은 곡률(TunnelOffset)을 써서 같은 공간에 있는 것처럼 보인다 —
    // 점이 링을 따라 같이 휘고, 깊이에 따라 크기·속도가 갈려 거리감이 생긴다.
    const double RAIN_START      = 292.6;   // ── 조정 손잡이: 시작 시각(4분 52.6초) ──
    // 점 하나가 Image 하나다. 전부 같은 스프라이트라 드로우콜은 한 덩어리로 묶이고,
    // 비용은 매 프레임 위치·크기·색을 갱신하며 생기는 캔버스 리빌드 쪽이다.
    const int    RAIN_COUNT      = 180;     // ── 조정 손잡이: 점 개수 ──
    const float  RAIN_SPEED_MUL  = 1.7f;    // ── 조정 손잡이: 링 대비 다가오는 속도 ──
    const float  RAIN_WORLD_SIZE = 0.010f;  // 점의 월드 지름 → 화면 크기 = 이 값 × FOCAL / z
    const float  RAIN_MIN_PX     = 2f;      // 멀리서 1px 밑으로 내려가면 깜빡이므로 하한
    const float  RAIN_FALL       = 0.06f;   // ── 조정 손잡이: 월드 낙하 속도(단위/초) ──
    // 낙하량을 미리 위로 되돌려 주는 비율. SpawnRainDot 참고.
    // 0 = 보정 없음(아래로 쏠림), 0.5 = 수명 한가운데가 원래 자리,
    // 1.0 = 다 흘러내린 끝에서야 원래 자리(위쪽으로 쏠림).
    // 가까워서 크게 보이는 쪽에 무게를 실으려고 0.5보다 조금 높게 잡았다.
    const float  RAIN_RISE_BIAS  = 0.75f;   // ── 조정 손잡이: 시작 높이 보정 ──
    const float  RAIN_SWAY       = 0.012f;  // ── 조정 손잡이: 좌우로 흩날리는 폭(월드) ──
    const float  RAIN_FADE_Z     = 1.6f;    // 이 깊이부터 카메라를 스치며 옅어진다

    // ── 곡 마무리와 루프 이음 ────────────────────────────────────────
    // 곡이 끝나면 BGM은 loop=true로 0초로 되감긴다(BGMManager). 그냥 두면 알록달록한 터널이
    // 한 프레임 만에 도입부 셀 배경으로 튀므로, 양쪽 끝을 검정으로 만들어 이어 붙인다.
    //   ENDING_BLACK_START~  터널·빗방울이 옅어지며 화면이 검정으로 정리된다(배경은 이미 검정).
    //   0~LOOP_FADE_SEC      되감긴 직후 그 검정을 걷어내며 도입부가 드러난다.
    // 배경 자체는 BLACK2_FULL 이후 계속 검정이라 여기서는 그 위 레이어만 지우면 된다.
    const double ENDING_BLACK_START = 308.6;  // ── 조정 손잡이: 암전 시작(5분 8.6초) ──
    const float  ENDING_FADE_SEC    = 2.5f;   // ── 조정 손잡이: 암전에 걸리는 시간 ──
    const float  LOOP_FADE_SEC      = 2.5f;   // ── 조정 손잡이: 되감긴 뒤 밝아지는 시간 ──

    RectTransform   _rainLayerRt;
    RectTransform[] _rainRts;
    Image[]         _rainImgs;
    float[]         _rainZ;
    float[]         _rainX, _rainY;   // 터널 축 기준 월드 오프셋 (벽 = 반지름 0.5)
    float[]         _rainHue;         // 점마다 다른 색상환 위치
    float[]         _rainPhase;       // 흩날림 위상
    Sprite          _rainDotSprite;

    RectTransform[] _ringRts;
    Image[]         _ringImgs;
    float[]         _ringZ;
    Image[]         _spokeImgs;
    Sprite          _ringSprite;
    Sprite          _spokeSprite;
    // 디스코 이펙트 레이어들. 자기 구간 밖에서는 GameObject를 통째로 꺼서
    // 매 프레임 갱신과 렌더를 둘 다 건너뛴다 (UpdateDiscoVisuals의 레이어 on/off 블록 참고).
    GameObject      _spotLayerGo;
    GameObject      _starLayerGo;
    RectTransform   _tunnelLayerRt;
    RectTransform   _spokeGroupRt;        // 스포크만 묶어 돌린다. 링은 원이라 돌려도 티가 안 나고,
                                          // 레이어째 돌리면 굽은 터널의 오프셋까지 같이 휘둘린다.
    float           _tunnelSpin;          // 누적 회전각(deg)
    float           _tunnelTravel;        // 터널 축을 따라 누적한 진행 거리 (z 단위)
    float           _tunnelCurveSeed;     // 구간별 곡률을 뽑는 해시 시드. 터널이 켜질 때마다 새로 뽑는다
    bool            _curveOn;             // 이번 터널이 굽는지. 1차는 일직선, 2차만 굽는다
    Image           _discoGridBackdrop;   // 도시 배경 구간에는 감춰야 빈 셀 반투명 효과가 살아남
    static readonly Color TUNNEL_COLOR     = new Color(0.30f, 0.75f, 1.00f); // skyblue (50~64s)
    static readonly Color TUNNEL_COLOR_RED = new Color(1.00f, 0.18f, 0.18f); // 81~97s 빨강

    // 토글 모드 전용 on/off 스프라이트
    Sprite _toggleWhiteOnSprite;
    Sprite _toggleWhiteOffSprite;
    Sprite _toggleBlackOnSprite;
    Sprite _toggleBlackOffSprite;
    Sprite _specialBlockSprite;  // 토글 모드 스페셜 블럭 이미지

    // 디스코 모드 무지개 블럭
    Sprite  _rainbowBlockSprite;
    Sprite  _burstRingSprite;                // 폭발 충격파용 얇은 링
    Sprite  _burstChipSprite;                // 폭발 조각용 작은 사각형
    Text[]  _discoHearts;                    // 무지개 블럭까지 남은 줄 수 게이지 (♥ 텍스트)
    const int   RAINBOW_BURST_COUNT = 110;   // ── 조정 손잡이: 터질 때 튀는 조각 수 ──
    const float RAINBOW_BURST_SEC   = 1.15f; // ── 조정 손잡이: 조각이 남아있는 시간 ──
    const int   RING_COUNT          = 2;     // ── 조정 손잡이: 충격파 링 개수 ──
    static readonly Color HEART_FULL  = new Color(1.00f, 0.28f, 0.48f);
    static readonly Color HEART_EMPTY = new Color(1.00f, 1.00f, 1.00f, 0.20f);

    // ── 색상 상수 ───────────────────────────────────────────────
    static readonly Color BG_DARK         = new Color(0.086f, 0.082f, 0.141f); // #161524 (화이트 모드 / 기본)
    static readonly Color BG_LIGHT        = new Color(0.96f,  0.96f,  1.00f);  // 블랙 모드 배경
    static readonly Color CELL_EMPTY_DARK = new Color(0.17f,  0.17f,  0.24f);  // 화이트 모드 빈 셀
    static readonly Color CELL_EMPTY_LIGHT= new Color(0.75f,  0.75f,  0.82f);  // 블랙 모드 빈 셀
    static readonly Color GOLD            = new Color(1f,     0.85f,  0.3f);
    // 도시 구간 빈 셀: 채움 대신 얼음빛 회색 내곽선만 → 도시를 가리지 않으면서 격자는 읽힘.
    // ── 조정 손잡이: 선 두께(px, 110×110 셀 기준)와 색/투명도 ──
    const int CELL_OUTLINE_PX = 2;
    static readonly Color CELL_OUTLINE_ICE = new Color(0.78f, 0.85f, 0.92f, 0.38f);


    // ═══════════════════════════════════════════════════════════
    void Start()
    {
        // GameManager가 씬에 없으면 자동 생성
        _gm = FindFirstObjectByType<GameManager>();
        if (_gm == null)
            _gm = new GameObject("GameManager").AddComponent<GameManager>();

        _gm.OnStateChanged  += OnGameStateChanged;
        _gm.OnComboCleared += ShowComboPopup;

        // 오디오 소스 생성 및 클립 로드
        _audioSource = gameObject.AddComponent<AudioSource>();
        // sfxClear는 null일 수 있다(토글 모드) — 클립이 없으면 재생 지점들이 알아서 건너뛴다.
        var sfxClearPath = ModeConfig.Current.sfxClear;
        var clip = string.IsNullOrEmpty(sfxClearPath) ? null : Resources.Load<AudioClip>(sfxClearPath);
        if (clip != null) _audioSource.clip = clip;

        // 모드에 맞는 BGM으로 전환
        BGMManager.GetOrCreate().PlayBGM(ModeConfig.Current.bgmClip);

        // 디스코 모드는 곡 재생 위치가 곧 연출 타임라인이다(밤거리 → 터널 → 암전).
        // 그런데 메인 메뉴에서 모드를 넘겨보는 동안 이미 disco 클립이 돌고 있어서,
        // PlayBGM이 "같은 클립"이라며 그냥 지나친다 → 그대로 두면 메뉴에서 흘러간
        // 아무 지점에서 시작한다. 새 판이면 처음으로, 이어하기면 저장해 둔 자리로 맞춘다.
        if (ModeSession.SelectedMode == 3)
        {
            BGMManager.Instance?.Seek(_gm.LoadedFromSave ? _gm.SavedBgmSec : 0.0);

            // 광과민성 경고가 걷힐 때까지는 무음. 아직 곡이 시작된 게 아니다.
            // 연출이 전부 재생 위치로 계산되므로, 여기서 곡이 돌면 플레이어가 보지도 못한
            // 도입부가 경고 뒤에서 그냥 지나가 버린다. 재개는 PhotoWarningRoutine이 맡는다.
            // (Seek이 Play를 부를 수 있으므로 되감기 다음에 멈춰야 한다.)
            BGMManager.Instance?.Pause();
        }

        // 광고 배너 표시
        AdManager.GetOrCreate().ShowBanner();

        _sfxSelect = Resources.Load<AudioClip>(ModeConfig.Current.sfxSelect);
        _sfxDecide = Resources.Load<AudioClip>(ModeConfig.Current.sfxDecide);

        if (ModeSession.SelectedMode == 2)
            _sfxToggle = Resources.Load<AudioClip>("Audio/SFX/toggle_switch");
        else if (ModeSession.SelectedMode == 3)
            _sfxRainbow = Resources.Load<AudioClip>("Audio/SFX/disco_special");

        // 스프라이트 미리 생성
        LoadColorSprites();
        _spr110 = MakeRoundedSprite(110, 110, 30);
        _spr200 = MakeRoundedSprite(200, 100, 36);
        _sprCellOutline = MakeRoundedOutlineSprite(110, 110, 30, CELL_OUTLINE_PX);

        if (ModeSession.SelectedMode == 1)
        {
            var meltTex = Resources.Load<Texture2D>("Sprites/Effects/melting");
            if (meltTex != null)
                _meltingSprite = Sprite.Create(meltTex,
                    new Rect(0, 0, meltTex.width, meltTex.height), new Vector2(0.5f, 0.5f));
        }

        BuildCanvas();
        BuildBackground();
        BuildScoreArea();
        BuildBackButton();
        BuildMuteButton();
        BuildGrid();
        BuildPieceTray();

        if (ModeSession.SelectedMode == 3)
        {
            BuildDiscoBall();
            BuildDiscoGridBackdrop();
            BuildStarLayer();
            BuildTunnelLayer();
            BuildWhiteFlashOverlay();
            _beatTracker = new GameObject("BeatTracker").AddComponent<BeatTracker>();

            // 모든 레이아웃이 끝난 뒤에 제자리를 기록해야 한다
            CollectShakeTargets();

            // 경고는 맨 마지막에 세워야 다른 레이어들 위로 올라간다(sibling 순서 = 그리는 순서).
            BuildPhotoWarning();
        }

        RefreshUI();
    }

    void OnDestroy()
    {
        if (_gm != null)
        {
            _gm.OnStateChanged  -= OnGameStateChanged;
            _gm.OnComboCleared -= ShowComboPopup;
        }

        // 씬을 떠날 때 BGM을 반드시 정상 상태로 돌려놓는다. 안 그러면 메뉴가 조용해진다.
        //   · 광과민성 경고 도중이면 재개를 맡은 코루틴이 같이 죽어 멈춘 채로 남는다
        //   · 게임오버 페이드 도중이면 볼륨이 0인 채로 남는다(페이드 코루틴은 BGMManager 소유라
        //     씬을 넘어가도 계속 돌아 결국 Pause까지 간다)
        // 둘 다 해당 없으면 아무 일도 하지 않으므로 무조건 불러도 안전하다.
        BGMManager.Instance?.RestorePlayback();   // 진행 중인 페이드부터 끊는다
        BGMManager.Instance?.Resume();
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) _gm?.SaveGame();
    }

    void LoadColorSprites()
    {
        var modeSpr = ModeConfig.Current.puzzleSprite;

        if (modeSpr != null)
        {
            // 아이스 등 단일 텍스처 모드: 같은 스프라이트를 모든 슬롯에 설정
            var tex = Resources.Load<Texture2D>(modeSpr);
            if (tex != null)
            {
                var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                for (int i = 0; i < _colorSprites.Length; i++)
                    _colorSprites[i] = spr;
            }
        }
        else
        {
            string[] defaults  = { "sky", "green", "yellow", "orange", "pink", "white" };
            var      overrides = ModeConfig.Current.colorSpriteNames;
            for (int i = 0; i < defaults.Length; i++)
            {
                string name = (overrides != null && i < overrides.Length && overrides[i] != null)
                    ? overrides[i] : defaults[i];
                var tex = Resources.Load<Texture2D>($"Sprites/Puzzles/{name}");
                if (tex != null)
                    _colorSprites[i] = Sprite.Create(
                        tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        // 토글 모드: on/off 스프라이트 로드
        if (ModeSession.SelectedMode == 2)
        {
            _toggleWhiteOnSprite  = LoadSpriteFromPath("Sprites/Puzzles/white_on");
            _toggleWhiteOffSprite = LoadSpriteFromPath("Sprites/Puzzles/white_off");
            _toggleBlackOnSprite  = LoadSpriteFromPath("Sprites/Puzzles/black_on");
            _toggleBlackOffSprite = LoadSpriteFromPath("Sprites/Puzzles/black_off");
            _specialBlockSprite   = LoadSpriteFromPath("Sprites/Puzzles/special_block");
        }

        // 디스코 모드
        if (ModeSession.SelectedMode == 3)
        {
            _rainbowBlockSprite = LoadSpriteFromPath("Sprites/Puzzles/rainbow_disco");

            // 충격파 링. 터널용 _ringSprite(두께 6%)를 키워 쓰면 테두리가 같이 두꺼워져
            // 굵은 훌라후프처럼 보이므로, 큰 원본에 얇은 선으로 따로 굽는다.
            // 512×1.8% ≈ 9px → 980px까지 늘려도 선은 약 17px로 가늘게 유지된다.
            _burstRingSprite = MakeRingSprite(512, 0.018f);

            // 폭발 조각: 모서리만 살짝 둥근 작은 사각형(색종이 조각)
            _burstChipSprite = MakeRoundedSprite(32, 32, 6);
        }
    }

    Sprite LoadSpriteFromPath(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
    }

    // ═══════════════════════════════════════════════════════════
    // UI 구성
    // ═══════════════════════════════════════════════════════════

    void BuildCanvas()
    {
        var go = new GameObject("Canvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 0;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight   = 1f;

        go.AddComponent<GraphicRaycaster>();
        _canvasRt = go.GetComponent<RectTransform>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    void BuildBackground()
    {
        var obj = new GameObject("Background");
        obj.transform.SetParent(_canvas.transform, false);
        _bgImage = obj.AddComponent<Image>();
        _bgImage.color = ModeSession.SelectedMode == 3
            ? new Color(0.05f, 0.03f, 0.10f)
            : BG_DARK;
        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void BuildScoreArea()
    {
        // HighScore (골드, 작게)
        var hsGo = new GameObject("HighScore");
        hsGo.transform.SetParent(_canvas.transform, false);
        _highScoreText            = hsGo.AddComponent<Text>();
        _highScoreText.font       = Font4();
        _highScoreText.fontSize   = 42;
        _highScoreText.fontStyle  = FontStyle.Bold;
        _highScoreText.color      = GOLD;
        _highScoreText.alignment  = TextAnchor.MiddleCenter;
        _highScoreText.text       = "BEST  0";
        var hsRt = hsGo.GetComponent<RectTransform>();
        bool discoMode = ModeSession.SelectedMode == 3;
        hsRt.anchorMin        = new Vector2(0.5f, 1f);
        hsRt.anchorMax        = new Vector2(0.5f, 1f);
        hsRt.pivot            = new Vector2(0.5f, 1f);
        hsRt.anchoredPosition = new Vector2(0, discoMode ? -105f : -70f);
        hsRt.sizeDelta        = new Vector2(700, 70);

        // Score (흰색, 크게)
        var sGo = new GameObject("Score");
        sGo.transform.SetParent(_canvas.transform, false);
        _scoreText            = sGo.AddComponent<Text>();
        _scoreText.font       = Font4();
        _scoreText.fontSize   = 110;
        _scoreText.fontStyle  = FontStyle.Bold;
        _scoreText.color      = Color.white;
        _scoreText.alignment  = TextAnchor.MiddleCenter;
        _scoreText.text       = "0";
        var sRt = sGo.GetComponent<RectTransform>();
        sRt.anchorMin        = new Vector2(0.5f, 1f);
        sRt.anchorMax        = new Vector2(0.5f, 1f);
        sRt.pivot            = new Vector2(0.5f, 1f);
        sRt.anchoredPosition = new Vector2(0, discoMode ? -185f : -140f);
        sRt.sizeDelta        = new Vector2(800, 130);

        // 토글 모드 게이지 (토글 모드일 때만 표시)
        if (ModeSession.SelectedMode == 2)
        {
            // 게이지 컨테이너 (점수 텍스트 아래)
            var gaugeGo = new GameObject("SpecialGauge");
            gaugeGo.transform.SetParent(_canvas.transform, false);
            var gaugeRt               = gaugeGo.AddComponent<RectTransform>();
            gaugeRt.anchorMin         = new Vector2(0.5f, 1f);
            gaugeRt.anchorMax         = new Vector2(0.5f, 1f);
            gaugeRt.pivot             = new Vector2(0.5f, 1f);
            gaugeRt.anchoredPosition  = new Vector2(0, -280);
            gaugeRt.sizeDelta         = new Vector2(200, 44);

            var circleSpr = MakeRoundedSprite(44, 44, 22);
            for (int i = 0; i < 2; i++)
            {
                var cGo = new GameObject($"GaugeCircle_{i}");
                cGo.transform.SetParent(gaugeGo.transform, false);
                var cImg = cGo.AddComponent<Image>();
                cImg.sprite        = circleSpr;
                cImg.type          = Image.Type.Sliced;
                cImg.color         = new Color(0.30f, 0.30f, 0.40f);
                cImg.raycastTarget = false;
                var cRt               = cGo.GetComponent<RectTransform>();
                cRt.anchorMin         = new Vector2(0.5f, 0.5f);
                cRt.anchorMax         = new Vector2(0.5f, 0.5f);
                cRt.pivot             = new Vector2(0.5f, 0.5f);
                cRt.anchoredPosition  = new Vector2(-32 + i * 64, 0);
                cRt.sizeDelta         = new Vector2(44, 44);
                _gaugeCircles[i]      = cImg;
            }
        }

        // 디스코 모드 하트 게이지 (무지개 블럭까지 남은 줄 수)
        if (ModeSession.SelectedMode == 3)
        {
            int n = GameManager.DISCO_LINES_PER_RAINBOW;

            var heartRoot = new GameObject("DiscoHeartGauge");
            heartRoot.transform.SetParent(_canvas.transform, false);
            var hRootRt              = heartRoot.AddComponent<RectTransform>();
            hRootRt.anchorMin        = new Vector2(0.5f, 1f);
            hRootRt.anchorMax        = new Vector2(0.5f, 1f);
            hRootRt.pivot            = new Vector2(0.5f, 1f);
            // 점수 텍스트 하단(-315)과 디스코 그리드 백드롭 상단(-385) 사이 70px에 딱 맞춘다
            hRootRt.anchoredPosition = new Vector2(0, -313);
            hRootRt.sizeDelta        = new Vector2(n * 78, 70);
            _heartRootRt             = hRootRt;

            _discoHearts = new Text[n];
            for (int i = 0; i < n; i++)
            {
                var hGo = new GameObject($"Heart_{i}");
                hGo.transform.SetParent(heartRoot.transform, false);
                var t           = hGo.AddComponent<Text>();
                t.font          = Font4();
                t.fontSize      = 58;
                t.alignment     = TextAnchor.MiddleCenter;
                t.text          = "♥";   // SCDream4/8 모두 U+2665 글리프를 갖고 있음(cmap 확인)
                t.color         = HEART_EMPTY;
                t.raycastTarget = false;

                var hRt              = hGo.GetComponent<RectTransform>();
                hRt.anchorMin        = new Vector2(0.5f, 0.5f);
                hRt.anchorMax        = new Vector2(0.5f, 0.5f);
                hRt.pivot            = new Vector2(0.5f, 0.5f);
                hRt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * 78f, 0f);
                hRt.sizeDelta        = new Vector2(72, 72);
                _discoHearts[i]      = t;
            }
        }
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

    void BuildBackButton()
    {
        var obj = new GameObject("BackButton");
        obj.transform.SetParent(_canvas.transform, false);

        var img = obj.AddComponent<Image>();
        img.sprite = MakeRoundedSprite(100, 80, 20);
        img.type   = Image.Type.Sliced;
        img.color  = new Color(0.25f, 0.25f, 0.35f);

        var btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            _gm?.SaveGame();
            SceneManager.LoadScene("Main");
        });

        var rt = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(30, -55);
        rt.sizeDelta        = new Vector2(100, 80);

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(obj.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.font      = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
        txt.fontSize  = 50;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text      = "<";
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
    }

    void BuildMuteButton()
    {
        var bgm = BGMManager.GetOrCreate();

        var obj = new GameObject("MuteButton");
        obj.transform.SetParent(_canvas.transform, false);

        var img = obj.AddComponent<Image>();
        img.preserveAspect = true;
        img.color          = new Color(0.886f, 0.910f, 0.941f);
        img.sprite         = LoadMuteSprite(bgm.IsMuted ? "Sprites/UI/mute" : "Sprites/UI/bgm_on");

        var btn    = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
        colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration     = 0.1f;
        btn.colors = colors;

        var rt              = obj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-30, -55);
        rt.sizeDelta        = new Vector2(100, 100);

        btn.onClick.AddListener(() =>
        {
            bgm.ToggleMute();
            img.sprite = LoadMuteSprite(bgm.IsMuted ? "Sprites/UI/mute" : "Sprites/UI/bgm_on");
        });
    }

    Sprite LoadMuteSprite(string name)
    {
        var tex = Resources.Load<Texture2D>(name);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    // ═══════════════════════════════════════════════════════════
    // 디스코 모드 전용
    // ═══════════════════════════════════════════════════════════

    // 디스코볼이 도는 방의 한 벽면을 그린다.
    // 벽의 빛 조각들은 행을 이루어 한 방향으로 흘러가다가
    // 화면 끝에서 사라지고, 반대편에서 새 빛이 들어와 끊임없이 이어진다.
    // 흐르는 방향과 속도는 구간에 따라 다르다 — UpdateDiscoVisuals의 carSection 참고.
    const int   DISCO_ROWS         = 9;
    const int   DISCO_COLS         = 13;
    const int   DISCO_SUBS         = 3;       // 셀당 sub-tile 개수: 메인 + 잔향 2개
    const float DISCO_TRACK_WIDTH  = 1400f;   // 가상 트랙 너비 (화면 밖으로 나간 타일이 wrap-around 위치로 워프할 때 보이지 않도록 화면 너비보다 넓게)

    // sub-tile별 (X 오프셋, Y 오프셋, 알파 배율).
    // X는 "진행 방향 뒤쪽으로 얼마나 끌리는지"의 크기만 담는다. 부호는 스크롤 방향에 따라
    // UpdateDiscoVisuals의 trailSign이 붙인다(구간마다 방향이 달라서).
    // Y는 작게 어긋나서 "정확히 같은 자리에 놓인 사각형 한 장" 느낌을 깸.
    static readonly float[] DISCO_SUB_OFFX  = {  0f,  16f,  36f };
    static readonly float[] DISCO_SUB_OFFY  = {  0f,   3f,  -2f };
    static readonly float[] DISCO_SUB_ALPHA = { 1.0f, 0.55f, 0.28f };

    void BuildDiscoBall()
    {
        const int cells = DISCO_ROWS * DISCO_COLS;
        const int total = cells * DISCO_SUBS;

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
        for (int r = 0; r < DISCO_ROWS; r++)
        {
            // 행 Y: 화면 전체를 균등 분할 (어긋남 없음)
            float rowY = -960f + 1920f * (r + 0.5f) / DISCO_ROWS;

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

        // 보드 블록 세로 펄스
        for (int r = 0; r < GameManager.SIZE; r++)
            for (int c = 0; c < GameManager.SIZE; c++)
                if (_blockOverlays[r, c] != null && _gm.Board[r, c] != 0)
                    _blockOverlays[r, c].rectTransform.localScale = new Vector3(1f, pulseY, 1f);

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
            _spotRts[i].sizeDelta = new Vector2(_spotBaseW[i], _spotBaseH[i]);

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

        // 전용 배경이 떠 있는 동안에는 기본 배경/그리드 백드롭을 감춘다.
        // (Unity의 fake-null 체크로 파괴 여부까지 함께 판정됨)
        bool sceneActive = SceneBackgroundActive;
        if (_bgImage != null && _bgImage.enabled == sceneActive)
        {
            _bgImage.enabled = !sceneActive;
            if (_discoGridBackdrop != null)
                _discoGridBackdrop.enabled = !sceneActive;
            RefreshGrid(); // 빈 셀 표현(내곽선 ↔ 채움)을 즉시 갱신
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
        img.color         = new Color(0f, 0f, 0f, 0.45f);
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
    }

    // ── 광과민성 경고 (디스코 모드 진입 직후) ──────────
    // 화면 전체를 불투명하게 덮는다. 반투명으로 하면 경고를 읽는 동안 뒤에서
    // 깜빡이는 연출이 그대로 비친다 — 경고의 뜻이 없어진다.
    //
    // 검정 판은 첫 프레임부터 불투명하고, 페이드 인은 그 위의 아이콘·문구에만 건다.
    // 판까지 같이 흐리게 시작하면 그 시간만큼 그리드·트레이가 먼저 보인다.
    // (배경은 blackout이 이미 검정으로 만들지만, 그리드와 트레이는 그 대상이 아니다.)
    const float WARN_FADE_IN  = 0.35f;  // ── 조정 손잡이: 문구가 떠오르는 시간(초) ──
    const float WARN_HOLD     = 2.0f;   // ── 조정 손잡이: 다 보인 채 머무르는 시간(초) ──
    const float WARN_FADE_OUT = 0.5f;   // ── 조정 손잡이: 사라지는 시간(초) ──

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
            float alpha  = Mathf.Clamp01(fadeIn) * intensity;
            Color c      = rainbow ? RainbowAt(z, now) : baseColor;
            _ringImgs[i].color = new Color(c.r, c.g, c.b, alpha);
        }

        // 스포크: intensity만 반영 (정적). 살짝 옅게 깔아 링이 주연이 되게.
        // 알록달록 구간에는 터널 끝(zFar)의 색을 따라가 스포크가 모이는 자리와 색이 맞는다.
        if (_spokeImgs != null)
        {
            float spokeAlpha = 0.55f * intensity;
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

    void BuildGrid()
    {
        // 960×960 그리드 컨테이너 (8×120)
        var go = new GameObject("Grid");
        go.transform.SetParent(_canvas.transform, false);
        _gridRt               = go.AddComponent<RectTransform>();
        _gridRt.anchorMin     = new Vector2(0.5f, 0.5f);
        _gridRt.anchorMax     = new Vector2(0.5f, 0.5f);
        _gridRt.pivot         = new Vector2(0.5f, 0.5f);
        _gridRt.sizeDelta     = new Vector2(960, 960);
        _gridRt.anchoredPosition = new Vector2(0, 80);  // 중앙보다 약간 위

        // 빈 셀과 블록을 서로 다른 컨테이너로 나눈다. 원래는 블록이 셀의 자식이라 둘이
        // 한 덩어리로 붙어 다녔는데, 그러면 그 사이에 아무것도 끼울 수 없다.
        // 디스코 빗방울이 "빈 셀 위 · 놓인 블록 아래"로 흐르려면 이 틈이 필요하다(BuildRainLayer).
        var cellLayerRt  = MakeGridLayer(go.transform, "CellLayer");
        var blockLayerRt = MakeGridLayer(go.transform, "BlockLayer");
        _blockLayerRt    = blockLayerRt;

        // 8×8 셀
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var cellPos = new Vector2(-420 + c * 120, 420 - r * 120);

                var cellGo = new GameObject($"Cell_{r}_{c}");
                cellGo.transform.SetParent(cellLayerRt, false);

                var img   = cellGo.AddComponent<Image>();
                img.sprite          = _spr110;
                img.type            = Image.Type.Sliced;
                img.color           = CELL_EMPTY_DARK;
                // 토글(스페셜 블럭)·디스코(무지개 블럭) 모드만 셀 탭을 받는다
                bool tapMode        = ModeSession.SelectedMode == 2 || ModeSession.SelectedMode == 3;
                img.raycastTarget   = tapMode;

                if (tapMode)
                {
                    var clickHandler = cellGo.AddComponent<GridCellClickHandler>();
                    clickHandler.Init(this, r, c);
                }
                _cellImages[r, c]   = img;

                // 블록 오버레이: 빈 셀 배경 위에 블록 이미지를 따로 렌더링.
                // 셀의 자식이 아니라 BlockLayer 밑에 나란히 두고 좌표를 직접 준다.
                var blockGo          = new GameObject($"Block_{r}_{c}");
                blockGo.transform.SetParent(blockLayerRt, false);
                var blockImg         = blockGo.AddComponent<Image>();
                blockImg.color       = Color.clear;
                blockImg.raycastTarget = false;
                _blockOverlays[r, c] = blockImg;
                var blockRt          = blockGo.GetComponent<RectTransform>();
                blockRt.anchorMin    = new Vector2(0.5f, 0.5f);
                blockRt.anchorMax    = new Vector2(0.5f, 0.5f);
                blockRt.pivot        = new Vector2(0.5f, 0.5f);
                // 셀(110)보다 10 큰 120. 예전에 셀의 자식으로 스트레치 + sizeDelta(10,10)이던 것과 같다.
                blockRt.sizeDelta        = new Vector2(120, 120);
                blockRt.anchoredPosition = cellPos;

                var rt = cellGo.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = cellPos;
                rt.sizeDelta        = new Vector2(110, 110);
            }
        }
    }

    // 그리드와 같은 크기·같은 원점을 갖는 빈 컨테이너. 셀과 블록을 나눠 담는다.
    RectTransform MakeGridLayer(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    void BuildPieceTray()
    {
        var trayGo = new GameObject("PieceTray");
        trayGo.transform.SetParent(_canvas.transform, false);
        var trayRt               = trayGo.AddComponent<RectTransform>();
        trayRt.anchorMin         = new Vector2(0.5f, 0f);
        trayRt.anchorMax         = new Vector2(0.5f, 0f);
        trayRt.pivot             = new Vector2(0.5f, 0f);
        trayRt.anchoredPosition  = new Vector2(0, 170);
        trayRt.sizeDelta         = new Vector2(1060, 360);
        _trayRt                  = trayRt;

        for (int i = 0; i < 3; i++)
        {
            var slotGo = new GameObject($"PieceSlot_{i}");
            slotGo.transform.SetParent(trayGo.transform, false);
            var slotRt               = slotGo.AddComponent<RectTransform>();
            slotRt.anchorMin         = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax         = new Vector2(0.5f, 0.5f);
            slotRt.pivot             = new Vector2(0.5f, 0.5f);
            slotRt.anchoredPosition  = new Vector2(-360 + i * 360, 0);
            slotRt.sizeDelta         = new Vector2(340, 340);
            _pieceSlots[i]           = slotGo;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // UI 갱신
    // ═══════════════════════════════════════════════════════════

    // OnStateChanged 이벤트 핸들러 – 클리어 이펙트가 있으면 코루틴으로 처리
    void OnGameStateChanged()
    {
        if (_gm == null) return;
        _highScoreText.text = $"BEST  {_gm.HighScore}";
        _scoreText.text     = _gm.Score.ToString();

        // 토글 모드: 배경색 및 텍스트 색상 갱신
        if (ModeSession.SelectedMode == 2)
        {
            RefreshToggleModeBackground();
            RefreshGauge();
        }
        else if (ModeSession.SelectedMode == 3)
        {
            RefreshDiscoHearts();
        }

        RefreshTray();

        if (_gm.LastClearedRows.Count > 0 || _gm.LastClearedCols.Count > 0)
        {
            if (ModeSession.SelectedMode == 2)
                StartCoroutine(PlayToggleClearEffect());
            else
                StartCoroutine(PlayClearEffect());
        }
        else
            RefreshGrid();
    }

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

    // 게이지 원 색상 갱신
    void RefreshGauge()
    {
        if (_gaugeCircles[0] == null) return;
        bool blackMode   = _gm.ToggleCurrentColor == 1;
        Color emptyColor = blackMode
            ? new Color(0.55f, 0.55f, 0.65f)
            : new Color(0.30f, 0.30f, 0.40f);

        for (int i = 0; i < 2; i++)
            _gaugeCircles[i].color = i < _gm.SpecialGauge ? GOLD : emptyColor;
    }

    // 셀 탭: 토글 모드는 스페셜 블럭 팝업, 디스코 모드는 무지개 블럭 발동
    public void OnGridCellClick(int r, int c)
    {
        if (_gm == null || _dragging || _busy) return;

        if (ModeSession.SelectedMode == 3)
        {
            // 발동하면 보드에서 즉시 사라지므로 연타해도 두 번 터지지 않는다
            if (_gm.Board[r, c] == GameManager.RAINBOW_BLOCK_VAL)
                StartCoroutine(PlayRainbowChain(r, c));
            return;
        }

        if (_gm.Board[r, c] != GameManager.SPECIAL_BLOCK_VAL) return;
        ShowColorSwapPopup();
    }

    // ── 디스코 무지개 블럭 연쇄 발동 ──────────────────────────────
    // 하나를 탭하면 보드에 남아 있는 나머지도 전부 따라 터진다. 블럭을 안 쓰고 모아 두면
    // 여러 개가 쌓이는데, 하나씩 눌러 없애게 하면 같은 연출이 지루하게 반복된다.
    //
    // 동시에 터뜨리지 않고 조금씩 어긋나게 하는 이유: 한 프레임에 다 터지면 화면 플래시와
    // 발동음이 겹쳐 한 덩어리로 뭉개진다. 간격을 두면 "번져 나가는 연쇄"로 읽힌다.
    const float RAINBOW_CHAIN_GAP = 0.15f;   // ── 조정 손잡이: 연쇄 사이 간격(초) ──

    IEnumerator PlayRainbowChain(int row, int col)
    {
        // 탭한 칸을 맨 앞에 두고, 나머지는 거기서 가까운 순으로 번지게 한다.
        var targets = new System.Collections.Generic.List<(int r, int c)> { (row, col) };
        for (int r = 0; r < GameManager.SIZE; r++)
            for (int c = 0; c < GameManager.SIZE; c++)
                if (_gm.Board[r, c] == GameManager.RAINBOW_BLOCK_VAL && !(r == row && c == col))
                    targets.Add((r, c));

        // 첫 칸은 정렬에서 빼야 한다 — 거리 0이라 어차피 맨 앞이지만, 순서를 못박아 둔다.
        targets.Sort(1, targets.Count - 1, System.Collections.Generic.Comparer<(int r, int c)>.Create(
            (a, b) => (Mathf.Abs(a.r - row) + Mathf.Abs(a.c - col))
                .CompareTo(Mathf.Abs(b.r - row) + Mathf.Abs(b.c - col))));

        for (int i = 0; i < targets.Count; i++)
        {
            var (r, c) = targets[i];

            // 십자 클리어는 다른 무지개 블럭을 남기므로(GameManager.ActivateRainbowBlock)
            // 여기 좌표는 연쇄가 끝날 때까지 유효하다. 그래도 연타로 같은 칸이 먼저
            // 터졌을 수 있으니 한 번 더 확인하고 넘어간다.
            if (_gm.Board[r, c] != GameManager.RAINBOW_BLOCK_VAL) continue;

            StartCoroutine(PlayRainbowBurst(r, c));
            if (i < targets.Count - 1) yield return new WaitForSeconds(RAINBOW_CHAIN_GAP);
        }

        // 무지개 발동은 조각을 놓는 경로가 아니라서 기존 게임오버 판정을 하나도 안 거친다.
        // 그래서 십자를 비워도 자리가 안 나면, 조각을 집었다 놓기 전까지 게임이 멈춘 것처럼
        // 보였다. 연쇄가 끝난 뒤 여기서 직접 확인한다.
        // (HasAnyValidMove는 무지개 블럭이 하나라도 남아 있으면 true다. 여기선 다 터진 뒤라 안전하다)
        yield return new WaitForSeconds(RAINBOW_BURST_SEC * 0.5f);   // 폭발이 한풀 꺾일 때까지

        // 기다리는 사이 조각을 집었으면 넘긴다 — EndDrag가 어차피 같은 판정을 한다.
        if (!_dragging && !_gm.HasAnyValidMove())
            ShowGameOver();
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

    void ShowColorSwapPopup()
    {
        var loc = LocalizationManager.Instance;

        var popGo = new GameObject("ColorSwapPopup");
        popGo.transform.SetParent(_canvas.transform, false);
        var bg = popGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        var bgRt = popGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        AddText(popGo.transform, loc.Get("swap_title"), 75, Color.white,
            new Vector2(0, 230), new Vector2(700, 100));
        AddText(popGo.transform, loc.Get("swap_desc"),
            40, new Color(0.75f, 0.75f, 0.85f), new Vector2(0, 140), new Vector2(860, 65));

        // "블랙 → 화이트" 버튼 (어두운 배경)
        var b2wGo  = new GameObject("Btn_B2W");
        b2wGo.transform.SetParent(popGo.transform, false);
        var b2wImg = b2wGo.AddComponent<Image>();
        b2wImg.sprite = MakeRoundedSprite(200, 100, 36);
        b2wImg.type   = Image.Type.Sliced;
        b2wImg.color  = new Color(0.14f, 0.13f, 0.22f);
        var b2wBtn = b2wGo.AddComponent<Button>();
        var b2wRt  = b2wGo.GetComponent<RectTransform>();
        b2wRt.anchorMin = b2wRt.anchorMax = new Vector2(0.5f, 0.5f);
        b2wRt.pivot     = new Vector2(0.5f, 0.5f);
        b2wRt.anchoredPosition = new Vector2(0, 30);
        b2wRt.sizeDelta        = new Vector2(660, 115);
        b2wBtn.onClick.AddListener(() => { _gm.ApplyColorSwap(true);  Destroy(popGo); });
        AddText(b2wGo.transform, loc.Get("swap_b2w"), 52, Color.white,
            Vector2.zero, Vector2.zero, fullStretch: true);

        // "화이트 → 블랙" 버튼 (밝은 배경)
        var w2bGo  = new GameObject("Btn_W2B");
        w2bGo.transform.SetParent(popGo.transform, false);
        var w2bImg = w2bGo.AddComponent<Image>();
        w2bImg.sprite = MakeRoundedSprite(200, 100, 36);
        w2bImg.type   = Image.Type.Sliced;
        w2bImg.color  = new Color(0.90f, 0.90f, 0.94f);
        var w2bBtn = w2bGo.AddComponent<Button>();
        var w2bRt  = w2bGo.GetComponent<RectTransform>();
        w2bRt.anchorMin = w2bRt.anchorMax = new Vector2(0.5f, 0.5f);
        w2bRt.pivot     = new Vector2(0.5f, 0.5f);
        w2bRt.anchoredPosition = new Vector2(0, -110);
        w2bRt.sizeDelta        = new Vector2(660, 115);
        w2bBtn.onClick.AddListener(() => { _gm.ApplyColorSwap(false); Destroy(popGo); });
        AddText(w2bGo.transform, loc.Get("swap_w2b"), 52, new Color(0.10f, 0.08f, 0.20f),
            Vector2.zero, Vector2.zero, fullStretch: true);

        // 취소 버튼
        var cancelGo  = new GameObject("CancelBtn");
        cancelGo.transform.SetParent(popGo.transform, false);
        cancelGo.AddComponent<Image>().color = Color.clear;
        var cancelBtn = cancelGo.AddComponent<Button>();
        var cancelRt  = cancelGo.GetComponent<RectTransform>();
        cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(0.5f, 0.5f);
        cancelRt.pivot     = new Vector2(0.5f, 0.5f);
        cancelRt.anchoredPosition = new Vector2(0, -260);
        cancelRt.sizeDelta        = new Vector2(400, 75);
        cancelBtn.onClick.AddListener(() => Destroy(popGo));
        AddText(cancelGo.transform, loc.Get("cancel"), 48, new Color(0.55f, 0.55f, 0.65f),
            Vector2.zero, Vector2.zero, fullStretch: true);
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
        if (ModeSession.SelectedMode == 3 && !_rainbowActivating)
            StartCoroutine(PlayClearNotes(cells));

        yield return StartCoroutine(FlashAndFade(cells));
        RefreshGrid();

        if (ModeSession.SelectedMode == 1)
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
        if (!_gm.HasAnyValidMove())
        {
            Destroy(tempRoot);
            ShowGameOver();
            yield break;
        }

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

        if (!_gm.HasAnyValidMove())
            ShowGameOver();

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

    void RefreshUI()
    {
        if (_gm == null) return;
        _highScoreText.text = $"BEST  {_gm.HighScore}";
        _scoreText.text     = _gm.Score.ToString();
        if (ModeSession.SelectedMode == 2)
        {
            RefreshToggleModeBackground();
            RefreshGauge();
        }
        else if (ModeSession.SelectedMode == 3)
        {
            RefreshDiscoHearts();
        }
        RefreshGrid();
        RefreshTray();
    }

    void RefreshGrid()
    {
        bool toggleMode = ModeSession.SelectedMode == 2;
        int  activeVal  = toggleMode
            ? (_gm.ToggleCurrentColor == 0 ? GameManager.TOGGLE_WHITE_IDX : GameManager.TOGGLE_BLACK_IDX) + 1
            : -1;
        Color cellEmpty = (toggleMode && _gm.ToggleCurrentColor == 1) ? CELL_EMPTY_LIGHT : CELL_EMPTY_DARK;
        // 전용 배경이 뜨는 동안은 채움을 거의 투명하게 → 배경이 그리드 사이로 그대로 보임.
        // 빈 셀은 아래에서 내곽선으로 따로 그리므로 이 값은 블록이 놓인 셀의 배경에만 적용된다.
        bool sceneBg = SceneBackgroundActive;
        if (sceneBg) cellEmpty.a = 0.15f;

        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                int v = _gm.Board[r, c];
                bool isSpecial = v == GameManager.SPECIAL_BLOCK_VAL;
                bool inactive  = toggleMode && v != 0 && v != activeVal && !isSpecial;

                // 비활성 블록 자리: off 스프라이트로 표시
                if (inactive)
                {
                    _cellImages[r, c].sprite = _spr110;
                    _cellImages[r, c].type   = Image.Type.Sliced;
                    _cellImages[r, c].color  = cellEmpty;

                    Sprite offSpr = (v == GameManager.TOGGLE_WHITE_IDX + 1)
                        ? _toggleWhiteOffSprite : _toggleBlackOffSprite;
                    if (offSpr != null)
                    {
                        _blockOverlays[r, c].sprite = offSpr;
                        _blockOverlays[r, c].type   = Image.Type.Simple;
                        _blockOverlays[r, c].color  = Color.white;
                    }
                    else
                    {
                        _blockOverlays[r, c].color = Color.clear;
                    }
                    continue;
                }

                // 스페셜 블럭: PNG 이미지 사용
                if (isSpecial)
                {
                    _cellImages[r, c].sprite = _spr110;
                    _cellImages[r, c].type   = Image.Type.Sliced;
                    _cellImages[r, c].color  = cellEmpty;
                    if (_specialBlockSprite != null)
                    {
                        _blockOverlays[r, c].sprite = _specialBlockSprite;
                        _blockOverlays[r, c].type   = Image.Type.Simple;
                        _blockOverlays[r, c].color  = Color.white;
                    }
                    else
                    {
                        _blockOverlays[r, c].sprite = _spr110;
                        _blockOverlays[r, c].type   = Image.Type.Sliced;
                        _blockOverlays[r, c].color  = GOLD;
                    }
                    continue;
                }

                // 디스코 무지개 블럭: rainbow_disco.png (크기 맥동·밝기는 UpdateRainbowBlocks가 담당)
                if (v == GameManager.RAINBOW_BLOCK_VAL)
                {
                    _cellImages[r, c].sprite = _spr110;
                    _cellImages[r, c].type   = Image.Type.Sliced;
                    _cellImages[r, c].color  = cellEmpty;
                    _blockOverlays[r, c].sprite = _rainbowBlockSprite != null ? _rainbowBlockSprite : _spr110;
                    _blockOverlays[r, c].type   = _rainbowBlockSprite != null
                        ? Image.Type.Simple : Image.Type.Sliced;
                    _blockOverlays[r, c].color  = Color.white;
                    continue;
                }

                // 배경 레이어. 도시 구간의 빈 셀만 채움 없는 내곽선으로 대체한다
                // (블록이 놓인 셀은 오버레이가 덮으므로 기존 반투명 채움 그대로).
                bool cityOutline = sceneBg && v == 0;
                _cellImages[r, c].sprite = cityOutline ? _sprCellOutline : _spr110;
                _cellImages[r, c].type   = Image.Type.Sliced;
                _cellImages[r, c].color  = cityOutline ? CELL_OUTLINE_ICE : cellEmpty;

                // 블록 오버레이
                if (v != 0)
                {
                    Sprite spr = null;
                    if (toggleMode)
                    {
                        spr = (v == GameManager.TOGGLE_WHITE_IDX + 1)
                            ? _toggleWhiteOnSprite : _toggleBlackOnSprite;
                    }
                    if (spr == null) spr = _colorSprites[v - 1];

                    if (spr != null)
                    {
                        _blockOverlays[r, c].sprite = spr;
                        _blockOverlays[r, c].type   = Image.Type.Simple;
                        _blockOverlays[r, c].color  = Color.white;
                    }
                    else
                    {
                        _blockOverlays[r, c].sprite = _spr110;
                        _blockOverlays[r, c].type   = Image.Type.Sliced;
                        _blockOverlays[r, c].color  = PieceData.Colors[v - 1];
                    }
                }
                else
                {
                    _blockOverlays[r, c].color = Color.clear;
                }
            }
    }

    void RefreshTray()
    {
        for (int i = 0; i < 3; i++)
        {
            _previewContainers[i] = null;
            foreach (Transform child in _pieceSlots[i].transform)
                Destroy(child.gameObject);

            if (_gm.CurrentPieces[i].placed) continue;

            BuildPiecePreview(_pieceSlots[i].transform, i, _gm.CurrentPieces[i]);
        }
    }

    void BuildPiecePreview(Transform parent, int slotIdx, GameManager.PieceInstance piece)
    {
        var shape  = PieceData.Shapes[piece.shapeIndex];
        var color  = PieceData.Colors[piece.colorIndex];
        int rows   = shape.Length;
        int cols   = shape[0].Length;

        float cs = Mathf.Min(100f, 320f / Mathf.Max(rows, cols));

        var containerGo = new GameObject("PiecePreview");
        containerGo.transform.SetParent(parent, false);

        var hitImg = containerGo.AddComponent<Image>();
        hitImg.color = Color.clear;

        _previewContainers[slotIdx] = containerGo;
        _previewCellSizes[slotIdx]  = cs;

        var cRt               = containerGo.GetComponent<RectTransform>();
        cRt.anchorMin         = new Vector2(0.5f, 0.5f);
        cRt.anchorMax         = new Vector2(0.5f, 0.5f);
        cRt.pivot             = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition  = Vector2.zero;
        cRt.sizeDelta         = new Vector2(cols * cs, rows * cs);

        var drag = containerGo.AddComponent<PieceDragHandler>();
        drag.Init(this, slotIdx, piece);

        int  cornerR    = Mathf.RoundToInt(30 * cs / 110);
        int  cell       = Mathf.RoundToInt(cs) - 4;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (shape[r][c] == 0) continue;

                var cellGo = new GameObject($"pc_{r}_{c}");
                cellGo.transform.SetParent(containerGo.transform, false);

                var img = cellGo.AddComponent<Image>();

                Sprite pspr = null;
                if (ModeSession.SelectedMode == 2)
                {
                    pspr = (piece.colorIndex == GameManager.TOGGLE_WHITE_IDX)
                        ? _toggleWhiteOnSprite : _toggleBlackOnSprite;
                }
                if (pspr == null) pspr = _colorSprites[piece.colorIndex];

                if (pspr != null)
                {
                    img.sprite = pspr;
                    img.type   = Image.Type.Simple;
                    img.color  = Color.white;
                }
                else
                {
                    img.sprite = MakeRoundedSprite(cell, cell, cornerR);
                    img.type   = Image.Type.Sliced;
                    img.color  = color;
                }
                img.raycastTarget = false;

                var rt = cellGo.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(
                    (c - (cols - 1) / 2f) * cs,
                   -(r - (rows - 1) / 2f) * cs);
                rt.sizeDelta        = new Vector2(cell, cell);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 드래그 처리
    // ═══════════════════════════════════════════════════════════

    public void BeginDrag(int idx, GameManager.PieceInstance piece, Vector2 screenPos)
    {
        if (_dragging || _busy) return;
        _dragging = true;
        _dragIdx  = idx;
        LiftContainer(idx);

        if (_audioSource != null && _sfxSelect != null)
            _audioSource.PlayOneShot(_sfxSelect);
    }

    void LiftContainer(int idx)
    {
        var container = _previewContainers[idx];
        if (container == null) return;

        container.transform.SetParent(_canvas.transform, false);
        container.transform.SetAsLastSibling();

        float cs = _previewCellSizes[idx];
        float baseScale = cs > 0f ? 120f / cs : 1f;
        if (cs > 0f)
            container.transform.localScale = Vector3.one * baseScale;

        var cg = container.GetComponent<CanvasGroup>();
        if (cg == null) cg = container.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        _dragContainer = container.GetComponent<RectTransform>();
    }

    void Update()
    {
        if (_dragging && _dragContainer != null)
        {
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null)
            {
                Vector2 screenPos = pointer.position.ReadValue();
                float sf = _canvas.scaleFactor;
                Vector2 canvasPos = (screenPos - new Vector2(Screen.width, Screen.height) * 0.5f) / sf;
                _dragContainer.anchorMin        = new Vector2(0.5f, 0.5f);
                _dragContainer.anchorMax        = new Vector2(0.5f, 0.5f);
                _dragContainer.pivot            = new Vector2(0.5f, 0.5f);
                _dragContainer.anchoredPosition = canvasPos + new Vector2(0, 260f);
            }
        }

        if (ModeSession.SelectedMode == 3)
        {
            UpdateDiscoVisuals();
#if UNITY_EDITOR
            // 에디터 전용 구간 이동 단축키
            //   F8  터널 직후 반짝임 구간
            //   F9  도시 등장(화이트아웃 직전)
            //   F10 자동차(드라이빙) 등장 구간 몇 초 전 — 도시→드라이빙 크로스페이드를 처음부터 볼 수 있게
            //   F11 2차 터널 진입 몇 초 전 — 검정 페이드인부터 볼 수 있게
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
                BGMManager.Instance?.Seek(CityLightBackground.APPEAR_START - 2.0);
            if (kb != null && kb.f8Key.wasPressedThisFrame)
                BGMManager.Instance?.Seek(100.6 - 2.0);
            if (kb != null && kb.f10Key.wasPressedThisFrame)
                BGMManager.Instance?.Seek(DrivingBackground.APPEAR_START - 4.0);
            if (kb != null && kb.f11Key.wasPressedThisFrame)
                BGMManager.Instance?.Seek(BLACK2_START - 4.0);
#endif
        }
    }

    bool GetGridCell(Vector2 screenPos, int[][] shape, out int row, out int col)
    {
        float sf = _canvas.scaleFactor;
        Vector2 adjustedScreenPos = screenPos + new Vector2(0, 260f * sf);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _gridRt, adjustedScreenPos, null, out Vector2 glp);

        int cursorRow = Mathf.FloorToInt((-glp.y + 480) / 120);
        int cursorCol = Mathf.FloorToInt(( glp.x + 480) / 120);
        row = cursorRow - shape.Length    / 2;
        col = cursorCol - shape[0].Length / 2;
        return true;
    }

    public void OnDrag(int idx, Vector2 screenPos)
    {
        if (!_dragging || _dragIdx != idx) return;

        var piece = _gm.CurrentPieces[_dragIdx];
        var shape = PieceData.Shapes[piece.shapeIndex];
        GetGridCell(screenPos, shape, out _previewRow, out _previewCol);
        UpdateGridPreview();
    }

    public void EndDrag(int idx, Vector2 screenPos)
    {
        if (!_dragging || _dragIdx != idx) return;

        var liftedContainer = _dragContainer != null ? _dragContainer.gameObject : null;

        var piece = _gm.CurrentPieces[_dragIdx];
        var shape = PieceData.Shapes[piece.shapeIndex];
        GetGridCell(screenPos, shape, out int dropRow, out int dropCol);

        bool ok = _gm.TryPlacePiece(_dragIdx, dropRow, dropCol);

        if (ok)
        {
            if (_audioSource != null && _sfxDecide != null)
                _audioSource.PlayOneShot(_sfxDecide);
            if (liftedContainer != null)
                Destroy(liftedContainer);
        }

        FinishDrag(ok);
    }

    void FinishDrag(bool placed)
    {
        int finishedIdx  = _dragIdx;
        _dragging        = false;
        _dragIdx         = -1;
        _previewRow      = -1;
        _previewCol      = -1;
        _dragContainer   = null;

        if (!placed)
        {
            RefreshGrid();
            var container = _previewContainers[finishedIdx];
            if (container != null)
            {
                container.transform.SetParent(_pieceSlots[finishedIdx].transform, false);

                var cg = container.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = true;

                var rt              = container.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                container.transform.localScale = Vector3.one;
            }
        }

        bool iceSlideWillRun = ModeSession.SelectedMode == 1 &&
            (_gm.LastClearedRows.Count > 0 || _gm.LastClearedCols.Count > 0);
        if (!iceSlideWillRun && !_gm.HasAnyValidMove())
            ShowGameOver();
    }

    void UpdateGridPreview()
    {
        RefreshGrid();
        if (_dragIdx < 0) return;

        var piece = _gm.CurrentPieces[_dragIdx];
        var shape = PieceData.Shapes[piece.shapeIndex];
        var color = PieceData.Colors[piece.colorIndex];
        bool valid = _gm.CanPlacePiece(_dragIdx, _previewRow, _previewCol);
        bool toggleMode = ModeSession.SelectedMode == 2;

        for (int r = 0; r < shape.Length; r++)
            for (int c = 0; c < shape[r].Length; c++)
            {
                if (shape[r][c] == 0) continue;
                int br = _previewRow + r;
                int bc = _previewCol + c;
                if (br < 0 || br >= 8 || bc < 0 || bc >= 8) continue;

                if (valid)
                {
                    var pspr = _colorSprites[piece.colorIndex];
                    if (pspr != null)
                    {
                        _blockOverlays[br, bc].sprite = pspr;
                        _blockOverlays[br, bc].type   = Image.Type.Simple;
                        _blockOverlays[br, bc].color  = new Color(1f, 1f, 1f, 0.55f);
                    }
                    else
                    {
                        _blockOverlays[br, bc].sprite = _spr110;
                        _blockOverlays[br, bc].type   = Image.Type.Sliced;
                        _blockOverlays[br, bc].color  = new Color(color.r, color.g, color.b, 0.55f);
                    }
                }
                else
                {
                    _blockOverlays[br, bc].sprite = _spr110;
                    _blockOverlays[br, bc].type   = Image.Type.Sliced;
                    _blockOverlays[br, bc].color  = new Color(1f, 0.25f, 0.25f, 0.55f);
                }
            }
    }

    // ═══════════════════════════════════════════════════════════
    // 게임 오버
    // ═══════════════════════════════════════════════════════════

    // 디스코 게임오버. 암전을 소리와 같은 길이로 맞춰, 음이 처지는 만큼 화면도 같이 닫히게 한다.
    // 상수를 둘로 나눠 둔 건 따로 만질 여지를 남기려는 것뿐이다.
    const float GAMEOVER_TAPE_SEC = 1.2f;   // ── 조정 손잡이: 음이 낮아지며 멎는 시간(초) ──
    const float GAMEOVER_FADE_SEC = 1.2f;   // ── 조정 손잡이: 화면이 검어지는 시간(초) ──

    IEnumerator FadeInCanvasGroup(CanvasGroup cg, float seconds)
    {
        for (float e = 0f; e < seconds; e += Time.deltaTime)
        {
            if (cg == null) yield break;
            cg.alpha = e / seconds;
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
    }

    void ShowGameOver()
    {
        // 디스코는 연출이 곡에 얹혀 있어서, 게임이 끝났는데 음악만 계속 돌면 붕 뜬다.
        // 테이프가 멎듯 음을 끌어내리고 화면을 완전한 검정으로 덮어 한 곡을 닫는다.
        bool discoMode = ModeSession.SelectedMode == 3;

        // 이미 떠 있으면 두 번 쌓지 않는다. 두 장이 겹치면 반투명이 두 겹으로 진해지고,
        // 아래 장이 버튼을 먹어 부활·다시시작이 안 눌린다.
        if (_gameOverOverlay != null) return;

        var overlayGo = new GameObject("GameOverOverlay");
        overlayGo.transform.SetParent(_canvas.transform, false);
        overlayGo.transform.SetAsLastSibling();
        var overlayImg = overlayGo.AddComponent<Image>();
        // 다른 모드는 판이 비쳐 보이는 78% 반투명 그대로. 디스코만 완전 암전.
        overlayImg.color = discoMode ? Color.black : new Color(0, 0, 0, 0.78f);
        var overlayRt = overlayGo.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        _gameOverOverlay = overlayGo;

        if (discoMode)
        {
            BGMManager.Instance?.TapeStop(GAMEOVER_TAPE_SEC);

            // 암전은 소리와 같은 길이로 나란히 닫힌다.
            var cg   = overlayGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(FadeInCanvasGroup(cg, GAMEOVER_FADE_SEC));
        }

        var loc = LocalizationManager.Instance;

        AddText(overlayGo.transform, loc.Get("game_over"), 95, Color.white,
            new Vector2(0, 200), new Vector2(900, 130));

        AddText(overlayGo.transform, $"{loc.Get("score")}  {_gm.Score}", 60, GOLD,
            new Vector2(0, 60), new Vector2(700, 90));

        AddText(overlayGo.transform, $"{loc.Get("best")}  {_gm.HighScore}", 50,
            new Color(0.8f, 0.8f, 0.8f),
            new Vector2(0, -30), new Vector2(700, 70));

        // ── 부활 버튼 (광고 시청, 한 판에 1회) ──────────────────
        if (!_reviveUsed)
        {
            var reviveGo = new GameObject("ReviveBtn");
            reviveGo.transform.SetParent(overlayGo.transform, false);
            var reviveImg = reviveGo.AddComponent<Image>();
            reviveImg.sprite = MakeRoundedBorderSprite(200, 100, 36, 4);
            reviveImg.type   = Image.Type.Sliced;
            reviveImg.color  = new Color(1f, 0.85f, 0.3f); // 골드 색상

            var reviveBtn = reviveGo.AddComponent<Button>();
            var reviveColors = reviveBtn.colors;
            reviveColors.normalColor      = Color.white;
            reviveColors.highlightedColor = new Color(1f, 1f, 1f, 0.8f);
            reviveColors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
            reviveBtn.colors = reviveColors;

            var reviveRt              = reviveGo.GetComponent<RectTransform>();
            reviveRt.anchorMin        = new Vector2(0.5f, 0.5f);
            reviveRt.anchorMax        = new Vector2(0.5f, 0.5f);
            reviveRt.pivot            = new Vector2(0.5f, 0.5f);
            reviveRt.anchoredPosition = new Vector2(0, -140);
            reviveRt.sizeDelta        = new Vector2(620, 120);

            AddText(reviveGo.transform,
                loc.CurrentLanguage == Language.Korean ? "▶ 광고 보고 부활" : "▶ Revive (Watch Ad)",
                48, Color.white,
                Vector2.zero, Vector2.zero, fullStretch: true);

            reviveBtn.onClick.AddListener(() =>
            {
                // 오프라인이면 광고가 어차피 안 뜨고 보상도 못 받음 → 클릭 자체를 차단.
                // NetworkChecker.Update가 다음 프레임에 오버레이를 띄워 사용자에게 안내.
                if (Application.internetReachability == NetworkReachability.NotReachable) return;
                AdManager.GetOrCreate().ShowRewarded(
                    onRewarded: () =>
                    {
                        _reviveUsed = true;

                        // 부활은 같은 판을 이어가는 것이므로 곡도 끊긴 자리에서 이어 붙인다.
                        if (discoMode)
                        {
                            BGMManager.Instance?.RestorePlayback();
                            BGMManager.Instance?.Resume();
                        }

                        _gameOverOverlay = null;
                        Destroy(overlayGo);
                        _gm.Revive();
                    },
                    onFailed: () =>
                    {
                        // 광고 준비 안 됨을 사용자에게 알림
                        Debug.Log("[InGameUI] Rewarded ad not available.");
                    });
            });
        }

        // ── 다시 시작 버튼 ────────────────────────────────────────
        var btnGo = new GameObject("RestartBtn");
        btnGo.transform.SetParent(overlayGo.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = MakeRoundedBorderSprite(200, 100, 36, 4);
        btnImg.type   = Image.Type.Sliced;
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color restartBorderColor);
        btnImg.color  = restartBorderColor;
        var btn = btnGo.AddComponent<Button>();
        var btnRt               = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin         = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax         = new Vector2(0.5f, 0.5f);
        btnRt.pivot             = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition  = new Vector2(0, -285);
        btnRt.sizeDelta         = new Vector2(620, 120);
        btn.onClick.AddListener(() =>
        {
            _reviveUsed = false;

            // 디스코는 곡을 처음부터 다시 튼다. 새 판이니 연출도 도입부부터 시작해야 한다.
            // 되감은 직후 구간(t < LOOP_FADE_SEC)이 검정에서 밝아오는 램프라, 암전이 그대로 이어진다.
            if (discoMode)
            {
                BGMManager.Instance?.RestorePlayback();
                BGMManager.Instance?.Seek(0.0);   // 멈춰 있으면 Seek이 재생까지 다시 건다
            }

            _gameOverOverlay = null;
            Destroy(overlayGo);
            _gm.ResetGame();
        });
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color restartTextColor);
        AddText(btnGo.transform, loc.Get("restart"), 55, restartTextColor,
            Vector2.zero, Vector2.zero, fullStretch: true);
    }

    void AddText(Transform parent, string txt, int size, Color color,
        Vector2 pos, Vector2 sizeDelta, bool fullStretch = false)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font      = Font4();
        t.fontSize  = size;
        t.fontStyle = FontStyle.Bold;
        t.color     = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.text      = txt;
        var rt = go.GetComponent<RectTransform>();
        if (fullStretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = sizeDelta;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 유틸 – 둥근 사각형 Sprite 생성
    // ═══════════════════════════════════════════════════════════

    Sprite MakeRoundedBorderSprite(int w, int h, int r, int border)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool outer = InRoundedRect(x, y, w, h, r);
                bool inner = InRoundedRect(x - border, y - border,
                    w - border * 2, h - border * 2, Mathf.Max(0, r - border));
                px[y * w + x] = (outer && !inner)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    Sprite MakeRoundedSprite(int w, int h, int r)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = InRoundedRect(x, y, w, h, r)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    // 속을 비운 라운드 사각 테두리. 바깥 라운드렉트에는 들어가고 thickness만큼 줄인
    // 안쪽 라운드렉트에는 안 들어가는 픽셀만 남겨 얇은 내곽선을 만든다.
    Sprite MakeRoundedOutlineSprite(int w, int h, int r, int thickness)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[w * h];
        int innerW = w - thickness * 2;
        int innerH = h - thickness * 2;
        int innerR = Mathf.Max(0, r - thickness);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool outer = InRoundedRect(x, y, w, h, r);
                bool inner = x >= thickness && y >= thickness
                          && x < w - thickness && y < h - thickness
                          && InRoundedRect(x - thickness, y - thickness, innerW, innerH, innerR);
                px[y * w + x] = (outer && !inner)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    bool InRoundedRect(int px, int py, int w, int h, int r)
    {
        int cx = px < r ? r : (px > w - 1 - r ? w - 1 - r : px);
        int cy = py < r ? r : (py > h - 1 - r ? h - 1 - r : py);
        float dx = px - cx, dy = py - cy;
        return dx * dx + dy * dy <= (float)r * r;
    }

    // 폰트 로드 실패 시 자기 자신을 부르면 무한 재귀(StackOverflow)라 빌트인 폰트로 떨어뜨린다.
    // MainMenuUI.Font4()와 같은 폴백.
    Font Font4() => Resources.Load<Font>("Fonts/SCDream4")
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
}

// ═══════════════════════════════════════════════════════════════
// 드래그 핸들러 (각 조각 프리뷰에 붙는 컴포넌트)
// ═══════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════
// 그리드 셀 탭 핸들러 (토글 모드 스페셜 블럭용)
// ═══════════════════════════════════════════════════════════════
public class GridCellClickHandler : MonoBehaviour, IPointerClickHandler
{
    InGameUI _ui;
    int _row, _col;

    public void Init(InGameUI ui, int row, int col)
    {
        _ui  = ui;
        _row = row;
        _col = col;
    }

    public void OnPointerClick(PointerEventData e) => _ui.OnGridCellClick(_row, _col);
}

public class PieceDragHandler : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IEndDragHandler
{
    InGameUI               _ui;
    int                    _slotIdx;
    GameManager.PieceInstance _piece;

    public void Init(InGameUI ui, int slotIdx, GameManager.PieceInstance piece)
    {
        _ui      = ui;
        _slotIdx = slotIdx;
        _piece   = piece;
    }

    public void OnPointerDown(PointerEventData e)
        => _ui.BeginDrag(_slotIdx, _piece, e.position);

    public void OnDrag(PointerEventData e)
        => _ui.OnDrag(_slotIdx, e.position);

    public void OnEndDrag(PointerEventData e)
        => _ui.EndDrag(_slotIdx, e.position);
}
