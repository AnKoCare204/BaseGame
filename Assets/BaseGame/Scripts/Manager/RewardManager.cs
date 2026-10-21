using System;
using System.Collections.Generic;
using Core.UI.Modals;
using CoreData;
using Cysharp.Threading.Tasks;
using TW.UGUI.Core.Modals;
using TW.UGUI.Core.Views;
using TW.Utility.DesignPattern;
using UnityEngine;

/// <summary>Cộng gói thưởng (shop, daily) vào kho rồi mở ModalReward. ModalReward đọc RewardResources và callback ở args[0].</summary>
public class RewardManager : Singleton<RewardManager>
{
    public List<GameResource> RewardResources { get; } = new();

    public void AddReward(List<GameResource> resources, string placement, string claimType)
    {
        RewardResources.Clear();
        if (resources == null) return;
        RewardResources.AddRange(resources);
        ClaimReward(placement, claimType);
    }

    public void AddReward(GameResource gameResource, string placement, string claimType)
    {
        RewardResources.Add(gameResource);
        ClaimReward(placement, claimType);
    }

    public void ShowReward(Action callback = null)
    {
        if (RewardResources is { Count: > 0 })
        {
            ShowRewardAsync(callback).Forget();
        }
    }

    async UniTask ShowRewardAsync(Action callback = null)
    {
        ViewOptions viewOptions = new ViewOptions(nameof(ModalReward));
        await ModalContainer.Find(ContainerKey.Modals).PushAsync(viewOptions,callback);
        RewardResources.Clear();
    }
    
    public void ClaimReward(string placement, string claimType)
    {
        for (int i = 0; i < RewardResources.Count; i++)
        {
            ClaimReward(RewardResources[i], placement, claimType);
        }
    }

    public void ClaimReward(GameResource gameResource, string placement, string claimType)
    {
        PlayerResourceManager.Instance.AddResourceValue(gameResource.Type, gameResource.Value.Value, placement, claimType);
    }
    
    // ===== Ported from Mahjong (event reward claim flow) =====
    public void AddResource(ClaimGameResourceGroup claimGameResourceGroup)
    {
        Debug.Log("AddResource: " + claimGameResourceGroup.GameResourceList.Count);
        ResourceData.Instance.AddClaimGameResourceGroup(claimGameResourceGroup);
    }

    public async UniTask ClaimResource(ClaimGameResourceGroup claimGameResourceGroup)
    {
        ViewOptions viewOptions = new ViewOptions(nameof(ActivityClaimReward));
        await TW.UGUI.Core.Activities.ActivityContainer.Find(ContainerKey.Activities).ShowAsync(viewOptions, claimGameResourceGroup);
    }

    public async UniTask TryClaimRewardGamePlay()
    {
        List<ClaimGameResourceGroup> claimGameResourceGroups = new List<ClaimGameResourceGroup>(ResourceData.Instance.ClaimGameResourceGroupList);
        foreach (ClaimGameResourceGroup claimGameResourceGroup in claimGameResourceGroups)
        {
            if (claimGameResourceGroup.ClaimGameResourceType != ClaimGameResourceType.GamePlay) continue;
            await ClaimResource(claimGameResourceGroup);
            await UIManager.Instance.ClaimRewardComplete();
        }
    }
}
