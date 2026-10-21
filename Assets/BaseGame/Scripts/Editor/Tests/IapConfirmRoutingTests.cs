using System;
using System.Collections.Generic;
using System.Reflection;
using Core.UI.Activities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Purchasing;

/// Mua IAP trong shop phải luôn trả thưởng qua callback của shop (popup "Tap to Claim"), kể cả khi Unity IAP (StoreKit 2)
/// bắn thêm một FailedOrder trùng giao dịch ngay trước ConfirmedOrder. Trước đây FailedOrder đó xoá m_IAPProduct,
/// ConfirmedOrder tới sau rơi vào IAPRewardDelivery (popup "CLAIM") và bỏ qua luồng riêng của gói (fail_offer revive, NoAds...).
/// Gọi thẳng PurchaserCallbacks với order dựng tay: Purchaser không chạy Awake nên không có store thật.
public class IapConfirmRoutingTests
{
    private const string ProductId = "iap_confirm_routing_test_pack";
    private const string ProcessedTxPrefKey = "iap_processed_tx_v1";

    private readonly List<string> loadingEvents = new();
    private Action savedShowLoading;
    private Action savedHideLoading;
    private bool hadProcessedTxPref;
    private string savedProcessedTxPref;

    private GameObject host;
    private Purchaser purchaser;
    private PurchaserCallbacks callbacks;
    private int successCount;
    private int failCount;
    private string transactionId;

    [SetUp]
    public void SetUp()
    {
        loadingEvents.Clear();
        successCount = 0;
        failCount = 0;
        transactionId = "tx_" + Guid.NewGuid().ToString("N");

        savedShowLoading = ActivityNotificationOverlayContext.Events.ShowLoadingBlock;
        savedHideLoading = ActivityNotificationOverlayContext.Events.HideLoadingBlock;
        ActivityNotificationOverlayContext.Events.ShowLoadingBlock = () => loadingEvents.Add("show");
        ActivityNotificationOverlayContext.Events.HideLoadingBlock = () => loadingEvents.Add("hide");

        // TryBeginDeliverTransaction ghi PlayerPrefs của editor: giữ lại giá trị cũ để trả về sau test.
        hadProcessedTxPref = PlayerPrefs.HasKey(ProcessedTxPrefKey);
        savedProcessedTxPref = PlayerPrefs.GetString(ProcessedTxPrefKey, string.Empty);

        host = EditorUtility.CreateGameObjectWithHideFlags("IapConfirmRoutingTest", HideFlags.HideAndDontSave);
        purchaser = host.AddComponent<Purchaser>();
        callbacks = new PurchaserCallbacks(purchaser);
    }

    [TearDown]
    public void TearDown()
    {
        ActivityNotificationOverlayContext.Events.ShowLoadingBlock = savedShowLoading;
        ActivityNotificationOverlayContext.Events.HideLoadingBlock = savedHideLoading;
        if (hadProcessedTxPref) PlayerPrefs.SetString(ProcessedTxPrefKey, savedProcessedTxPref);
        else PlayerPrefs.DeleteKey(ProcessedTxPrefKey);
        PlayerPrefs.Save();
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
    }

    // Người chơi bấm mua trong shop: BuyIAPProduct gán m_IAPProduct, OnPurchasePending cất transaction id rồi gửi confirm.
    private void StartInSessionPurchaseWithConfirmInFlight()
    {
        purchaser.m_IAPProduct = new IAPProduct(ProductId, () => successCount++, () => failCount++);
        purchaser.StashPendingTransaction(ProductId, transactionId);
    }

    // Race A: OnPurchasePending tới 2 lần cho cùng giao dịch (kết quả purchase() + Transaction.updates),
    // lần confirm thứ hai bị ConfirmOrderUseCase trả FailedOrder ExistingPurchasePending ngay lập tức.
    [Test]
    public void DuplicateConfirmRequest_ThenConfirmed_UsesShopCallback()
    {
        StartInSessionPurchaseWithConfirmInFlight();

        callbacks.OnPurchaseConfirmed(new FailedOrder(PendingOrder(), PurchaseFailureReason.ExistingPurchasePending,
            "Duplicate order requested for confirmation."));
        callbacks.OnPurchaseConfirmed(ConfirmedOrder());

        Assert.AreEqual(1, successCount, "ConfirmedOrder không đi qua callback của shop");
        Assert.AreEqual(0, failCount);
        Assert.IsNull(purchaser.m_IAPProduct);
    }

