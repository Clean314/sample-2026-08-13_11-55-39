using System;
using UnityEngine;

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Purchasing;
#endif

/// <summary>
/// 인앱 결제 배관. 상품이 둘 이상이 되면서 따로 뽑았다.
///
/// StoreController 는 앱에 하나만 있어야 한다 — 상품마다 만들면 연결이 겹쳐 어느 쪽 콜백이
/// 오는지 알 수 없게 된다. 그래서 연결·조회·구매·복원을 여기서 전부 맡고, 상품별 클래스
/// (RemoveAds, UnlockAll)는 ID 만 들고 이 앞을 지나간다.
///
/// 소유 여부는 PlayerPrefs 에 둔다. 스토어에 못 붙는 상황(비행기 모드 등)에서도 이미 산
/// 사람이 산 대로 쓸 수 있어야 하기 때문이다. 진짜 원본은 스토어에 있고, 앱을 켤 때마다
/// 복원으로 맞춘다.
/// </summary>
public static class Store
{
    /// <summary>상품 권한이 생겼을 때. 배너를 걷는 등 상품별 뒤처리를 여기에 건다.</summary>
    public static event Action<string> OnGranted;

    static string PrefKey(string productId) => $"iap_{productId}_owned";

    /// <summary>이미 가진 상품인지.</summary>
    public static bool IsOwned(string productId) => PlayerPrefs.GetInt(PrefKey(productId), 0) == 1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// 구매 상태를 되돌린다. 한 번 사고 나면 제안 화면과 잠금 화면을 다시 볼 수 없어
    /// 시험이 막히기 때문이다.
    ///
    /// 기기에서는 스토어의 소유 기록까지 지우지는 못한다 — 앱을 다시 켜면 복원으로
    /// 되살아난다. 그 자리에서 화면만 되돌려 보는 용도다.
    /// </summary>
    public static void DebugRevoke(string productId)
    {
        PlayerPrefs.DeleteKey(PrefKey(productId));
        PlayerPrefs.Save();
    }
#endif

    /// <summary>권한을 준다. 이미 갖고 있으면 아무 일도 하지 않는다.</summary>
    public static void Grant(string productId)
    {
        if (IsOwned(productId)) return;

        PlayerPrefs.SetInt(PrefKey(productId), 1);
        PlayerPrefs.Save();
        OnGranted?.Invoke(productId);
    }

#if UNITY_EDITOR

    /// <summary>에디터에서는 결제 창 없이 성공 경로까지 시험할 수 있게 열어 둔다.</summary>
    public static bool HasProduct(string productId) => true;

    public static void Purchase(string productId, Action onSuccess, Action onFailed = null)
    {
        Debug.Log($"[Store] 에디터: {productId} 구매 성공으로 간주.");
        Grant(productId);
        onSuccess?.Invoke();
    }

#elif UNITY_ANDROID || UNITY_IOS

    static StoreController _store;
    static readonly Dictionary<string, Product> _products = new Dictionary<string, Product>();

    // 결제는 화면을 한 번 떠났다 돌아오는 흐름이라 콜백을 들고 있어야 한다.
    // 결제 창은 한 번에 하나만 뜨므로 한 쌍이면 충분하다.
    static string _pendingId;
    static Action _onSuccess;
    static Action _onFailed;

    /// <summary>
    /// 그 상품을 실제로 팔 수 있는지. false 면 화면이 구매 버튼을 잠근다 —
    /// 눌러도 실패만 하는 버튼을 살려 두면 고장난 것으로 읽힌다.
    /// </summary>
    public static bool HasProduct(string productId) => _products.ContainsKey(productId);

