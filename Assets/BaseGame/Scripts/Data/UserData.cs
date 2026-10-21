using TW.Reactive.CustomComponent;
using System;
using System.Collections.Generic;
using System.Globalization;
using DataModule;
using Manager;
using Sirenix.Utilities;
using UnityEngine;

namespace CoreData
{
    [System.Serializable]
    [DataGenerate(0)]
    public class UserData : IDataModule
    {
        public static UserData Instance => InGameDataManager.Instance.InGameData.UserData;
        public string Key => nameof(UserData);
        public int TargetVersion => 1;
        [field: SerializeField] public int Version { get; set; } = 1;
        public int CurrentLevel = 1;

        #region Level vượt kho level (xem LevelLoopResolver)

        /// <summary>Level trong kho đang được mượn để chơi. 0 = chưa mượn level nào.</summary>
        public int LoopSourceLevel = 0;

        /// <summary>Số level người chơi mà LoopSourceLevel được quay cho. Ghim lại để retry / mở lại game không đổi màn.</summary>
        public int LoopSourceForLevel = 0;

        /// <summary>Các level nguồn đã quay gần đây (cũ -> mới), để không lặp lại ngay.</summary>
        public List<int> RecentLoopSourceLevels = new();

        #endregion

        public bool LevelLoopMigrationApplied = false;
        public bool PendingNewLevelNotice = false;
        public string FirstSessionDay;
        public string LastSessionDay;
        public int SessionCount = 0;
        public int IAPCount = 0;
        public int AdsRewardCount = 0;
        public int AdsInterstitialCount = 0;

        public Reactive<int> CountAdsInterstitialInDay = new(0);
        public Reactive<string> LastDayCountAdsInterstitial = new("");
        public Reactive<int> CountShowPopupNoAdsInDay = new(0);
        public List<int> CheckPointRetentionRecordedDays = new();

        public int LevelCheckpoint = 0;
        public int LevelRetryCount = -1;

        /// <summary>Level mà LevelRetryCount thuộc về. 0 = save của bản app cũ chưa có field này (coi là level hiện tại).</summary>
        public int LevelRetryLevel = 0;
        public int CompleteTreetopMission;
        public List<int> LevelPlayed = new();

        public void SetLevelRetry(int retryCount, int level)
        {
            LevelRetryCount = retryCount;
            LevelRetryLevel = level;
            this.SaveData();
        }

        /// <summary>Số lần chơi lại của lượt sắp nạp ở level này (xem LevelRetryRule).</summary>
        public int GetNextRetry(int level) => LevelRetryRule.NextRetry(LevelRetryCount, LevelRetryLevel, level);

        /// <summary>True the first time this level is marked. Later calls for the same or an earlier level do nothing.</summary>
        public bool TryMarkLevelCheckpoint(int level)
        {
            if (level <= LevelCheckpoint) return false;
            LevelCheckpoint = level;
            this.SaveData();
            return true;
        }
        public void SetDefault()
        {
            CurrentLevel = 1;
            LoopSourceLevel = 0;
            LoopSourceForLevel = 0;
            RecentLoopSourceLevels = new List<int>();
            FirstSessionDay = null;
            LastSessionDay = null;
            SessionCount = 0;
            IAPCount = 0;
            AdsRewardCount = 0;
            AdsInterstitialCount = 0;
            CountAdsInterstitialInDay = new(0);
            LastDayCountAdsInterstitial = new("");
            CountShowPopupNoAdsInDay = new(0);
            CheckPointRetentionRecordedDays = new();
            LevelCheckpoint = 0;
            LevelRetryCount = -1;
            LevelRetryLevel = 0;
            CompleteTreetopMission = 0;
            LevelPlayed = new List<int>();
        }

        public void AddLevelPlayed(int level)
        {
            LevelPlayed ??= new List<int>();
            LevelPlayed.Add(level);
            while (LevelPlayed.Count > 20) LevelPlayed.RemoveAt(0);
            this.SaveData();
        }

        public int GetLastLevelPlayed()
        {
            if (LevelPlayed == null || LevelPlayed.Count == 0) return 0;
            return LevelPlayed[^1];
        }

        public void Migrate()
        {

        }