    // Race B: Transaction.updates giao giao dịch trước, rồi kết quả purchase() tới với giao dịch đã ghi log
    // nên AppleStoreImpl bắn OnPurchaseFailed DuplicateTransaction trong khi confirm đầu vẫn đang chạy.
    [Test]
    public void DuplicateTransactionWhileConfirmInFlight_ThenConfirmed_UsesShopCallback()
    {
        StartInSessionPurchaseWithConfirmInFlight();

        callbacks.OnPurchaseFailed(new FailedOrder(PendingOrder(), PurchaseFailureReason.DuplicateTransaction,
            "Purchase has already been confirmed."));
        callbacks.OnPurchaseConfirmed(ConfirmedOrder());

        Assert.AreEqual(1, successCount, "ConfirmedOrder không đi qua callback của shop");
        Assert.AreEqual(0, failCount);
        Assert.IsNull(purchaser.m_IAPProduct);
    }

    // Lỗi trùng chỉ là tiếng vọng: không được tắt loading giữa chừng.
    [Test]
    public void DuplicateFailureWhileConfirmInFlight_KeepsLoadingAndPurchaseLock()
    {
        StartInSessionPurchaseWithConfirmInFlight();

        callbacks.OnPurchaseFailed(new FailedOrder(PendingOrder(), PurchaseFailureReason.DuplicateTransaction, "dup"));
        callbacks.OnPurchaseConfirmed(new FailedOrder(PendingOrder(), PurchaseFailureReason.ExistingPurchasePending, "dup"));

        CollectionAssert.DoesNotContain(loadingEvents, "hide");
        Assert.IsNotNull(purchaser.m_IAPProduct);
    }

    // Không có confirm nào đang chạy (vd. mua lại gói đã xác nhận): vẫn là lỗi thật, phải nhả khoá, không kẹt loading.
    [Test]
    public void DuplicateTransactionWithoutConfirmInFlight_FailsPurchase()
    {
        purchaser.m_IAPProduct = new IAPProduct(ProductId, () => successCount++, () => failCount++);

        callbacks.OnPurchaseFailed(new FailedOrder(PendingOrder(), PurchaseFailureReason.DuplicateTransaction, "dup"));

        Assert.AreEqual(1, failCount);
        Assert.IsNull(purchaser.m_IAPProduct);
        CollectionAssert.Contains(loadingEvents, "hide");
    }

    // Confirm thật sự thất bại: báo fail, nhả khoá, và giao dịch không còn bị coi là đang confirm.
    [Test]
    public void ConfirmFailedForReal_FailsPurchaseAndClearsInFlight()
    {
        StartInSessionPurchaseWithConfirmInFlight();

        callbacks.OnPurchaseConfirmed(new FailedOrder(PendingOrder(), PurchaseFailureReason.Unknown, "finish failed"));

        Assert.AreEqual(1, failCount);
        Assert.IsNull(purchaser.m_IAPProduct);
        CollectionAssert.Contains(loadingEvents, "hide");
        Assert.IsFalse(purchaser.IsConfirmInFlight(ProductId, transactionId));
    }

    [Test]
    public void UserCancelled_FailsPurchase()
    {
        purchaser.m_IAPProduct = new IAPProduct(ProductId, () => successCount++, () => failCount++);

        callbacks.OnPurchaseFailed(new FailedOrder(Cart(), PurchaseFailureReason.UserCancelled, "User cancelled"));

        Assert.AreEqual(1, failCount);
        Assert.AreEqual(0, successCount);
        Assert.IsNull(purchaser.m_IAPProduct);
        CollectionAssert.Contains(loadingEvents, "hide");
    }

    [Test]
    public void NormalPurchase_UsesShopCallback()
    {
        StartInSessionPurchaseWithConfirmInFlight();

        callbacks.OnPurchaseConfirmed(ConfirmedOrder());

        Assert.AreEqual(1, successCount);
        Assert.IsNull(purchaser.m_IAPProduct);
    }

    private PendingOrder PendingOrder() => new PendingOrder(Cart(), new FakeOrderInfo(transactionId));

    // ConfirmedOrder thật (ConfirmOrderUseCase) mang Info của PendingOrder; callback không đọc transaction id ở bước này.
    private ConfirmedOrder ConfirmedOrder() => new ConfirmedOrder(Cart(), new FakeOrderInfo(transactionId));

    private static Cart Cart()
    {
        // Product chỉ có constructor internal: dùng factory của chính package như khi store trả sản phẩm lạ.
        MethodInfo factory = typeof(Product).GetMethod("CreateUnknownProduct", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(string) }, null);
        var product = (Product)factory.Invoke(null, new object[] { ProductId });
        return new Cart(new CartItem(product));
    }

    private sealed class FakeOrderInfo : IOrderInfo
    {
        public FakeOrderInfo(string transactionId) => TransactionID = transactionId;

        public IAppleOrderInfo Apple => null;
        public IGoogleOrderInfo Google => null;
        public IPaymentProvidersOrderInfo PaymentProviders => null;
        public List<IPurchasedProductInfo> PurchasedProductInfo { get; set; } = new();
        public string Receipt => string.Empty;
        public string TransactionID { get; }
    }
}
