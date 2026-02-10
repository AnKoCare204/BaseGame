using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;

public static class PurchaserLogger
{
    private static bool IsShowing => true;
#if RELEASE_ONLY
    private static bool IsShowing => false;
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log(string message)
    {
        if (!IsShowing) return;
        Debug.Log($"<color=green>[Purchaser]</color> {message}");
    }

    public static void LogFetchedProducts(List<Product> products)
    {
        if (products.Count > 0)
        {
            foreach (var product in products)
            {
                Log($"Fetched {product.definition.id}");
            }
        }
        else
        {
            Log("No Products Fetched.");
        }
    }

    public static void LogReceiptValidation(IPurchaseReceipt productReceipt)
    {
        Log(
            $"Product ID: '{productReceipt.productID}', Date: '{productReceipt.purchaseDate}', Transaction ID: '{productReceipt.transactionID}'");
        LogGooglePlayReceiptValidationInfo(productReceipt);
        LogAppleReceiptValidationInfo(productReceipt);
    }

    public static void LogGooglePlayReceiptValidationInfo(IPurchaseReceipt productReceipt)
    {
        GooglePlayReceipt googleReceipt = productReceipt as GooglePlayReceipt;
        if (googleReceipt != null)
        {
            Log($"GooglePlay - State: '{googleReceipt.purchaseState}', Token: '{googleReceipt.purchaseToken}'");
        }
    }

    public static void LogAppleReceiptValidationInfo(IPurchaseReceipt productReceipt)
    {
        AppleInAppPurchaseReceipt appleReceipt = productReceipt as AppleInAppPurchaseReceipt;
        if (appleReceipt != null)
        {
            Log(
                $"Apple - Original Transaction: '{appleReceipt.originalTransactionIdentifier}', Expiration Date : '{appleReceipt.subscriptionExpirationDate}', Cancellation Date : '{appleReceipt.cancellationDate}', Quandtity : '{appleReceipt.quantity}'");
        }
    }

    public static void LogCompletedPurchase(Product product, IOrderInfo orderInfo)
    {
        Log("===========");
        Log($"Purchased Product: '{product.definition.id}'");
        Log($"Product transaction id: {orderInfo.TransactionID}.");
        Log($"Product receipt length: {orderInfo.Receipt?.Length}.");
        Log($"Product Type: '{product.definition.type}'");
    }

    public static void LogFailedConfirmation(Product cartItemProduct, PurchaseFailureReason reason)
    {
        Log("===========");
        Log($"Failed to confirm Product: '{cartItemProduct.definition.id}'");
        Log($"Reason: {reason}");
    }

    public static void LogConfirmedOrder(Product product, IOrderInfo orderInfo)
    {
        Log("===========");
        Log($"Confirmed Product: '{product.definition.id}'");
        Log($"Product transaction id: {orderInfo.TransactionID}.");
        Log($"Product receipt length: {orderInfo.Receipt?.Length}.");
        Log($"Product Type: '{product.definition.type}'");
    }

    public static void LogFailedPurchase(Product cartItemProduct, PurchaseFailureReason reason)
    {
        Log("===========");
        Log("PurchaseFailed");
        Log($"Product: '{cartItemProduct.definition.storeSpecificId}'");
        Log($"FailureReason: {reason.ToString()}.");
    }

    public static void LogDeferredPurchase(Product cartItemProduct)
    {
        Log("===========");
        Log("PurchaseDeferred");
        Log($"Product: '{cartItemProduct.definition.storeSpecificId}'");
    }
}