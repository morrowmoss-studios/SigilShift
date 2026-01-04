using UnityEngine;

public class SigilAdsManager : MonoBehaviour
{
    public static SigilAdsManager Instance { get; private set; }

    [Header("IronSource / LevelPlay")]
    [SerializeField] private string iOSAppKey = "YOUR_IOS_APP_KEY_HERE";

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

        // Hook interstitial events (new API)
        IronSourceInterstitialEvents.onAdReadyEvent  += OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent += OnInterstitialClosed;

        // Pre-load the first ad
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
    // Public API – call this after a level completes
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

    // ----------------------------------------------------
    // Callbacks
    // ----------------------------------------------------
    private void OnInterstitialReady(IronSourceAdInfo adInfo)
    {
        // Optional: Debug.Log("Interstitial ready");
    }

    private void OnInterstitialClosed(IronSourceAdInfo adInfo)
    {
        // After the user closes an ad, load the next one
        LoadInterstitial();
    }

    private void OnDestroy()
    {
#if UNITY_IOS && !UNITY_EDITOR
        IronSourceInterstitialEvents.onAdReadyEvent  -= OnInterstitialReady;
        IronSourceInterstitialEvents.onAdClosedEvent -= OnInterstitialClosed;
#endif
    }
}
