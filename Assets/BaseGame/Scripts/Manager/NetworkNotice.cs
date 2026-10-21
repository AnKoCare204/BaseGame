using Core.UI.Activities;
using UnityEngine;

/// <summary>
/// Thông báo cho người chơi khi mua IAP / xem reward ads thất bại vì mạng hoặc store (toast của ActivityNotificationOverlay).
/// SetOfflineOverride giả lập trạng thái mạng cho test trong Editor.
/// </summary>
public static class NetworkNotice
{
    public const string NoInternetMessage = "No internet connection.\nPlease check your connection and try again.";
    public const string AdsNotAvailableMessage = "Ads not available.\nPlease try again later.";
    public const string StoreNotReadyMessage = "Store is not ready.\nPlease try again.";

    private static bool? offlineOverride;

    /// <summary>Chỉ biết máy không có kết nối mạng nào (tắt wifi/4G); có wifi nhưng không ra được internet vẫn là false.</summary>
    public static bool IsOffline => offlineOverride ?? Application.internetReachability == NetworkReachability.NotReachable;

    /// <summary>Giả lập mất mạng / có mạng (test / Editor). Gọi ClearOfflineOverride để quay lại trạng thái thật.</summary>
    public static void SetOfflineOverride(bool offline) => offlineOverride = offline;

    public static void ClearOfflineOverride() => offlineOverride = null;

    public static void ShowNoInternet() => ShowError(NoInternetMessage);

    public static void ShowStoreNotReady() => ShowError(StoreNotReadyMessage);

    /// <summary>Reward ads không show được: mất mạng thì báo mạng, còn lại là chưa có ads.</summary>
    public static void ShowAdsUnavailable() => ShowError(IsOffline ? NoInternetMessage : AdsNotAvailableMessage);

    private static void ShowError(string message) => ActivityNotificationOverlayContext.Events.ShowError?.Invoke(message);
}
