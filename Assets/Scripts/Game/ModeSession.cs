/// <summary>
/// 씬 간 선택된 모드 인덱스를 전달하는 static 컨테이너.
/// 이 번호는 ModeConfig.Modes 배열의 순서이자 PlayerPrefs 키 접두사("m{n}_")다 —
/// 값을 바꾸면 저장된 최고 점수와 이어하기가 다른 모드 것으로 읽히므로 건드리지 않는다.
/// </summary>
public static class ModeSession
{
    public const int NORMAL = 0;
    public const int ICE    = 1;
    public const int TOGGLE = 2;
    public const int DISCO  = 3;

    public static int SelectedMode { get; set; } = NORMAL;

    // 모드 분기가 코드 곳곳에 흩어져 있다. 번호를 직접 비교하는 대신 여기에 물으면,
    // 모드가 늘거나 순서가 바뀌어도 고칠 곳이 이 파일 하나로 남는다.
    public static bool IsNormal => SelectedMode == NORMAL;
    public static bool IsIce    => SelectedMode == ICE;
    public static bool IsToggle => SelectedMode == TOGGLE;
    public static bool IsDisco  => SelectedMode == DISCO;
}
