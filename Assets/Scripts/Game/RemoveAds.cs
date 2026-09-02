using System;
using UnityEngine;

/// <summary>
/// "광고 제거" 상품.
///
/// 무엇을 없애는가 — 배너와 전면 광고. 보상형(부활)은 그대로 둔다. 보상형은 플레이어가
/// 이득을 보려고 스스로 고르는 것이라, 없애면 오히려 살 수 있는 수단을 뺏는 셈이 된다.
///
/// 결제 배관은 Store 가 맡는다. 여기는 ID 와 이 상품만의 뒤처리(배너 걷기)만 들고 있다.
/// </summary>
public static class RemoveAds
{
    /// <summary>
    /// Play Console 과 App Store Connect 에 이 ID 로 등록해야 한다(비소모성).
    /// 양쪽 ID 가 다르면 스토어별 매핑이 따로 필요해지므로 같게 맞춘다.
    /// </summary>
    public const string PRODUCT_ID = "remove_ads";

    /// <summary>이미 구매했는지. 배너·전면 광고를 띄우는 쪽에서 먼저 확인한다.</summary>
    public static bool Owned => Store.IsOwned(PRODUCT_ID);

    /// <summary>이 상품을 실제로 팔 수 있는지. false 면 화면이 구매 버튼을 잠근다.</summary>
    public static bool StoreReady => Store.HasProduct(PRODUCT_ID);

    public static void Purchase(Action onSuccess, Action onFailed = null)
        => Store.Purchase(PRODUCT_ID, onSuccess, onFailed);

    /// <summary>
    /// 구매가 확정되면 배너를 그 자리에서 걷는다. 복원으로 권한이 생긴 경우에도 같은
    /// 자리를 지나가므로, 앱을 다시 켰을 때 배너가 잠깐 떴다 사라지는 일이 없다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        Store.OnGranted += id =>
        {
            if (id == PRODUCT_ID) AdManager.Instance?.DestroyBanner();
        };
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>구매 상태를 되돌린다(제안 팝업을 다시 보려고).</summary>
    public static void DebugRevoke() => Store.DebugRevoke(PRODUCT_ID);
#endif
}
