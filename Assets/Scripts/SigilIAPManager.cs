using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// Handles in-app purchases for SigilShift.
/// Right now it only cares about a single non-consumable: "Remove Ads".
/// </summary>
public class SigilIAPManager : MonoBehaviour, IStoreListener
{
    public static SigilIAPManager Instance { get; private set; }

    [Header("Product IDs – MUST MATCH THE STORES EXACTLY")]
    [Tooltip("iOS product id for the non-consumable Remove Ads purchase")]
    [SerializeField] private string removeAdsProductIdIOS;

    [Tooltip("Android product id for the non-consumable Remove Ads purchase")]
    [SerializeField] private string removeAdsProductIdAndroid;

    // PlayerPrefs key used by both IAP and Ads manager
    public const string RemoveAdsPrefsKey = "SigilShift_RemoveAds";

    private IStoreController _storeController;
    private IExtensionProvider _extensionProvider;

    private string RemoveAdsProductId
    {
        get
        {
#if UNITY_IOS
            return removeAdsProductIdIOS;
#elif UNITY_ANDROID
            return removeAdsProductIdAndroid;
#else
            // Editor / other – just pick something so Unity IAP can run its fake store
            return string.IsNullOrEmpty(removeAdsProductIdIOS)
                ? removeAdsProductIdAndroid
                : removeAdsProductIdIOS;
#endif
        }
    }

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
        InitializeIAP();
    }

    // -------------------------------------------------------
    // Initialization
    // -------------------------------------------------------
    private void InitializeIAP()
    {
        if (_storeController != null)
            return; // already done

        var module  = StandardPurchasingModule.Instance();
        var builder = ConfigurationBuilder.Instance(module);

        if (string.IsNullOrEmpty(RemoveAdsProductId))
        {
            Debug.LogError("[IAP] Remove Ads product id is EMPTY. Fill it in SigilIAPManager inspector.");
        }
        else
        {
            Debug.Log("[IAP] Adding Remove Ads product: " + RemoveAdsProductId);
            builder.AddProduct(RemoveAdsProductId, ProductType.NonConsumable);
        }

        UnityPurchasing.Initialize(this, builder);
    }

    // -------------------------------------------------------
    // Public API – what your UI calls
    // -------------------------------------------------------

    /// <summary>
    /// Called by the "Remove Ads" button.
    /// </summary>
    public void BuyRemoveAds()
    {
        if (_storeController == null)
        {
            Debug.LogWarning("[IAP] BuyRemoveAds called but IAP is not initialized yet.");
            return;
        }

        Debug.Log("[IAP] Initiating purchase for Remove Ads: " + RemoveAdsProductId);
        _storeController.InitiatePurchase(RemoveAdsProductId);
    }

    /// <summary>
    /// Optional: a "Restore Purchases" button on iOS.
    /// </summary>
    public void RestorePurchases()
    {
#if UNITY_IOS
        if (_extensionProvider == null)
        {
            Debug.LogWarning("[IAP] RestorePurchases called but IAP not initialized yet.");
            return;
        }

        var apple = _extensionProvider.GetExtension<IAppleExtensions>();
        apple.RestoreTransactions(result =>
        {
            Debug.Log("[IAP] RestorePurchases completed. Result = " + result);
        });
#else
        Debug.Log("[IAP] RestorePurchases is only needed on iOS.");
#endif
    }

    // -------------------------------------------------------
    // IStoreListener implementation
    // -------------------------------------------------------
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("[IAP] OnInitialized");
        _storeController  = controller;
        _extensionProvider = extensions;

        // If the user already bought Remove Ads on this store account,
        // we should respect that immediately.
        var product = controller.products.WithID(RemoveAdsProductId);
        if (product != null && product.hasReceipt)
        {
            Debug.Log("[IAP] Remove Ads already purchased previously – granting now.");
            GrantRemoveAds();
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError("[IAP] Initialization failed: " + error);
    }

#if UNITY_2022_1_OR_NEWER
    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError("[IAP] Initialization failed: " + error + " - " + message);
    }
#endif

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        Debug.Log("[IAP] ProcessPurchase: " + args.purchasedProduct.definition.id);

        if (string.Equals(args.purchasedProduct.definition.id, RemoveAdsProductId, StringComparison.Ordinal))
        {
            Debug.Log("[IAP] Remove Ads purchase SUCCESS.");
            GrantRemoveAds();
        }
        else
        {
            Debug.LogWarning("[IAP] ProcessPurchase for unknown product id: " +
                             args.purchasedProduct.definition.id);
        }

        // we’re doing simple non-consumables, so just complete immediately
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning("[IAP] Purchase failed: " + product.definition.id +
                         " reason: " + failureReason);
    }

    // -------------------------------------------------------
    // Actually turning ads off
    // -------------------------------------------------------
    private void GrantRemoveAds()
    {
        Debug.Log("[IAP] GrantRemoveAds – setting RemoveAds flag & notifying SigilAdsManager.");

        PlayerPrefs.SetInt(RemoveAdsPrefsKey, 1);
        PlayerPrefs.Save();

        if (SigilAdsManager.Instance != null)
        {
            SigilAdsManager.Instance.OnAdsRemovedByPurchase();
        }
    }
}
