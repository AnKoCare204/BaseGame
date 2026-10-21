using System;
using BaseGame.Scripts;
using DataModule;
using Manager;
using UnityEngine;

namespace CoreData
{
    // Persistent state for the "auto-open ModalNoAds after watching an interstitial" feature.
    // Kept as its own data module so it does not affect any existing save data.
    [Serializable]
    [DataGenerate(15)]
    public class NoAdsPopupData : IDataModule
    {
        public static NoAdsPopupData Instance => InGameDataManager.Instance.InGameData.NoAdsPopupData;
        public string Key => nameof(NoAdsPopupData);
        public int TargetVersion => 1;
        [field: SerializeField] public int Version { get; set; } = 1;

        // UTC date (MM/dd/yyyy ...) the counters below belong to.
        [field: SerializeField] public string ShowDate { get; set; } = "";
        [field: SerializeField] public int ShowCountToday { get; set; } = 0;
        // Full UTC timestamp of the last time the popup was shown (for the capping gap).
        [field: SerializeField] public string LastShowTime { get; set; } = "";

        public void SetDefault()
        {
            ShowDate = "";
            ShowCountToday = 0;
            LastShowTime = "";
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
            return DataSerializer.FromJson<NoAdsPopupData>(json);
        }

        // Resets the per-day counters when the UTC day changes.
        public void EnsureToday()
        {
            DateTime today = DateTimeManager.UtcNow.Date;
            DateTime savedDate = ShowDate.ToDateTime();
            if (savedDate.Date == today) return;
            ShowDate = today.ToEnUsString();
            ShowCountToday = 0;
            LastShowTime = "";
            this.SaveData();
        }

        public bool CanShow(int showPerDay, int gapSeconds)
        {
            EnsureToday();
            if (ShowCountToday >= showPerDay) return false;
            if (ShowCountToday == 0) return true; // first show of the day: no capping gap required
            DateTime lastShow = LastShowTime.ToDateTime();
            if (lastShow == default) return true;
            return (DateTimeManager.UtcNow - lastShow).TotalSeconds >= gapSeconds;
        }

        public void RegisterShown()
        {
            EnsureToday();
            ShowCountToday++;
            LastShowTime = DateTimeManager.UtcNow.ToEnUsString();
            this.SaveData();
        }
    }
}
