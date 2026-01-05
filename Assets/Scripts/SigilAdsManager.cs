using System;
using UnityEngine;

public class SigilAdsManager : MonoBehaviour
{
    public static SigilAdsManager Instance { get; private set; }

    // Whatever we should do when a rewarded ad successfully pays out
    public Action PendingRewardAction;

    [Header("IronSource / LevelPlay")]
    [SerializeField] private string iOSAppKey = "24ca1a025";

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

        // -------- Interstitial events --------
        IronSourceInterstitialEvents.onAdReadyEvent  += OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent += OnInterstitialClosed;

        // -------- Rewarded events --------
        IronSourceRewardedVideoEvents.onAdRewardedEvent += OnRewardedVideoRewarded;
        IronSourceRewardedVideoEvents.onAdClosedEvent   += OnRewardedVideoClosed;

        // Pre-load ads
        LoadInterstitial();
        IronSource.Agent.loadRewardedVideo();
#endif
    }

    private void OnApplicationPause(bool isPaused)
    {
#if UNITY_IOS && !UNITY_EDITOR
        IronSource.Agent.onApplicationPause(isPaused);
#endif
    }

    // ----------------------------------------------------
    // Interstitials (between levels)
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
        // Debug.Log("Interstitial ready");
    }

    private void OnInterstitialClosed(IronSourceAdInfo adInfo)
    {
        // Load the next one
        LoadInterstitial();
    }
#endif

    // ----------------------------------------------------
    // Rewarded – used for extra hints
    // ----------------------------------------------------
    public void ShowRewardedForHint(Action onRewardEarned)
    {
        // Always keep a fallback so the game still works without ads
        void Fallback()
        {
            onRewardEarned?.Invoke();
        }

#if UNITY_IOS && !UNITY_EDITOR
        if (IronSource.Agent.isRewardedVideoAvailable())
        {
            PendingRewardAction = onRewardEarned;
            IronSource.Agent.showRewardedVideo("extra_hint");
        }
        else
        {
            // No ad available – just grant the hint
            Fallback();
        }
#else
        // In Editor / non-iOS: just grant immediately
        Fallback();
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    private void OnRewardedVideoRewarded(IronSourcePlacement placement, IronSourceAdInfo adInfo)
    {
        // This is where we actually GIVE the reward
        PendingRewardAction?.Invoke();
        PendingRewardAction = null;
    }

    private void OnRewardedVideoClosed(IronSourceAdInfo adInfo)
    {
        // Prep the next rewarded ad
        IronSource.Agent.loadRewardedVideo();
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
