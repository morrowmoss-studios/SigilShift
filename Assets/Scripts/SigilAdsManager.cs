using UnityEngine;
using System;

public class SigilAdsManager : MonoBehaviour
{
    public static SigilAdsManager Instance { get; private set; }

    [Header("IronSource / LevelPlay")]
    [SerializeField] private string iOSAppKey = "24ca1a025";

    // Callback we’ll invoke when the player actually earns the hint
    private Action _pendingHintRewardCallback;

    private void Awake()
    {
        // simple singleton
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
        // Init SDK
        IronSource.Agent.init(iOSAppKey);
        IronSource.Agent.validateIntegration();    // optional but nice for debug

        // -------- Interstitial events --------
        IronSourceInterstitialEvents.onAdReadyEvent  += OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent += OnInterstitialClosed;

        // -------- Rewarded events (for hints) --------
        IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedVideoRewarded;
        IronSourceRewardedVideoEvents.onAdClosedEvent   += OnRewardedVideoClosed;

        // Pre-load first interstitial
        LoadInterstitial();
#endif
    }

    private void OnApplicationPause(bool isPaused)
    {
#if UNITY_IOS && !UNITY_EDITOR
        IronSource.Agent.onApplicationPause(isPaused);
#endif
    }

    // ----------------------------------------------------
    // Interstitials (used e.g. every few completed levels)
    // ----------------------------------------------------
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
        // Optional: Debug.Log("[Ads] Interstitial ready.");
    }

    private void OnInterstitialClosed(IronSourceAdInfo adInfo)
    {
        // After the user closes an ad, load the next one
        LoadInterstitial();
    }

    // ----------------------------------------------------
    // Rewarded video – specifically “extra_hint”
    // ----------------------------------------------------

    /// <summary>
    /// Ask to show a rewarded ad that will grant an extra hint.
    /// onRewardGranted will be called once the user *actually* earns it.
    /// </summary>
    public void ShowRewardedForHint(Action onRewardGranted)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (!IronSource.Agent.isRewardedVideoAvailable())
        {
            Debug.Log("[Ads] No rewarded video available for hint.");
            return;
        }

        _pendingHintRewardCallback = onRewardGranted;
        IronSource.Agent.showRewardedVideo("extra_hint");
#else
        // In editor / non-iOS builds: fake the reward instantly so you can test flow.
        onRewardGranted?.Invoke();
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    private bool _earnedCurrentReward = false;

    // Called by LevelPlay when the user has *earned* the reward
    private void OnRewardedVideoRewarded(IronSourcePlacement placement, IronSourceAdInfo info)
    {
        _earnedCurrentReward = true;
    }

    // Called when the rewarded ad closes (finished or skipped)
    private void OnRewardedVideoClosed(IronSourceAdInfo info)
    {
        if (_earnedCurrentReward && _pendingHintRewardCallback != null)
        {
            _pendingHintRewardCallback.Invoke();
        }

        _earnedCurrentReward = false;
        _pendingHintRewardCallback = null;
    }
#endif

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
