using System;
using System.Collections;
using UnityEngine;

#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

/// <summary>
/// Google AdMob 배너 / 보상형 광고 관리 싱글턴.
/// 씬에 없으면 자동 생성됩니다 (GetOrCreate 패턴).
/// </summary>
public class AdManager : MonoBehaviour
{
    /// <summary>보상형 광고를 보여 준 결과. 실패의 책임이 누구에게 있는지로 나눈다.</summary>
    public enum RewardOutcome
    {
        Rewarded,      // 끝까지 봤다 — 보상 지급
        Skipped,       // 스스로 닫았다 — 지급 안 함
        Unavailable,   // 광고를 아예 못 띄웠다 — 우리 사정이므로 부르는 쪽이 판단
    }

    public static AdManager Instance { get; private set; }

    // ── 광고 단위 ID ──────────────────────────────────────────────
    // 안드로이드는 실제 ID다. 개발 중에 이 광고를 직접 누르면 계정이 정지될 수 있으니,
    // 화면에 뜨는지만 확인하고 클릭하지 않는다.
    //
    // APP_ID 는 런타임에서 읽히지 않는다 — SDK 는 매니페스트의 APPLICATION_ID 메타데이터를
    // 본다(Assets > Google Mobile Ads > Settings 가 거기에 써 넣는다). 여기 값은 대조용이다.
    //
    // iOS 는 아직 앱을 등록하지 않아 테스트 ID 그대로 둔다.
#if UNITY_ANDROID
    const string APP_ID      = "ca-app-pub-8759899777428361~1010312109";
    const string BANNER_ID   = "ca-app-pub-8759899777428361/9978522515";
    const string REWARDED_ID = "ca-app-pub-8759899777428361/6174268559";
    const string INTER_ID    = "ca-app-pub-8759899777428361/2693455363";
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

    // ── 동의(UMP) 진행 상태 ───────────────────────────────────────
    // UMP 콜백이 어느 스레드로 오는지 SDK 가 보장하지 않는다. 위쪽 Awake 에서 켜는
    // RaiseAdEventsOnUnityMainThread 는 광고 이벤트에만 걸리고 여기에는 미치지 않는다.
    // 그래서 콜백 안에서는 플래그만 세우고, 실제 일은 Update 가 메인 스레드에서 한다.
    volatile bool _consentFlowDone;   // 동의 절차 종료(동의·거부·실패 무관)
    volatile bool _sdkReady;          // MobileAds.Initialize 완료
    bool _initStarted;
    bool _bannerWanted;               // 준비되기 전에 ShowBanner 가 불렸다
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
        // 광고 콜백은 기본적으로 백그라운드 스레드에서 온다. 그 스레드에서 Destroy 나
        // UI 조작 같은 유니티 API 를 부르면 예외가 나고, 보상을 받았는데도 화면이
        // 그대로 남는다(부활 버튼을 다시 눌러야 했던 원인). 초기화 전에 켜 둬야 한다.
        //
        // 실제로 기기 로그에 이렇게 찍혔다:
        //   UnityException: GetInt can only be called from the main thread.
        //     RemoveAds.get_Owned  ←  AdManager.LoadInterstitial  ←  AndroidJavaProxy.Invoke
        //
        // SDK 11.0.0 CHANGELOG 는 이 속성을 obsolete 로 적고 MobileAdsEventExecutor.ExecuteInUpdate
        // 를 권한다. 다만 그쪽은 콜백마다 일일이 감싸야 해서 하나만 빠뜨려도 같은 버그가 돌아온다.
        // 지금은 전역으로 한 번에 막는 이쪽을 쓴다. 속성이 실제로 제거되면 그때 옮긴다.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        RequestConsent();
#endif
    }