        /// <summary>Ghim level nguồn vừa quay cho <paramref name="playerLevel"/> và đẩy nó vào lịch sử chống lặp.</summary>
        public void SetLoopSourceLevel(int playerLevel, int sourceLevel, int keepRecentCount)
        {
            LoopSourceForLevel = playerLevel;
            LoopSourceLevel = sourceLevel;

            RecentLoopSourceLevels ??= new List<int>();
            RecentLoopSourceLevels.Remove(sourceLevel);
            RecentLoopSourceLevels.Add(sourceLevel);
            int keep = Mathf.Max(0, keepRecentCount);
            while (RecentLoopSourceLevels.Count > keep) RecentLoopSourceLevels.RemoveAt(0);

            this.SaveData();
        }

        public string ToJson()
        {
            return DataSerializer.ToJson(this);
        }

        public IDataModule FromJson(string json)
        {
            return DataSerializer.FromJson<UserData>(json);
        }
        public void ApplyLevelLoopMigration(int previousVersionMaxLevel)
        {
            if (LevelLoopMigrationApplied) return;
            LevelLoopMigrationApplied = true;

            if (previousVersionMaxLevel > 0 && CurrentLevel > previousVersionMaxLevel)
            {
                CurrentLevel = previousVersionMaxLevel;
                PendingNewLevelNotice = true; // tell the UI to show the "New Level" modal
            }

            this.SaveData();
        }
        public void MarkNewLevelNoticeShown()
        {
            if (!PendingNewLevelNotice) return;
            PendingNewLevelNotice = false;
            this.SaveData();
        }

        #region CheckPoint Retention

        public int GetCheckPointDayRetention()
        {
            EnsureFirstSessionDayForCheckPoint();
            DateTime firstDate = DateTime.Parse(FirstSessionDay, CultureInfo.InvariantCulture).Date;
            return (int)(DateTimeManager.UtcNow.Date - firstDate).TotalDays;
        }

        public int RecordCheckPointDayRetention()
        {
            int dayRetention = GetCheckPointDayRetention();
            CheckPointRetentionRecordedDays ??= new List<int>();

            if (!CheckPointRetentionRecordedDays.Contains(dayRetention))
            {
                CheckPointRetentionRecordedDays.Add(dayRetention);
                this.SaveData();
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("check_point_day_retention", dayRetention.ToString());
            }

            return dayRetention;
        }

        private void EnsureFirstSessionDayForCheckPoint()
        {
            if (!string.IsNullOrEmpty(FirstSessionDay)) return;
            SetFirstSessionDay();
        }

        #endregion

        public int GetDayRetentionIgnoreEmpty()
        {
            if (string.IsNullOrEmpty(FirstSessionDay))
            {
                FirstSessionDay = DateTime.Now.ToString(CultureInfo.InvariantCulture);
            }

            DateTime firstDate = DateTime.Parse(FirstSessionDay, CultureInfo.InvariantCulture);
            return (int)((DateTime.Now.Date - firstDate.Date).TotalDays);
        }

        public void IncreaseSessionCount()
        {
            SessionCount++;
            this.SaveData();
            if (SessionCount == 1)
            {
                PlayerResourceManager.Instance.AddInfiniteLife(60 * 60);
            }
        }

        public void IncreaseAdsRewardCount()
        {
            AdsRewardCount++;
            this.SaveData();

            UserPropertyManager.SetUserPropertyFirebase?.Invoke("ads_reward_count", AdsRewardCount.ToString());
        }

        public void IncreaseAdsInterstitialCount()
        {
            AdsInterstitialCount++;
            this.SaveData();

            UserPropertyManager.SetUserPropertyFirebase?.Invoke("ads_inter_count", AdsInterstitialCount.ToString());
        }

        public void IncreaseIAPCount()
        {
            IAPCount++;
            this.SaveData();

            UserPropertyManager.SetUserPropertyFirebase?.Invoke("iap_count", IAPCount.ToString());
        }

        public void SetFirstSessionDay()
        {
            FirstSessionDay = DateTimeManager.UtcNow.ToString(CultureInfo.InvariantCulture);
            UserPropertyManager.SetUserPropertyFirebase?.Invoke("first_play_date", FirstSessionDay);
            this.SaveData();
        }

        public void SetLastSessionDay()
        {
            LastSessionDay = DateTimeManager.UtcNow.ToString(CultureInfo.InvariantCulture);
            UserPropertyManager.SetUserPropertyFirebase?.Invoke("last_played_date", LastSessionDay);

            // campaign_source: media_source do AppsFlyer conversion data cache lại (xem AppsFlyerObjectScript).
            string campaignSource = PlayerPrefs.GetString("campaign_source", "");
            if (!string.IsNullOrEmpty(campaignSource))
            {
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("campaign_source", campaignSource);
            }

            this.SaveData();
        }
    }
}
