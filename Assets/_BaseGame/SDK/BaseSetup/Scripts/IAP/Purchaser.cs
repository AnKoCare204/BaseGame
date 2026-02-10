using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        ConnectToStore();
    }

    private void InitCatalog()
    {
        List<ProductDefinition> initialProductsToFetch = new List<ProductDefinition>();
        // List<ShopPackageDataConfig> shopPackages = ShopGlobalConfig.Instance.shopPackages;
        // for (int i = 0; i < shopPackages.Count; i++)
        // {
        //     if (shopPackages[i].priceType != PriceType.IAP) continue;
        //     initialProductsToFetch.Add(new ProductDefinition(shopPackages[i].packageId,
        //         shopPackages[i].nonConsume ? ProductType.NonConsumable : ProductType.Consumable));
        // }

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

    public void RestorePurchases()
    {
        m_PurchasingService.RestoreTransactions(OnTransactionsRestored);
    }

    private void OnTransactionsRestored(bool success, string error)
    {
        PurchaserLogger.Log("Transactions restored: " + success);
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
            (message) =>
            {
                PurchaserLogger.Log($"Initialization failed, IAP service dependency error: {message}");
            });
    }

    private void CreateCrossPlatformValidator()
    {
#if !UNITY_EDITOR
            try
            {
                if (CanCrossPlatformValidate())
                {
#if !DEBUG_STOREKIT_TEST
                    m_CrossPlatformValidator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);
#else
                    m_CrossPlatformValidator = new CrossPlatformValidator(GooglePlayTangle.Data(), AppleTangle.Data(), Application.identifier);
#endif
                }
            }
            catch (NotImplementedException exception)
            {
                PurchaserLogger.Log("===========");
                PurchaserLogger.Log($"Cross Platform Validator Not Implemented: {exception}");
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
        m_IAPProduct = product;
        InitiatePurchase(product);
        // ActivityBlockContext.Events.WaitForPurchase?.Invoke(true);
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
            // ActivityBlockContext.Events.WaitForPurchase?.Invoke(false);
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
        if (CanCrossPlatformValidate())
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