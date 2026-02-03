using System;
using UnityEngine;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
using Unity.Services.LevelPlay;
#endif

public class SigilAdsManager : MonoBehaviour
{
    public static SigilAdsManager Instance { get; private set; }

    [Header("LevelPlay App Keys")]
    [SerializeField] private string iOSAppKey = "24ca1a025";
    [SerializeField] private string androidAppKey = "24ca1d9ad";

    [Header("Rewarded Ad Unit IDs (platform-specific)")]
    [Tooltip("iOS Rewarded Ad Unit ID (HintReward)")]
    [SerializeField] private string rewardedAdUnitId_iOS = "";

    [Tooltip("Android Rewarded Ad Unit ID (HintReward)")]
    [SerializeField] private string rewardedAdUnitId_Android = "";

    [Header("Interstitial Ad Unit IDs (platform-specific)")]
    [Tooltip("iOS Interstitial Ad Unit ID (After2Puzzles)")]
    [SerializeField] private string interstitialAdUnitId_iOS = "";

    [Tooltip("Android Interstitial Ad Unit ID (After2Puzzles)")]
    [SerializeField] private string interstitialAdUnitId_Android = "";

    [Header("Interstitial frequency")]
    [Tooltip("Show an interstitial every N puzzle completions (not unique levels).")]
    [SerializeField] private int showInterstitialEveryNCompletions = 2;

    [Header("LevelPlay Test Suite (DEV ONLY)")]
    [Tooltip("Only works in DEVELOPMENT_BUILD. Never runs in a normal release build.")]
    [SerializeField] private bool enableTestSuiteInDevBuild = false;

    private int _completedPuzzlesSinceLastInterstitial = 0;

    // we remember what to do when the rewarded ad actually pays out
    private Action _pendingHintReward;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
    private bool _sdkInitialized;
    private LevelPlayRewardedAd _rewardedAd;
    private LevelPlayInterstitialAd _interstitialAd;
#endif

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
        InitLevelPlay();
#else
        Debug.Log("[Ads] Editor / non-mobile build – ads simulated.");
#endif
    }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
    // ----------------------------------------------------
    // LevelPlay init
    // ----------------------------------------------------
    private void InitLevelPlay()
    {
        string appKey = GetAppKeyForPlatform();
        if (string.IsNullOrEmpty(appKey))
        {
            Debug.LogError("[Ads] App key is EMPTY for this platform – check SigilAdsManager inspector.");
            return;
        }

        // IMPORTANT: Enable Test Suite metadata BEFORE init.
        // Safety: this only runs in DEVELOPMENT_BUILD (never in real release).
#if DEVELOPMENT_BUILD
        if (enableTestSuiteInDevBuild)
        {
            Debug.Log("[Ads] Enabling LevelPlay Test Suite (DEV BUILD).");
            LevelPlay.SetMetaData("is_test_suite", "enable");
        }
#endif

        LevelPlay.OnInitSuccess += OnSdkInitSuccess;
        LevelPlay.OnInitFailed  += OnSdkInitFailed;

        Debug.Log($"[Ads] Initializing LevelPlay with appKey={appKey}");
        LevelPlay.Init(appKey);
    }

    private string GetAppKeyForPlatform()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer)
            return iOSAppKey;
        if (Application.platform == RuntimePlatform.Android)
            return androidAppKey;

        return null;
    }

    private string GetRewardedAdUnitIdForPlatform()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer)
            return rewardedAdUnitId_iOS;
        if (Application.platform == RuntimePlatform.Android)
            return rewardedAdUnitId_Android;

        return null;
    }

    private string GetInterstitialAdUnitIdForPlatform()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer)
            return interstitialAdUnitId_iOS;
        if (Application.platform == RuntimePlatform.Android)
            return interstitialAdUnitId_Android;

        return null;
    }

    private void OnSdkInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[Ads] LevelPlay SDK initialized successfully.");
        _sdkInitialized = true;

        // Launch Test Suite AFTER init success.
        // SAFETY: only in DEVELOPMENT_BUILD
#if DEVELOPMENT_BUILD
        if (enableTestSuiteInDevBuild)
        {
            Debug.Log("[Ads] Launching LevelPlay Test Suite (DEV BUILD).");
            LevelPlay.LaunchTestSuite();
            return; // IMPORTANT: do not load/show real ads when using Test Suite flow
        }
