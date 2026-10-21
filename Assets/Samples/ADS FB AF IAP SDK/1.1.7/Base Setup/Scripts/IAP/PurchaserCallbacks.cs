using Core.UI.Activities;
using Cysharp.Threading.Tasks;
// using Manager;
using SDK;
using SDK.Analytics;
using System.Collections.Generic;
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

        // First launch after update: silently finish pre-fix pending orders
        // (already delivered by the old build) without granting, so users who
        // bought before the fix are not given the pack a second time.
        if (!purchaser.IsBacklogReconciled)
        {
            ReconcilePreFixBacklog(existingOrders);
        }
    }

    private void ReconcilePreFixBacklog(Orders existingOrders)
    {
        var pendingOrders = existingOrders?.PendingOrders;
        if (pendingOrders != null)
        {
            foreach (var order in pendingOrders)
            {
                string transactionId = order.Info?.TransactionID;
                foreach (var cartItem in order.CartOrdered.Items())
                {
                    string productID = cartItem.Product.definition.id;
                    purchaser.MarkBacklogTransactionAsDelivered(productID, transactionId);
                    PurchaserLogger.Log(
                        $"[Backlog] '{productID}' tx '{transactionId}' treated as already delivered by a previous build. Finishing without granting.");
                }

                // Finish the transaction so the store stops re-delivering it. The
                // resulting OnPurchaseConfirmed is skipped by the idempotency guard.
                purchaser.ConfirmBacklogOrder(order);
            }
        }

        purchaser.MarkBacklogReconciled();
    }

    public void OnExistingPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        PurchaserLogger.Log("===========");
        PurchaserLogger.Log("OnExistingPurchasesFetchFailed:");
        PurchaserLogger.Log(failure.Message);
    }

    public void OnPurchasePending(PendingOrder order)
    {
        string transactionId = order.Info?.TransactionID;

        foreach (var cartItem in order.CartOrdered.Items())
        {
            var product = cartItem.Product;

            PurchaserLogger.LogCompletedPurchase(product, order.Info);
            purchaser.ValidatePurchaseIfPossible(order.Info);

            // The confirmed order will not carry the transaction id, so remember
            // it now (per product) for the idempotency check at confirm time.
            purchaser.StashPendingTransaction(product.definition.id, transactionId);
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

        // A second confirm request for a transaction whose first confirm is
        // still running (the store delivered the same transaction twice).
        if (reason == PurchaseFailureReason.ExistingPurchasePending && IsEchoOfConfirmInFlight(failedOrder))
        {
            PurchaserLogger.Log($"Duplicate confirm request for tx '{failedOrder.Info?.TransactionID}'. Waiting for the first one.");
            return;
        }

        string transactionId = failedOrder.Info?.TransactionID;
        foreach (var cartItem in failedOrder.CartOrdered.Items())
        {
            PurchaserLogger.LogFailedConfirmation(cartItem.Product, reason);

            // This confirm is over, so its transaction is no longer in flight.
            string productID = cartItem.Product.definition.id;
            if (purchaser.IsConfirmInFlight(productID, transactionId))
            {
                purchaser.PopPendingTransaction(productID);
            }
        }
        purchaser.m_IAPProduct?.OnFail?.Invoke();
        purchaser.m_IAPProduct = null;
        ActivityNotificationOverlayContext.Events.HideLoadingBlock?.Invoke();
    }

    public void OnPurchaseConfirmed(ConfirmedOrder order)
    {
        foreach (var cartItem in order.CartOrdered.Items())
        {
            var product = cartItem.Product;
            string productID = product.definition.id;

            PurchaserLogger.LogConfirmedOrder(product, order.Info);

            // Idempotency: the transaction id was captured at the pending stage
            // (the confirmed order does not carry it). If we have already
            // delivered this transaction (store re-delivery of a pending /
            // deferred purchase across sessions), skip it so we never grant the
            // same purchase twice.
            string transactionId = purchaser.PopPendingTransaction(productID);
            if (!purchaser.TryBeginDeliverTransaction(transactionId))
            {
                PurchaserLogger.Log($"Transaction '{transactionId}' for '{productID}' already delivered. Skipping.");
                continue;
            }

            // Revenue / analytics tracking (AppsFlyer, EventManager).
            OnBuyIAPSuccess(product);

            // Deliver the items. Prefer the in-session shop callback (keeps the
            // reward popup / shop UX) when it matches this product; otherwise
            // fall back to central delivery by product id so deferred / pending /
            // restored / interrupted purchases still credit the user.
            bool inSessionHandled = purchaser.m_IAPProduct != null &&
                                    purchaser.m_IAPProduct.ProductId == productID &&
                                    purchaser.m_IAPProduct.OnSuccess != null;

            if (inSessionHandled)
            {
                purchaser.m_IAPProduct.OnSuccess.Invoke();
            }
            else
            {
                IAPRewardDelivery.GrantByProductId(productID);
            }
        }

        purchaser.m_IAPProduct = null;
        ActivityNotificationOverlayContext.Events.HideLoadingBlock?.Invoke();
    }

    public void OnPurchaseFailed(FailedOrder failedOrder)
    {
        var reason = failedOrder.FailureReason;

        // The purchase() result of a transaction that Transaction.updates
        // already delivered and that is being confirmed right now.
        if (reason == PurchaseFailureReason.DuplicateTransaction && IsEchoOfConfirmInFlight(failedOrder))
        {
            PurchaserLogger.Log($"Duplicate transaction '{failedOrder.Info?.TransactionID}' while its confirm is running. Waiting for it.");
            return;
        }

        foreach (var cartItem in failedOrder.CartOrdered.Items())
        {
            PurchaserLogger.LogFailedPurchase(cartItem.Product, reason);
        }
        purchaser.m_IAPProduct?.OnFail?.Invoke();
        purchaser.m_IAPProduct = null;
        ActivityNotificationOverlayContext.Events.HideLoadingBlock?.Invoke();
    }

    // StoreKit 2 can hand the same transaction over twice (the purchase()
    // result and Transaction.updates). The second copy comes back as a
    // duplicate failure while the first confirm is still running. That confirm
    // reports its own result, so the in-session purchase (m_IAPProduct, the
    // loading block) must stay untouched; clearing it here sent the confirmed
    // order to IAPRewardDelivery instead of the shop callback.
    private bool IsEchoOfConfirmInFlight(FailedOrder failedOrder)
    {
        string transactionId = failedOrder.Info?.TransactionID;
        foreach (var cartItem in failedOrder.CartOrdered.Items())
        {
            if (purchaser.IsConfirmInFlight(cartItem.Product.definition.id, transactionId))
            {
                return true;
            }
        }
        return false;
    }

    public void OnOrderDeferred(DeferredOrder deferredOrder)
    {
        // A deferred / pending order is NOT a failure. It means the payment is
        // still being processed (e.g. Pix / boleto / verified card on Android,
        // "Ask to Buy" awaiting parental approval on iOS). The money is not
        // charged yet and the items must NOT be granted here.
        //
        // Do NOT call OnFail (that would tell the shop the purchase failed) and
        // release the in-progress lock so the user isn't stuck on a spinner.
        // When the payment later settles, OnPurchaseConfirmed fires and the
        // items are delivered by product id via IAPRewardDelivery, even if this
        // happens in a later session.
        foreach (var cartItem in deferredOrder.CartOrdered.Items())
        {
            PurchaserLogger.LogDeferredPurchase(cartItem.Product);
        }
        purchaser.m_IAPProduct = null;
        ActivityNotificationOverlayContext.Events.HideLoadingBlock?.Invoke();
    }
    public void OnBuyIAPSuccess(Product product)
    {
        string productID = product.definition.id;
        decimal num = product.metadata.localizedPrice;
        string currencyCode = product.metadata.isoCurrencyCode;


#if RELEASE_ONLY
        if (PlayerHelperActive.Instance.GetIsHelperActive())
        {
            return;
        }
        AppsFlyerManager.TrackAppsFlyerPurchase(productID, num, currencyCode);
#endif
        
        EventManager.TriggerEvent("BuyIAPSuccess");
        // InGameAnalyticController.EventTrackIAPConfirm?.Invoke(productID, "shop");
        
        if (productID == "removeads" || productID == "removeadssale")
        {
            // InGameDataManager.Instance.InGameData.ResourceDataSave.AddResourceValue(SpecialResourceType.NoAds, 1);
        }
    }
}