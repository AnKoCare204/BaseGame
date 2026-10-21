using System;
using System.Collections.Generic;
using Core.UI.Modals;
using Cysharp.Threading.Tasks;
using DataModule;
using Game.Helper;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TW.UGUI.Core.Modals;
using TW.Utility.DesignPattern;
using UnityEngine;
using R3;
using TW.UGUI.Core.Views;
using Manager;
using CoreData;
using Core;

public class TreetopManager : Singleton<TreetopManager>
{
    public const int EventLoopDays = 3;
    public static Action OnInitializeComplete { get; set; }
    [field: SerializeField] public TreetopData Data { get; private set; }
    private TreetopGlobalConfig GlobalConfig => TreetopGlobalConfig.Instance;
    [field: SerializeField] public bool NeedShowUIWin { get; private set; } = false;
    [field: SerializeField] public bool NeedShowUIFalse { get; private set; } = false;
    [field: SerializeField] public int LastShowUILevel { get;  private set; } = -1;
    public DateTime EventEndTimeUtc { get; private set; }
    private ClaimGameResourceGroup _rewards = new();
    public bool IsEventUnlock => TreetopRemoteConfig.Instance.LevelUnlock > 0 && 
                                 TreetopRemoteConfig.Instance.LevelUnlock <= GameManager.Instance.Level.Value;
    [ShowInInspector] public bool IsInitialize { get; private set; }
    
    #region Save & Load

    private void LoadData()
    {
        // IsUnlocked.Value = false;
        // if(UserData.Instance.CurrentLevel < LevelUnlock) return;
        // IsUnlocked.Value = true;
        Data = TreetopData.Instance;
        if (Data.EventEndTimeUtc.IsNullOrWhitespace())
        {
            OpenNewEvent();
            return;
        }
        EventEndTimeUtc = Data.EventEndTimeUtc.StringToDateTime();

        // Same rule as AntLab: reset when the saved cycle no longer matches the current
        // anchored period. Covers expiry AND saves written by the old rolling-window logic
        // (their end time falls off the grid, so existing players realign on first load).
        DateTime anchoredEndTime = GetCurrentPeriodStartTime(DateTimeManager.UtcNow).AddDays(EventLoopDays);
        if (DateTimeManager.UtcNow >= EventEndTimeUtc || EventEndTimeUtc != anchoredEndTime)
        {
            OpenNewEvent();
            return;
        }
    }

    // Period grid anchored to the remote config start date, one period every EventLoopDays
    // (same convention as AntLabManager.GetCurrentPeriodStartTime).
    private DateTime GetCurrentPeriodStartTime(DateTime utcNow)
    {
        DateTime eventStartTime = TreetopRemoteConfig.Instance.TreetopStartDate;
        double elapsedDays = (utcNow - eventStartTime).TotalDays;
        int periodIndex = (int)Math.Floor(elapsedDays / EventLoopDays);
        if (periodIndex < 0) periodIndex = 0;
        return eventStartTime.AddDays(periodIndex * EventLoopDays);
    }

    private void SaveData()
    {
        Data.SaveData();
    }
    #endregion

    #region Unity functions

    public async UniTask Initialized()
    {
        NeedShowUIWin = false;
        _rewards = new();
        NeedShowUIFalse = false;
        LastShowUILevel = 0;
        if (IsInitialize) return;
        await UniTask.Yield();
        DateTime utcNow = DateTimeManager.UtcNow;
        if (!TreetopRemoteConfig.Instance.IsBetweenEvent(utcNow)) return;
        if (TreetopRemoteConfig.Instance.LevelUnlock <= 0) return;
        //if (!CardCollectionManager.Instance.IsActiveForReward()) return;
        IsInitialize = true;
        LoadData();
        LastShowUILevel = Data.Streak;
        OnInitializeComplete?.Invoke();
    }