#endif

        SetupRewardedAd();
        SetupInterstitialAd();
    }

    private void OnSdkInitFailed(LevelPlayInitError error)
    {
        Debug.LogError($"[Ads] LevelPlay SDK init FAILED: {error}");
        _sdkInitialized = false;
    }

    // ----------------------------------------------------
    // Rewarded: +1 hint video
    // ----------------------------------------------------
    private void SetupRewardedAd()
    {
        string rewardedId = GetRewardedAdUnitIdForPlatform();
        if (string.IsNullOrEmpty(rewardedId))
        {
            Debug.LogError("[Ads] Rewarded Ad Unit ID is empty for this platform – set it in SigilAdsManager.");
            return;
        }

        _rewardedAd = new LevelPlayRewardedAd(rewardedId);

        _rewardedAd.OnAdLoaded     += OnRewardedLoaded;
        _rewardedAd.OnAdLoadFailed += OnRewardedLoadFailed;
        _rewardedAd.OnAdClosed     += OnRewardedClosed;
        _rewardedAd.OnAdRewarded   += OnRewardedRewarded;

        Debug.Log($"[Ads] Loading first rewarded ad… (adUnitId={rewardedId})");
        _rewardedAd.LoadAd();
    }

    private void OnRewardedLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log("[Ads] Rewarded ad loaded and ready.");
    }

    private void OnRewardedLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[Ads] Rewarded ad FAILED to load: {error}");
    }

    private void OnRewardedClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("[Ads] Rewarded ad closed – reloading.");
        _rewardedAd?.LoadAd();
    }

    private void OnRewardedRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Debug.Log("[Ads] Rewarded ad completed – granting hint.");
        _pendingHintReward?.Invoke();
        _pendingHintReward = null;
    }

    // ----------------------------------------------------
    // Interstitial: show every N puzzle completions
    // ----------------------------------------------------
    private void SetupInterstitialAd()
    {
        string interstitialId = GetInterstitialAdUnitIdForPlatform();
        if (string.IsNullOrEmpty(interstitialId))
        {
            Debug.LogWarning("[Ads] Interstitial Ad Unit ID is empty for this platform – interstitials disabled until set.");
            return;
        }

        _interstitialAd = new LevelPlayInterstitialAd(interstitialId);

        _interstitialAd.OnAdLoaded     += OnInterstitialLoaded;
        _interstitialAd.OnAdLoadFailed += OnInterstitialLoadFailed;
        _interstitialAd.OnAdClosed     += OnInterstitialClosed;

        Debug.Log($"[Ads] Loading first interstitial ad… (adUnitId={interstitialId})");
        _interstitialAd.LoadAd();
    }

    private void OnInterstitialLoaded(LevelPlayAdInfo adInfo)
    {
        Debug.Log("[Ads] Interstitial loaded and ready.");
    }

    private void OnInterstitialLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[Ads] Interstitial FAILED to load: {error}");
    }

    private void OnInterstitialClosed(LevelPlayAdInfo adInfo)
    {
        Debug.Log("[Ads] Interstitial closed – reloading.");
        _interstitialAd?.LoadAd();
    }
#endif

    // ----------------------------------------------------
    // Public API used by your game
    // ----------------------------------------------------

    /// <summary>
    /// Called from HintAdPopupController when the player confirms
    /// they want to watch an ad for +1 hint.
    /// </summary>
    public void ShowRewardedForHint(Action onRewarded)
    {
#if !(UNITY_IOS || UNITY_ANDROID) || UNITY_EDITOR
        Debug.Log("[Ads] Simulating rewarded hint in editor / non-mobile build.");
        onRewarded?.Invoke();
#else
        if (!_sdkInitialized)
        {
            Debug.LogWarning("[Ads] LevelPlay not initialized yet – cannot show rewarded.");
            return;
        }

        if (_rewardedAd == null)
        {
            Debug.LogWarning("[Ads] Rewarded ad object not created.");
            return;
        }

        if (!_rewardedAd.IsAdReady())
        {
            Debug.LogWarning("[Ads] Rewarded ad not ready yet.");
            return;
        }

        _pendingHintReward = onRewarded;
        Debug.Log("[Ads] Showing rewarded ad for +1 hint.");
        _rewardedAd.ShowAd();
#endif
    }

    /// <summary>
    /// Called from RunePuzzleManager when a puzzle is completed.
    /// Counts completions even if the same puzzle is replayed.
    /// Shows an interstitial every N completions.
    /// </summary>
    public void NotifyLevelCompleted()
    {
        _completedPuzzlesSinceLastInterstitial++;

#if (UNITY_EDITOR || !(UNITY_IOS || UNITY_ANDROID))
        if (_completedPuzzlesSinceLastInterstitial >= showInterstitialEveryNCompletions)
        {
            _completedPuzzlesSinceLastInterstitial = 0;
            Debug.Log("[Ads] (Editor) Would show interstitial now (N completions reached).");
        }
#else
        if (_completedPuzzlesSinceLastInterstitial < showInterstitialEveryNCompletions)
            return;

        _completedPuzzlesSinceLastInterstitial = 0;

        if (!_sdkInitialized)
        {
            Debug.LogWarning("[Ads] LevelPlay not initialized – skipping interstitial.");
            return;
        }

        if (_interstitialAd == null)
        {
            Debug.LogWarning("[Ads] Interstitial ad object not created (check ID / init).");
            return;
        }

        if (!_interstitialAd.IsAdReady())
        {
            Debug.LogWarning("[Ads] Interstitial not ready yet – skipping and reloading.");
            _interstitialAd.LoadAd();
            return;
        }

        Debug.Log("[Ads] Showing interstitial (N puzzle completions reached).");
        _interstitialAd.ShowAd();
#endif
    }

    private void OnDestroy()
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        if (_rewardedAd != null)
        {
            _rewardedAd.OnAdLoaded     -= OnRewardedLoaded;
            _rewardedAd.OnAdLoadFailed -= OnRewardedLoadFailed;
            _rewardedAd.OnAdClosed     -= OnRewardedClosed;
            _rewardedAd.OnAdRewarded   -= OnRewardedRewarded;
        }

        if (_interstitialAd != null)
        {
            _interstitialAd.OnAdLoaded     -= OnInterstitialLoaded;
            _interstitialAd.OnAdLoadFailed -= OnInterstitialLoadFailed;
            _interstitialAd.OnAdClosed     -= OnInterstitialClosed;
        }

        LevelPlay.OnInitSuccess -= OnSdkInitSuccess;
        LevelPlay.OnInitFailed  -= OnSdkInitFailed;
#endif
    }
}
