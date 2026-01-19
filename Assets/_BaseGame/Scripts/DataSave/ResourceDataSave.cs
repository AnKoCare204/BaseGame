using System;
using System.Collections.Generic;
using System.Globalization;
using Manager;
using TW.Reactive.CustomComponent;
using UnityEngine;
using Sirenix.OdinInspector;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

[Serializable]
public class ResourceDataSave : IDataSave<ResourceDataSave>
{
    public static ResourceDataSave Instance => InGameDataManager.Instance.InGameData.ResourceDataSave;
    [field: SerializeField]
    public List<GameResource> Resources { get; set; } = new()
    {
        new GameResource(CurrencyType.Coin, 5),
        new GameResource(CurrencyType.Gem, 5),
    };
    [field: SerializeField] public string LastAutoLifeAdd { get; set; } = "";
    [field: SerializeField] public string InfinityLifeExpireTime { get; set; } = "";
    [field: SerializeField] public string InfinityExtraRewardExpireTime { get; set; } = "";
    [field: SerializeField] public string InfinityExtraPackExpireTime { get; set; } = "";
    [field: SerializeField] public string InfinityExtraTimeExpireTime { get; set; } = "";

    public bool IsDirty { get; set; }
    public ResourceDataSave DefaultData()
    {
        return this;
    }
    [Button]
    public string ToJson()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new ReactiveValueContractResolver(),
            Converters = new JsonConverter[]
            {
                new ReactiveValueConverter()
            }
        };

        return JsonConvert.SerializeObject(this, settings);
    }

    public ResourceDataSave FromJson(string json)
    {
        var settings = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            ContractResolver = new ReactiveValueContractResolver(),
            Converters = new JsonConverter[]
            {
                new ReactiveValueConverter()
            }
        };

        return JsonConvert.DeserializeObject<ResourceDataSave>(json, settings);
    }

    #region  Resource

    public GameResource GetGameResource(Enum specificType)
    {
        ResourceType type = specificType.GetResourceType();
        for (int i = 0; i < Resources.Count; i++)
        {
            GameResource resource = Resources[i];
            if (resource.Type != type) continue;
            if (resource.SpecificType != (int)(object)specificType) continue;
            return resource;
        }
        GameResource newResource = new GameResource(specificType, 0);
        Resources.Add(newResource);
        return newResource;
    }
    public ReactiveValue<float> GetReactiveValue(Enum type)
    {
        return GetGameResource(type).Value;
    }

    public void SetResourceValue(Enum type, float value)
    {
        GetReactiveValue(type).Value = value;
        this.SaveData();
    }

    public void AddResourceValue(SpecialResourceType type, float value)
    {
        switch (type)
        {
            case SpecialResourceType.InfiniteLife:
                // PlayerResourceManager.Instance.AddInfiniteLife(value);
                break;
            case SpecialResourceType.InfiniteExtraReward:
                // PlayerResourceManager.Instance.AddInfiniteExtraReward(value);
                break;
            case SpecialResourceType.NoAds:
                GameResource noAdsResource = GetGameResource(type);
                if (value > 0)
                    noAdsResource.Value.Value = 1;
                break;
            case SpecialResourceType.None:
                break;
            case SpecialResourceType.Vip:
                break;
            case SpecialResourceType.BattlePass:
                break;
            case SpecialResourceType.InfiniteExtraTime:
                // PlayerResourceManager.Instance.AddInfiniteExtraTime(value);
                break;
            case SpecialResourceType.InfiniteExtraPack:
                // PlayerResourceManager.Instance.AddInfiniteExtraPack(value);
                break;
            case SpecialResourceType.FrameProfile:
                // ProfileDataSave.Instance.FrameDatas.Find(x => x.FrameIndex == Mathf.RoundToInt(value)).IsUnlocked = true;
                // ProfileDataSave.Instance.SaveData();

                // ProfileDataSave.Instance.OnChangeStatusNotification(new FrameNotificationCondition(), Mathf.RoundToInt(value));
                break;
            case SpecialResourceType.AvatarProfile:
                // ProfileDataSave.Instance.AvatarDatas.Find(x => x.AvatarIndex == Mathf.RoundToInt(value)).IsUnlocked = true;
                // ProfileDataSave.Instance.SaveData();

                // ProfileDataSave.Instance.OnChangeStatusNotification(new AvatarNotificationCondition(), Mathf.RoundToInt(value));
                break;
            case SpecialResourceType.NameProfile:
                // EffectNameDataSave.Instance.EffectNameDataSaveDetails.Find(x => x.Id == Mathf.RoundToInt(value)).IsUnlocked = true;
                // EffectNameDataSave.Instance.SaveData();

                // EffectNameDataSave.Instance.OnChangeStatusNotification(new NameNotificationCondition(), Mathf.RoundToInt(value));
                break;
            case SpecialResourceType.BadgeProfile:
                // ProfileDataSave.Instance.BadgeDatas.Find(x => x.BadgeIndex == Mathf.RoundToInt(value)).IsUnlocked = true;
                // ProfileDataSave.Instance.SaveData();

                // ProfileDataSave.Instance.OnChangeStatusNotification(new BadgeNotificationCondition(), Mathf.RoundToInt(value));
                break;

            default:
                GetReactiveValue(type).Value += value;
                break;
        }
        this.SaveData();
    }
    public void SubResourceValue(SpecialResourceType type, float value)
    {
        GetReactiveValue(type).Value -= value;
        this.SaveData();
    }
    
    public void AddResourceValue(CurrencyType type, float value)
    {
        GetReactiveValue(type).Value += value;
        if (type == CurrencyType.Coin)
            InGameDataManager.Instance.InGameData.UserProfileDataSave.TotalCoinEarn += (int)value;
        this.SaveData();
    }
    public void SubResourceValue(CurrencyType type, float value)
    {
        GetReactiveValue(type).Value -= value;
        if (type == CurrencyType.Coin)
            InGameDataManager.Instance.InGameData.UserProfileDataSave.TotalCoinRemain += (int)value;
        if (GetReactiveValue(type).Value < 0)
        {
            GetReactiveValue(type).Value = 0;
        }
        this.SaveData();
    }
    #endregion
}