using System;
using System.Collections.Generic;
using Core.UI.Activities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// Mua IAP / xem reward ads khi mất mạng hoặc store chưa tải được sản phẩm: phải báo cho người chơi và không kẹt loading.
/// Purchaser dựng trên object HideAndDontSave, không chạy Awake nên store chưa có sản phẩm nào (giống lúc mở game mất mạng).
public class NetworkNoticeTests
{
    private const string MissingProductId = "network_notice_test_missing_pack";

    private readonly List<string> loadingEvents = new();
    private readonly List<string> errors = new();
    private Action savedShowLoading;
    private Action savedHideLoading;
    private Action<string> savedShowError;

    private GameObject host;
    private Purchaser purchaser;
    private int failCount;

    [SetUp]
    public void SetUp()
    {
        loadingEvents.Clear();
        errors.Clear();
        failCount = 0;

        savedShowLoading = ActivityNotificationOverlayContext.Events.ShowLoadingBlock;
        savedHideLoading = ActivityNotificationOverlayContext.Events.HideLoadingBlock;
        savedShowError = ActivityNotificationOverlayContext.Events.ShowError;
        ActivityNotificationOverlayContext.Events.ShowLoadingBlock = () => loadingEvents.Add("show");
        ActivityNotificationOverlayContext.Events.HideLoadingBlock = () => loadingEvents.Add("hide");
        ActivityNotificationOverlayContext.Events.ShowError = message => errors.Add(message);

        NetworkNotice.SetOfflineOverride(false);

        host = EditorUtility.CreateGameObjectWithHideFlags("NetworkNoticeTest", HideFlags.HideAndDontSave);
        purchaser = host.AddComponent<Purchaser>();
    }

    [TearDown]
    public void TearDown()
    {
        ActivityNotificationOverlayContext.Events.ShowLoadingBlock = savedShowLoading;
        ActivityNotificationOverlayContext.Events.HideLoadingBlock = savedHideLoading;
        ActivityNotificationOverlayContext.Events.ShowError = savedShowError;
        NetworkNotice.ClearOfflineOverride();
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
    }

    private void Buy() => purchaser.BuyIAPProduct(new IAPProduct(MissingProductId, null, () => failCount++));

    private bool IsLoadingVisible => loadingEvents.Count > 0 && loadingEvents[loadingEvents.Count - 1] == "show";

    // Lỗi gốc: InitiatePurchase tắt loading rồi BuyIAPProduct mới bật, không còn ai tắt nữa.
    [Test]
    public void BuyIAPProduct_ProductMissing_EndsWithLoadingHidden()
    {
        Buy();
        Assert.IsFalse(IsLoadingVisible, "Loading còn bật: " + string.Join(",", loadingEvents));
    }

    [Test]
    public void BuyIAPProduct_ProductMissing_ShowsStoreNotReady()
    {
        Buy();
        CollectionAssert.AreEqual(new[] { NetworkNotice.StoreNotReadyMessage }, errors);
    }

    [Test]
    public void BuyIAPProduct_ProductMissing_InvokesOnFail()
    {
        Buy();
        Assert.AreEqual(1, failCount);
    }

    // Lần bấm sau không được bị chặn bởi IsPurchaseInProgress.
    [Test]
    public void BuyIAPProduct_ProductMissing_ReleasesPurchaseLock()
    {
        Buy();
        Assert.IsNull(purchaser.m_IAPProduct);
    }

    [Test]
    public void BuyIAPProduct_Offline_ShowsNoInternet()
    {
        NetworkNotice.SetOfflineOverride(true);
        Buy();
        CollectionAssert.AreEqual(new[] { NetworkNotice.NoInternetMessage }, errors);
    }

    [Test]
    public void BuyIAPProduct_Offline_NeverShowsLoading()
    {
        NetworkNotice.SetOfflineOverride(true);
        Buy();
        CollectionAssert.DoesNotContain(loadingEvents, "show");
    }

    [Test]
    public void BuyIAPProduct_Offline_InvokesOnFailAndReleasesLock()
    {
        NetworkNotice.SetOfflineOverride(true);
        Buy();
        Assert.AreEqual(1, failCount);
        Assert.IsNull(purchaser.m_IAPProduct);
    }

    [TestCase(true, NetworkNotice.NoInternetMessage)]
    [TestCase(false, NetworkNotice.AdsNotAvailableMessage)]
    public void ShowAdsUnavailable_PicksMessageByNetwork(bool offline, string expected)
    {
        NetworkNotice.SetOfflineOverride(offline);
        NetworkNotice.ShowAdsUnavailable();
        CollectionAssert.AreEqual(new[] { expected }, errors);
    }
}