    private void Update()
    {
        if(!IsInitialize) return;
        if(!IsEventUnlock) return;
        if(Data.EventEndTimeUtc.IsNullOrWhitespace()) return;
        // if (!CardCollectionManager.Instance.IsActiveForReward())
        // {
        //     IsInitialize = false;
        //     OnInitializeComplete?.Invoke();
        //     return;
        // }
        if (DateTimeManager.UtcNow >= EventEndTimeUtc)
        {
            OpenNewEvent();
            return;
        }
    }

    #endregion

    #region Manager funstions
    private void OpenNewEvent()
    {
        Data.SetDefault();
        DateTime now = DateTimeManager.UtcNow;
        // End of the current anchored period, not now + 3 days: every player's cycle ends at
        // the same global time, and a reset mid-period only runs until that period's boundary.
        EventEndTimeUtc = GetCurrentPeriodStartTime(now).AddDays(EventLoopDays);
        Data.EventEndTimeUtc = EventEndTimeUtc.DateTimeToString();
        NeedShowUIWin = false;
        NeedShowUIFalse = false;
        LastShowUILevel = 0;
        SaveData();
    }
    [Button]
    public void WinLevel()
    {
        NeedShowUIFalse = false;
        _rewards.GameResourceList.Clear();
        if(!IsInitialize) return;
        if(!IsEventUnlock) return;
        if(Data.EventEndTimeUtc.IsNullOrWhitespace()) return;
      
        if (DateTime.UtcNow >= EventEndTimeUtc)
        {
            OpenNewEvent();
            return;
        }
        if(Data.Streak >= GlobalConfig.Streaks[^1].Level) return;
        // if (Data.Streak > LastShowUILevel)
        // {
        //     
        // }
        // LastShowUILevel = Data.Streak;
        //int cachedStreak = Data.Streak;
        Data.Streak++;
        
        if (Data.MaxStreak < Data.Streak)
        {
            Data.MaxStreak = Data.Streak;
            TreetopStreakConfig config = GlobalConfig.GetConfigByLevel(Data.Streak);
            if (config != null)
            {
                NeedShowUIWin = true;
                if (config.DefaultRewards != null)
                    _rewards.GameResourceList.AddRange(config.DefaultRewards);
            }

            if (config == GlobalConfig.Streaks[^1])
            {
                UserData.Instance.CompleteTreetopMission++;
                UserData.Instance.SaveData();
            }
        }
        SaveData();
    }
    [Button]
    public void LoseLevel()
    {
        Debug.Log("Lose Treetop");
        if(!IsInitialize) return;
        if(!IsEventUnlock) return;
        if(Data.EventEndTimeUtc.IsNullOrWhitespace()) return;
        if (DateTime.UtcNow >= EventEndTimeUtc)
        {
            OpenNewEvent();
            return;
        }
        if(Data.Streak >= GlobalConfig.Streaks[^1].Level) return;
        if(Data.Streak == 0) return;
        TreetopStreakConfig config = GlobalConfig.GetLastCheckpointByLevel(Data.Streak);
        if(Data.Streak == config.Level) return;
        NeedShowUIFalse = true;
        LastShowUILevel = Data.Streak;
        Data.Streak = config.Level;
        SaveData();
    }

    public async UniTask TryShowWinLevel()
    {
        if(!IsInitialize) return;
        if(!IsEventUnlock) return;
        if(!NeedShowUIWin) return;
        ModalOptions modalTreetop = new ModalOptions(nameof(ModalTreetop), backdropAlpha: 0f);
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(modalTreetop,ModalTreetop.ModalState.ShowWinReward,LastShowUILevel);
        ClearShowUI();
    }

    public async UniTask TryClaimReward()
    {
        if(!IsInitialize) return;
        if(!IsEventUnlock) return;
        if(_rewards.GameResourceList.Count <= 0) return;
        // ClaimGameResourceGroup ClaimGroup = new ClaimGameResourceGroup();
        // ClaimGroup.GameResourceList = _rewards;
        _rewards.ClaimType = "OceanAscensioncReward";
        _rewards.Placement = "OceanAscension";
        _rewards.TitleName = "Ocean Ascension Reward";
        //IngameFirebaseAnalystic.Instance.TrackTreetopClaim(
        //    GameManager.Instance.Level.Value.ToString(),
        //    Data.MaxStreak.ToString());
        await RewardManager.Instance.ClaimResource(_rewards);
    }

