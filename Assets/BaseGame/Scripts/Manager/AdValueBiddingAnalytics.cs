using System;
using System.Collections.Generic;
using System.Globalization;
using DataModule;
using Manager;

/// <summary>
/// Event value bidding cho Google Ads theo spec ABI (AppLab 2026, Week 1):
///   watch_ad_10      - bắn khi user đã xem 10 interstitial, sau đó bắn lại ở MỌI interstitial tiếp theo.
///   watch_ad_20      - tương tự ở mốc 20.
///   booster_spend_10 - bắn khi user đã dùng 10 booster, sau đó bắn lại ở MỌI lần dùng booster tiếp theo.
/// Param: value (string, USD) + currency ("USD").
///
/// Strategy 1 của Google: đếm trên cả vòng đời, cache trên máy (AdValueBiddingData), không reset.
/// value luôn là doanh thu ads tuyệt đối bằng USD, không phải số đếm: lần bắn đầu của mỗi event mang
/// TOÀN BỘ doanh thu ads kể từ khi cài; các lần sau mang phần doanh thu phát sinh kể từ lần bắn trước
/// của chính event đó (với watch_ad_* thì đó là doanh thu của ad vừa xem, cộng thêm banner/rewarded
/// xen giữa nếu có) - cộng dồn mọi lần bắn luôn bằng đúng tổng doanh thu, không đếm trùng.
/// Player có doanh thu $0.00 vẫn bắn với value 0. Chỉ tính ad revenue, không join IAP.
/// in_app_purchase là event chuẩn của Firebase, đã tự bắn sẵn - không làm ở đây.
/// </summary>
public static class AdValueBiddingAnalytics
{
    private const string EventWatchAd10 = "watch_ad_10";
    private const string EventWatchAd20 = "watch_ad_20";
    private const string EventBoosterSpend10 = "booster_spend_10";
    private const int WatchAd10Threshold = 10;
    private const int WatchAd20Threshold = 20;
    private const int BoosterSpend10Threshold = 10;
    private const string Currency = "USD";

    // Ad revenue có thể về trước khi InGameDataManager load xong (app open ad lúc khởi động):
    // giữ lại và áp vào lần gọi kế tiếp để không mất đồng nào.
    private static readonly List<Action> Pending = new List<Action>();

    // Không dùng InGameDataManager.Instance != null: getter đó tự tạo singleton khi chưa có.
    private static bool IsDataReady => InGameDataManager.IsDataLoaded;

    /// <summary>
    /// Gọi ở MỌI impression ads đã được trả tiền (mọi mediation, mọi format).
    /// Cộng doanh thu; nếu là interstitial thì tăng bộ đếm và xét watch_ad_10 / watch_ad_20.
    /// </summary>
    public static void OnAdRevenuePaid(double revenueUsd, string adFormat)
    {
        bool isInterstitial = IsInterstitial(adFormat);
        Run(() =>
        {
            AdValueBiddingData data = AdValueBiddingData.Instance;
            if (revenueUsd > 0) data.TotalAdRevenueUsd += revenueUsd;
            if (isInterstitial)
            {
                data.InterstitialCount++;
                if (data.InterstitialCount >= WatchAd10Threshold)
                {
                    data.LastSentRevenueWatchAd10 = Fire(EventWatchAd10, data.TotalAdRevenueUsd, data.LastSentRevenueWatchAd10);
                }
                if (data.InterstitialCount >= WatchAd20Threshold)
                {
                    data.LastSentRevenueWatchAd20 = Fire(EventWatchAd20, data.TotalAdRevenueUsd, data.LastSentRevenueWatchAd20);
                }
            }
            data.SaveData();
        });
    }

    /// <summary>Gọi mỗi khi user dùng 1 booster (mọi chỗ đang bắn booster_spend).</summary>
    public static void OnBoosterSpent()
    {
        Run(() =>
        {
            AdValueBiddingData data = AdValueBiddingData.Instance;
            data.BoosterSpendCount++;
            if (data.BoosterSpendCount == BoosterSpend10Threshold)
            {
                data.LastSentRevenueBoosterSpend10 = Fire(EventBoosterSpend10, data.TotalAdRevenueUsd, data.LastSentRevenueBoosterSpend10);
            }
            data.SaveData();
        });
    }

    /// <summary>Bắn event với value = doanh thu phát sinh kể từ lần bắn trước (lần đầu = toàn bộ). Trả về mốc mới.</summary>
    private static double Fire(string eventName, double totalRevenueUsd, double lastSentRevenueUsd)
    {
        double value = lastSentRevenueUsd < 0 ? totalRevenueUsd : Math.Max(0, totalRevenueUsd - lastSentRevenueUsd);
        EventTrackingManager.TrackEventFirebase?.Invoke(
            eventName,
            new AnalyticsParameter("value", value.ToString(CultureInfo.InvariantCulture)),
            new AnalyticsParameter("currency", Currency));
        return totalRevenueUsd;
    }

    private static bool IsInterstitial(string adFormat)
    {
        // MAX: "INTER"; AdMob: "Interstitial"
        return !string.IsNullOrEmpty(adFormat)
               && adFormat.IndexOf("inter", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void Run(Action action)
    {
        if (!IsDataReady)
        {
            Pending.Add(action);
            return;
        }

        if (Pending.Count > 0)
        {
            List<Action> flush = new List<Action>(Pending);
            Pending.Clear();
            foreach (Action pending in flush) pending();
        }

        action();
    }
}
