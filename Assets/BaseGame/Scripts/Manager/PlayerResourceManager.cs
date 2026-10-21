using TW.Reactive.CustomComponent;
using System;
using System.Collections.Generic;
using Core;
using Core.Manager;
using Core.UI.Modals;
using CoreData;
using DataModule;
using Game.Helper;
using Manager;
using R3;
using Sirenix.OdinInspector;
using TW.Utility.DesignPattern;
using UnityEngine;

public class PlayerResourceManager : Singleton<PlayerResourceManager>
{
    public DateTime LastAutoLifeAdd { get; set; }
    public DateTime InfinityLifeExpireTime { get; set; }
    [field: SerializeField] public Reactive<int> CurrentLife { get; set; }
    public bool HasLife => IsEnoughResourceValue(ResourceType.Life, 1);
    
    public int MaxLife => GameManager.Instance.MaxLife;
    
    public bool HasFullLife => IsEnoughResourceValue(ResourceType.Life, MaxLife);
    public bool HasLifeAutoFill =>
        LastAutoLifeAdd + TimeSpan.FromMinutes(DefaultGlobalConfig.Instance.DefaultFillHeartTime) <=
        DateTimeManager.UtcNow;

    public bool HasInfiniteLife => InfinityLifeExpireTime > DateTimeManager.UtcNow;
    public TimeSpan InfinityLifeExpire => InfinityLifeExpireTime - DateTimeManager.UtcNow;

    public TimeSpan TimeToNextLife => LastAutoLifeAdd + TimeSpan.FromMinutes(DefaultGlobalConfig.Instance.DefaultFillHeartTime) - DateTimeManager.UtcNow;
    
    
#if UNITY_EDITOR
    [ShowInInspector] public string LastAutoLifeAddEditor => LastAutoLifeAdd.DateTimeToString();
    [ShowInInspector] public string InfinityLifeExpireTimeEditor => InfinityLifeExpireTime.DateTimeToString();
    [ShowInInspector] public string TimeToNextLifeEditor => TimeToNextLife.TimeSpanToStringUI();
#endif

    private void Start()
    {
        LastAutoLifeAdd = ResourceData.Instance.LastAutoLifeAdd.StringToDateTime();
        InfinityLifeExpireTime = ResourceData.Instance.InfinityLifeExpireTime.StringToDateTime();
        CurrentLife = GetResource(ResourceType.Life).Value;
        CurrentLife.Pairwise().Subscribe(OnLifeChange).AddTo(this);
        CurrentLife.Subscribe((int value) =>
        {
            if (HasFullLife && !HasInfiniteLife)
            {
                ModalFillHeartContext.Events.FullHeartEvent?.Invoke();
            }
        });
    }

    private void Update()
    {
        // if (!DateTimeManager.IsSynced && !DateTimeManager.IsLocalSynced) return;
        int currentLife = Mathf.RoundToInt(CurrentLife.Value);
        if (currentLife < MaxLife)
        {
            if (HasLifeAutoFill)
            {
                CurrentLife.Value = Mathf.Min(CurrentLife.Value + 1, MaxLife);
            }
        }
    }

    private void OnLifeChange((int previous, int current) life)
    {
        int prevLife = life.previous;
        int currLife = life.current;
        if (prevLife == MaxLife && currLife < DefaultGlobalConfig.Instance.DefaultLife)
        {
            LastAutoLifeAdd = DateTimeManager.UtcNow;
            ResourceData.Instance.LastAutoLifeAdd = LastAutoLifeAdd.DateTimeToString();
            ResourceData.Instance.SaveData();
        }
        else if (life.previous < life.current && HasLifeAutoFill)
        {
            LastAutoLifeAdd =
                LastAutoLifeAdd.Add(TimeSpan.FromMinutes(DefaultGlobalConfig.Instance.DefaultFillHeartTime));
            ResourceData.Instance.LastAutoLifeAdd = LastAutoLifeAdd.DateTimeToString();
            ResourceData.Instance.SaveData();
        }
    }
    public void AddInfiniteLife(int time)
    {
        InfinityLifeExpireTime = HasInfiniteLife
            ? InfinityLifeExpireTime.AddSeconds(time)
            : DateTimeManager.UtcNow.AddSeconds(time);
        ResourceData.Instance.InfinityLifeExpireTime = InfinityLifeExpireTime.DateTimeToString();
        ResourceData.Instance.SaveData();
    }

    #region Resource

    public GameResource GetResource(ResourceType type)
    {
        return ResourceData.Instance.GetResource(type);
    }
    public bool IsEnoughResourceValue(ResourceType type, int value)
    {
        return GetResource(type).Value >= value;
    }

