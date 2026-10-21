using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.UI.Activities;
using Cysharp.Threading.Tasks;
using TW.Utility.DesignPattern;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using UnityEngine.Purchasing.Security;

public class Purchaser : Singleton<Purchaser>
{
    public IAPProduct m_IAPProduct = null;
    private IStoreService m_StoreService;
    private IProductService m_ProductService;
    private IPurchaseService m_PurchasingService;

    private ICatalogProvider m_CatalogProvider = new CatalogProvider();
    private CrossPlatformValidator m_CrossPlatformValidator;

    private readonly PurchaserCallbacks purchaserCallbacks;
    private bool IsPurchaseInProgress => m_IAPProduct != null;

    // --- Transaction idempotency ---------------------------------------------
    // Persisted set of transaction ids we have already delivered. The store may
    // re-deliver a pending/deferred transaction (Pix, boleto, Ask-to-Buy,
    // interrupted purchases) across sessions; this guarantees we never grant the
    // same purchase twice.
    private const string k_ProcessedTxPrefKey = "iap_processed_tx_v1";
    private const int k_MaxProcessedTx = 300;
    private List<string> m_ProcessedTransactions;

    private void EnsureProcessedTransactionsLoaded()
    {
        if (m_ProcessedTransactions != null) return;
        m_ProcessedTransactions = new List<string>();
        string raw = PlayerPrefs.GetString(k_ProcessedTxPrefKey, string.Empty);
        if (string.IsNullOrEmpty(raw)) return;
        foreach (string id in raw.Split('|'))
        {
            if (!string.IsNullOrEmpty(id)) m_ProcessedTransactions.Add(id);
        }
    }

    // The store only exposes a usable TransactionID on the PendingOrder; on the
    // ConfirmedOrder it is empty. We capture it at the pending stage keyed by
    // product id so the confirm stage can use it for idempotency.
    private readonly Dictionary<string, string> m_PendingTxByProduct = new Dictionary<string, string>();

