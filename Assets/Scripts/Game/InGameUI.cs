using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 인게임 화면 자동 생성 스크립트
/// 빈 씬에 빈 GameObject 하나 만들고 이 스크립트만 붙이면 됩니다.
/// </summary>
public partial class InGameUI : MonoBehaviour
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
    Image[]     _gaugeCircles;                       // 색 전환 게이지 표시 (토글 모드)

    // 토글 모드 스페셜 블럭: 그리드 위에서 끌어 줄을 고른다.
    // 손가락이 올라간 칸이 줄을 정하고, "최근에 민 방향"이 가로/세로를 정한다.
    // 시작점부터의 총 이동으로 축을 잡으면 먼 줄로 건너가는 동안 축이 엉킨다.
    // 특히 스페셜 블럭 자신의 줄 — 쓰는 순간 자기 칸이 메워져서 가장 자주 고르는 줄인데 —
    // 그게 제일 안 잡힌다. 그래서 방금 민 방향만 본다.
    bool          _specDrag;
    bool          _specHorizontal;
    bool          _specAxisSet;
    Vector2       _specAnchor;              // 마지막으로 축을 판단한 화면 좌표
    int           _specRow = -1, _specCol = -1;  // 끌고 있는 스페셜 블럭의 자리
    int           _specLine = -1;           // 지금 조준 중인 줄 (없으면 -1)
    float         _specEndTime = -99f;      // 드래그 직후 따라오는 탭을 걸러낸다
    Image         _lineHiRow, _lineHiCol;   // 그리드 위에 얹는 조준 줄 하이라이트

    // 탭했을 때 뜨는 드래그 안내. 문구 대신 손이 직접 한 번 쓸어 보인다 —
    // 조작을 글로 적어 두면 읽지 않고, 읽어도 어느 방향으로 미는지가 안 그려진다.
    Sprite        _dragGuideSprite;
    GameObject    _dragGuideGo;
    Coroutine     _dragGuideCo;

    // 축을 다시 판단하는 데 필요한 이동량(레퍼런스 해상도 기준 px)
    const float SPEC_AXIS_STEP = 45f;

    // 손놓음을 놓쳤다고 판단하기까지 기다리는 시간. 누른 직후의 한두 프레임을 피할 만큼만.
    const float DRAG_WATCHDOG_SEC = 0.15f;

    // 스페셜 블럭으로 한 줄을 맞췄을 때 그 줄이 한 칸씩 넘어가는 연출.
    // 뒤집기는 이 모드의 규칙(화이트↔블랙)을 그대로 그린 동작이라 설명이 필요 없다.
    const float SWAP_FLIP_SEC     = 0.22f;    // 한 칸이 뒤집히는 데 걸리는 시간
    const float SWAP_FLIP_STAGGER = 0.028f;   // 옆 칸으로 번지는 간격
    Coroutine   _swapFlipCo;

    // 안내 손 아이콘 크기와 한 번 쓸어 보이는 거리·시간.
    // 도착점은 블럭과 행도 열도 다른 칸이다 — 같은 줄 안에서 끝나면 그 줄 안에서만
    // 움직일 수 있는 것처럼 보인다.
    const float GUIDE_HAND_SIZE  = 170f;
    const float GUIDE_SWEEP_SEC  = 0.85f;
    const int   GUIDE_SWEEP_ROWS = 2;
    const int   GUIDE_SWEEP_COLS = 3;

    // drag.png(512²)에서 손끝이 닿는 지점은 중심에서 왼쪽 위로 치우쳐 있다.
    // 아이콘 중심을 이만큼 오른쪽 아래로 밀어야 손끝이 목표 칸에 정확히 얹힌다.
    static readonly Vector2 GUIDE_TIP_OFFSET = new Vector2(0.227f, -0.246f);

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

    // 떠 있는 게임오버 오버레이. 두 장이 쌓이지 않게 붙잡아 두고, CheckGameOver가
    // 매 프레임 불려도 한 번만 뜨게 하는 문지기 역할도 한다.
    GameObject _gameOverOverlay;

    // 이 시각까지는 게임오버 판정을 미룬다. 무지개 폭발처럼 "보드는 이미 확정됐지만
    // 아직 보여 줄 게 남은" 연출을 검은 화면이 잘라먹지 않게 하는 용도다.
    float _gameOverHoldUntil;

    // ── 드래그 상태 ─────────────────────────────────────────────
    int           _dragIdx      = -1;
    bool          _dragging     = false;
    float         _dragStartTime;   // 놓친 손놓음을 정리하는 감시용(AbortStuckDrag)
    bool          _busy         = false;   // 아이스 슬라이드 체인 중 입력 차단
    RectTransform _dragContainer;   // 집어든 조각 컨테이너 (트레이에서 분리됨)
    int           _previewRow   = -1;
    int           _previewCol   = -1;


    // ── 오디오 ──────────────────────────────────────────────────
    AudioSource _audioSource;
    AudioClip   _sfxSelect;   // 선택: 조각을 집었을 때
    AudioClip   _sfxDecide;   // 결정: 조각을 그리드에 놓았을 때
    AudioClip   _sfxToggle;   // 토글 모드: 화이트↔블랙 전환 효과음
    AudioClip   _sfxToggleSpecial;   // 토글 모드: 스페셜 블럭으로 한 줄을 맞췄을 때
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
    // 조준 줄 색. 한 가지뿐이다 — 어느 줄이든 고를 수 있으므로, 줄마다 색을 달리하면
    // 되는 줄/안 되는 줄로 읽힌다. 이 띠는 "지금 이 줄을 겨누고 있다"만 말한다.
    static readonly Color LINE_HI = new Color(0.35f, 0.68f, 1.00f, 0.32f);

    // 검은 배경(화이트 모드) 위에서 띠 알파에 곱하는 값. 밝은 배경과 세기를 맞춘다.
    const float LINE_HI_DARK_BG = 0.62f;

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

    // ── 디스코 콤보 상태등 ──────────────────────────────────────
    // 디스코는 콤보를 유지해야 살아남는다. 지금 콤보가 몇인지, 그리고 이번 세트가
    // 아직 위험한지를 상시로 보여 준다. 남은 조각 수는 트레이가 이미 보여 주므로 여기서 안 센다.
    // 첫 클리어 전(콤보 0)에는 규칙이 아직 안 걸리므로 아무것도 띄우지 않는다.
    Text _comboStatusText;
    // 하트 게이지와 같은 줄, 오른쪽. y는 글자 상단 기준이라 글자를 키운 만큼 위로 올려
    // 하트와 눈높이를 맞춘다(점수 하단 -315와 그리드 백드롭 상단 -385 사이 띠).
    // 상자 정중앙에 맞추면 글꼴이 아래에 비워 두는 디센더 자리 때문에 눈에는 처져 보인다.
    static readonly Vector2 COMBO_STATUS_POS  = new Vector2(312, -292);
    static readonly Color   COMBO_STATUS_SAFE = Color.white;
    static readonly Color   COMBO_STATUS_RISK = new Color(1f, 0.28f, 0.34f);
    const int   COMBO_STATUS_FONT        = 64;    // ── 조정 손잡이: 상태등 글자 크기 ──
    const float COMBO_RISK_PULSE         = 4.5f;  // ── 조정 손잡이: 경고 깜빡임 속도 ──
    const float COMBO_RISK_PULSE_LAST    = 9f;    // 마지막 조각 하나가 남았을 때의 속도
    const float COMBO_STATUS_PULSE_SCALE = 0.10f; // 경고일 때 같이 커졌다 작아지는 폭
    int   _comboStatusShown = -1;   // 지금 글자로 떠 있는 콤보 수 (문자열 재생성 방지)

    // 매 프레임 다시 할 필요가 없는 것들을 걸러내는 표시들
    bool  _boardNeedsCheck = true;  // 게임오버 판정을 다시 해야 하는지
    float _lastPulseY      = -1f;   // 마지막으로 블록에 적용한 세로 배율

    // ── 공통 스프라이트 캐시 ────────────────────────────────────
    Sprite _spr110;   // 110×110 r=30  (그리드 셀용)

    // 게임오버 버튼 두 개가 같은 규격을 쓴다. 화면을 띄울 때마다 두 장을 새로 굽지 않게 캐시.
    Sprite _sprBtnBorder;
    Sprite BtnBorderSprite
    {
        get
        {
            if (_sprBtnBorder == null) _sprBtnBorder = MakeRoundedBorderSprite(200, 100, 36, 4);
            return _sprBtnBorder;
        }
    }
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

    // 터널이 화면을 다 먹지 않게 눌러 두는 값. 링은 원래 페이드인 뒤 불투명이라
    // 구간 내내 배경이 통째로 터널이 됐고, 그 위에 얹힌 보드가 묻혔다.
    const float TUNNEL_RING_ALPHA  = 0.1f;   // ── 조정 손잡이: 링 최대 불투명도 ──
    const float TUNNEL_SPOKE_ALPHA = 0.2f;   // ── 조정 손잡이: 스포크(트레일) 불투명도 ──

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
    const float  TUNNEL2_SPEED_MUL = 1.3f;   // ── 조정 손잡이: 1차 대비 링 속도 ──
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
    // 그리드 뒤에 까는 검은 판. 배경이 무엇이든 그 위에 일정한 바닥을 만들어 준다.
    Image           _discoGridBackdrop;
    // ── 조정 손잡이: 그 판의 진하기 ──
    // 배경이 밝아지면 BRIGHT 쪽으로 옮겨 간다. 화이트아웃 구간에서 유리 격자가
    // 흰 배경에 묻히지 않으려면 바닥이 그만큼 어두워져야 한다.
    const float DISCO_BACKDROP_ALPHA        = 0.45f;  // 평소(어두운 배경)
    const float DISCO_BACKDROP_ALPHA_BRIGHT = 0.92f;  // 화이트아웃 최대
    const float DISCO_BACKDROP_SCENE        = 0.45f;  // 도시·드라이빙 구간의 가중치

    // 아이스 뒤로가기 버튼의 화살표. IceBackground 의 WAVE_DARK 와 같은 파랑이다.
    static readonly Color32 ICE_BACK_BLUE = new Color32(0, 112, 216, 255);
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
    static readonly Color CELL_EMPTY_DARK = new Color(0.17f,  0.17f,  0.24f);  // 화이트 모드 빈 셀
    // 아이스에서 블록이 놓인 셀의 밑판. 위에 불투명한 얼음 PNG 가 덮이므로 거의 보이지 않지만,
    // 모서리 곡률 차이로 삐져나오는 부분이 배경을 가리지 않게 반만 남긴다.
    const float ICE_FILL_ALPHA = 0.5f;   // ── 조정 손잡이: 블록 놓인 셀 밑판의 불투명도 ──

    static readonly Color CELL_EMPTY_LIGHT= new Color(0.75f,  0.75f,  0.82f);  // 블랙 모드 빈 셀
    static readonly Color GOLD            = new Color(1f,     0.85f,  0.3f);
    // 도시 구간 빈 셀: 채움 대신 얼음빛 회색 내곽선만 → 도시를 가리지 않으면서 격자는 읽힘.
    // ── 조정 손잡이: 선 두께(px, 110×110 셀 기준)와 색/투명도 ──
    const int CELL_OUTLINE_PX = 2;

    // 디스코 빈 셀: 색도 윤곽선도 없이 유리판 한 장.
    //
    // 배경이 곡을 따라 통째로 바뀌는 모드라, 셀에 색을 주면 그때그때 다른 물건처럼 보인다.
    // (예전에는 도시 구간만 내곽선, 나머지는 반투명 채움이라 둘이 번갈아 나왔다)
    // 흰색만 아주 옅게 깔면 뒤 색을 그대로 통과시키고 밝기만 살짝 올려서, 배경이 뭐든
    // 유리는 늘 같은 유리로 보인다.
    //
    // 가장자리는 선을 긋지 않고 안쪽으로 번지게 한다. 선이면 테두리로 읽히지만
    // 번지면 판의 두께로 읽힌다 — 유리처럼 보이는 건 흐릿함이 아니라 이 두 가지다.
    //
    // 세 겹으로 쌓는다. 셋 다 가장자리에서 안쪽으로만 번지고 셀 밖으로는 넘어가지 않는다.
    //   막   가장 옅게 깔리는 바탕. 배경이 검을 때 격자가 아예 사라지지 않을 만큼만.
    //   번짐 넓고 약하게 퍼지는 빛. 유리의 두께처럼 읽힌다.
    //   선   가장자리 1.5px 의 밝은 줄. 유리 모서리에 빛이 걸린 자리다.
    //
    // 값이 작아 보이는 건 이 프로젝트가 Linear 색 공간이기 때문이다(ProjectSettings).
    // 선형에서는 알파 0.09 가 화면에 sRGB 0.33(회색 판)으로 나온다 — sRGB 기준으로
    // 눈에 보이길 원하는 밝기의 약 2.2제곱이 여기 들어갈 값이다.
    // 형태와 세기는 손에 맞춘 값이고, 색 두 개만 cell.png 에서 가져왔다.
    //   안쪽 — 막 + 번짐 + 가장자리 선. 밝은 하늘색이고 가운데로 갈수록 사라진다.
    //   바깥 — 남색 그림자. 검정이 아니라 남색인 게 레퍼런스의 색감이고, 디스코 배경과도 붙는다.
    // 이웃 타일의 그림자와 사이에서 만나 어두운 이음매를 만든다. 배경이 밝을 때 격자가
    // 읽히는 건 이 이음매 덕이다 — 밝은 글로우는 밝은 배경에서 대비를 잃는다.
    const float GLASS_INSET        = 7f;     // ── 조정 손잡이: 유리를 셀 안으로 밀어 넣는 폭 ──
    const float GLASS_CORNER       = 23f;    // ── 조정 손잡이: 유리 모서리 반지름 ──
    const float GLASS_BODY_ALPHA   = 0.006f; // ── 조정 손잡이: 바탕 막 ──
    const float GLASS_RIM_ALPHA    = 0.100f; // ── 조정 손잡이: 번짐의 세기 ──
    // 18 을 넘기면 네 모서리의 글로우가 가운데서 만나 X 자 자국이 생긴다.
    const float GLASS_RIM_PX       = 12f;    // ── 조정 손잡이: 번짐이 안으로 퍼지는 거리 ──
    const float GLASS_EDGE_ALPHA   = 0.450f; // ── 조정 손잡이: 가장자리 선의 세기 ──
    const float GLASS_EDGE_PX      = 1.8f;   // ── 조정 손잡이: 그 선의 두께 ──
    const float GLASS_SHADOW_ALPHA = 0.55f;  // ── 조정 손잡이: 바깥 그림자 진하기 ──
    const float GLASS_SHADOW_PX    = 3.6f;   // ── 조정 손잡이: 그림자가 퍼지는 거리 ──

    static readonly Color32 GLASS_TINT   = new Color32(190, 229, 255, 255);  // 안쪽 글로우
    static readonly Color32 GLASS_SHADOW = new Color32(  0,  53,  88, 255);  // 바깥 그림자

    Sprite _sprGlassCell;
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
        if (ModeSession.IsDisco)
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

        if (ModeSession.IsToggle)
            _sfxToggle = Resources.Load<AudioClip>("Audio/SFX/toggle_switch");
        else if (ModeSession.IsDisco)
            _sfxRainbow = Resources.Load<AudioClip>("Audio/SFX/disco_special");

        // 스프라이트 미리 생성
        LoadColorSprites();
        _spr110 = MakeRoundedSprite(110, 110, 30);
        _sprCellOutline = MakeRoundedOutlineSprite(110, 110, 30, CELL_OUTLINE_PX);

        if (ModeSession.IsIce)
        {
            var meltTex = Resources.Load<Texture2D>("Sprites/Effects/melting");
            if (meltTex != null)
                _meltingSprite = Sprite.Create(meltTex,
                    new Rect(0, 0, meltTex.width, meltTex.height), new Vector2(0.5f, 0.5f));
        }

        BuildCanvas();
        BuildBackground();

        // 배경 이미지 바로 위, 점수·그리드보다는 아래.
        if (ModeSession.IsIce)
            IceBackground.Create(_canvas.transform, _bgImage.transform.GetSiblingIndex() + 1);
        if (ModeSession.IsToggle) BuildToggleScene();
        BuildScoreArea();
        BuildBackButton();
        BuildMuteButton();
        BuildHelpButton();
        BuildGrid();
        if (ModeSession.IsToggle) BuildToggleGridBackdrop();
        BuildPieceTray();

        if (ModeSession.IsDisco)
        {
            BuildDiscoBall();
            BuildDiscoGridBackdrop();
            BuildStarLayer();
            BuildTunnelLayer();
            BuildWhiteFlashOverlay();
            _beatTracker = new GameObject("BeatTracker").AddComponent<BeatTracker>();

            // 모든 레이아웃이 끝난 뒤에 제자리를 기록해야 한다
            CollectShakeTargets();

            // 흔들림 대상을 기록한 뒤에 덮는다 — 띠는 같이 흔들리면 안 된다.
            BuildLetterbox();

            // 경고는 맨 마지막에 세워야 다른 레이어들 위로 올라간다(sibling 순서 = 그리는 순서).
            //
            // 이어받은 판이 이미 끝난 판이면 띄우지 않는다. 곧 게임오버 화면이 뜰 판인데
            // 경고까지 겹치면, 아직 아무것도 시작하지 않은 사람에게 시작 안내를 보여 주는
            // 셈이 된다. 그때는 다시하기나 부활로 "이제 시작한다"를 고른 뒤에 뜬다.
            // 곡은 이 자리에서 이미 멈춰 뒀으므로, 경고를 건너뛰어도 무음으로 남는다 —
            // 판이 끝난 뒤의 침묵은 TapeStop이 만드는 것과 같은 상태다.
            if (!RunAlreadyOver) BuildPhotoWarning();
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
        if (!pause) return;

        // 앱이 내려가는 순간 붙잡고 있던 것은 놓는다. 돌아왔을 때 손은 이미 떠났는데
        // 조각만 커서를 따라다니는 상태로 남지 않게.
        if (_dragging) FinishDrag(false);
        if (_specDrag) FinishSpecialDrag();

        _gm?.SaveGame();
    }

    // PC에서 다른 창으로 넘어가는 경우. 모바일에서는 OnApplicationPause가 같은 일을 한다.
    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        if (_dragging) FinishDrag(false);
        if (_specDrag) FinishSpecialDrag();
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
        if (ModeSession.IsToggle)
        {
            _toggleWhiteOnSprite  = LoadSpriteFromPath("Sprites/Puzzles/white_on");
            _toggleWhiteOffSprite = LoadSpriteFromPath("Sprites/Puzzles/white_off");
            _toggleBlackOnSprite  = LoadSpriteFromPath("Sprites/Puzzles/black_on");
            _toggleBlackOffSprite = LoadSpriteFromPath("Sprites/Puzzles/black_off");
            _specialBlockSprite   = LoadSpriteFromPath("Sprites/Puzzles/special_block");
            _sfxToggleSpecial     = Resources.Load<AudioClip>("Audio/SFX/toggle_special");
            _dragGuideSprite      = LoadSpriteFromPath("Sprites/UI/drag");
        }

        // 빈 셀 유리판. 디스코에서 만든 스타일인데 아이스도 같이 쓴다.
        // 안쪽 글로우와 바깥 그림자가 붙어 있어, 배경이 밝은 하늘이든 어두운 터널이든
        // 둘 중 하나는 항상 대비를 만든다 — 아이스가 겪던 문제가 정확히 그것이었다.
        if (ModeSession.IsDisco || ModeSession.IsIce)
            _sprGlassCell = MakeGlassCellSprite(110);

        // 디스코 모드
        if (ModeSession.IsDisco)
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
        scaler.matchWidthOrHeight   = 0f;   // 가로 기준 — 긴 화면에서 판이 잘리지 않게 (CanvasMetrics 참고)

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
        // 아이스는 이 한 장이 하늘이자 바다다. IceBackground 가 그 위에 구름·빙산·물결만 얹는다.
        _bgImage.color = ModeSession.IsDisco ? new Color(0.05f, 0.03f, 0.10f)
                       : ModeSession.IsIce   ? (Color)IceBackground.SKY
                       :                       BG_DARK;
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
        bool discoMode = ModeSession.IsDisco;
        hsRt.anchorMin        = new Vector2(0.5f, 1f);
        hsRt.anchorMax        = new Vector2(0.5f, 1f);
        hsRt.pivot            = new Vector2(0.5f, 1f);
        hsRt.anchoredPosition = new Vector2(0, discoMode ? -105f : -70f);
        hsRt.sizeDelta        = new Vector2(700, 70);

        // Score (크게)
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
        if (ModeSession.IsToggle)
        {
            // 게이지 컨테이너 (점수 텍스트 아래)
            var gaugeGo = new GameObject("SpecialGauge");
            gaugeGo.transform.SetParent(_canvas.transform, false);
            var gaugeRt               = gaugeGo.AddComponent<RectTransform>();
            gaugeRt.anchorMin         = new Vector2(0.5f, 1f);
            gaugeRt.anchorMax         = new Vector2(0.5f, 1f);
            gaugeRt.pivot             = new Vector2(0.5f, 1f);
            gaugeRt.anchoredPosition  = new Vector2(0, -280);
            gaugeRt.sizeDelta         = new Vector2(GameManager.TOGGLE_CLEARS_PER_SWITCH * 64 + 36, 44);

            var circleSpr = MakeRoundedSprite(44, 44, 22);
            _gaugeCircles = new Image[GameManager.TOGGLE_CLEARS_PER_SWITCH];
            for (int i = 0; i < _gaugeCircles.Length; i++)
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
                // 원들을 가운데 기준 좌우 대칭으로 배치 (간격 64)
                cRt.anchoredPosition  = new Vector2((i - (GameManager.TOGGLE_CLEARS_PER_SWITCH - 1) * 0.5f) * 64f, 0);
                cRt.sizeDelta         = new Vector2(44, 44);
                _gaugeCircles[i]      = cImg;
            }
        }

        // 디스코 모드 하트 게이지 (무지개 블럭까지 남은 줄 수)
        if (ModeSession.IsDisco)
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

            // 콤보 상태등 — 하트와 같은 높이의 오른쪽 자리. 하트(무지개 진행도)와 역할이
            // 다르므로 섞지 않고 나란히 둔다.
            var csGo = new GameObject("ComboStatus");
            csGo.transform.SetParent(_canvas.transform, false);
            _comboStatusText               = csGo.AddComponent<Text>();
            _comboStatusText.font          = Resources.Load<Font>("Fonts/SCDream8") ?? Font4();
            _comboStatusText.fontSize      = COMBO_STATUS_FONT;
            _comboStatusText.fontStyle     = FontStyle.BoldAndItalic;
            _comboStatusText.alignment     = TextAnchor.MiddleCenter;
            _comboStatusText.raycastTarget = false;
            _comboStatusText.text          = "";
            // 글자가 상자보다 커도 잘리지 않게. 이 자리는 점수와 그리드 사이 70px 띠라
            // 상자를 글자에 맞춰 키울 여유가 없다.
            _comboStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _comboStatusText.verticalOverflow   = VerticalWrapMode.Overflow;

            var csShadow            = csGo.AddComponent<Shadow>();
            csShadow.effectColor    = new Color(0f, 0f, 0f, 0.75f);
            csShadow.effectDistance = new Vector2(4f, -4f);

            // 디스코는 화이트아웃이 수시로 터진다. 그림자만으로는 그 순간 글자가 배경에
            // 녹아 사라져서 검은 외곽선을 같이 두른다. 팝업처럼 검정 판을 깔면 확실하지만
            // 여기는 하트와 같은 띠에 얹혀 있어 판이 들어갈 높이가 없다.
            var csOutline            = csGo.AddComponent<Outline>();
            csOutline.effectColor    = new Color(0f, 0f, 0f, 0.85f);
            csOutline.effectDistance = new Vector2(3f, 3f);

            var csRt              = csGo.GetComponent<RectTransform>();
            csRt.anchorMin        = new Vector2(0.5f, 1f);
            csRt.anchorMax        = new Vector2(0.5f, 1f);
            csRt.pivot            = new Vector2(0.5f, 1f);
            csRt.anchoredPosition = COMBO_STATUS_POS;
            csRt.sizeDelta        = new Vector2(360, 84);
        }
    }

    void BuildBackButton()
    {
        var obj = new GameObject("BackButton");
        obj.transform.SetParent(_canvas.transform, false);

        // 아이스는 밝은 하늘 위라 어두운 판이 겉돈다. 흰 판에 파란 화살표로 뒤집으면
        // 얼음-바다 팔레트에 맞으면서 대비도 4.9:1 로 넉넉하다.
        bool iceBack = ModeSession.IsIce;

        var img = obj.AddComponent<Image>();
        img.sprite = MakeRoundedSprite(100, 80, 20);
        img.type   = Image.Type.Sliced;
        img.color  = iceBack ? Color.white : new Color(0.25f, 0.25f, 0.35f);

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
        txt.color     = iceBack ? (Color)ICE_BACK_BLUE : Color.white;
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
        img.sprite         = LoadSpriteFromPath(bgm.IsMuted ? "Sprites/UI/mute" : "Sprites/UI/bgm_on");

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
            img.sprite = LoadSpriteFromPath(bgm.IsMuted ? "Sprites/UI/mute" : "Sprites/UI/bgm_on");
        });
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

    /// <summary>
    /// 지금 이 판이 이미 끝나 있는지. 저장된 판을 이어받았을 때만 참이 될 수 있다
    /// (새 판은 절대 시작부터 막혀 있지 않다). CheckGameOver가 게임오버 화면을 띄우는
    /// 조건과 같은 것을 본다.
    /// </summary>
    bool RunAlreadyOver => _gm != null && (_gm.ComboFailed || !_gm.HasAnyValidMove());

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
                bool tapMode        = ModeSession.IsToggle || ModeSession.IsDisco;
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
        _boardNeedsCheck = true;   // 판이 바뀌었으니 게임오버 판정을 다시 한 번
        _highScoreText.text = $"BEST  {_gm.HighScore}";
        _scoreText.text     = _gm.Score.ToString();

        // 토글 모드: 배경색 및 텍스트 색상 갱신
        if (ModeSession.IsToggle)
        {
            RefreshToggleModeBackground();
            RefreshGauge();
        }
        else if (ModeSession.IsDisco)
        {
            RefreshDiscoHearts();
        }

        RefreshTray();

        if (_gm.LastClearedRows.Count > 0 || _gm.LastClearedCols.Count > 0)
        {
            if (ModeSession.IsToggle)
                StartCoroutine(PlayToggleClearEffect());
            else
                StartCoroutine(PlayClearEffect());
        }
        else
            RefreshGrid();
    }

    // 셀 탭: 토글 모드는 스페셜 블럭 팝업, 디스코 모드는 무지개 블럭 발동
    public void OnGridCellClick(int r, int c)
    {
        if (_gm == null || _dragging || _busy) return;

        if (ModeSession.IsDisco)
        {
            // 발동하면 보드에서 즉시 사라지므로 연타해도 두 번 터지지 않는다.
            // 탭한 블럭 하나만 터진다 — 보드에 남은 다른 무지개 블럭은 그대로 둔다.
            if (_gm.Board[r, c] == GameManager.RAINBOW_BLOCK_VAL)
            {
                // 십자가 비는 순간 보드는 이미 확정이지만 폭발은 이제부터 보여 준다.
                // 그동안 판정을 미뤄서 검은 화면이 연출을 잘라먹지 않게 한다.
                _gameOverHoldUntil = Time.time + RAINBOW_BURST_SEC * 0.6f;
                StartCoroutine(PlayRainbowBurst(r, c));
            }
            return;
        }

        // 드래그가 끝나면서 따라 들어오는 탭은 무시한다.
        if (Time.time - _specEndTime < 0.25f) return;

        // 탭만으로는 아무 일도 일어나지 않는다. 끄는 조작이라는 것만 손으로 보여 준다 —
        // 이 안내가 없으면 눌러도 반응이 없어서 고장난 줄 안다.
        if (_gm.Board[r, c] != GameManager.SPECIAL_BLOCK_VAL) return;
        ShowDragGuide(r, c);
    }

    void RefreshUI()
    {
        if (_gm == null) return;
        _highScoreText.text = $"BEST  {_gm.HighScore}";
        _scoreText.text     = _gm.Score.ToString();
        if (ModeSession.IsToggle)
        {
            RefreshToggleModeBackground();
            RefreshGauge();
        }
        else if (ModeSession.IsDisco)
        {
            RefreshDiscoHearts();
        }
        RefreshGrid();
        RefreshTray();
    }

    void RefreshGrid()
    {
        bool toggleMode = ModeSession.IsToggle;
        int  activeVal  = toggleMode
            ? (_gm.ToggleCurrentColor == 0 ? GameManager.TOGGLE_WHITE_IDX : GameManager.TOGGLE_BLACK_IDX) + 1
            : -1;
        // 빈 셀도 방을 따라간다 — 흰 방이면 밝은 셀, 검은 방이면 어두운 셀.
        Color cellEmpty = (toggleMode && _gm.ToggleCurrentColor == 0) ? CELL_EMPTY_LIGHT : CELL_EMPTY_DARK;
        // 전용 배경이 뜨는 동안은 채움을 거의 투명하게 → 배경이 그리드 사이로 그대로 보임.
        // 빈 셀은 아래에서 내곽선으로 따로 그리므로 이 값은 블록이 놓인 셀의 배경에만 적용된다.
        bool sceneBg = SceneBackgroundActive;
        if (sceneBg) cellEmpty.a = 0.15f;
        if (ModeSession.IsIce) cellEmpty.a *= ICE_FILL_ALPHA;

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

                // 배경 레이어.
                //   디스코·아이스 : 빈 칸은 언제나 같은 유리판. 배경이 바뀌어도 안 흔들린다.
                //   토글          : 방 밝기에 맞춰 바뀌는 안쪽 글로우 셀.
                //   그 밖         : 도시 구간의 빈 셀만 채움 없는 내곽선으로 대체
                //                   (블록이 놓인 셀은 오버레이가 덮으므로 기존 채움 그대로).
                // 두 스프라이트 다 해당 모드에서만 구워지므로 null 검사가 곧 모드 검사다.
                Sprite bakedCell = v != 0 ? null
                                 : _sprGlassCell != null ? _sprGlassCell
                                 : _toggleCellSprite;
                if (bakedCell != null)
                {
                    _cellImages[r, c].sprite = bakedCell;
                    _cellImages[r, c].type   = Image.Type.Simple;
                    _cellImages[r, c].color  = Color.white;   // 색과 세기는 스프라이트가 들고 있다
                }
                else
                {
                    bool cityOutline = sceneBg && v == 0;
                    _cellImages[r, c].sprite = cityOutline ? _sprCellOutline : _spr110;
                    _cellImages[r, c].type   = Image.Type.Sliced;
                    _cellImages[r, c].color  = cityOutline ? CELL_OUTLINE_ICE : cellEmpty;
                }

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
                if (ModeSession.IsToggle)
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
                    // 그리드 폴백과 같은 공용 9-slice 스프라이트를 쓴다. 셀 크기마다 새로
                    // 구우면 트레이가 갱신될 때마다 Texture2D가 쌓이고 아무도 지우지 않는다.
                    img.sprite = _spr110;
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
        // 스페셜 블럭을 끄는 중에는 조각을 집을 수 없다.
        if (_dragging || _busy || _specDrag) return;

        _dragStartTime = Time.unscaledTime;

        // 조각을 집었으면 드래그 안내는 걷는다 — 판 위에 손이 하나 더 떠 있으면 헷갈린다.
        StopDragGuide();
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
        // 안드로이드 뒤로가기(= Keyboard.escapeKey). 메인 메뉴와 같은 규칙으로
        // "한 겹 벗기기"다 — 도움말이 떠 있으면 그것부터 닫고, 없을 때만 메뉴로 나간다.
        var backKb = UnityEngine.InputSystem.Keyboard.current;
        if (backKb != null && backKb.escapeKey.wasPressedThisFrame)
        {
            if (_helpOverlay != null && _helpOverlay.activeSelf)
            {
                HideHelp();
            }
            else
            {
                // 뒤로가기 버튼과 같은 동작이어야 한다. 판을 저장하지 않으면 이어 하기가 깨진다.
                _gm?.SaveGame();
                SceneManager.LoadScene("Main");
                return;
            }
        }

        AbortStuckDrag();
        CheckGameOver();

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

        if (ModeSession.IsDisco)
        {
            UpdateDiscoVisuals();
            UpdateComboStatus();   // 경고가 깜빡여야 해서 매 프레임
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

    // 손(또는 마우스 버튼)을 놓았는데 끝 이벤트가 안 들어온 드래그를 스스로 정리한다.
    //
    // PC에서는 창 밖에서 버튼을 떼면 up 이벤트가 앱에 오지 않아 OnEndDrag가 영영 안 불린다.
    // 그러면 조각이 커서를 계속 따라다니면서 놓지도 취소하지도 못하는 상태로 굳는다.
    // 모바일은 손을 떼면 터치 종료가 반드시 전달되지만, 통화·알림창·앱 전환처럼 터치가
    // 취소되는 경로가 있어서 같은 안전장치가 필요하다.
    //
    // 놓친 게 확실할 때만 건드린다 — 누른 직후 한 프레임은 EventSystem이 아직 입력을
    // 반영하기 전일 수 있어서, 그때 정리하면 집자마자 놓치는 것처럼 보인다.
    void AbortStuckDrag()
    {
        if (!_dragging && !_specDrag) return;
        if (Time.unscaledTime - _dragStartTime < DRAG_WATCHDOG_SEC) return;

        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer != null && pointer.press.isPressed) return;

        // 놓은 자리가 어딘지 알 수 없으므로 배치하지 않고 되돌린다.
        // 의도하지 않은 자리에 조각이 놓이는 것보다 트레이로 돌아가는 쪽이 낫다.
        if (_dragging) FinishDrag(false);
        if (_specDrag) FinishSpecialDrag();
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

        // 게임오버 판정은 여기서 하지 않는다 — Update의 CheckGameOver가 매 프레임 본다.
    }

    void UpdateGridPreview()
    {
        RefreshGrid();
        if (_dragIdx < 0) return;

        var piece = _gm.CurrentPieces[_dragIdx];
        var shape = PieceData.Shapes[piece.shapeIndex];
        var color = PieceData.Colors[piece.colorIndex];
        bool valid = _gm.CanPlacePiece(_dragIdx, _previewRow, _previewCol);
        bool toggleMode = ModeSession.IsToggle;

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

    // ── 게임오버 판정 ───────────────────────────────────────────
    // 매 프레임 한 곳에서만 판단한다. 상태를 바꾸는 경로가 여럿(조각 배치, 무지개 발동,
    // 색상 변환, 아이스 슬라이드)인데 각 경로가 자기 판정을 들고 있었더니, 새 경로를
    // 만들 때마다 빠뜨려서 "조각을 집었다 놓으면 그제서야 끝나는" 구멍이 계속 생겼다.
    // 여기 하나로 모으면 어떤 경로로 보드가 바뀌든 다음 프레임에 잡힌다.
    void CheckGameOver()
    {
        if (_gm == null || _gameOverOverlay != null) return;

        // 아직 보드가 확정이 아닌 순간들 — 조작 중, 아이스 슬라이드 체인 중, 무지개 발동 중.
        // 여기서 걸리면 보드가 곧 또 바뀐다는 뜻이라 재판정 표시를 세워 둔다.
        // 아이스 슬라이드(SlideDown·CheckAndClearLinesQuiet)는 일부러 OnStateChanged를
        // 거치지 않으므로, 이 표시가 없으면 체인이 끝난 뒤 아무도 다시 판정하지 않는다.
        if (_dragging || _busy || _rainbowActivating) { _boardNeedsCheck = true; return; }
        if (Time.time < _gameOverHoldUntil) return;

        // 아이스는 클리어 직후 슬라이드가 이어진다. 그 코루틴이 _busy를 세우기까지
        // 클리어 연출(약 0.3초)만큼 틈이 있어, 그 사이에 판단하면 더 내려올 블록을
        // 못 본 채 끝내 버린다. 지운 줄 목록이 비었는지로 그 틈을 가려낸다.
        if (ModeSession.IsIce &&
            (_gm.LastClearedRows.Count > 0 || _gm.LastClearedCols.Count > 0))
        { _boardNeedsCheck = true; return; }

        // 보드가 그대로면 결과도 그대로다. HasAnyValidMove는 조각 3개 × 64칸을 전부
        // 훑는 완전 탐색이라 매 프레임 돌릴 이유가 없다 — 바뀐 뒤 한 번만 본다.
        if (!_boardNeedsCheck) return;
        _boardNeedsCheck = false;

        if (_gm.ComboFailed || !_gm.HasAnyValidMove())
            ShowGameOver();
    }

    void ShowGameOver()
    {
        // 디스코는 연출이 곡에 얹혀 있어서, 게임이 끝났는데 음악만 계속 돌면 붕 뜬다.
        // 테이프가 멎듯 음을 끌어내리고 화면을 완전한 검정으로 덮어 한 곡을 닫는다.
        bool discoMode = ModeSession.IsDisco;

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

        AddText(overlayGo.transform, loc.Get(discoMode ? "game_over_disco" : "game_over"), 95,
            Color.white, new Vector2(0, 200), new Vector2(900, 130));

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
            reviveImg.sprite = BtnBorderSprite;
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

                // 광고가 뜨는 동안 연타되는 것을 막는다.
                reviveBtn.interactable = false;
                var reviveLabel = reviveGo.GetComponentInChildren<Text>();
                string reviveWas = reviveLabel != null ? reviveLabel.text : null;

                AdManager.GetOrCreate().ShowRewarded(outcome =>
                {
                    // 광고를 못 튼 것은 우리 사정이다. 점수가 걸린 판을 광고 사정으로
                    // 뺏으면 화가 나는 게 당연하다 — 그럴 때는 그냥 부활시켜 준다.
                    //
                    // 네트워크를 끊어 광고를 회피하는 꼼수를 막지 않는 이유: 부활은
                    // _reviveUsed 로 한 판에 한 번뿐이라, 회피해도 정직하게 본 사람과
                    // 부활 횟수가 같다. 아낀 것은 광고 시청 시간뿐이고 점수 이득은 없다.
                    if (outcome == AdManager.RewardOutcome.Skipped)
                    {
                        // 스스로 닫았다. 판을 이어 주지 않고 버튼만 되살린다.
                        if (reviveBtn != null) reviveBtn.interactable = true;
                        return;
                    }

                    if (outcome == AdManager.RewardOutcome.Unavailable)
                        Debug.Log("[InGameUI] 광고를 띄우지 못해 부활만 지급한다.");

                    _reviveUsed = true;

                    // 부활은 같은 판을 이어가는 것이므로 곡도 끊긴 자리에서 이어 붙인다.
                    // 다만 바로 켜지 않는다 — 광과민성 경고가 걷힌 뒤에 PhotoWarningRoutine이
                    // 이어 붙인다. TapeStop이 이미 멈춰 놨지만, 감속이 끝나기 전에 눌렀으면
                    // 아직 돌고 있을 수 있어서 여기서 확실히 멈춘다.
                    if (discoMode)
                    {
                        BGMManager.Instance?.RestorePlayback();
                        BGMManager.Instance?.Pause();
                    }

                    _gameOverOverlay = null;
                    Destroy(overlayGo);
                    _gm.Revive();

                    // 번쩍이는 화면으로 곧장 돌아가지 않게 경고를 한 번 더 보여 준다.
                    if (discoMode) BuildPhotoWarning();
                });
            });
        }

        // ── 다시 시작 버튼 ────────────────────────────────────────
        var btnGo = new GameObject("RestartBtn");
        btnGo.transform.SetParent(overlayGo.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.sprite = BtnBorderSprite;
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
            // 광고를 산 사람은 곧장 새 판으로. 아니면 제거를 한 번 권하고,
            // 거절하면 전면 광고를 본 뒤에 시작한다.
            if (RemoveAds.Owned) { RestartRun(discoMode); return; }

            ShowRemoveAdsOffer(
                onPurchased: () => RestartRun(discoMode),
                onDeclined:  () =>
                {
                    // 광고 소리가 게임 음악 위에 겹치지 않게 잠시 멈춘다.
                    // 디스코는 게임오버에서 TapeStop이 이미 멎게 해 뒀다.
                    if (!discoMode) BGMManager.Instance?.Pause();
                    AdManager.GetOrCreate().ShowInterstitial(() => RestartRun(discoMode));
                });
        });
        ColorUtility.TryParseHtmlString("#e2e8f0", out Color restartTextColor);
        AddText(btnGo.transform, loc.Get("restart"), 55, restartTextColor,
            Vector2.zero, Vector2.zero, fullStretch: true);
    }

    /// <summary>게임오버 화면에서 새 판으로 넘어가는 실제 동작.

    /// <summary>
    /// 새 판을 깔기 전에 떠 있는 연출을 걷어낸다.
    ///
    /// 각 연출은 자기 코루틴이 끝나면서 스스로 치우지만, 게임오버가 그 중간에 끼면
    /// 다음 판이 시작된 뒤에도 잔상이 남아 있는 구간이 생긴다. 이전 판의 흔적이
    /// 0점짜리 새 판 위에 떠 있으면 방금 뭔가 터진 것처럼 읽힌다.
    ///
    /// 이름으로 찾는 이유: 연출마다 부모가 달라 한 곳에 모여 있지 않다. 여기 목록을
    /// 늘리는 것보다 새 연출을 만들 때 이 이름 규칙을 따르는 편이 낫다.
    /// </summary>
    void ClearTransientEffects()
    {
        if (_canvas == null) return;

        // 연출 루트 이름들. 코루틴이 Destroy 하는 것과 같은 오브젝트다.
        string[] fxRoots =
        {
            "RainbowBurstFX", "ClearNoteFX", "ToggleClearFX",
            "ComboPopup", "MeltTrail", "SlideGhost",
        };

        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i);
            foreach (var name in fxRoots)
            {
                if (child.name != name) continue;
                Destroy(child.gameObject);
                break;
            }
        }

        // 빗방울은 파괴 대상이 아니라 재사용되는 레이어다. 곡이 0초로 돌아가면 다음
        // 프레임에 알아서 꺼지지만, 그 한 프레임이 눈에 남으므로 여기서 먼저 끈다.
        if (_rainLayerRt != null) _rainLayerRt.gameObject.SetActive(false);
    }
    /// 광고 제거 제안과 전면 광고가 앞에 끼면서, 이 부분만 따로 불릴 수 있어야 했다.
    /// 치울 화면은 인자로 받지 않고 _gameOverOverlay에서 찾는다 — 중간에 제안 화면으로
    /// 바뀌어 있을 수 있어서, 넘겨받은 참조는 이미 파괴된 것일 수 있다.</summary>
    void RestartRun(bool discoMode)
    {
        _reviveUsed = false;
        ClearTransientEffects();   // 이전 판의 잔상이 새 판 위에 남지 않게

        // 디스코는 곡을 처음부터 다시 튼다. 새 판이니 연출도 도입부부터 시작해야 한다.
        // 첫 실행과 같은 순서다 — 되감아 두고 멈춘 뒤, 경고가 걷힐 때 곡이 시작된다.
        // 그래야 플레이어가 보지도 못한 도입부가 경고 뒤에서 지나가 버리지 않는다.
        // (Seek이 Play를 부르므로 되감기 다음에 멈춰야 한다)
        if (discoMode)
        {
            BGMManager.Instance?.RestorePlayback();
            BGMManager.Instance?.Seek(0.0);
            BGMManager.Instance?.Pause();
        }
        else
        {
            // 전면 광고 때문에 멈춰 놨을 수 있다. 멈춘 적이 없으면 아무 일도 안 한다.
            BGMManager.Instance?.Resume();
        }

        if (_gameOverOverlay != null) Destroy(_gameOverOverlay);
        _gameOverOverlay = null;
        _gm.ResetGame();

        // 새 판도 첫 실행처럼 경고부터 보여 주고 시작한다.
        if (discoMode) BuildPhotoWarning();
    }

    // ── 다시 시작 전 광고 제거 제안 ──────────────────────────────
    // 게임오버 화면을 치우고 그 자리를 이어받는다. 반투명 두 장이 겹치면 뒤엣것이 비쳐
    // 지저분하고, 이 시점에 점수판은 이미 볼 만큼 봤다.
    //
    // _gameOverOverlay 자리를 물려받는 게 중요하다. 그냥 지우기만 하면 판이 여전히 죽은
    // 상태라 CheckGameOver가 다음 프레임에 게임오버 화면을 다시 띄운다.
    // 구매가 실패해도 되돌아갈 곳은 필요 없다 — 실패 문구가 이 화면에 뜨고 버튼이 다시
    // 살아나므로, 여기서 광고 보기를 고르면 된다.
    void ShowRemoveAdsOffer(System.Action onPurchased, System.Action onDeclined)
    {
        var loc = LocalizationManager.Instance;

        if (_gameOverOverlay != null) Destroy(_gameOverOverlay);

        var go = new GameObject("RemoveAdsOffer");
        go.transform.SetParent(_canvas.transform, false);
        go.transform.SetAsLastSibling();
        _gameOverOverlay = go;
        var dim       = go.AddComponent<Image>();
        dim.color     = new Color(0.03f, 0.03f, 0.07f, 1f);   // 불투명 — 뒤가 비치지 않게
        var goRt       = go.GetComponent<RectTransform>();
        goRt.anchorMin = Vector2.zero;
        goRt.anchorMax = Vector2.one;
        goRt.offsetMin = goRt.offsetMax = Vector2.zero;

        // 아이콘·제목·설명·버튼 두 개를 한 덩어리로 보고 화면 한가운데에 맞춘다.
        // 덩어리는 아이콘 위쪽(+410)에서 거절 버튼 아래쪽(-415)까지라 중심이 0에 온다.
        // 실패 문구(-490)는 평소 비어 있어서 덩어리 밖에 두고 아래에 매단다.
        // no_ad.png는 caution.png와 같은 흰 실루엣 + 투명 배경이라 어두운 판 위에서 그대로 읽힌다.
        var iconSprite = LoadSpriteFromPath("Sprites/Logo/no_ad");
        if (iconSprite != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var iconImg            = iconGo.AddComponent<Image>();
            iconImg.sprite         = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget  = false;
            var iconRt              = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin        = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot            = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(0, 280);
            iconRt.sizeDelta        = new Vector2(260, 260);
        }

        AddText(go.transform, loc.Get("iap_title"), 70, Color.white,
            new Vector2(0, 60), new Vector2(900, 110));
        AddText(go.transform, loc.Get("iap_desc"), 38, new Color(0.78f, 0.78f, 0.86f),
            new Vector2(0, -50), new Vector2(900, 90));

        // 실패 문구는 자리만 잡아 두고 비워 둔다. 결제가 안 되면 여기에 뜬다.
        var failGo = new GameObject("FailNote");
        failGo.transform.SetParent(go.transform, false);
        var failText           = failGo.AddComponent<Text>();
        failText.font          = Font4();
        failText.fontSize      = 34;
        failText.alignment     = TextAnchor.MiddleCenter;
        failText.color         = new Color(1f, 0.45f, 0.45f);
        failText.raycastTarget = false;
        failText.text          = "";
        var failRt             = failGo.GetComponent<RectTransform>();
        failRt.anchorMin       = failRt.anchorMax = new Vector2(0.5f, 0.5f);
        failRt.pivot           = new Vector2(0.5f, 0.5f);
        failRt.anchoredPosition = new Vector2(0, -490);
        failRt.sizeDelta       = new Vector2(900, 60);

        // 구매 버튼. 결제가 아직 안 붙어 있으면 잠그고 이유를 버튼에 적는다.
        bool storeReady = RemoveAds.StoreReady;

        var buyGo = new GameObject("BuyBtn");
        buyGo.transform.SetParent(go.transform, false);
        var buyImg    = buyGo.AddComponent<Image>();
        buyImg.sprite = BtnBorderSprite;
        buyImg.type   = Image.Type.Sliced;
        buyImg.color  = storeReady ? new Color(1.00f, 0.82f, 0.25f)
                                   : new Color(0.42f, 0.42f, 0.48f);
        var buyBtn    = buyGo.AddComponent<Button>();
        buyBtn.interactable = storeReady;
        var buyRt              = buyGo.GetComponent<RectTransform>();
        buyRt.anchorMin        = buyRt.anchorMax = new Vector2(0.5f, 0.5f);
        buyRt.pivot            = new Vector2(0.5f, 0.5f);
        buyRt.anchoredPosition = new Vector2(0, -180);
        buyRt.sizeDelta        = new Vector2(620, 130);
        AddText(buyGo.transform, loc.Get(storeReady ? "iap_buy" : "iap_later"),
            storeReady ? 55 : 46,
            storeReady ? new Color(1.00f, 0.82f, 0.25f) : new Color(0.55f, 0.55f, 0.62f),
            Vector2.zero, Vector2.zero, fullStretch: true);

        // 거절 버튼
        var watchGo = new GameObject("WatchAdBtn");
        watchGo.transform.SetParent(go.transform, false);
        var watchImg    = watchGo.AddComponent<Image>();
        watchImg.sprite = BtnBorderSprite;
        watchImg.type   = Image.Type.Sliced;
        watchImg.color  = new Color(0.62f, 0.62f, 0.70f);
        var watchBtn    = watchGo.AddComponent<Button>();
        var watchRt              = watchGo.GetComponent<RectTransform>();
        watchRt.anchorMin        = watchRt.anchorMax = new Vector2(0.5f, 0.5f);
        watchRt.pivot            = new Vector2(0.5f, 0.5f);
        watchRt.anchoredPosition = new Vector2(0, -350);
        watchRt.sizeDelta        = new Vector2(620, 130);
        AddText(watchGo.transform, loc.Get("iap_watch"), 48, new Color(0.80f, 0.80f, 0.88f),
            Vector2.zero, Vector2.zero, fullStretch: true);

        buyBtn.onClick.AddListener(() =>
        {
            // 결제 창이 떠 있는 동안 연타로 두 번 사지 않게 잠근다.
            buyBtn.interactable   = false;
            watchBtn.interactable = false;
            RemoveAds.Purchase(
                onSuccess: () => onPurchased?.Invoke(),
                onFailed:  () =>
                {
                    buyBtn.interactable   = true;
                    watchBtn.interactable = true;
                    failText.text         = loc.Get("iap_failed");
                });
        });

        watchBtn.onClick.AddListener(() => onDeclined?.Invoke());
    }

    /// <summary>
    /// 버튼 글자를 잠깐 다른 문구로 바꿨다가 되돌린다. 광고가 아직 안 실려서 눌러도
    /// 아무 일이 없는 순간에, 왜 안 되는지 그 자리에서 알려 주려는 것이다.
    /// </summary>
    IEnumerator FlashLabel(Text label, string message, string restore)
    {
        if (label == null) yield break;
        label.text = message;
        yield return new WaitForSeconds(1.6f);
        if (label != null) label.text = restore;
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

    /// <summary>
    /// 디스코 빈 셀용 유리 타일. cell.png 를 그대로 옮긴 프로파일이다.
    ///
    /// 셀 사각형(size)보다 GLASS_INSET 만큼 작은 타일을 그리고, 그렇게 남은 테두리 공간을
    /// 그림자가 쓴다. 셀 rect 자체는 그대로라 탭 판정에는 영향이 없다.
    /// 안팎이 붙어 있어 배경이 어둡든 밝든 둘 중 하나는 항상 대비를 만든다.
    /// </summary>
    Sprite MakeGlassCellSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];

        float half  = size * 0.5f;
        float inner = half - GLASS_INSET - GLASS_CORNER;  // 모서리 원의 중심이 놓이는 사각형의 반너비

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 타일 경계까지의 거리. 안쪽이 양수, 바깥(그림자 영역)이 음수다.
                float ax = Mathf.Abs(x + 0.5f - half) - inner;
                float ay = Mathf.Abs(y + 0.5f - half) - inner;
                float ox = Mathf.Max(ax, 0f);
                float oy = Mathf.Max(ay, 0f);
                float dist = GLASS_CORNER - Mathf.Sqrt(ox * ox + oy * oy);
                if (ax < 0f && ay < 0f) dist = GLASS_CORNER - Mathf.Max(ax, ay);

                if (dist <= 0f)
                {
                    // 바깥 — 경계에서 가장 진하고 멀어질수록 사라진다.
                    // 스프라이트 끝에서 딱 잘리면 네모난 자국이 남으므로 마지막 3px 을 마저 깎는다.
                    float d  = -dist;
                    float sh = GLASS_SHADOW_ALPHA
                             * Mathf.Exp(-(d / GLASS_SHADOW_PX) * (d / GLASS_SHADOW_PX))
                             * Mathf.Clamp01((GLASS_INSET - d) / 2f);
                    px[y * size + x] = new Color32(GLASS_SHADOW.r, GLASS_SHADOW.g, GLASS_SHADOW.b,
                                                   (byte)(Mathf.Clamp01(sh) * 255f));
                    continue;
                }

                // 안쪽 — 막 위에 넓은 번짐과 얇은 가장자리 선을 얹는다.
                float rim  = GLASS_RIM_ALPHA  * Mathf.Exp(-(dist / GLASS_RIM_PX)  * (dist / GLASS_RIM_PX));
                float edge = GLASS_EDGE_ALPHA * Mathf.Exp(-(dist / GLASS_EDGE_PX) * (dist / GLASS_EDGE_PX));
                float a    = GLASS_BODY_ALPHA + rim + edge;
                a *= Mathf.Clamp01(dist);   // 타일 경계 1px 안티에일리어싱

                px[y * size + x] = new Color32(GLASS_TINT.r, GLASS_TINT.g, GLASS_TINT.b,
                                               (byte)(Mathf.Clamp01(a) * 255f));
            }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
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
// 셀 하나가 탭과 드래그를 둘 다 받는다. 드래그는 토글 모드 스페셜 블럭 전용이고,
// 시작 칸에 스페셜 블럭이 없으면 InGameUI 쪽에서 그냥 무시된다.
// IBeginDragHandler를 쓰므로 손가락이 문턱을 넘어야 드래그로 갈린다 — 그 덕에 탭과 안 겹친다.
public class GridCellClickHandler : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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

    public void OnBeginDrag(PointerEventData e) => _ui.BeginSpecialDrag(_row, _col, e.position);
    public void OnDrag(PointerEventData e)      => _ui.UpdateSpecialDrag(e.position);
    public void OnEndDrag(PointerEventData e)   => _ui.EndSpecialDrag(e.position);
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
