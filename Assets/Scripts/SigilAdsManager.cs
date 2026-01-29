using System;             // <- needed for Action
using UnityEngine;

public class SigilAdsManager : MonoBehaviour
{
    public static SigilAdsManager Instance { get; private set; }

    [Header("IronSource / LevelPlay")]
    [SerializeField] private string iOSAppKey = "24ca1a025";
    [SerializeField] private string androidAppKey = "24ca1d9ad";

    // We remember what to do when the rewarded ad finishes
    private Action _pendingHintReward;

    [Header("Interstitial frequency")]
    [SerializeField] private int showInterstitialEveryNCompletions = 2;   // every 2 puzzles
    private int _completedLevelsSinceLastAd = 0;

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
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR

        string appKey =
#if UNITY_IOS
            iOSAppKey;
#else
            androidAppKey;
#endif

        if (string.IsNullOrEmpty(appKey))
        {
            Debug.LogError("[Ads] App key is EMPTY for this platform – check SigilAdsManager inspector.");
            return;
        }

        Debug.Log($"[Ads] Initializing IronSource with appKey={appKey}");
        IronSource.Agent.init(appKey);
        IronSource.Agent.validateIntegration();    // optional but handy for debug

        // Interstitial events
        IronSourceInterstitialEvents.onAdReadyEvent    += OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent   += OnInterstitialClosed;

        // Rewarded video events
        IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedVideoRewarded;

        // Pre-load the first interstitial
        LoadInterstitial();
#else
        Debug.Log("[Ads] Running in editor / non-mobile build – ads not initialized.");
#endif
    }

    private void OnApplicationPause(bool isPaused)
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        IronSource.Agent.onApplicationPause(isPaused);
#endif
    }

    private void OnDestroy()
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        IronSourceInterstitialEvents.onAdReadyEvent    -= OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent   -= OnInterstitialClosed;
        IronSourceRewardedVideoEvents.onAdRewardedEvent -= OnRewardedVideoRewarded;
#endif
    }

    // ----------------------------------------------------
    // Interstitials
    // ----------------------------------------------------

    public void NotifyLevelCompleted()
    {
        _completedLevelsSinceLastAd++;

        if (_completedLevelsSinceLastAd >= showInterstitialEveryNCompletions)
        {
            _completedLevelsSinceLastAd = 0;
            ShowInterstitialIfReady();
        }
    }

    public void ShowInterstitialIfReady()
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        if (IronSource.Agent.isInterstitialReady())
        {
            Debug.Log("[Ads] Showing interstitial.");
            IronSource.Agent.showInterstitial();
        }
        else
        {
            Debug.Log("[Ads] Interstitial not ready yet.");
        }
#else
        Debug.Log("[Ads] Simulating interstitial in editor / non-mobile build.");
#endif
    }

    public void LoadInterstitial()
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        Debug.Log("[Ads] Requesting interstitial load.");
        IronSource.Agent.loadInterstitial();
#endif
    }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
    private void OnInterstitialReady(IronSourceAdInfo adInfo)
    {
        Debug.Log("[Ads] Interstitial ready.");
    }

    private void OnInterstitialClosed(IronSourceAdInfo adInfo)
    {
        Debug.Log("[Ads] Interstitial closed – loading next.");
        LoadInterstitial();
    }
#endif

    // ----------------------------------------------------
    // Rewarded video for extra hints
    // ----------------------------------------------------

    public void ShowRewardedForHint(Action onRewarded)
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        if (IronSource.Agent.isRewardedVideoAvailable())
        {
            Debug.Log("[Ads] Showing rewarded video for extra hint.");
            // remember what to do when the ad finishes
            _pendingHintReward = onRewarded;
            IronSource.Agent.showRewardedVideo("extra_hint");
        }
        else
        {
            Debug.Log("[Ads] Rewarded video not available.");
        }
#else
        // EDITOR / non-mobile: just fake the ad instantly
        Debug.Log("[Ads] Simulating rewarded hint in editor / non-mobile build.");
        onRewarded?.Invoke();
#endif
    }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
    // Called by IronSource when the user actually earns the reward
    private void OnRewardedVideoRewarded(IronSourcePlacement placement, IronSourceAdInfo adInfo)
    {
        Debug.Log("[Ads] Rewarded video completed – granting hint.");
        _pendingHintReward?.Invoke();
        _pendingHintReward = null;
    }
#endif
}
