using TW.Reactive.CustomComponent;
using System;
using BaseGame.Scripts;
using Core;
using Core.UI.Modals;
using CoreData;
using Cysharp.Threading.Tasks;
using DataModule;
using Manager;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;

public class UserDataManager : Singleton<UserDataManager>
{
    public Reactive<int> CountAdsInterstitialInDay = new(0);
    public Reactive<string> LastDayCountAdsInterstitial = new("");
    public Reactive<int> CountShowPopupNoAdsInDay = new(0);
    public int[] countShowRemoveAds;
    public bool needCheckShowPopupNoAds = false;

    private TimeSpan timeToEndDay;
    [ShowInInspector]
    public int secondsToEndDay => (int)timeToEndDay.TotalSeconds;
    private void Start()
    {
        LoadData();
    }

    private void LoadData()
    {
        CountAdsInterstitialInDay = UserData.Instance.CountAdsInterstitialInDay;
        LastDayCountAdsInterstitial = UserData.Instance.LastDayCountAdsInterstitial;
        CountShowPopupNoAdsInDay = UserData.Instance.CountShowPopupNoAdsInDay;
        countShowRemoveAds = DefaultGlobalConfig.Instance.NumberShowNoAds;
        CheckDay();
        this.UpdateAsObservable().Subscribe(ChangeTime).AddTo(this);
    }

    private void ChangeTime(Unit _)
    {
        var dayEnd = DateTimeManager.UtcNow.Date.AddDays(1);
// #if UNITY_EDITOR
//         timeToEndDay = dayEnd.Subtract(LastDayCountAdsInterstitial.Value.ToDateTime());
// #endif
// #if !UNITY_EDITOR
//         timeToEndDay = dayEnd.Subtract(DateTimeManager.UtcNow);
// #endif
        timeToEndDay = dayEnd.Subtract(DateTimeManager.UtcNow);
        
        if (timeToEndDay.TotalSeconds <= 0)
        {
            ResetTime();
        }
    }

    private void CheckDay()
    {
        var today = DateTimeManager.UtcNow.Date;
        var lastDay = LastDayCountAdsInterstitial.Value.ToDateTime().Date;
        if (lastDay < today)
        {
            ResetTime();
        }
    }

    private void ResetTime()
    {
        var today = DateTimeManager.UtcNow.Date;
        LastDayCountAdsInterstitial.Value = today.ToEnUsString();
        CountAdsInterstitialInDay.Value = 0;
        CountShowPopupNoAdsInDay.Value = 0;
        UserData.Instance.SaveData();
    }

    public void IncreaseCountAdsInterstitialInDay()
    {
        CountAdsInterstitialInDay.Value++;
        UserData.Instance.SaveData();
    }
    
    public void SetCheckShowPopupNoAds(bool value)
    {
        needCheckShowPopupNoAds = value;
    }
    
    public void CheckToShowPopupNoAds()
    {
        if (ShopData.Instance.RemoveADs || !needCheckShowPopupNoAds || !OnCheckShowRemoveAdsPopup() || !ConditionShowPopupNoAds()) return;
        _ = ShowPopupRemoveAds();
        needCheckShowPopupNoAds = false;
    }

    private bool ConditionShowPopupNoAds()
    {
        return CountShowPopupNoAdsInDay.Value < DefaultGlobalConfig.Instance.CountShowNoAdsMax;
    }

    private async UniTask ShowPopupRemoveAds()
    {
        CountShowPopupNoAdsInDay.Value += 1;
        UserData.Instance.SaveData();
        var viewOptions = new ViewOptions("ModalNoAds");
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions, true);
    }

    private bool OnCheckShowRemoveAdsPopup()
    {
        for (var i = 0; i < countShowRemoveAds.Length; i++)
        {
            if (countShowRemoveAds[i] == CountAdsInterstitialInDay.Value)
            {
                return true;
            }
        }

        return false;
    }
}