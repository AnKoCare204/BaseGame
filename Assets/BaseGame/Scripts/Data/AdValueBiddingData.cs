using DataModule;
using Manager;
using UnityEngine;

/// <summary>
/// Bộ đếm cho các event value bidding của Google Ads (watch_ad_10, watch_ad_20, booster_spend_10).
/// Theo Strategy 1 của Google (Bamboo Event Bidding): đếm trên cả vòng đời của player, cache trên
/// máy, KHÔNG BAO GIỜ reset. Doanh thu là tổng tuyệt đối tính bằng USD kể từ khi cài game.
/// </summary>
[System.Serializable]
[DataGenerate(20)]
public class AdValueBiddingData : IDataModule
{
    public static AdValueBiddingData Instance => InGameDataManager.Instance.InGameData.AdValueBiddingData;
    public string Key => nameof(AdValueBiddingData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;

    /// <summary>Tổng doanh thu ads (mọi format) tính bằng USD kể từ khi cài game.</summary>
    public double TotalAdRevenueUsd;
    /// <summary>Tổng số interstitial đã xem kể từ khi cài game.</summary>
    public int InterstitialCount;
    /// <summary>Tổng số booster đã dùng kể từ khi cài game (mỗi booster_spend = 1).</summary>
    public int BoosterSpendCount;

    // Mốc TotalAdRevenueUsd tại lần bắn gần nhất của từng event; -1 = chưa bắn lần nào.
    // Lần bắn đầu mang toàn bộ doanh thu kể từ khi cài, các lần sau mang phần phát sinh kể từ lần bắn trước.
    public double LastSentRevenueWatchAd10 = -1;
    public double LastSentRevenueWatchAd20 = -1;
    public double LastSentRevenueBoosterSpend10 = -1;

    public void SetDefault()
    {
        TotalAdRevenueUsd = 0;
        InterstitialCount = 0;
        BoosterSpendCount = 0;
        LastSentRevenueWatchAd10 = -1;
        LastSentRevenueWatchAd20 = -1;
        LastSentRevenueBoosterSpend10 = -1;
    }

    public void Migrate()
    {
    }

    public string ToJson()
    {
        return DataSerializer.ToJson(this);
    }

    public IDataModule FromJson(string json)
    {
        return DataSerializer.FromJson<AdValueBiddingData>(json);
    }
}
