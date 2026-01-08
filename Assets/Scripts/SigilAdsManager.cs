using System;             // <- needed for Action
using UnityEngine;

public class SigilAdsManager : MonoBehaviour
{
    public static SigilAdsManager Instance { get; private set; }

    [Header("IronSource / LevelPlay")]
    [SerializeField] private string iOSAppKey = "24ca1a025";

    // We remember what to do when the rewarded ad finishes
    private Action _pendingHintReward;
    
    [SerializeField] private int showInterstitialEveryNCompletions = 3;
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
#if UNITY_IOS && !UNITY_EDITOR
        // Init SDK
        IronSource.Agent.init(iOSAppKey);
        IronSource.Agent.validateIntegration();    // optional but handy for debug

        // Interstitial events
        IronSourceInterstitialEvents.onAdReadyEvent  += OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent += OnInterstitialClosed;

        // Rewarded video events
        IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedVideoRewarded;

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

    // ----------------------------------------------------
    // Interstitials (optional, for later)
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

#if UNITY_IOS && !UNITY_EDITOR
    private void OnInterstitialReady(IronSourceAdInfo adInfo)
    {
        // Optional: Debug.Log("Interstitial ready");
    }

    private void OnInterstitialClosed(IronSourceAdInfo adInfo)
    {
        // After the user closes an ad, load the next one
        LoadInterstitial();
    }
#endif

    private void OnDestroy()
    {
#if UNITY_IOS && !UNITY_EDITOR
        IronSourceInterstitialEvents.onAdReadyEvent  -= OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent -= OnInterstitialClosed;
        IronSourceRewardedVideoEvents.onAdRewardedEvent -= OnRewardedVideoRewarded;
#endif
    }

    // ----------------------------------------------------
    // Rewarded video for extra hints
    // ----------------------------------------------------
    public void ShowRewardedForHint(Action onRewarded)
    {
#if UNITY_IOS && !UNITY_EDITOR
        if (IronSource.Agent.isRewardedVideoAvailable())
        {
            // remember what to do when the ad finishes
            _pendingHintReward = onRewarded;
            IronSource.Agent.showRewardedVideo("extra_hint");
        }
        else
        {
            Debug.Log("[Ads] Rewarded video not available.");
        }
#else
        // EDITOR / non-iOS: just fake the ad instantly
        Debug.Log("[Ads] Simulating rewarded hint in editor / non-iOS build.");
        onRewarded?.Invoke();
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    // Called by LevelPlay when the user actually earns the reward
    private void OnRewardedVideoRewarded(IronSourcePlacement placement, IronSourceAdInfo adInfo)
    {
        _pendingHintReward?.Invoke();
        _pendingHintReward = null;
    }
#endif
    
    public void NotifyLevelCompleted()
    {
        _completedLevelsSinceLastAd++;

        if (_completedLevelsSinceLastAd >= showInterstitialEveryNCompletions)
        {
            _completedLevelsSinceLastAd = 0;
            ShowInterstitialIfReady();
        }
    }
}
