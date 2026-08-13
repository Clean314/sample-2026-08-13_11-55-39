using System;
using UnityEngine;

#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Api;
#endif

/// <summary>
/// Google AdMob 배너 / 보상형 광고 관리 싱글턴.
/// 씬에 없으면 자동 생성됩니다 (GetOrCreate 패턴).
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    // ── 광고 단위 ID ──────────────────────────────────────────────
    // ※ 현재는 Google 공식 테스트 ID 사용.
    //   출시 전에 AdMob 콘솔에서 발급받은 실제 ID로 교체하세요.
#if UNITY_ANDROID
    const string APP_ID      = "ca-app-pub-3940256099942544~3347511713"; // 테스트 앱 ID
    const string BANNER_ID   = "ca-app-pub-3940256099942544/9214589741"; // 적응형 배너 테스트 ID
    const string REWARDED_ID = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IOS
    const string APP_ID      = "ca-app-pub-3940256099942544~1458002511";
    const string BANNER_ID   = "ca-app-pub-3940256099942544/2435281174"; // 적응형 배너 테스트 ID
    const string REWARDED_ID = "ca-app-pub-3940256099942544/1712485313";
#else
    const string APP_ID      = "";
    const string BANNER_ID   = "";
    const string REWARDED_ID = "";
#endif

#if UNITY_ANDROID || UNITY_IOS
    BannerView  _banner;
    RewardedAd  _rewardedAd;
    bool        _rewardedReady;
#endif

    // ── 생성 ─────────────────────────────────────────────────────
    public static AdManager GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("AdManager");
        return go.AddComponent<AdManager>();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID || UNITY_IOS
        MobileAds.Initialize(_ =>
        {
            Debug.Log("[AdManager] MobileAds initialized.");
            LoadRewardedAd();
        });
#endif
    }

    // ═══════════════════════════════════════════════════════════
    // 배너 광고
    // ═══════════════════════════════════════════════════════════

    /// <summary>하단에 배너를 표시합니다. 이미 표시 중이면 무시합니다.</summary>
    public void ShowBanner()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (_banner != null) { _banner.Show(); return; }

        AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        _banner = new BannerView(BANNER_ID, adaptiveSize, AdPosition.Bottom);
        _banner.LoadAd(new AdRequest());
        _banner.OnBannerAdLoaded       += () => Debug.Log("[AdManager] Banner loaded.");
        _banner.OnBannerAdLoadFailed   += e  => Debug.LogWarning($"[AdManager] Banner failed: {e.GetMessage()}");
#endif
    }

    /// <summary>배너를 숨깁니다 (파괴하지 않음).</summary>
    public void HideBanner()
    {
#if UNITY_ANDROID || UNITY_IOS
        _banner?.Hide();
#endif
    }

    /// <summary>배너를 완전히 파괴합니다.</summary>
    public void DestroyBanner()
    {
#if UNITY_ANDROID || UNITY_IOS
        _banner?.Destroy();
        _banner = null;
#endif
    }

    // ═══════════════════════════════════════════════════════════
    // 보상형 광고
    // ═══════════════════════════════════════════════════════════

#if UNITY_ANDROID || UNITY_IOS
    void LoadRewardedAd()
    {
        _rewardedReady = false;
        RewardedAd.Load(REWARDED_ID, new AdRequest(), (ad, err) =>
        {
            if (err != null)
            {
                Debug.LogWarning($"[AdManager] Rewarded load failed: {err.GetMessage()}");
                return;
            }
            _rewardedAd    = ad;
            _rewardedReady = true;
            Debug.Log("[AdManager] Rewarded ad loaded.");

            // 광고 종료 후 다음 광고 미리 로드
            _rewardedAd.OnAdFullScreenContentClosed  += () => LoadRewardedAd();
            _rewardedAd.OnAdFullScreenContentFailed  += _ => LoadRewardedAd();
        });
    }
#endif

    /// <summary>보상형 광고가 시청 가능한 상태인지 반환합니다.</summary>
    public bool IsRewardedReady
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return _rewardedReady && _rewardedAd != null && _rewardedAd.CanShowAd();
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 보상형 광고를 표시합니다.
    /// <para>onRewarded : 광고를 끝까지 시청한 경우 호출됩니다.</para>
    /// <para>onFailed   : 광고를 건너뛰거나 로드되지 않은 경우 호출됩니다.</para>
    /// </summary>
    public void ShowRewarded(Action onRewarded, Action onFailed = null)
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!IsRewardedReady)
        {
            Debug.LogWarning("[AdManager] Rewarded ad not ready.");
            onFailed?.Invoke();
            return;
        }

        bool rewarded = false;

        _rewardedAd.Show(reward =>
        {
            rewarded = true;
            onRewarded?.Invoke();   // Unity SDK v8+는 메인 스레드에서 호출됨
        });

        _rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            if (!rewarded) onFailed?.Invoke();
        };
#else
        // 에디터 / 미지원 플랫폼: 광고 없이 즉시 보상 지급 (테스트용)
        Debug.Log("[AdManager] Editor: rewarded ad simulated.");
        onRewarded?.Invoke();
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID || UNITY_IOS
        _banner?.Destroy();
        _rewardedAd?.Destroy();
#endif
    }
}
