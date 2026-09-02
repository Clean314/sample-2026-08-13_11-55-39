using System;

/// <summary>
/// "모든 모드 해금" 상품.
///
/// 원래 각 모드는 앞 모드에서 일정 점수를 넘겨야 열린다. 이 상품은 그 조건을 건너뛴다 —
/// 실력이 아니라 시간을 사는 것에 가깝다.
///
/// 잠긴 모드에서 시작 버튼이 흐릿하게 죽어 있던 자리를 이 구매 버튼이 대신한다.
/// 못 하는 이유만 보여 주는 것보다, 지금 할 수 있는 선택을 주는 편이 낫다.
///
/// 결제 배관은 Store 가 맡는다.
/// </summary>
public static class UnlockAll
{
    /// <summary>Play Console 과 App Store Connect 에 이 ID 로 등록해야 한다(비소모성).</summary>
    public const string PRODUCT_ID = "unlock_all_modes";

    /// <summary>샀는지. 모드 잠금 판정이 이 값을 먼저 본다.</summary>
    public static bool Owned => Store.IsOwned(PRODUCT_ID);

    /// <summary>이 상품을 실제로 팔 수 있는지. false 면 화면이 구매 버튼을 잠근다.</summary>
    public static bool StoreReady => Store.HasProduct(PRODUCT_ID);

    public static void Purchase(Action onSuccess, Action onFailed = null)
        => Store.Purchase(PRODUCT_ID, onSuccess, onFailed);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>구매 상태를 되돌린다(잠금 화면을 다시 보려고).</summary>
    public static void DebugRevoke() => Store.DebugRevoke(PRODUCT_ID);
#endif
}