    public async UniTask TryShowLoseLevel()
    {
        if(!IsInitialize) return;
        if(!IsEventUnlock) return;
        if(!NeedShowUIFalse) return;
        ModalOptions modalTreetop = new ModalOptions(nameof(ModalTreetop), backdropAlpha: 0f);
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(modalTreetop,ModalTreetop.ModalState.ShowLose,LastShowUILevel);
        ClearShowUI();
    }

    public void ClearNeedShowUI()
    {
        NeedShowUIWin = false;
        NeedShowUIFalse = false;
    }
    public void ClearShowUI()
    {
        LastShowUILevel = Data.Streak;
    }

    public void HandleFailOnLastProgress()
    {
        if (!IsInitialize || Data == null) return;
        if(!IsEventUnlock) return;
        if(Data.EventEndTimeUtc.IsNullOrWhitespace()) return;
        if (DateTime.UtcNow >= EventEndTimeUtc)
        {
            OpenNewEvent();
            return;
        }
        if(Data.Streak >= GlobalConfig.Streaks[^1].Level) return;
        if(Data.Streak == 0) return;
        TreetopStreakConfig config = GlobalConfig.GetLastCheckpointByLevel(Data.Streak);
        if(Data.Streak == config.Level) return;
        Data.Streak = config.Level;
        LastShowUILevel = Data.Streak;
        SaveData();
    }
    #endregion
    public bool IsCompleted()
    {
        return Data.Streak >= GlobalConfig.Streaks[^1].Level;
    }

    public bool IsNextClaimed()
    {
        return Data.Streak < Data.MaxStreak;
    }

    public bool IsInCheckpoint()
    {
        TreetopStreakConfig config = GlobalConfig.GetLastCheckpointByLevel(Data.Streak);
        return Data.Streak == config.Level;
    }

    /// <summary>
    /// Adds <paramref name="point"/> streak levels at once (cheat / bulk grant) and pays out every
    /// gift between the old MaxStreak and the new streak, not just the level landed on.
    /// The streak is clamped to the last configured level, so jumping past the end grants the
    /// remaining gifts and stops there instead of running the streak out of config range.
    /// </summary>
    public void AddCustomPoint(int point)
    {
        NeedShowUIWin = false;
        NeedShowUIFalse = false;
        _rewards.GameResourceList.Clear();

        int maxLevel = GlobalConfig.Streaks[^1].Level;
        int targetStreak = Mathf.Clamp(Data.Streak + point, 0, maxLevel);
        if (targetStreak == Data.Streak) return;

        Data.Streak = targetStreak;

        for (int level = Data.MaxStreak + 1; level <= Data.Streak; level++)
        {
            TreetopStreakConfig config = GlobalConfig.GetConfigByLevel(level);
            if (config == null) continue;
            List<GameResource> rewards = config.DefaultRewards;
            if (rewards == null || rewards.Count == 0) continue;
            // PlayerResourceManager.Instance.AddResourceValue(rewards, "treetopmission");
            _rewards.GameResourceList.AddRange(rewards);
        }

        if (Data.MaxStreak < Data.Streak)
        {
            Data.MaxStreak = Data.Streak;
        }
        TryClaimReward().Forget();
        SaveData();
    }
}

[System.Serializable]
[DataGenerate(12)]
public class TreetopData : IDataModule
{
    public static TreetopData Instance => InGameDataManager.Instance.InGameData.TreetopData;
    public string Key => nameof(TreetopData);
    public int TargetVersion => 1;
    [field: SerializeField] public int Version { get; set; } = 1;
    public string EventEndTimeUtc;
    public int Streak;
    public int MaxStreak;
    public bool IsShowTut = false;
    public void SetDefault()
    {
        EventEndTimeUtc = "";
        Streak = 0;
        MaxStreak = 0;
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
        return DataSerializer.FromJson<TreetopData>(json);
    }
}
