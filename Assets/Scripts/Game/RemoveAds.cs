using System;
using UnityEngine;

/// <summary>
/// "광고 제거" 구매 상태.
///
/// 무엇을 없애는가 — 배너와 전면 광고. 보상형(부활)은 그대로 둔다. 보상형은 플레이어가
/// 이득을 보려고 스스로 고르는 것이라, 없애면 오히려 살 수 있는 수단을 뺏는 셈이 된다.
///
/// 결제 자체는 아직 붙어 있지 않다. Unity Purchasing 패키지를 넣고 스토어 콘솔에 상품을
/// 등록하는 건 프로젝트 밖 작업이라, 여기서는 그 자리만 만들어 둔다.
/// Purchase()가 실제 결제를 부르고 성공 시 Grant()를 호출하도록 바꾸면 나머지 흐름은
/// 손대지 않아도 그대로 돌아간다.
/// </summary>
public static class RemoveAds
{
    const string PREF_KEY = "remove_ads_owned";

    /// <summary>이미 구매했는지. 배너·전면 광고를 띄우는 쪽에서 먼저 확인한다.</summary>
    public static bool Owned => PlayerPrefs.GetInt(PREF_KEY, 0) == 1;

    /// <summary>
    /// 스토어 결제가 실제로 연결돼 있는지. false면 제안 화면이 구매 버튼을 잠그고
    /// "추후 연결"로 알린다 — 눌러도 실패만 하는 버튼을 살려 두면 고장으로 보인다.
    /// Unity Purchasing을 붙이면 이 값을 true로 바꾼다.
    /// </summary>
    public const bool StoreReady =
#if UNITY_EDITOR
        true;    // 에디터에서는 성공 경로까지 시험할 수 있게 열어 둔다
#else
        false;
#endif

    /// <summary>
    /// 구매를 시도한다.
    /// 에디터에서는 결제 창 없이 성공으로 친다 — 보상형 광고(AdManager.ShowRewarded)가
    /// 에디터에서 즉시 보상을 주는 것과 같은 규칙이라, 두 경로를 같은 방식으로 시험할 수 있다.
    /// </summary>
    public static void Purchase(Action onSuccess, Action onFailed = null)
    {
#if UNITY_EDITOR
        Debug.Log("[RemoveAds] 에디터: 구매 성공으로 간주.");
        Grant();
        onSuccess?.Invoke();
#else
        // TODO: Unity Purchasing 연결 지점.
        //   1) Package Manager 에서 In App Purchasing 설치
        //   2) 스토어 콘솔에 상품 등록(예: remove_ads, 비소모성)
        //   3) 구매 성공 콜백에서 Grant() 호출 후 onSuccess
        //   4) 앱 시작 시 복원(restore) 처리 — 재설치한 사람이 다시 사지 않게
        Debug.LogWarning("[RemoveAds] 결제가 아직 연결되지 않았다.");
        onFailed?.Invoke();
#endif
    }

    /// <summary>구매 성공 처리. 배너는 그 자리에서 걷는다.</summary>
    public static void Grant()
    {
        PlayerPrefs.SetInt(PREF_KEY, 1);
        PlayerPrefs.Save();
        AdManager.Instance?.DestroyBanner();
    }

#if UNITY_EDITOR
    /// <summary>에디터에서 구매 상태를 되돌린다(제안 팝업을 다시 보려고).</summary>
    public static void DebugRevoke()
    {
        PlayerPrefs.DeleteKey(PREF_KEY);
        PlayerPrefs.Save();
    }
#endif
}
