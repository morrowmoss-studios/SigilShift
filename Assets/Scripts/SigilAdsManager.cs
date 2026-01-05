using System;
using UnityEngine;

public class SigilAdsManager : MonoBehaviour
{
    public static SigilAdsManager Instance { get; private set; }

    [Header("IronSource / LevelPlay")]
    [SerializeField] private string iOSAppKey = "24ca1a025";

    // This will be set by whoever is asking for a rewarded ad (e.g. hint system)
    private Action _pendingRewardCallback;
    private bool _rewardGranted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // ---- Init SDK ----
        IronSource.Agent.init(iOSAppKey);
        IronSource.Agent.validateIntegration();    // optional but handy for debug

        // ---- Interstitial events ----
        IronSourceInterstitialEvents.onAdReadyEvent  += OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent += OnInterstitialClosed;

        // ---- Rewarded events ----
        IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedVideoRewarded;
        IronSourceRewardedVideoEvents.onAdClosedEvent   += OnRewardedVideoClosed;

        // Pre-load the first interstitial
        LoadInterstitial();
#endif
    }

    private void OnApplicationPause(bool isPaused)
    {
#if UNITY_IOS && !UNITY_EDITOR
        IronSource.Agent.onApplicationPause(isPaused);
#endif
    }

    // =====================================================
    // INTERSTITIALS  (e.g. every few completed levels)
    // =====================================================
    public void ShowInterstitialIfReady()
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (IronSource.Agent.isInterstitialReady())
        {
            IronSource.Agent.showInterstitial();
        }
#endif
    }

    public void LoadInterstitial()
    {
#if UNITY_IOS && !UNITY_EDITOR
        IronSource.Agent.loadInterstitial();
#endif
    }

    private void OnInterstitialReady(IronSourceAdInfo adInfo)
    {
        // Debug.Log("Interstitial ready");
    }

    private void OnInterstitialClosed(IronSourceAdInfo adInfo)
    {
        // After the user closes an ad, load the next one
        LoadInterstitial();
    }

    // =====================================================
    // REWARDED – used for “watch ad to get a hint”
    // =====================================================

    /// <summary>
    /// Show a rewarded ad. If fully watched, onRewardEarned will be invoked.
    /// </summary>
    public void ShowRewardedForHint(Action onRewardEarned)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (IronSource.Agent.isRewardedVideoAvailable())
        {
            _pendingRewardCallback = onRewardEarned;
            _rewardGranted = false;

            // "extra_hint" is a placement name you can configure in the dashboard.
            IronSource.Agent.showRewardedVideo("extra_hint");
        }
        else
        {
            // Optional: show a small popup like "No ad available right now."
            // For now we just do nothing.
        }
#else
        // In editor or non-iOS: pretend the ad was watched and grant instantly.
        onRewardEarned?.Invoke();
#endif
    }

    // Called when the SDK says “user earned the reward”
    private void OnRewardedVideoRewarded(IronSourcePlacement placement, IronSourceAdInfo adInfo)
    {
        _rewardGranted = true;
    }

    // Called when the rewarded video closes (user dismissed it)
    private void OnRewardedVideoClosed(IronSourceAdInfo adInfo)
    {
        if (_rewardGranted && _pendingRewardCallback != null)
        {
            _pendingRewardCallback.Invoke();
        }

        _rewardGranted = false;
        _pendingRewardCallback = null;
    }

    private void OnDestroy()
    {
#if UNITY_IOS && !UNITY_EDITOR
        IronSourceInterstitialEvents.onAdReadyEvent  -= OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent -= OnInterstitialClosed;

        IronSourceRewardedVideoEvents.onAdRewardedEvent -= OnRewardedVideoRewarded;
        IronSourceRewardedVideoEvents.onAdClosedEvent   -= OnRewardedVideoClosed;
#endif
    }
}
