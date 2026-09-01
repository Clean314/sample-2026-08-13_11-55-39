using UnityEngine;

/// <summary>
/// 캔버스 좌표를 계산할 때 쓰는 값들.
///
/// CanvasScaler 를 가로 기준(matchWidthOrHeight = 0)으로 두었다. 그래서 가로는 어떤 기기에서든
/// 항상 REF_W 단위이고, 대신 세로가 화면 비율을 따라 늘어난다 — 16:9 면 1920, 요즘 흔한
/// 20:9 짜리 폰이면 2400 쯤 된다.
///
/// 세로 기준으로 맞추면 반대가 된다. 세로가 1920 으로 고정되는 대신 긴 화면에서 가로가
/// 1080 보다 좁아진다. 1080 폭으로 그려 둔 게임판과 버튼이 그만큼 화면 밖으로 잘려 나간다.
/// 세로로 하는 게임이라 잘리면 안 되는 쪽은 가로다.
///
/// 그래서 "화면 위끝"은 더 이상 960 이 아니라 HalfHeight 이고, 세로 전체를 훑는 계산은
/// 1920 이 아니라 Height 를 쓴다.
///
/// 주의: 16:9 보다 짧고 넓은 화면(4:3 태블릿 등)에서는 반대로 세로가 1920 보다 좁아져
/// 위아래가 잘린다. 지금 나오는 폰은 전부 16:9 보다 기니 문제되지 않지만, 태블릿을
/// 지원하게 되면 그때는 화면 끝에 붙는 요소들을 앵커로 다시 잡아야 한다.
/// </summary>
public static class CanvasMetrics
{
    /// <summary>CanvasScaler.referenceResolution 의 가로. 가로 기준이라 실제 캔버스 폭과 같다.</summary>
    public const float REF_W = 1080f;

    /// <summary>설계 기준 세로(16:9). 화면이 이보다 길면 Height 가 더 크다.</summary>
    public const float REF_H = 1920f;

    /// <summary>지금 화면에서 캔버스 세로가 몇 단위인지.</summary>
    public static float Height =>
        Screen.width > 0 ? REF_W * Screen.height / Screen.width : REF_H;

    /// <summary>화면 위끝의 y. 아래끝은 -HalfHeight 다.</summary>
    public static float HalfHeight => Height * 0.5f;
}