    public void StashPendingTransaction(string productId, string transactionId)
    {
        if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(transactionId)) return;
        m_PendingTxByProduct[productId] = transactionId;
    }

    public string PopPendingTransaction(string productId)
    {
        if (string.IsNullOrEmpty(productId)) return null;
        if (m_PendingTxByProduct.TryGetValue(productId, out string txId))
        {
            m_PendingTxByProduct.Remove(productId);
            return txId;
        }
        return null;
    }

    // True while a confirm for this product's transaction has been sent but has
    // not come back yet (stashed at the pending stage, popped on its result).
    // An empty transaction id matches any in-flight confirm of the product.
    public bool IsConfirmInFlight(string productId, string transactionId)
    {
        if (string.IsNullOrEmpty(productId)) return false;
        if (!m_PendingTxByProduct.TryGetValue(productId, out string stashedTxId)) return false;
        return string.IsNullOrEmpty(transactionId) || stashedTxId == transactionId;
    }

    // Returns true if this transaction has NOT been delivered before (and marks
    // it as delivered). Returns false if it was already delivered, so callers
    // should skip granting. Empty/unknown ids are always allowed through.
    public bool TryBeginDeliverTransaction(string transactionId)
    {
        if (string.IsNullOrEmpty(transactionId)) return true;
        EnsureProcessedTransactionsLoaded();
        if (m_ProcessedTransactions.Contains(transactionId)) return false;

        m_ProcessedTransactions.Add(transactionId);
        if (m_ProcessedTransactions.Count > k_MaxProcessedTx)
        {
            m_ProcessedTransactions.RemoveRange(0, m_ProcessedTransactions.Count - k_MaxProcessedTx);
        }
        PlayerPrefs.SetString(k_ProcessedTxPrefKey, string.Join("|", m_ProcessedTransactions));
        PlayerPrefs.Save();
        return true;
    }

    // --- One-time backlog reconciliation -------------------------------------
    // When a user updates from an older build that already delivered a purchase
    // but left the store transaction unfinished, the store re-delivers that
    // transaction to this build as a fetched pending order. Because the
    // idempotency store above is empty for pre-fix purchases, the item would be
    // granted a SECOND time. To prevent that, on the first launch of this build
    // we stop auto-processing fetched pending orders, then silently finish the
    // pre-existing ones without granting (they were already delivered by the old
    // build) and mark them processed. This runs exactly once per device.
    private const string k_BacklogReconciledKey = "iap_backlog_reconciled_v1";

    public bool IsBacklogReconciled => PlayerPrefs.GetInt(k_BacklogReconciledKey, 0) == 1;

    public void SetProcessFetchedPendingOrders(bool value)
    {
        m_PurchasingService?.ProcessPendingOrdersOnPurchasesFetched(value);
    }

    // Mark a pre-fix (already delivered) transaction so the confirm callback it
    // triggers will be skipped, and remember its id per product for correlation.
    public void MarkBacklogTransactionAsDelivered(string productId, string transactionId)
    {
        StashPendingTransaction(productId, transactionId);
        TryBeginDeliverTransaction(transactionId);
    }

    public void ConfirmBacklogOrder(PendingOrder order)
    {
        ConfirmOrder(order);
    }

    public void MarkBacklogReconciled()
    {
        PlayerPrefs.SetInt(k_BacklogReconciledKey, 1);
        PlayerPrefs.Save();
        // Restore normal behaviour: from now on, fetched pending orders (e.g. a
        // deferred Pix/boleto purchase that settles in a later session) are
        // processed and delivered as usual.
        SetProcessFetchedPendingOrders(true);
    }

    public Purchaser()
    {
        purchaserCallbacks = new PurchaserCallbacks(this);
    }

    // Here we create the services that will be used by the PaywallManager.
    protected override void Awake()
    {
        base.Awake();
        CreateServices();
    }

    // Here we initialize the catalog, the IAP service, the cross platform validator and connect to the store.
    // If you want to initialize this automatically, change the function signature to "void Start()"
    public void Start()
    {
        InitCatalog();
        InitializeIapService();
        CreateCrossPlatformValidator();

        // On the first launch after updating to this build, do NOT auto-process
        // fetched pending orders. Any that come back are pre-fix purchases the
        // old build already delivered; OnExistingPurchasesFetched reconciles them
        // (finish without granting) so the user is not given items twice.
        if (!IsBacklogReconciled)
        {
            SetProcessFetchedPendingOrders(false);
        }

        ConnectToStore();
    }

    private void InitCatalog()
    {
        List<ProductDefinition> initialProductsToFetch = new List<ProductDefinition>();
        List<ShopPackageDataConfig> shopPackages = ShopGlobalConfig.Instance.shopPackages;
        for (int i = 0; i < shopPackages.Count; i++)
        {
            if (shopPackages[i].priceType != PriceType.IAP) continue;
            initialProductsToFetch.Add(new ProductDefinition(shopPackages[i].packageId,
                shopPackages[i].consumable ? ProductType.Consumable : ProductType.NonConsumable));
        }

        // Here are the definition of your catalog products.
        // You can modify or add products here matching the skuIds you define on your different platforms stores.
        // const string k_IapConsumable30GemsSkuID = "com.unity.iap.test.30.gems";
        // const string k_IapNonConsumableNoAdsSkuID = "com.unity.iap.test.no.ads";
        // const string k_IapSubscriptionAdventurePassSkuID = "com.unity.iap.test.adventure.pass";

        // var initialProductsToFetch = new List<ProductDefinition>
        // {
        //     new ProductDefinition(k_IapConsumable30GemsSkuID, ProductType.Consumable),
        //     new ProductDefinition(k_IapNonConsumableNoAdsSkuID, ProductType.NonConsumable),
        //     new ProductDefinition(k_IapSubscriptionAdventurePassSkuID, ProductType.Subscription)
        // };
        var storeSpecificIdsByProductId = new Dictionary<string, StoreSpecificIds>();
        // var adventurePassIds = new StoreSpecificIds
        // {
        //     { k_IapSubscriptionAdventurePassSkuID, UnityEngine.Purchasing.GooglePlay.Name },
        //     { k_IapSubscriptionAdventurePassSkuID, UnityEngine.Purchasing.AppleAppStore.Name }
        // };
        // storeSpecificIdsByProductId.Add(k_IapSubscriptionAdventurePassSkuID, adventurePassIds);

        m_CatalogProvider.AddProducts(initialProductsToFetch, storeSpecificIdsByProductId);
    }

    private void CreateServices()
    {
        m_StoreService = UnityIAPServices.DefaultStore();
        m_ProductService = UnityIAPServices.DefaultProduct();
        m_PurchasingService = UnityIAPServices.DefaultPurchase();
        ConfigureServiceCallbacks();
    }

    private void ConfigureServiceCallbacks()
    {
        ConfigureProductServiceCallbacks();
        ConfigurePurchasingServiceCallbacks();
    }

    private void ConfigureProductServiceCallbacks()
    {
        m_ProductService.OnProductsFetched += purchaserCallbacks.OnInitialProductsFetched;
        m_ProductService.OnProductsFetchFailed += purchaserCallbacks.OnInitialProductsFetchFailed;
    }

    private void ConfigurePurchasingServiceCallbacks()
    {
        m_PurchasingService.OnPurchasesFetched += purchaserCallbacks.OnExistingPurchasesFetched;
        m_PurchasingService.OnPurchasesFetchFailed += purchaserCallbacks.OnExistingPurchasesFetchFailed;
        m_PurchasingService.OnPurchasePending += purchaserCallbacks.OnPurchasePending;
        m_PurchasingService.OnPurchaseConfirmed += purchaserCallbacks.OnPurchaseConfirmed;
        m_PurchasingService.OnPurchaseFailed += purchaserCallbacks.OnPurchaseFailed;
        m_PurchasingService.OnPurchaseDeferred += purchaserCallbacks.OnOrderDeferred;
    }

    public void FetchExistingPurchases()
    {
        m_PurchasingService.FetchPurchases();
    }

    public void RestorePurchases(Action<bool, string> onComplete = null)
    {
        // The store may not be connected yet (cold start, no network). Calling
        // through would only get "Store not connected." back from the service,
        // so report it to the caller in the same shape as a real failure.
        if (m_PurchasingService == null)
        {
            OnTransactionsRestored(false, "Purchase service not ready.");
            onComplete?.Invoke(false, "Purchase service not ready.");
            return;
        }

        m_PurchasingService.RestoreTransactions((success, error) =>
        {
            OnTransactionsRestored(success, error);
            onComplete?.Invoke(success, error);
        });
    }

    private void OnTransactionsRestored(bool success, string error)
    {
        PurchaserLogger.Log($"Transactions restored: {success} - {error}");
    }

    public static bool IsReceiptAvailable(Orders existingOrders)
    {
        return existingOrders != null &&
               (existingOrders.ConfirmedOrders.Any(order => !string.IsNullOrEmpty(order.Info.Receipt)) ||
                existingOrders.PendingOrders.Any(order => !string.IsNullOrEmpty(order.Info.Receipt)));
    }

    private void InitializeIapService()
    {
        IAPService.Initialize(OnServiceInitialized,
            (message) => { PurchaserLogger.Log($"Initialization failed, IAP service dependency error: {message}"); });
    }

    private void CreateCrossPlatformValidator()
    {
        // m_CrossPlatformValidator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);
#if !UNITY_EDITOR
        try
        {
            if (CanCrossPlatformValidate())
            {
#if !DEBUG_STOREKIT_TEST
                m_CrossPlatformValidator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(),
                    Application.identifier);
#else
                    m_CrossPlatformValidator =
 new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);
#endif
            }
        }
        catch (NotImplementedException exception)
        {
            PurchaserLogger.Log("===========");
            PurchaserLogger.Log($"Cross Platform Validator Not Implemented: {exception}");
        }
        catch (Exception exception)
        {
            PurchaserLogger.Log("===========");
            PurchaserLogger.Log($"Cross Platform Validator Exception: {exception}");
        }