#if UNITY_ANDROID || UNITY_IOS

    // ═══════════════════════════════════════════════════════════
    // 광고 동의 (UMP)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 광고를 요청하기 전에 동의를 받는다.
    ///
    /// 유럽경제지역과 영국 이용자에게 광고를 보여 주려면 인증된 동의 관리 플랫폼을 반드시
    /// 거쳐야 한다. 이 절차 없이 그 지역에 배포하면 광고가 채워지지 않거나 정책 위반이 된다.
    ///
    /// 국가별로 분기할 필요는 없다 — UMP 가 이용자 위치를 스스로 판단해서, 동의가 필요 없는
    /// 곳에서는 아무 화면도 띄우지 않고 조용히 통과시킨다. 그래서 한국만 배포하든 전 세계로
    /// 넓히든 이 코드는 그대로다.
    ///
    /// 어떤 이유로 실패해도 게임은 계속되어야 한다. 광고를 못 받을 뿐이지 판이 막히면 안 된다.
    /// </summary>
    void RequestConsent()
    {
        var request = new ConsentRequestParameters
        {
            // 앱이 만 13세 이상을 대상으로 하므로 false 다.
            // Play Console 의 "타겟 연령층" 선언과 반드시 같아야 한다.
            TagForUnderAgeOfConsent = false,
        };

        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null)
                Debug.LogWarning($"[AdManager] 동의 정보 갱신 실패: {updateError.Message}");

            // 갱신에 실패해도 양식을 시도해 본다. 이미 받아 둔 동의가 남아 있을 수 있다.
            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null)
                    Debug.LogWarning($"[AdManager] 동의 양식 실패: {formError.Message}");

                _consentFlowDone = true;
            });
        });
    }

    void Update()
    {
        if (!_consentFlowDone || _initStarted) return;
        _initStarted = true;

        // 동의를 거부했거나 절차가 실패한 경우. 광고 없이 게임만 돌아간다.
        if (!ConsentInformation.CanRequestAds())
        {
            Debug.Log("[AdManager] 동의가 없어 광고를 요청하지 않는다.");
            return;
        }

        MobileAds.Initialize(_ =>
        {
            Debug.Log("[AdManager] MobileAds initialized.");
            _sdkReady = true;
            LoadRewardedAd();
            LoadInterstitial();

            // 동의를 받는 동안 메뉴가 이미 배너를 요청했을 수 있다.
            if (_bannerWanted) ShowBanner();
        });
    }

#endif

    // ═══════════════════════════════════════════════════════════
    // 배너 광고
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 동의를 나중에 바꿀 수 있는 진입점을 앱이 제공해야 하는지.
    ///
    /// 유럽경제지역·영국 이용자에게만 true 가 된다. 그 지역에서는 한 번 정한 뒤에도 마음을
    /// 바꿀 수 있어야 하므로 버튼을 반드시 띄워야 한다. 반대로 그 밖의 지역에서는 띄우지
    /// 않는 편이 낫다 — 눌러도 나올 화면이 없어 고장난 버튼으로 보인다.
    /// </summary>
    public bool IsPrivacyOptionsRequired
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return _consentFlowDone &&
                   ConsentInformation.PrivacyOptionsRequirementStatus
                       == PrivacyOptionsRequirementStatus.Required;
#else
            return false;
#endif
        }
    }

    /// <summary>동의 설정 화면을 다시 연다.</summary>
    public void ShowPrivacyOptions()
    {
#if UNITY_ANDROID || UNITY_IOS
        ConsentForm.ShowPrivacyOptionsForm(error =>
        {
            if (error != null)
                Debug.LogWarning($"[AdManager] 동의 설정 화면 실패: {error.Message}");
        });
#endif
    }

    /// <summary>하단에 배너를 표시합니다. 이미 표시 중이면 무시합니다.</summary>
    public void ShowBanner()
    {
        // 광고를 제거한 사람에게는 부르는 쪽을 고치지 않아도 안 뜨게 여기서 막는다.
        if (RemoveAds.Owned) return;
#if UNITY_ANDROID || UNITY_IOS
        // 동의 절차가 끝나기 전에는 광고를 요청하면 안 된다. 뜻만 기억해 두고,
        // SDK 가 준비되면 초기화 콜백이 이 함수를 다시 부른다.
        _bannerWanted = true;
        if (!_sdkReady) return;

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
        _bannerWanted = false;
        _banner?.Hide();
#endif
    }

    /// <summary>배너를 완전히 파괴합니다.</summary>
    public void DestroyBanner()
    {
#if UNITY_ANDROID || UNITY_IOS
        _bannerWanted = false;
        _banner?.Destroy();
        _banner = null;
#endif
    }

    // ═══════════════════════════════════════════════════════════
    // 보상형 광고
    // ═══════════════════════════════════════════════════════════

