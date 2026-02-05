using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

public class SigilIAPManagerV2 : MonoBehaviour, IStoreListener
{
    public static SigilIAPManagerV2 Instance { get; private set; }

    // Product ID – must match EXACTLY on Apple & Google consoles
    public const string ProductId_RemoveAds = "com.morrowmoss.sigilshift.removeads";

    // PlayerPrefs key for the "ads removed" flag
    public const string RemoveAdsPrefsKey = "RemoveAdsPurchased";

    private static IStoreController storeController;
    private static IExtensionProvider storeExtensionProvider;

    /// <summary>
    /// True if this device/account owns the Remove Ads purchase.
    /// </summary>
    public bool HasRemovedAds { get; private set; }

    // ------------------------------------------------------------
    //  Singleton
    // ------------------------------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load persisted "ads removed" state
        HasRemovedAds = PlayerPrefs.GetInt(RemoveAdsPrefsKey, 0) == 1;
    }

    private void Start()
    {
        if (!IsInitialized())
        {
            InitializePurchasing();
        }
    }

    // ------------------------------------------------------------
    //  Init
    // ------------------------------------------------------------
    public void InitializePurchasing()
    {
        if (IsInitialized())
            return;

        var module  = StandardPurchasingModule.Instance();
        var builder = ConfigurationBuilder.Instance(module);

        // One non-consumable: Remove Ads
        builder.AddProduct(ProductId_RemoveAds, ProductType.NonConsumable);

        Debug.Log("[IAP] Initializing Unity IAP...");
        UnityPurchasing.Initialize(this, builder);
    }

    private bool IsInitialized()
    {
        return storeController != null && storeExtensionProvider != null;
    }

    // ------------------------------------------------------------
    //  Public API called by UI
    // ------------------------------------------------------------

    /// <summary>
    /// Called by your "Remove Ads" button.
    /// </summary>
    public void BuyRemoveAds()
    {
        if (HasRemovedAds)
        {
            Debug.Log("[IAP] Remove Ads already purchased on this device.");
            return;
        }

        if (!IsInitialized())
        {
            Debug.LogWarning("[IAP] BuyRemoveAds called but IAP not initialized.");
            return;
        }

        Product product = storeController.products.WithID(ProductId_RemoveAds);
        if (product != null && product.availableToPurchase)
        {
            Debug.Log($"[IAP] Initiating purchase: {product.definition.id}");
            storeController.InitiatePurchase(product);
        }
        else
        {
            Debug.LogWarning("[IAP] Remove Ads product not available to purchase.");
        }
    }

    /// <summary>
    /// iOS / macOS restore flow. On Android, Google auto-restores.
    /// </summary>
    public void RestorePurchases()
    {
#if UNITY_IOS || UNITY_STANDALONE_OSX
        if (!IsInitialized())
        {
            Debug.LogWarning("[IAP] RestorePurchases called but IAP not initialized.");
            return;
        }

        Debug.Log("[IAP] Restoring purchases (Apple platforms).");
        var apple = storeExtensionProvider.GetExtension<IAppleExtensions>();

        // IMPORTANT: your IAP version's RestoreTransactions expects Action<bool, string>
        apple.RestoreTransactions((result, message) =>
        {
            Debug.Log($"[IAP] RestorePurchases result: {result}, message={message}");
            // Entitlements are still granted through ProcessPurchase when receipts are re-sent.
        });
#else
        Debug.Log("[IAP] RestorePurchases is only supported on Apple platforms.");
#endif
    }

    // ------------------------------------------------------------
    //  IStoreListener
    // ------------------------------------------------------------

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("[IAP] OnInitialized");
        storeController        = controller;
        storeExtensionProvider = extensions;

        // Check if Remove Ads already owned (receipt present)
        Product product = storeController.products.WithID(ProductId_RemoveAds);
        if (product != null && product.hasReceipt)
        {
            Debug.Log("[IAP] Remove Ads already owned (receipt present).");
            HasRemovedAds = true;
            PlayerPrefs.SetInt(RemoveAdsPrefsKey, 1);
            PlayerPrefs.Save();
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError("[IAP] OnInitializeFailed: " + error);
    }

#if UNITY_2017_1_OR_NEWER
    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"[IAP] OnInitializeFailed: {error} - {message}");
    }
#endif

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        Debug.Log("[IAP] ProcessPurchase: " + args.purchasedProduct.definition.id);

        if (string.Equals(args.purchasedProduct.definition.id, ProductId_RemoveAds,
                StringComparison.Ordinal))
        {
            Debug.Log("[IAP] Remove Ads purchase SUCCESS.");
            HasRemovedAds = true;

            PlayerPrefs.SetInt(RemoveAdsPrefsKey, 1);
            PlayerPrefs.Save();

            return PurchaseProcessingResult.Complete;
        }

        Debug.LogWarning("[IAP] ProcessPurchase for unhandled product: " +
                         args.purchasedProduct.definition.id);
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning($"[IAP] Purchase FAILED: {product.definition.id} – {failureReason}");
    }
}