#endif
    }

    private void OnServiceInitialized()
    {
        // The IAP service is ready to use.
        PurchaserLogger.Log("===========");
        PurchaserLogger.Log("IAP Service initialized.");
    }

    private async UniTask ConnectToStore()
    {
        await m_StoreService.Connect();
        PurchaserLogger.Log("===========");
        PurchaserLogger.Log("Store Connected.");

        FetchInitialProducts();
    }

    private void FetchInitialProducts()
    {
        m_CatalogProvider.FetchProducts(m_ProductService.FetchProductsWithNoRetries,
            DefaultStoreHelper.GetDefaultStoreName());
    }

    public void BuyIAPProduct(IAPProduct product)
    {
        if (IsPurchaseInProgress) return;
        if (NetworkNotice.IsOffline)
        {
            NetworkNotice.ShowNoInternet();
            product.OnFail?.Invoke();
            return;
        }
        m_IAPProduct = product;
        // Bật loading TRƯỚC khi gọi store: InitiatePurchase tắt nó ngay khi store chưa có sản phẩm này.
        ActivityNotificationOverlayContext.Events.ShowLoadingBlock?.Invoke();
        InitiatePurchase(product);
    }

    public void InitiatePurchase(IAPProduct iapProduct)
    {
        Product product = FindProduct(iapProduct.ProductId);
        if (product != null)
        {
            m_PurchasingService?.PurchaseProduct(product);
        }
        else
        {
            PurchaserLogger.Log($"The product service has no product with the ID {iapProduct.ProductId}");
            m_IAPProduct = null;
            ActivityNotificationOverlayContext.Events.HideLoadingBlock?.Invoke();
            NetworkNotice.ShowStoreNotReady();
            iapProduct.OnFail?.Invoke();
            RefetchProducts();
        }
    }

    // Mở game lúc mất mạng thì lần tải sản phẩm đầu thất bại và store không tự thử lại: tải lại khi người chơi bấm mua.
    private void RefetchProducts()
    {
        if (m_StoreService == null) return;
        if (m_StoreService.GetConnectionState() == ConnectionState.Connected)
        {
            FetchInitialProducts();
        }
        else
        {
            ConnectToStore().Forget();
        }
    }

    public Product FindProduct(string productId)
    {
        return GetFetchedProducts()?.FirstOrDefault(product => product.definition.id == productId);
    }

    public ReadOnlyObservableCollection<Product> GetFetchedProducts()
    {
        return m_ProductService?.GetProducts();
    }

    public void ConfirmOrderIfAutomatic(PendingOrder order)
    {
        if (ShouldConfirmOrderAutomatically(order))
        {
            ConfirmOrder(order);
        }
    }

    private bool ShouldConfirmOrderAutomatically(PendingOrder order)
    {
        return true;
        // var containsItemToNotAutoConfirm = false;
        // var containsItemToAutoConfirm = false;
        //
        // foreach (var cartItem in order.CartOrdered.Items())
        // {
        //     var matchingButton = FindMatchingButtonByProduct(cartItem.Product.definition.id);
        //
        //     if (matchingButton)
        //     {
        //         if (matchingButton.consumePurchase)
        //         {
        //             containsItemToAutoConfirm = true;
        //         }
        //         else
        //         {
        //             containsItemToNotAutoConfirm = true;
        //         }
        //     }
        // }
        //
        // if (containsItemToNotAutoConfirm && containsItemToAutoConfirm)
        // {
        //     PurchaserLogger.Log("===========");
        //     PurchaserLogger.Log("Pending Order contains some products to not confirm. Confirming by default!");
        // }
        //
        // return containsItemToAutoConfirm;
    }


    private void ConfirmOrder(PendingOrder pendingOrder)
    {
        m_PurchasingService.ConfirmPurchase(pendingOrder);
    }

    public void ConfirmPendingPurchaseForId(string id)
    {
        var product = FindProduct(id);
        var order = product != null ? GetPendingOrder(product) : null;

        if (order != null)
        {
            ConfirmOrder(order);
        }
    }

    private PendingOrder GetPendingOrder(Product product)
    {
        var orders = m_PurchasingService.GetPurchases();

        foreach (var order in orders)
        {
            if (order is PendingOrder pendingOrder &&
                pendingOrder.CartOrdered.Items().First()?.Product.definition.storeSpecificId ==
                product.definition.storeSpecificId)
            {
                return pendingOrder;
            }
        }

        return null;
    }

    public void ValidatePurchaseIfPossible(IOrderInfo orderInfo)
    {
        if (CanCrossPlatformValidate() && m_CrossPlatformValidator != null)
        {
            ValidatePurchase(orderInfo);
        }
    }

    private bool CanCrossPlatformValidate()
    {
        return IsGooglePlay() ||
               IsAppleStore();
    }

    private void ValidatePurchase(IOrderInfo orderInfo)
    {
        try
        {
            // Kiểm tra null để tránh NullReferenceException
            if (m_CrossPlatformValidator == null)
            {
                PurchaserLogger.Log("CrossPlatformValidator is null, skipping validation.");
                return;
            }

            if (orderInfo == null)
            {
                PurchaserLogger.Log("OrderInfo is null, cannot validate purchase.");
                return;
            }

            if (string.IsNullOrEmpty(orderInfo.Receipt))
            {
                PurchaserLogger.Log("Receipt is null or empty, cannot validate purchase.");
                return;
            }

            var result = m_CrossPlatformValidator.Validate(orderInfo.Receipt);

            if (IsGooglePlay() || IsAppleStore())
            {
                PurchaserLogger.Log("Validated Receipt. Contents:");
                foreach (IPurchaseReceipt productReceipt in result)
                {
                    PurchaserLogger.LogReceiptValidation(productReceipt);
                }
            }
            else
            {
                PurchaserLogger.Log("Validated Receipt.");
            }
        }
        catch (IAPSecurityException ex)
        {
            PurchaserLogger.Log("Invalid receipt, not unlocking content. " + ex);
        }
        catch (Exception ex)
        {
            PurchaserLogger.Log("Error validating purchase: " + ex.Message);
        }
    }

    private bool IsGooglePlay()
    {
        return Application.platform == RuntimePlatform.Android &&
               DefaultStoreHelper.GetDefaultStoreName() == UnityEngine.Purchasing.GooglePlay.Name;
    }

    private bool IsAppleStore()
    {
        return (Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.OSXPlayer ||
                Application.platform == RuntimePlatform.tvOS) &&
               DefaultStoreHelper.GetDefaultStoreName() == UnityEngine.Purchasing.AppleAppStore.Name;
    }
}