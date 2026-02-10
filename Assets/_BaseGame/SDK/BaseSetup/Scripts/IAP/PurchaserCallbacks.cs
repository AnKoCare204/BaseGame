using System.Collections.Generic;
using Cysharp.Threading.Tasks;
// using Manager;
using SDK;
using SDK.Analytics;
using UnityEngine.Purchasing;

public class PurchaserCallbacks
{
    private Purchaser purchaser;

    public PurchaserCallbacks(Purchaser purchaser)
    {
        this.purchaser = purchaser;
    }

    public void OnInitialProductsFetched(List<Product> products)
    {
        PurchaserLogger.Log("===========");
        PurchaserLogger.Log("OnInitialProductsFetched:");
        PurchaserLogger.LogFetchedProducts(products);
        purchaser.FetchExistingPurchases();
    }

    public void OnInitialProductsFetchFailed(ProductFetchFailed failure)
    {
        PurchaserLogger.Log("===========");
        PurchaserLogger.Log("OnInitialProductsFetchFailed:");
        PurchaserLogger.Log(failure.FailureReason);
    }

    public void OnExistingPurchasesFetched(Orders existingOrders)
    {
        PurchaserLogger.Log("===========");
        PurchaserLogger.Log("OnExistingPurchasesFetched:");
        PurchaserLogger.Log(Purchaser.IsReceiptAvailable(existingOrders)
            ? "Success - Found Existing Orders with receipts"
            : "Notice: - No Existing Orders with receipts");
    }

    public void OnExistingPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        PurchaserLogger.Log("===========");
        PurchaserLogger.Log("OnExistingPurchasesFetchFailed:");
        PurchaserLogger.Log(failure.Message);
    }

    public void OnPurchasePending(PendingOrder order)
    {
        foreach (var cartItem in order.CartOrdered.Items())
        {
            var product = cartItem.Product;

            PurchaserLogger.LogCompletedPurchase(product, order.Info);
            purchaser.ValidatePurchaseIfPossible(order.Info);
        }

        purchaser.ConfirmOrderIfAutomatic(order);
    }

    public void OnPurchaseConfirmed(Order order)
    {
        switch (order)
        {
            case FailedOrder failedOrder:
                OnConfirmationFailed(failedOrder);
                break;
            case ConfirmedOrder confirmedOrder:
                OnPurchaseConfirmed(confirmedOrder);
                break;
        }
    }

    private void OnConfirmationFailed(FailedOrder failedOrder)
    {
        var reason = failedOrder.FailureReason;

        foreach (var cartItem in failedOrder.CartOrdered.Items())
        {
            PurchaserLogger.LogFailedConfirmation(cartItem.Product, reason);
        }
        purchaser.m_IAPProduct.OnFail?.Invoke();
        purchaser.m_IAPProduct = null;
        // ActivityBlockContext.Events.WaitForPurchase?.Invoke(false);
    }

    public void OnPurchaseConfirmed(ConfirmedOrder order)
    {
        foreach (var cartItem in order.CartOrdered.Items())
        {
            var product = cartItem.Product;

            PurchaserLogger.LogConfirmedOrder(product, order.Info);
            OnBuyIAPSuccess(product);

        }

        purchaser.m_IAPProduct?.OnSuccess?.Invoke();
        purchaser.m_IAPProduct = null;
        // UserDataManager.Instance.UploadUserDataToServer(false, false).Forget();
        // ActivityBlockContext.Events.WaitForPurchase?.Invoke(false);
        
    }

    public void OnPurchaseFailed(FailedOrder failedOrder)
    {
        var reason = failedOrder.FailureReason;

        foreach (var cartItem in failedOrder.CartOrdered.Items())
        {
            PurchaserLogger.LogFailedPurchase(cartItem.Product, reason);
        }
        purchaser.m_IAPProduct.OnFail?.Invoke();
        purchaser.m_IAPProduct = null;
        // ActivityBlockContext.Events.WaitForPurchase?.Invoke(false);
    }

    public void OnOrderDeferred(DeferredOrder deferredOrder)
    {
        foreach (var cartItem in deferredOrder.CartOrdered.Items())
        {
            PurchaserLogger.LogDeferredPurchase(cartItem.Product);
        }
        purchaser.m_IAPProduct.OnFail?.Invoke();
        purchaser.m_IAPProduct = null;
        // ActivityBlockContext.Events.WaitForPurchase?.Invoke(false);
    }
    public void OnBuyIAPSuccess(Product product)
    {
        string productID = product.definition.id;
        decimal num = product.metadata.localizedPrice;
        string currencyCode = product.metadata.isoCurrencyCode;
        // if (GameManager.Instance.BuildType == BuildType.Release)
        // {
        //     AppsFlyerManager.TrackAppsFlyerPurchase(productID, num, currencyCode);
        //     // AnalyticsManager.TrackIAPFakeRevenue(num, currencyCode);
            
        //     // Update InMobi publisher signals with IAP purchase
        //     // ABIInMobiPublisherSignalsManager.OnIAPPurchase(num, currencyCode);
        // }
        
        EventManager.TriggerEvent("BuyIAPSuccess");
        // InGameAnalyticController.EventTrackIAPConfirm?.Invoke(productID, "shop");
        
        if (productID == "removeads" || productID == "removeadssale")
        {
            // InGameDataManager.Instance.InGameData.ResourceDataSave.AddResourceValue(SpecialResourceType.NoAds, 1);
        }
    }
}