#if UNITY_ANDROID || UNITY_IOS
    // 로드 실패는 대개 일시적이다 — 앱을 켠 직후 네트워크가 아직 안 붙은 경우가 흔하다.
    // 실패하고 끝내면 그 판 내내 광고 버튼이 죽어 있으므로, 스스로 몇 번 더 받아 본다.
    const float AD_RETRY_DELAY = 8f;
    const int   AD_RETRY_MAX   = 5;
    int _rewardedRetries;
    int _interRetries;

    IEnumerator RetryLoadAfter(float sec, Action load)
    {
        yield return new WaitForSeconds(sec);
        load();
    }

    void LoadRewardedAd()
    {
        _rewardedReady = false;
        RewardedAd.Load(REWARDED_ID, new AdRequest(), (ad, err) =>
        {
            if (err != null)
            {
                Debug.LogWarning($"[AdManager] Rewarded load failed: {err.GetMessage()}");
                if (_rewardedRetries++ < AD_RETRY_MAX)
                    StartCoroutine(RetryLoadAfter(AD_RETRY_DELAY, LoadRewardedAd));
                return;
            }
            _rewardedAd    = ad;
            _rewardedReady = true;
            _rewardedRetries = 0;
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
    /// 보상형 광고를 보여 주고 결과를 알린다.
    ///
    /// 결과를 셋으로 나눈 이유: 부르는 쪽이 "보상을 줄지"를 판단하려면 실패의 종류를
    /// 알아야 한다. 사용자가 스스로 닫은 것과, 광고를 아예 못 튼 것은 책임이 다르다.
    /// </summary>
    public void ShowRewarded(Action<RewardOutcome> done)
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!IsRewardedReady)
        {
            Debug.LogWarning("[AdManager] Rewarded ad not ready.");
            LoadRewardedAd();   // 전면 광고와 같게 — 다음 기회를 위해 다시 받아 둔다
            done?.Invoke(RewardOutcome.Unavailable);
            return;
        }

        bool rewarded = false;
        bool reported = false;
        void Report(RewardOutcome outcome)
        {
            if (reported) return;   // 보상·닫힘·실패가 겹쳐 들어와도 한 번만
            reported = true;
            done?.Invoke(outcome);
        }

        // 닫힘·실패 처리를 Show() 보다 먼저 건다. 뒤에 걸면 그 사이에 끝나는 경우를 놓친다.
        _rewardedAd.OnAdFullScreenContentClosed += () =>
            Report(rewarded ? RewardOutcome.Rewarded : RewardOutcome.Skipped);
        _rewardedAd.OnAdFullScreenContentFailed += _ => Report(RewardOutcome.Unavailable);

        _rewardedAd.Show(reward =>
        {
            // 보상은 이 시점에 이미 확정이다. 닫힘을 기다리지 않는다.
            rewarded = true;
            Report(RewardOutcome.Rewarded);   // RaiseAdEventsOnUnityMainThread 덕에 메인 스레드다
        });
#else
        // 에디터 / 미지원 플랫폼: 광고 없이 즉시 보상 지급 (테스트용)
        Debug.Log("[AdManager] Editor: rewarded ad simulated.");
        done?.Invoke(RewardOutcome.Rewarded);
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
                if (_interRetries++ < AD_RETRY_MAX)
                    StartCoroutine(RetryLoadAfter(AD_RETRY_DELAY, LoadInterstitial));
                return;
            }
            _interstitial = ad;
            _interRetries = 0;
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