    /// <summary>
    /// 씬과 무관하게 앱을 켤 때 한 번 붙는다. 메뉴를 거치지 않고 인게임에서 시작해도
    /// 소유 여부를 알아야 배너를 띄울지 정할 수 있다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static async void Connect()
    {
        try
        {
            _store = UnityIAPServices.StoreController();

            _store.OnProductsFetched     += OnProductsFetched;
            _store.OnProductsFetchFailed += e => Debug.LogWarning($"[Store] 상품 조회 실패: {e}");

            _store.OnPurchasePending   += OnPurchasePending;
            _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _store.OnPurchaseFailed    += OnPurchaseFailed;

            // 재설치·기기 변경 때 이미 산 사람이 다시 사지 않게 한다.
            _store.OnPurchasesFetched     += OnPurchasesFetched;
            _store.OnPurchasesFetchFailed += e => Debug.LogWarning($"[Store] 구매 내역 조회 실패: {e}");

            await _store.Connect();

            _store.FetchProducts(new List<ProductDefinition>
            {
                new ProductDefinition(RemoveAds.PRODUCT_ID,  ProductType.NonConsumable),
                new ProductDefinition(UnlockAll.PRODUCT_ID,  ProductType.NonConsumable),
            });
        }
        catch (Exception e)
        {
            // 스토어에 못 붙어도 게임은 그대로 돌아가야 한다. 구매 버튼이 잠길 뿐이다.
            Debug.LogWarning($"[Store] 연결 실패: {e.Message}");
        }
    }

    static void OnProductsFetched(List<Product> products)
    {
        foreach (var p in products)
        {
            _products[p.definition.id] = p;
            Debug.Log($"[Store] 상품 확인: {p.definition.id}");
        }

        // 상품을 알아야 소유 여부도 물어볼 수 있다.
        _store.FetchPurchases();
    }

    /// <summary>이미 가진 것은 조용히 되살린다. 재설치했다고 다시 사게 두면 안 된다.</summary>
    static void OnPurchasesFetched(Orders orders)
    {
        foreach (var id in orders.ConfirmedOrders.SelectMany(ProductIds).Distinct())
        {
            if (IsOwned(id)) continue;
            Debug.Log($"[Store] 이전 구매를 복원한다: {id}");
            Grant(id);
        }
    }

    static IEnumerable<string> ProductIds(Order order) =>
        order.CartOrdered.Items().Select(i => i.Product.definition.id);

    /// <summary>
    /// 결제가 승인됐지만 아직 확정 전이다. 여기서 권한을 먼저 주고 확정한다 —
    /// 확정을 먼저 하면 그 사이에 앱이 죽었을 때 돈만 나가고 권한이 없는 상태가 된다.
    /// </summary>
    static void OnPurchasePending(PendingOrder order)
    {
        foreach (var id in ProductIds(order)) Grant(id);
        _store.ConfirmPurchase(order);
    }

    static void OnPurchaseConfirmed(Order order)
    {
        bool mine = _pendingId != null && ProductIds(order).Contains(_pendingId);

        switch (order)
        {
            case ConfirmedOrder:
                Debug.Log("[Store] 구매 확정.");
                if (mine) Finish(true);
                break;

            case FailedOrder failed:
                Debug.LogWarning($"[Store] 구매 확정 실패: {failed.FailureReason} {failed.Details}");
                if (mine) Finish(false);
                break;
        }
    }

    static void OnPurchaseFailed(FailedOrder order)
    {
        // 사용자가 결제 창을 닫은 경우도 여기로 온다. 실패로 알리되 요란하게 굴지 않는다.
        Debug.Log($"[Store] 구매 실패: {order.FailureReason}");
        Finish(false);
    }

    static void Finish(bool success)
    {
        var ok   = _onSuccess;
        var fail = _onFailed;
        _pendingId = null;
        _onSuccess = null;
        _onFailed  = null;

        if (success) ok?.Invoke();
        else         fail?.Invoke();
    }

    /// <summary>구매를 시도한다. 실패하면 반드시 onFailed 로 돌아온다.</summary>
    public static void Purchase(string productId, Action onSuccess, Action onFailed = null)
    {
        if (IsOwned(productId))
        {
            onSuccess?.Invoke();
            return;
        }

        if (_store == null || !_products.TryGetValue(productId, out var product))
        {
            Debug.LogWarning($"[Store] {productId} 를 아직 팔 수 없다.");
            onFailed?.Invoke();
            return;
        }

        _pendingId = productId;
        _onSuccess = onSuccess;
        _onFailed  = onFailed;
        _store.PurchaseProduct(product);
    }

#else

    // 결제를 지원하지 않는 플랫폼. 구매 버튼은 잠긴 채로 둔다.
    public static bool HasProduct(string productId) => false;

    public static void Purchase(string productId, Action onSuccess, Action onFailed = null)
    {
        Debug.LogWarning("[Store] 이 플랫폼에는 결제가 없다.");
        onFailed?.Invoke();
    }

#endif
}
