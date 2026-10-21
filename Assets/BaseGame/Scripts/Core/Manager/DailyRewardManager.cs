using TW.Reactive.CustomComponent;
using System;
using BaseGame.Scripts;
using CoreData;
using DataModule;
using Manager;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;

public class DailyRewardManager : Singleton<DailyRewardManager>
{
    public Reactive<string> timeDailyResetStr;
    public Reactive<int> dailyRewardClaimCount;
    private DateTime timeReset;
    private bool loadDone = false;
    
    private void Start()
    {
        LoadData();
        this.UpdateAsObservable().Subscribe(ChangeTime).AddTo(this);
    }

    private void LoadData()
    {
        timeDailyResetStr = DailyDataSave.Instance.TimeDailyReset;
        dailyRewardClaimCount = DailyDataSave.Instance.DailyRewardClaimCount;
        timeReset = timeDailyResetStr.Value.ToDateTime();
        var timeSubtract = CheckEndDay();
        if (timeSubtract)
            ResetDailyReward();
        loadDone = true;
    }

    private void ChangeTime(Unit _)
    {
        if (!loadDone) return;
        if (CheckEndDay())
        {
            ResetDailyReward();
        }
    }

    private bool CheckEndDay()
    {
        var currentTime = DateTimeManager.UtcNow;
        return currentTime.Subtract(timeReset).TotalSeconds >= 0;
    }

    private void ResetDailyReward()
    {
        var nextDay = DateTimeManager.UtcNow.Date.AddDays(1);
        timeDailyResetStr.Value = nextDay.ToEnUsString();
        timeReset = nextDay;
        dailyRewardClaimCount.Value = 0;
        DailyDataSave.Instance.SaveData();
    }

    public bool IsCanClaimThisReward(int index)
    {
        return index == dailyRewardClaimCount.Value;
    }

    [Button]
    public void AddDayToTimeReset()
    {
        ResetDailyReward();
        DailyDataSave.Instance.SaveData();
    }

    public bool ClaimReward(int index)
    {
        var isCanClaim = IsCanClaimThisReward(index);
        if (!isCanClaim) return false;
        dailyRewardClaimCount.Value++;
        DailyDataSave.Instance.SaveData();
        return true;
    }

    public bool IsClaimed(int i)
    {
        return i < dailyRewardClaimCount.Value;
    }

    public bool IsCurrentReward(int i)
    {
        return i == dailyRewardClaimCount.Value;
    }
}