    public void SetResourceValue(ResourceType type, int value)
    {
        ResourceData.Instance.SetResourceValue(type, value);
        ResourceData.Instance.SaveData();
    }

    public void AddResourceValue(ResourceType type, int value, string placement, string claimType)
    {
        switch (type)
        {
            case ResourceType.None:
                break;
            case ResourceType.Money:
                ResourceData.Instance.AddResourceValue(type, value);
                IngameFirebaseAnalystic.Instance.TrackCoinEarn(placement, claimType, value);
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("value_coins_earn_count", ResourceData.Instance.totalCoinsEarned.ToString());
                break;
            case ResourceType.Life:
                if (HasInfiniteLife) return;
                ResourceData.Instance.AddResourceValue(type, value);
                GameResource lifeValue = ResourceData.Instance.GetResource(ResourceType.Life);
                if (lifeValue.Value <= MaxLife) break;
                ResourceData.Instance.SetResourceValue(ResourceType.Life, MaxLife);
                break;
            case ResourceType.NoAds:
                if (value > 0)
                {
                    ShopData.Instance.RemoveADs = true;
                    ShopData.Instance.IsRemoveADs.Value = true;
                    ShopData.Instance.SaveData();
                    EventAdsManager.ShowBannerAds?.Invoke(false);
                }
                break;
            case ResourceType.InfiniteLife:
                AddInfiniteLife(value);
                break;
            case ResourceType.BonusLife:
                // BattlePassData.Instance.BonusLife += value;
                ResourceData.Instance.SaveData();
                break;
            case ResourceType.Hint or ResourceType.Undo or ResourceType.Shuffle:
                ResourceData.Instance.AddResourceValue(type, value);
                IngameFirebaseAnalystic.Instance.TrackBoosterEarn(placement,type.ToString(),value,claimType);
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("booster_earn_count", ResourceData.Instance.totalBoostersEarned.ToString());
                break;
            default:
                ResourceData.Instance.AddResourceValue(type, value);
                break;
        }
        ResourceData.Instance.SaveData();
    }

    public void SubResourceValue(ResourceType type, int value, string placement, string spendType)
    {
        switch (type)
        {
            case ResourceType.Life:
                if (HasInfiniteLife) return;
                ResourceData.Instance.SubResourceValue(type, value);
                break;
            case ResourceType.Money:
                ResourceData.Instance.SubResourceValue(type, value);
                TrackLevelResourceSpend(isBooster: false, value);
                IngameFirebaseAnalystic.Instance.TrackCoinSpend(placement, spendType, value);
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("value_coins_spend_count", ResourceData.Instance.totalCoinsSpent.ToString());
                break;
            case ResourceType.Hint or ResourceType.Undo or ResourceType.Shuffle:
                ResourceData.Instance.SubResourceValue(type, value);
                TrackLevelResourceSpend(isBooster: true, value);
                IngameFirebaseAnalystic.Instance.TrackBoosterSpeend(placement,type.ToString(),value);
                UserPropertyManager.SetUserPropertyFirebase?.Invoke("booster_spend_count", ResourceData.Instance.totalBoostersSpent.ToString());
                break;
        }
        
        ResourceData.Instance.SaveData();
    }
    public void AddResourceValue(List<GameResource> gameResources, string placement, string claimType)
    {
        foreach (GameResource gameResource in gameResources)
        {
            AddResourceValue(gameResource, placement, claimType);
        }
    }
    public void AddResourceValue(GameResource[] gameResources, string placement, string claimType)
    {
        foreach (GameResource gameResource in gameResources)
        {
            AddResourceValue(gameResource, placement, claimType);
        }
    }
    public void AddResourceValue(GameResource gameResource, string placement, string claimType)
    {
        AddResourceValue(gameResource.Type, gameResource.Value, placement, claimType);
    }
    
    public void AddResourceValue(ResourceType type, int value, string claimType)
    {
        AddResourceValue(type, value, claimType, claimType);
    }

    public bool IsEnoughResource(GameResource cost)
    {
        return IsEnoughResourceValue(cost.Type, cost.Value);
    }

    private void TrackLevelResourceSpend(bool isBooster, int value)
    {
        if (LevelManager.Instance == null || !LevelManager.Instance.IsTrackingLevelSession) return;

        if (isBooster)
        {
            LevelManager.Instance.AddLevelBoosterSpend(value);
            return;
        }

        LevelManager.Instance.AddLevelCoinSpend(value);
    }

    public void ConsumeResource(GameResource resource, string placement, string spendType)
    {
        switch (resource.Type)
        {
            case ResourceType.None:
                break;
            case ResourceType.InfiniteLife:
                break;
            default:
                SubResourceValue(resource.Type, resource.Value, placement, spendType);
                break;
        }
    }
    #endregion
}