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
    const string INTER_ID    = "ca-app-pub-3940256099942544/1033173712"; // 전면 테스트 ID
#elif UNITY_IOS
    const string APP_ID      = "ca-app-pub-3940256099942544~1458002511";
    const string BANNER_ID   = "ca-app-pub-3940256099942544/2435281174"; // 적응형 배너 테스트 ID
    const string REWARDED_ID = "ca-app-pub-3940256099942544/1712485313";
    const string INTER_ID    = "ca-app-pub-3940256099942544/4411468910"; // 전면 테스트 ID
#else
    const string APP_ID      = "";
    const string BANNER_ID   = "";
    const string REWARDED_ID = "";
    const string INTER_ID    = "";
#endif

#if UNITY_ANDROID || UNITY_IOS
    BannerView      _banner;
    RewardedAd      _rewardedAd;
    bool            _rewardedReady;
    InterstitialAd  _interstitial;
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
            LoadInterstitial();
        });
#endif
    }

    // ═══════════════════════════════════════════════════════════
    // 배너 광고
    // ═══════════════════════════════════════════════════════════

    /// <summary>하단에 배너를 표시합니다. 이미 표시 중이면 무시합니다.</summary>
    public void ShowBanner()
    {
        // 광고를 제거한 사람에게는 부르는 쪽을 고치지 않아도 안 뜨게 여기서 막는다.
        if (RemoveAds.Owned) return;
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

    // ═══════════════════════════════════════════════════════════
    // 전면 광고
    // ═══════════════════════════════════════════════════════════
    // 건너뛰기 버튼이 몇 초 뒤에 뜨는지는 앱이 정하지 못한다 — 광고 크리에이티브와
    // 네트워크가 정하고, 영상 소재는 대개 5초쯤 뒤에 닫기가 나타난다.
    // 앱에서 할 수 있는 건 "언제 띄울지"까지다.

#if UNITY_ANDROID || UNITY_IOS
    void LoadInterstitial()
    {
        if (RemoveAds.Owned) return;

        InterstitialAd.Load(INTER_ID, new AdRequest(), (ad, err) =>
        {
            if (err != null)
            {
                Debug.LogWarning($"[AdManager] Interstitial load failed: {err.GetMessage()}");
                return;
            }
            _interstitial = ad;
            Debug.Log("[AdManager] Interstitial loaded.");
        });
    }
#endif

    public bool IsInterstitialReady
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return !RemoveAds.Owned && _interstitial != null && _interstitial.CanShowAd();
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 전면 광고를 띄우고, 닫히면 onClosed를 부른다.
    /// 광고를 못 띄우는 상황(미로드·구매자·에디터)에서도 onClosed는 반드시 불린다 —
    /// 부르는 쪽이 "광고가 끝나면 이어서 할 일"을 여기에 맡기기 때문에, 안 부르면 게임이 멎는다.
    /// </summary>
    public void ShowInterstitial(Action onClosed)
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!IsInterstitialReady)
        {
            Debug.LogWarning("[AdManager] Interstitial not ready.");
            onClosed?.Invoke();
            LoadInterstitial();   // 다음 기회를 위해 다시 받아 둔다
            return;
        }

        bool done = false;
        void Finish()
        {
            if (done) return;   // Closed와 Failed가 겹쳐 들어와도 한 번만
            done = true;
            _interstitial = null;
            LoadInterstitial();
            onClosed?.Invoke();
        }

        _interstitial.OnAdFullScreenContentClosed += Finish;
        _interstitial.OnAdFullScreenContentFailed += _ => Finish();
        _interstitial.Show();
#else
        // 에디터 / 미지원 플랫폼: 광고 없이 곧바로 이어간다 (보상형과 같은 규칙)
        Debug.Log("[AdManager] Editor: interstitial skipped.");
        onClosed?.Invoke();
#endif
    }

    void OnDestroy()
    {
#if UNITY_ANDROID || UNITY_IOS
        _banner?.Destroy();
        _rewardedAd?.Destroy();
        _interstitial?.Destroy();
#endif
    }
}
